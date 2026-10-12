# 150. Настройки

**Статус:** принято.

## Решение
Все числовые параметры правил вынесены в классы настроек и `appsettings.json`. **Значения по умолчанию совпадают с контрактом** — на них рассчитана автопроверка.

```json
"Booking": {
  "RefundCutoff": "02:00:00",
  "WaitlistCloseBefore": "01:00:00"
},
"Schedule": {
  "DefaultLimit": 20,
  "MaxLimit": 100
},
"RateLimit": {
  "PermitLimit": 60,
  "Window": "00:01:00",
  "RedisBreakDuration": "00:00:30"
},
"Idempotency": {
  "Retention": "1.00:00:00",
  "CleanupInterval": "01:00:00",
  "CleanupBatchSize": 1000
},
"Cache": {
  "EnrollmentCardTtl": "00:05:00"
}
```

- Классы настроек проверяются при старте (`ValidateOnStart`): например, `WaitlistCloseBefore` положительный.
- В тестах значения переопределяются через `ApiApplicationFactory`, например большой `PermitLimit` для всех тестов, кроме тестов ограничителя.
