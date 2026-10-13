-- Засев для нагрузочной проверки (docs/adr/170_load-testing.md).
-- Запуск: docker compose exec -T postgres psql -U club -d club -v classes=1000000 < scripts/load-seed.sql
-- Пароль всех пользователей load*@club.test такой же, как у штатных сидов.

\if :{?rooms}
\else
\set rooms 50
\endif
\if :{?users}
\else
\set users 20000
\endif
\if :{?classes}
\else
\set classes 500000
\endif
\if :{?enrollments}
\else
\set enrollments 2000000
\endif
\if :{?waitlist}
\else
\set waitlist 100000
\endif
\if :{?popular}
\else
\set popular 20
\endif

\set ON_ERROR_STOP on
\timing on

BEGIN;

CREATE TEMP TABLE load_rooms ON COMMIT DROP AS
WITH inserted AS (
    INSERT INTO rooms (name)
    SELECT 'Load room ' || i FROM generate_series(1, :rooms) AS i
    RETURNING id
)
SELECT row_number() OVER (ORDER BY id) AS n, id FROM inserted;
CREATE UNIQUE INDEX ON load_rooms (n);

CREATE TEMP TABLE load_users ON COMMIT DROP AS
WITH inserted AS (
    INSERT INTO users (email, password_hash, role, created_at)
    SELECT
        format('load%s@club.test', lpad(i::text, 6, '0')),
        (SELECT password_hash FROM users WHERE email = 'user001@club.test'),
        'user',
        now()
    FROM generate_series(1, :users) AS i
    RETURNING id
)
SELECT row_number() OVER (ORDER BY id) AS n, id FROM inserted;
CREATE UNIQUE INDEX ON load_users (n);

INSERT INTO passes (user_id, remaining_visits, valid_until)
SELECT id, 100 + (random() * 900)::int, current_date + 365 FROM load_users;

-- Занятия: ±180 дней от сейчас, вместимость 10–30, каждое двадцатое отменено.
CREATE TEMP TABLE load_classes ON COMMIT DROP AS
WITH inserted AS (
    INSERT INTO classes (room_id, title, starts_at, duration_minutes, capacity, is_cancelled)
    SELECT
        r.id,
        'Load class ' || i,
        date_trunc('minute', now() - interval '180 days' + (random() * interval '360 days')),
        45 + (i % 4) * 15,
        10 + (i % 21),
        i % 20 = 0
    FROM generate_series(1, :classes) AS i
    JOIN load_rooms AS r ON r.n = 1 + (i % :rooms)
    RETURNING id, capacity, starts_at, is_cancelled
)
SELECT * FROM inserted;

-- Брони по 1 месту: на занятие не больше вместимости, в среднем enrollments / classes.
-- Каждая десятая отменена и места не занимает.
INSERT INTO enrollments (user_id, class_id, seats, status, created_at, cancelled_at, refunded_visits)
SELECT
    u.id,
    c.id,
    1,
    CASE WHEN s % 10 = 0 THEN 'cancelled' ELSE 'active' END,
    c.starts_at - interval '3 days',
    CASE WHEN s % 10 = 0 THEN c.starts_at - interval '1 day' END,
    CASE WHEN s % 10 = 0 THEN 1 END
FROM load_classes AS c
CROSS JOIN LATERAL generate_series(
    1,
    least(c.capacity, floor(random() * (2.0 * :enrollments / :classes + 1))::int)) AS s
JOIN load_users AS u ON u.n = 1 + ((c.id * 7919 + s) % :users);

-- Очередь: будущие неотменённые занятия, по одной записи на пользователя и занятие.
CREATE TEMP TABLE load_future ON COMMIT DROP AS
SELECT row_number() OVER (ORDER BY id) AS n, id
FROM load_classes
WHERE starts_at > now() + interval '2 hours' AND NOT is_cancelled;
CREATE UNIQUE INDEX ON load_future (n);

INSERT INTO waitlist_entries (user_id, class_id, seats, status, created_at)
SELECT u.id, f.id, 1 + (i % 2), 'waiting', now() - (i % 1000) * interval '1 second'
FROM generate_series(1, :waitlist) AS i
JOIN load_future AS f ON f.n = 1 + (i::bigint * 104729) % (SELECT count(*) FROM load_future)
JOIN load_users AS u ON u.n = 1 + (i % :users)
ON CONFLICT DO NOTHING;

-- Популярные занятия для сценария одновременной записи: через неделю, вместимость 30, пустые.
INSERT INTO classes (room_id, title, starts_at, duration_minutes, capacity, is_cancelled)
SELECT
    (SELECT id FROM load_rooms WHERE n = 1),
    'Load popular class ' || i,
    date_trunc('hour', now() + interval '7 days') + i * interval '1 hour',
    60,
    30,
    false
FROM generate_series(1, :popular) AS i;

COMMIT;

ANALYZE rooms, users, passes, classes, enrollments, waitlist_entries;

SELECT
    (SELECT count(*) FROM classes) AS classes,
    (SELECT count(*) FROM enrollments) AS enrollments,
    (SELECT count(*) FROM waitlist_entries) AS waitlist,
    (SELECT count(*) FROM users) AS users;
