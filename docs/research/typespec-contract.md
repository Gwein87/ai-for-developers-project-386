# TypeSpec, генерация OpenAPI и проверка API-контракта

Дата исследования: 8 ноября 2026 года. Контекст: задача
[«Исследовать генерацию OpenAPI из TypeSpec и проверку API-контракта»](https://github.com/Gwein87/ai-for-developers-project-386/issues/4).
Это исследование для принятия решения; команды и конфигурация ниже — предложения,
они не применялись. Термины соответствуют актуальным требованиям карты.

## Вывод

Рекомендуется цепочка **TypeSpec → OpenAPI 3.0.0 JSON → TypeScript-типы**.
Хранить исходник и оба генерируемых артефакта в Git; проверять воспроизводимость
генерации отдельно от соответствия настоящих HTTP-ответов. Бэкенд ASP.NET Core 10
реализует контракт, а интеграционные xUnit-тесты читают тот же OpenAPI.
Это предложение для проекта, а не установленное требование инструментов.

## Подтверждённые возможности и версии

Проверен конкретный стабильный набор `@typespec/compiler`, `@typespec/http`,
`@typespec/openapi`, `@typespec/openapi3` **1.17.0**. Метаданные npm для emitter
требуют compiler/http/openapi `^1.17.0`; четыре точные версии удовлетворяют этим
диапазонам. Пакеты требуют Node.js `>=22.0.0`. Дополнительные peers
json-schema/versioning/streams/events/sse необязательны для обычного HTTP-контракта.
Проверены опубликованные метаданные и исходники стабильного тега, установка
и компиляция не выполнялись. [Compiler 1.17.0](https://raw.githubusercontent.com/microsoft/typespec/typespec-stable@1.17.0/packages/compiler/package.json),
[emitter 1.17.0](https://raw.githubusercontent.com/microsoft/typespec/typespec-stable@1.17.0/packages/openapi3/package.json),
[опубликованные метаданные emitter](https://registry.npmjs.org/@typespec%2fopenapi3/1.17.0).

Emitter 1.17.0 поддерживает OpenAPI **3.0.0, 3.1.0 и 3.2.0**, сериализацию YAML
и JSON; по умолчанию используется 3.0.0. Рекомендация начать с **3.0.0 JSON**
снижает требования к последующим инструментам; JSON удобен для чтения тестами.
При необходимости 3.1 выбирается явно, вместе с проверкой совместимости валидатора.
[Опции в стабильном исходнике](https://raw.githubusercontent.com/microsoft/typespec/typespec-stable@1.17.0/packages/openapi3/src/lib.ts).

Для фронтенда подходит **openapi-typescript 7.10.1**: поддерживает OpenAPI 3.0/3.1,
генерирует типы без runtime-кода, peer TypeScript — `^5.x`, что совместимо
с текущим `~5.9.3`. Типы сами не проверяют JSON во время выполнения.
[Пакет версии 7.10.1](https://raw.githubusercontent.com/openapi-ts/openapi-typescript/openapi-typescript@7.10.1/packages/openapi-typescript/package.json),
[возможности генератора](https://openapi-ts.dev/introduction).

## Предлагаемый воспроизводимый процесс

В корневые devDependencies при реализации добавить точные версии пяти пакетов
выше, без `^`/`~`. Использовать существующий Node.js 24 из quality workflow;
для локальной среды и CI зафиксировать одинаковую patch-версию Node и версию npm.
После изменения зависимостей выполнить из корня:

```sh
npm install --save-dev --save-exact @typespec/compiler@1.17.0 @typespec/http@1.17.0 @typespec/openapi@1.17.0 @typespec/openapi3@1.17.0 openapi-typescript@7.10.1
```

Закоммитить сгенерированный корневой `package-lock.json`, в CI использовать
`npm ci`. Lock-файл фиксирует также транзитивные зависимости — точных прямых
версий для этого недостаточно. [Рекомендации TypeSpec по воспроизводимости](https://typespec.io/docs/handbook/reproducibility/).

Предлагаемые пути:

| Файл | Роль |
| --- | --- |
| `src/HexletCalendar.Contracts/main.tsp` | Единственный редактируемый исходник HTTP-контракта |
| `src/HexletCalendar.Contracts/tspconfig.yaml` | Настройки генерации |
| `src/HexletCalendar.Contracts/generated/openapi.json` | Генерируемый контракт для тестов, документации и фронтенда |
| `src/HexletCalendar.Web/src/api/generated/schema.d.ts` | Генерируемые типы фронтенда |

`HexletCalendar.Contracts` здесь — каталог, не новый .NET-проект. Входной
TypeSpec импортирует `@typespec/http` и `@typespec/openapi`, задаёт `@service`,
операции и модели. Пример предлагаемого `tspconfig.yaml`:

```yaml
emit:
  - "@typespec/openapi3"
options:
  "@typespec/openapi3":
    emitter-output-dir: "{project-root}/generated"
    file-type: json
    output-file: openapi.json
    openapi-versions:
      - "3.0.0"
    new-line: lf
```

Команды должны быть корневыми npm scripts, чтобы запускать установленный локально
CLI и не скачивать незакреплённый `latest`:

```json
{
  "contract:openapi": "tsp compile src/HexletCalendar.Contracts --warn-as-error",
  "contract:types": "openapi-typescript src/HexletCalendar.Contracts/generated/openapi.json -o src/HexletCalendar.Web/src/api/generated/schema.d.ts",
  "contract:generate": "npm run contract:openapi && npm run contract:types",
  "contract:check": "node scripts/check-contract.mjs"
}
```

`check-contract.mjs` предлагается написать как переносимую проверку: сгенерировать
оба результата во временные каталоги, сравнить байты и набор файлов с Git-артефактами,
завершиться ненулевым кодом при отличиях или отсутствии файла. Не переписывать
проверяемые файлы до сравнения. Простого `git diff --exit-code` недостаточно:
он не замечает новые untracked-файлы. Настройки LF исключают различия Windows/Linux.
У генератора TS есть отдельный `--check`; он проверяет только актуальность TS,
а не актуальность OpenAPI или работу API. [CLI openapi-typescript](https://openapi-ts.dev/cli),
[настройки emitter](https://typespec.io/docs/emitters/openapi3/reference/emitter/).

При реализации добавить `contract:check` в начало существующего `npm run check`,
сохранив все нынешние проверки. `.github/workflows/hexlet-check.yml` не менять.
Выходные файлы не редактировать вручную; изменение контракта проходит через `.tsp`.
Это политика проекта, требующая согласования.

## Типы, время и ошибки

**Факты.** `int32` отображается в OpenAPI как `integer/int32`;
`@minValue`, `@maxValue`, `@minLength`, `@maxLength`, `@pattern` задают ограничения
схемы. `@encode("rfc3339")` для `utcDateTime` даёт `string/date-time`.
[Правила преобразования emitter](https://typespec.io/docs/emitters/openapi3/openapi/).
`plainDate` — календарная дата без часового пояса, `plainTime` — время на часах
без пояса; `integer` не ограничен диапазоном C# `int`, поэтому использовать его
для идентификатора типа события было бы неточно.
[Встроенные типы](https://typespec.io/docs/standard-library/built-in-data-types/).

**Предложения для согласования HTTP-контракта:**

- Идентификатор типа события: `int32`, положительный; в C# `int`, в TS `number`.
  Автоинкремент БД не является клиентским полем создания. Явные minimum/maximum
  полезны, поскольку поддержка числового `format` зависит от валидатора.
- Длительность в минутах: `int32` с согласованными границами, не ISO duration.
- Начало/конец слота и бронирования: момент времени RFC 3339, например
  `2026-11-08T06:00:00Z`, с явным поясом; без неоднозначных локальных datetime.
  Рекомендуется `utcDateTime` с явным RFC 3339 encoding и выдача UTC. В C# удобно
  `DateTimeOffset`, в транспортных TS-типах сохранять строку. Если требуется
  строго суффикс `Z`, это дополнительное правило, которое нужно выразить
  ограничением и тестом: общий `format: date-time` допускает смещения.
- Параметр календарного дня, если понадобится: `plainDate`, `YYYY-MM-DD`.
  Окно «сегодня + 13 дней» вычислять в согласованном `Europe/Moscow`, не в UTC
  и не в часовом поясе браузера. Имя зоны и UTC offset — разные данные;
  `plainTime` само по себе не идентифицирует слот.

TypeSpec позволяет выразить отдельные ответы через union, `@statusCode`, `@body`
и `@header`; `@error` помечает модель ошибки, но не выбирает за проект конкретные
HTTP-коды и структуру сообщения. [Модели ответов](https://typespec.io/docs/getting-started/getting-started-rest/07-custom-response-models/).

Рекомендуемый вариант для обсуждения: **400** для ошибок формата/валидации,
**409** для уже занятого слота, `application/problem+json` с `type`, `title`,
`status`, необязательными `detail`/`instance`; расширения `code` для стабильного
машинного кода и `errors: Record<string[]>` для ошибок полей. Обязательность
полей — решение проекта: RFC 9457 описывает стандартную форму и расширения,
но не требует такой набор полей для любого ответа.
[RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html).
В TypeSpec описывать реальные модели тела и явный Content-Type каждого ответа.
Отдельно согласовать обработку отсутствующего типа события и недоступного времени,
чтобы они не попадали автоматически в общий конфликт.

ASP.NET Core предоставляет Problem Details и настройку их содержимого;
в .NET 10 валидация Minimal API включается через `AddValidation`, а ошибки
возвращаются как 400. Эти механизмы не читают TypeSpec и сами не гарантируют
согласованный `code`, имена полей, media type и одинаковое тело для плохого JSON,
ошибки привязки параметров, валидации и бизнес-конфликта.
[Обработка ошибок ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0),
[валидация .NET 10](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0).

## Проверка соответствия работающего сервера

Генерация доказывает, что исходник компилируется и артефакты актуальны.
Она **не доказывает**, что обработчик возвращает описанный JSON, отвергает
неверный запрос или атомарно запрещает пересечение бронирований.

Минимально рекомендуются xUnit + `Microsoft.AspNetCore.Mvc.Testing` в существующем
backend test project. `WebApplicationFactory` запускает приложение через TestServer
и даёт HttpClient; реальный порт для этого не требуется.
[Интеграционные тесты ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0&pivots=xunit).

Предлагаемый тестовый помощник читает **сгенерированный** OpenAPI, выбирает
операцию по method/path, проверяет фактические status, Content-Type, обязательные
заголовки и JSON по схеме ответа с разрешением `$ref`. Для OpenAPI 3.0 возможен
парсер NSwag.Core и валидация схем через NJsonSchema; библиотека заявляет чтение,
проверку JSON и поддержку OpenAPI DTO schemas. Это кандидат для реализации,
не проверенная здесь совместная интеграция с .NET 10: её версии потребуется
закрепить после короткой проверки на используемых схемах, nullable, `$ref`,
int32 и date-time. Нельзя передавать весь OpenAPI как обычную JSON Schema.
[NSwag](https://github.com/RicoSuter/NSwag),
[NJsonSchema: возможности и Validate](https://github.com/RicoSuter/NJsonSchema).

У каждого договорённого ответа нужен сценарий; у каждого endpoint — хотя бы
один запрос. Отдельный тест покрытия сравнивает method/path из OpenAPI с перечнем
сценариев, чтобы новая операция не осталась без проверки. Для валидатора нужны
контрольные испорченные ответы: отсутствующее обязательное поле, неверный тип,
неверный формат даты и неизвестный HTTP status должны давать ошибку проверки.
Эти тесты ловят и ложное ощущение защиты от неправильно подключённого валидатора.

Негативные сценарии должны отправлять отсутствующие/неверные поля, плохой JSON,
дробный/выходящий за int32 идентификатор и дату без пояса. Бизнес-тесты отдельно
проверяют пересечение между типами событий, общую границу интервалов, прошлое время,
границу окна и два конкурирующих запроса на один слот. Проверка схемы ответа
не заменяет проверку состояния БД и атомарности. Все эти тесты должны попадать
в существующий `npm test` и далее в `npm run check`.

Альтернатива для последующего усиления — Schemathesis по тому же JSON и поднятому
HTTP API: генерирует запросы и проверяет status, media type, headers и response
schema. Это дополнительная Python-зависимость и отдельный запуск сервера,
поэтому для минимального старта не предлагается; бизнес-инварианты всё равно
нуждаются в сценариях. [Проверки Schemathesis](https://github.com/schemathesis/schemathesis/blob/master/docs/reference/checks.md).

## ASP.NET Core и React без второго источника

Сейчас `Program.cs` вызывает `AddOpenApi`/`MapOpenApi`, а README обещает
`GET /openapi/v1.json` в Development. При будущем переходе рекомендуется
**заменить runtime-генерацию выдачей TypeSpec-артефакта по прежнему URL**, включив
JSON в выходные файлы приложения; условия доступности оставить предметом
реализации. Приёмка должна сравнивать выдаваемый JSON с артефактом.
Так DTO/атрибуты ASP.NET не создают альтернативную нормативную спецификацию.
В этом исследовании код и README не изменены.

На фронтенде брать request/response types из `schema.d.ts`, использовать их
в небольшом адаптере над `fetch` и в тестовых данных. Генерация типов не создаёт
клиент, не форматирует даты и не обеспечивает runtime-валидацию. Полный генератор
серверного кода или SDK для минимального старта не требуется; транспортные DTO
бэкенда можно реализовать вручную и проверять HTTP-тестами по единому артефакту.

## Что ещё должен решить человек

1. Принять ли предложенные версии, OpenAPI 3.0 JSON, хранение обоих результатов
   в Git и замену runtime OpenAPI на выдачу артефакта.
2. В задаче HTTP-контракта согласовать конкретные маршруты, поля, обязательность,
   границы строк и длительности, точность времени и допустимость входных смещений.
3. Там же выбрать форму Problem Details, машинные коды и различие 400/404/409
   для недоступного времени и неизвестного типа события; определить политику
   неизвестных JSON-полей и nullable. Исследование этих продуктовых решений
   не принимает.

До реализации остаётся техническая проверка всей закреплённой цепочки и выбранного
.NET-валидатора на небольшом контракте. Это явно непроверенная часть рекомендации,
а не выполненный прототип. Зависимости не установлены, исходники приложения,
CI и миграции не изменялись. Для единственного Markdown-отчёта `npm run check`
не запускался.
