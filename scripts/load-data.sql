-- Данные для scripts/load.js: пользователи load* с активной бронью на будущее занятие и популярные занятия.
-- Запуск: docker compose exec -T postgres psql -U club -d club -tA < scripts/load-data.sql > /tmp/load-data.json
SELECT json_build_object(
    'users', (
        SELECT json_agg(x)
        FROM (
            SELECT DISTINCT ON (u.id) u.email, e.id AS "enrollmentId"
            FROM users AS u
            JOIN enrollments AS e ON e.user_id = u.id AND e.status = 'active'
            JOIN classes AS c ON c.id = e.class_id AND c.starts_at > now()
            WHERE u.email LIKE 'load%'
            ORDER BY u.id, e.id
            LIMIT 100
        ) AS x),
    'popularClassIds', (
        SELECT json_agg(id ORDER BY id) FROM classes WHERE title LIKE 'Load popular class %'),
    'admin', 'admin001@club.test');
