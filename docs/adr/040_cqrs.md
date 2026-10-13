# 040. Команды и запросы на Ovm.Cqrs

**Статус:** принято.

## Контекст
У восьми ручек разная модель доступа к данным. Команды работают через транзакцию и `FOR UPDATE`. Запросы обходятся без блокировок, используют `AsNoTracking`, проекции в DTO и, для карточки, кэш.

## Решение
Облегчённый CQRS на пакете [`Ovm.Cqrs`](https://www.nuget.org/packages/Ovm.Cqrs/) (`0.1.0-alpha.2`, есть сборка под `net9.0`).

| Ручка | Тип | Сообщение |
|---|---|---|
| `POST /enrollments` | команда | `CreateEnrollmentCommand` |
| `POST /enrollments/{id}/seats` | команда | `ChangeEnrollmentSeatsCommand` |
| `POST /enrollments/{id}/cancel` | команда | `CancelEnrollmentCommand` |
| `GET /enrollments/{id}` | запрос | `GetEnrollmentCardQuery` |
| `GET /schedule` | запрос | `GetScheduleQuery` |
| `GET /analytics/load` | запрос | `GetRoomLoadQuery` |
| `POST /enrollments/waitlist` | команда | `JoinWaitlistCommand` |
| `POST /enrollments/waitlist/{id}/cancel` | команда | `LeaveWaitlistCommand` |

Обработчик называется по сообщению с суффиксом типа: `GetEnrollmentCardQuery` → `GetEnrollmentCardQueryHandler`, `CreateEnrollmentCommand` → `CreateEnrollmentCommandHandler`.

Правила:
- контроллер зависит только от `ICommandProcessor` или `IQueryProcessor` и формирует ответ;
- обработчик координирует сценарий и владеет транзакцией ([060](060_transactions.md));
- **обработчики не вызывают друг друга.** Общая логика вынесена в компоненты без собственной транзакции. Например, перевод из очереди вызывают обработчики отмены и изменения мест внутри уже открытой транзакции;
- запросы читают через отдельный контекст чтения ([180](180_read-context.md)), команды — через `UnitOfWork` и репозитории;
- **команды не используют контекст чтения.** Если команде нужно вернуть данные (ручка 2 возвращает карточку брони), она читает их через `UnitOfWork` в своей же транзакции, после `SaveChanges` и до `Commit`: одно подключение на запрос, в ответе уже новые данные. Одинаковые выборки для обеих сторон лежат рядом в статическом хелпере (`EnrollmentCardReader`), а их совпадение проверяет тест;
- отдельной базы для чтения и событий нет.

## Последствия
- Один обработчик на ручку дальше разрастается в отдельный файл тестов на обработчик.
- Пакет в статусе alpha, и его код тоже будет на виду у проверяющих.
