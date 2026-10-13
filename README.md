# Fitness Club API

Нужны .NET 9 SDK и Docker.

## Запуск

```
docker compose up -d
dotnet run --project WebApi
```

Приложение слушает `http://localhost:5080`, Swagger — `http://localhost:5080/swagger`,
проверка живости — `http://localhost:5080/health`. Миграции и начальные данные применяются
при старте.

Занятия в начальных данных расставлены относительно момента первого запуска. Чтобы получить
свежий набор, пересоздайте базу:

```
docker compose down -v
docker compose up -d
```

## Миграции

```
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --context ClubDbContext --project DataAccessLayer --startup-project WebApi
```

Применяются при следующем запуске приложения.

После изменения схемы обновите модель чтения (`DataReadAccessLayer`) — она генерируется из БД. Нужны
поднятый Postgres и применённые миграции. Строка подключения берётся из `WebApi/appsettings.json`.
Сначала сборка, потом удаление прошлой генерации (`--force` не удаляет модели исчезнувших таблиц):

```
dotnet tool restore
dotnet build WebApi
rm -rf DataReadAccessLayer/Models DataReadAccessLayer/ClubReadDbContext.cs
dotnet dotnet-ef dbcontext scaffold Name=ConnectionStrings:Postgres Npgsql.EntityFrameworkCore.PostgreSQL \
  --project DataReadAccessLayer --startup-project WebApi \
  --namespace DataReadAccessLayer.Models --context-namespace DataReadAccessLayer \
  --context ClubReadDbContext --output-dir Models --context-dir . \
  --table rooms --table classes --table enrollments --table passes --table waitlist_entries \
  --no-onconfiguring --no-build
```

## Тесты

```
dotnet test
```

Тестам нужны запущенные Postgres и Redis из `docker compose`. База для прогона создаётся
отдельная и удаляется после.

## Учётные данные

Пароль у всех пользователей — `Password123!`.

| Роль | Логины |
|---|---|
| `user` | `user001@club.test` … `user200@club.test` |
| `admin` | `admin001@club.test` … `admin005@club.test` |

Вход — `POST /auth/login` с телом `{ "email": "...", "password": "..." }`,
в ответе `accessToken`.

## Нагрузочная проверка

Подробности и результаты — [`docs/adr/170_load-testing.md`](docs/adr/170_load-testing.md). Нужны Docker и запущенный `docker compose`; k6 запускается в контейнере.

1. Засев: 500 тыс. занятий, 2 млн броней, 100 тыс. записей в очереди, 20 тыс. пользователей `load*@club.test` с тем же паролем. Повторный засев упадёт на существующих email. Объёмы меняются через `-v`, например `-v classes=1000000`.
   ```
   docker compose exec -T postgres psql -U club -d club < scripts/load-seed.sql
   ```
2. Данные для k6: пользователи с бронями и популярные занятия.
   ```
   mkdir -p /tmp/load
   docker compose exec -T postgres psql -U club -d club -tA < scripts/load-data.sql > /tmp/load/load-data.json
   ```
3. Приложение в Release с выключенным лимитом частоты, в отдельном терминале. Дождаться `Now listening on: http://localhost:5080`.
   ```
   RateLimit__PermitLimit=1000000 ASPNETCORE_URLS=http://localhost:5080 dotnet run -c Release --no-launch-profile --project WebApi
   ```
4. Прогон: запись на популярное занятие, расписание, аналитика и карточка — 30 с. `POPULAR` — номер популярного занятия (0–19): каждое заполняется за один прогон.
   ```
   docker run --rm -v "$PWD/scripts:/scripts" -v /tmp/load:/data grafana/k6:0.54.0 run -e POPULAR=0 /scripts/load.js
   ```
   Поиск предела — число VU растёт до 500: `run -e STRESS=1 /scripts/load.js`.
5. Инварианты после прогона — оба запроса должны вернуть 0:
   ```
   docker compose exec -T postgres psql -U club -d club -c "select count(*) from classes c where c.capacity < (select coalesce(sum(seats), 0) from enrollments e where e.class_id = c.id and e.status = 'active')"
   docker compose exec -T postgres psql -U club -d club -c "select count(*) from passes where remaining_visits < 0"
   ```
