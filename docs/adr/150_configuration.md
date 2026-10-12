# 150. Настройки

**Статус:** принято.

## Решение
Все числовые параметры правил вынесены в классы настроек и `appsettings.json`. **Значения по умолчанию совпадают с контрактом** — на них рассчитана автопроверка.

**Длительности задаются целым числом секунд, а не строкой времени.** Имя параметра оканчивается на `Seconds`. Так значения однозначны и удобно задаются переменными окружения: `Booking__RefundCutoffSeconds=7200`. Класс настроек дополнительно отдаёт `TimeSpan` (`RefundCutoff`), код правил работает с ним.

```json
"Booking": {
  "RefundCutoffSeconds": 7200,
  "WaitlistCloseBeforeSeconds": 3600
},
"Schedule": {
  "DefaultLimit": 20,
  "MaxLimit": 100
},
"RateLimit": {
  "PermitLimit": 60,
  "WindowSeconds": 60,
  "RedisBreakDurationSeconds": 30
},
"Idempotency": {
  "RetentionSeconds": 86400,
  "CleanupIntervalSeconds": 3600,
  "CleanupBatchSize": 1000
},
"Cache": {
  "EnrollmentCardTtlSeconds": 300
}
```

- Классы настроек проверяются при старте (`ValidateOnStart`): например, `WaitlistCloseBeforeSeconds` положительный.
- В тестах значения переопределяются через `ApiApplicationFactory`, например большой `PermitLimit` для всех тестов, кроме тестов ограничителя.
