# 040. Команды и запросы на Ovm.Cqrs

**Статус:** принято.

## Контекст
У восьми ручек разная модель доступа к данным. Команды работают через транзакцию и `FOR UPDATE`. Запросы обходятся без блокировок, используют `AsNoTracking`, проекции в DTO и, для карточки, кэш.

## Решение
Облегчённый CQRS на пакете [`Ovm.Cqrs`](https://www.nuget.org/packages/Ovm.Cqrs/) (`0.1.0-alpha.2`, есть сборка под `net9.0`).

| Ручка | Тип | Обработчик |
|---|---|---|
| `POST /enrollments` | команда | `CreateEnrollment` |
| `POST /enrollments/{id}/seats` | команда | `ChangeEnrollmentSeats` |
| `POST /enrollments/{id}/cancel` | команда | `CancelEnrollment` |
| `GET /enrollments/{id}` | запрос | `GetEnrollmentCard` |
| `GET /schedule` | запрос | `GetSchedule` |
| `GET /analytics/load` | запрос | `GetRoomLoad` |
| `POST /enrollments/waitlist` | команда | `JoinWaitlist` |
| `POST /enrollments/waitlist/{id}/cancel` | команда | `LeaveWaitlist` |

Правила:
- контроллер зависит только от `ICommandProcessor` или `IQueryProcessor` и формирует ответ;
- обработчик координирует сценарий и владеет транзакцией ([060](060_transactions.md));
- **обработчики не вызывают друг друга.** Общая логика вынесена в компоненты без собственной транзакции. Например, перевод из очереди вызывают обработчики отмены и изменения мест внутри уже открытой транзакции;
- отдельной модели чтения и событий нет.

## Последствия
- Один обработчик на ручку дальше разрастается в отдельный файл тестов на обработчик.
- Пакет в статусе alpha, и его код тоже будет на виду у проверяющих.
