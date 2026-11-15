# Чувствительность теста пересечения бронирований

Правило: бронирования разных типов событий не могут частично пересекаться; общая занятость календаря проверяется при подтверждении.

Базовый коммит: `2f8e942`. Эксперимент выполнен в отдельном временном worktree. Временное нарушение ограничило запрос занятости условием `item.EventTypeId == eventTypeId`; сломанная реализация не коммитилась и не публиковалась.

Команда обоих запусков:

```text
dotnet test tests/HexletCalendar.Api.Tests/HexletCalendar.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~ConcurrentOverlappingRequestsAcrossTypesHaveOnlyOneWinner
```

- Красный: exit 1, `Failed: 1`, `Passed: 0`; `Assert.Equal() Failure`, `Expected: 1`, `Actual: 2` в `BookingTests.cs:150`. Оба запроса разных типов на частично пересекающееся время получили подтверждение, поскольку нарушенная проверка не видела бронь другого типа.
- Зелёный после восстановления общего запроса занятости: exit 0, `Passed: 1`, `Failed: 0`.

Первый запуск до подготовки SQLite fixture падал на отсутствии `calendar.db` и не засчитывался как красный результат. Fixture подготовлена штатной `npm run db:test:prepare` через `dotnet ef` без создания новой миграции.
