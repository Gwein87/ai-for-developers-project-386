# Проверка цепочки TypeSpec и валидации HTTP-контракта

Проверено 8 ноября 2026 года для [«Проверить совместимость генерации и валидации контракта»](https://github.com/Gwein87/ai-for-developers-project-386/issues/9).
Ветка codex/contract-toolchain-check — изолированный проверочный артефакт, без PR и слияния в main. Контракт с GET /probe ненормативный: он не задаёт API HexletCalendar.

## Результат

Цепочка TypeSpec → OpenAPI 3.0.0 JSON → TypeScript-типы работает в установленной среде.
NSwag.Core читает этот OpenAPI, NJsonSchema проверяет JSON настоящих HTTP-ответов через WebApplicationFactory в .NET 10.
Две независимые генерации побайтно совпали с обоими сохранёнными артефактами.

Полный npm run check завершился успешно:

- ESLint;
- dotnet format для API и тестов, анализаторы .NET;
- Knip без новых исключений;
- Commitlint;
- 5 frontend-тестов;
- 14 backend-тестов, включая существующий дымовой тест;
- production-сборка React/Vite.

## Проверенные версии

| Инструмент | Версия |
| --- | --- |
| Node.js | 24.19.0 |
| npm | 11.17.0 |
| .NET SDK | 10.0.302 |
| @typespec/compiler, @typespec/http, @typespec/openapi, @typespec/openapi3 | 1.17.0 |
| openapi-typescript | 7.10.1 |
| TypeScript | 5.9.3 |
| NSwag.Core | 14.7.1 |
| NJsonSchema | 11.6.1 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.10 |

Прямые npm-зависимости закреплены точно; package-lock.json создан npm install.
NSwag.Core и NJsonSchema закреплены явно в тестовом csproj.

## Как воспроизвести

Из корня этой ветки:

```sh
npm ci
npm run contract:generate
npm run check
```

Команда contract:generate запускает локальный tsp compile с --warn-as-error, затем локальный openapi-typescript.
Настройки emitter задают OpenAPI 3.0.0, JSON и LF. Правило .gitattributes сохраняет
LF генерируемых артефактов при checkout в Windows и Linux; настройки emitter
сами по себе не отменяют core.autocrlf.
Команда contract:check компилирует модель для проверки доступности HTTP-операции и operationId, затем дважды генерирует JSON и .d.ts во временные каталоги и сравнивает их с сохранёнными файлами.
Отсутствующий или изменённый проверяемый артефакт вызывает ошибку.
Отдельный tsc --noEmit проверяет реальное использование сгенерированного типа в тесте.
TypeScript объявлен в корневых devDependencies, поскольку tsc вызывается из корневого npm script; это устранило сообщение Knip об undeclared binary без исключения.

Основные файлы:

- src/HexletCalendar.Contracts/main.tsp и tspconfig.yaml;
- src/HexletCalendar.Contracts/generated/openapi.json и schema.d.ts;
- scripts/check-contract-toolchain.mjs;
- tests/HexletCalendar.Web.Tests/ContractProbeTypes.test.tsx;
- tests/HexletCalendar.Api.Tests/ContractToolchainTests.cs.

## Что проверяет .NET

Пробный ответ проходит через настоящий HTTP pipeline TestServer, настроенный только внутри WebApplicationFactory; Program.cs приложения не менялся.
Помощник выбирает операцию по методу и пути, проверяет объявленный status и Content-Type, затем валидирует тело по схеме соответствующего ответа из того же сгенерированного JSON.

Проверены:

- корректные целочисленный id, timestamp с Z или явным смещением, вложенный $ref и nullable строка;
- отсутствие обязательного id;
- строка или дробное значение вместо целочисленного id;
- выход за явно заданную верхнюю границу int32;
- неправильная дата и дата без часового пояса;
- пропущенное обязательное поле вложенной модели и неправильный тип nullable поля;
- неизвестный HTTP status и Content-Type;
- отдельный объявленный ответ application/problem+json.

Успешные примеры принимаются, перечисленные испорченные ответы отвергаются.
Валидация не заменяет бизнес-тесты пересечений, повторов запросов и состояния SQLite.

## Найденное ограничение date-time

Первоначальный utcDateTime с RFC 3339 encoding дал string/date-time.
На NJsonSchema 11.6.1 ответ со значением 2026-11-08T09:00:00 без пояса неожиданно прошёл валидацию: соответствующий отрицательный тест упал, остальные 13 backend-тестов прошли.

Для проверки явного ограничения пробный timestamp заменён строковым scalar с format date-time и pattern, требующим полную дату, время и суффикс Z либо смещение.
После повторной генерации отрицательный тест прошёл вместе со всей цепочкой.
Это показывает, что одного format недостаточно для выбранного требования; формат транспорта и точный pattern для реального API ещё должен согласовать человек.

Применённый pattern проверяет форму и наличие пояса, а не доказывает полное покрытие всех календарных крайних случаев RFC 3339.
Для окончательного контракта нужны отрицательные примеры именно выбранной точности, зоны и диапазона.

## Транзитивная зависимость emitter

Установка emitter 1.17.0 выбрала @scalar/json-magic 0.13.5, который закрепляет undici 7.24.4.
npm audit сообщил высокую уязвимость undici и связанные предупреждения.
В проверочной ветке применён узкий override только @scalar/json-magic@0.13.5 → undici 7.30.1.
Повторный npm install сообщил только 7 moderate предупреждений существующей цепочки typescript-eslint/minimatch; новая высокая уязвимость исчезла.
С данным override генерация и npm run check проходят.

Существующее исключение GHSA-g3hv-75wg-m249 не расширялось; новых исключений и отключённых проверок нет.
При внедрении нужно заново проверить актуальные метаданные и аудит, предпочесть исправленную upstream-цепочку, если она появилась; решение о конкретном наборе зависимостей остаётся в вопросе об API.

## Границы проверки и дальнейшее решение

- Проверен маленький контракт с одной операцией, вложенным $ref, nullable, ограниченным int32 и строкой date-time.
- Не проверялись полный API приложения, генерация серверного кода, большие схемы или все конструкции OpenAPI.
- Вторая независимая runtime OpenAPI-спецификация не создавалась; будущая выдача TypeSpec-артефакта по существующему URL остаётся решением карты.
- Маршруты, формат времени, ответы ошибок и применение проверенных инструментов согласуются в [«Согласовать HTTP API и применение TypeSpec-контракта»](https://github.com/Gwein87/ai-for-developers-project-386/issues/7).
- Изменения существуют только в проверочной ветке. Миграций нет; основной рабочий каталог и приложение в main не менялись.

Источники для интерпретации результатов: [TypeSpec OpenAPI emitter](https://typespec.io/docs/emitters/openapi3/reference/emitter/), [openapi-typescript](https://openapi-ts.dev/introduction), [NSwag.Core 14.7.1](https://www.nuget.org/packages/NSwag.Core/14.7.1), [NJsonSchema 11.6.1](https://www.nuget.org/packages/NJsonSchema/11.6.1).
