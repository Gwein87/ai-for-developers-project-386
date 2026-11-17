# Календарь звонков

Сервис бронирования звонков. Владелец создаёт типы событий с указанием наименования, описания и длительности. Гость выбирает подходящие время и тип события и создаёт бронирование.

Правила сообщений коммитов описаны в [CONTRIBUTING.md](CONTRIBUTING.md).
Термины предметной области определены в [GLOSSARY.md](GLOSSARY.md).

## Сценарий работы

### Владелец календаря

1. Создаёт тип события с указанием наименования, описания и длительности.
2. Просматривает список бронирований.

### Гость

1. Выбирает тип события.
2. Открывает календарь со слотами на ближайшие 14 дней.
3. Выбирает свободное время и подтверждает запись на звонок.

Продолжительность слота определяется выбранным типом события. После создания бронирования занятые слоты исключаются из доступных. Бронирования не могут пересекаться по времени, даже если относятся к разным типам событий.

## Стек и структура

- Бэкенд: .NET 10 и ASP.NET Core. SQLite через EF Core.
- Фронтенд: React, TypeScript, Vite, Tailwind CSS и [shadcn/ui](https://ui.shadcn.com/docs/installation/vite).
- Исходный код находится в `src`, тесты — в `tests`.
- В `HexletCalendar.slnx` включены только проекты бэкенда.

```text
src/
  HexletCalendar.Api/             HTTP API бэкенда
  HexletCalendar.Data/            контекст, модель и миграции SQLite
  HexletCalendar.Contracts/       TypeSpec и генерируемые артефакты
  HexletCalendar.ContractGenerator/ генератор DTO C#
  HexletCalendar.Web/             React-приложение
tests/
  HexletCalendar.Api.Tests/       интеграционные тесты API
  HexletCalendar.Web.Tests/       тесты React-приложения
```

API использует общий модуль хранения `HexletCalendar.Data`: модель, контекст EF Core
и CLI-миграции. `HexletCalendar.ContractGenerator` генерирует DTO независимо от API.

## Локальный запуск

Требуются .NET SDK 10 и Node.js 20.19+ либо 22.12+ с npm.
Команды ниже выполняются из корня репозитория.

```sh
dotnet restore src/HexletCalendar.Api/HexletCalendar.Api.csproj
dotnet tool restore
npm install
```

Перед первым запуском [подготовьте SQLite](#sqlite-и-миграции).

Запустите бэкенд и фронтенд в двух терминалах:

```sh
dotnet run --project src/HexletCalendar.Api --launch-profile http
```

```sh
npm run dev
```

Фронтенд: <http://localhost:5173>. Vite перенаправляет запросы `/api` на
<http://localhost:5294>. Главная страница описывает сервис и содержит навигацию:
«Записаться» ведёт на `/booking`: первый экран выбора типа события.
В разделе владельца календаря (`/admin`) владелец создаёт типы событий и видит их список.
API предоставляет `GET /api/health`, `GET /api/event-types` и
`POST /api/event-types`; в Development также доступен
`GET /openapi/v1.json`. Health-проверка подтверждает работу API, без обращения к БД.
В production запросы `/api` должны направляться к бэкенду через reverse proxy.
Для frontend-маршрутов `/booking` и `/admin` сервер должен возвращать `index.html`.

## Локальный запуск в Docker

Соберите один образ с API и frontend из корня репозитория:

```sh
docker build -t hexlet-calendar .
```

Для учебного запуска пустая SQLite подготавливается в образе через `dotnet ef`
при `docker build`. Контейнер запускает приложение с этой базой; при новом
контейнере данные сбрасываются. Миграции при запуске приложения не выполняются.

```sh
docker run --rm -p 8080:8080 hexlet-calendar
```

Контейнер слушает порт из `PORT` (по умолчанию 8080). Например:

```sh
docker run --rm -e PORT=9090 -p 9090:9090 hexlet-calendar
```

Приложение доступно на <http://localhost:8080>; проверка API —
<http://localhost:8080/api/health>.

Учебный экземпляр на Render: <https://hexlet-calendar-386.onrender.com>.
Он использует бесплатный сервис без постоянного диска. После перезапуска сервиса,
нового развёртывания или пересоздания контейнера данные сбрасываются до пустой
базы из образа, поэтому созданные типы событий и бронирования исчезают.

Для подключения Render MCP к Codex используйте OAuth:

```sh
codex mcp add render --url https://mcp.render.com/mcp --oauth-client-id codex
```

Завершите вход в открывшемся браузере. Безопасный шаблон конфигурации находится
в `.codex/config.toml.example`; данные OAuth-сессии и ключи в репозиторий не
добавляются.

Если данные нужно сохранить между запусками, подготовьте SQLite отдельно через
CLI в каталоге `data` и подключите его к контейнеру (PowerShell):

```powershell
New-Item -ItemType Directory -Force data
dotnet tool restore
dotnet ef database update --project src/HexletCalendar.Data --startup-project src/HexletCalendar.Api --connection "Data Source=$((Resolve-Path data).Path)/calendar.db"
docker run --rm -p 8080:8080 --mount "type=bind,source=$((Resolve-Path data).Path),target=/data" -e 'ConnectionStrings__Calendar=Data Source=/data/calendar.db;Mode=ReadWrite' hexlet-calendar
```

Каталог `data` хранит SQLite и её WAL-файлы между запусками контейнера.

Для Linux/macOS используйте `-v "$(pwd)/data:/data"` вместо PowerShell-выражения
в `--mount`; предварительно создайте каталог `data`.

## Сборка и проверки

```sh
npm run check
```

Команда запускает линтер, тесты фронтенда и бэкенда, а затем production-сборку
React-приложения. GitHub Actions выполняет эту же команду при каждом `push`.

В обязательные quality gates входят:

- ESLint и Knip для frontend-кода и зависимостей;
- анализаторы .NET SDK, `dotnet format` и NuGet Audit;
- Commitlint для новых сообщений коммитов;
- jactionlint для поддерживаемых workflow GitHub Actions;
- OSV-Scanner при каждом push и по еженедельному расписанию.

OSV-Scanner временно исключает `GHSA-g3hv-75wg-m249` для dev-only зависимостей
ESLint до появления совместимого исправления. Исключение пересматривается
2 декабря 2026 года и задано в `osv-scanner.toml`.

Фронтенд тестируется с помощью Vitest и React Testing Library в окружении
jsdom. Дымовой тест монтирует корневой компонент и проверяет, что стартовый
экран приложения отображается.

Бэкенд тестируется с помощью xUnit и `Microsoft.AspNetCore.Mvc.Testing`.
Дымовой тест поднимает ASP.NET Core-приложение в памяти и проверяет ответ
`GET /api/health`.

Решение содержит API, модуль хранения, генератор контракта и тесты API. Для сборки бэкенда используйте:

```sh
dotnet build src/HexletCalendar.Api/HexletCalendar.Api.csproj
```

Компоненты shadcn/ui находятся в `src/HexletCalendar.Web/src/components/ui`.
Настройки CLI — в `components.json`. Для добавления компонентов:

```sh
cd src/HexletCalendar.Web
npx shadcn@latest add dialog
```

## SQLite и миграции

База — один локальный файл SQLite. Приложение открывает существующий файл
(`Mode=ReadWrite`), не создаёт базу и не применяет миграции при запуске.
Подготовьте базу явно из корня репозитория:

```sh
dotnet tool restore
dotnet ef database update --project src/HexletCalendar.Data --startup-project src/HexletCalendar.Api --connection "Data Source=hexlet-calendar.db"
```

Относительный путь в строке подключения приложения считается от его рабочего
каталога. Для запуска из разных каталогов задайте один абсолютный путь:
`ConnectionStrings__Calendar="Data Source=/absolute/path/calendar.db;Mode=ReadWrite"`.
EF Core SQLite включает WAL при явном создании файла через CLI; это свойство
сохраняется в файле. Все процессы API должны использовать один и тот же файл.
Health-проверка остаётся доступной без подготовленной базы. Отсутствующая или
неподготовленная база даёт техническую ошибку при обращении к данным.

**Перед созданием каждой миграции необходимо получить отдельное разрешение
пользователя.** Создание и применение выполняются только через `dotnet ef`.
Не используйте `EnsureCreated`, `Migrate` или SQL-скрипты для обхода этого порядка.

`npm run test:api` явно обновляет тестовый шаблон в
`tests/HexletCalendar.Api.Tests/Fixtures/calendar.db` через CLI до запуска API.
Каждый сценарий работает с отдельной временной копией настоящей SQLite.
Для отдельного запуска xUnit сначала выполните `npm run db:test:prepare`.

## Контракт API

Редактируемый источник — `src/HexletCalendar.Contracts/main.tsp`.
OpenAPI 3.0.0 JSON, DTO C# и типы TypeScript находятся в `generated` и хранятся
в Git с LF. После изменения TypeSpec выполните из корня:

```sh
npm run contract:generate
npm run check
```

Сгенерированные файлы вручную не редактируются. Проверка дважды независимо
генерирует OpenAPI, TypeScript и DTO C#, сравнивая с сохранёнными файлами.
Отсутствующий артефакт также считается ошибкой.
Приложения используют эти типы; HTTP-тесты проверяют ответы по OpenAPI.
В Development `/openapi/v1.json` выдаёт этот же сгенерированный файл.

TypeSpec закреплён на 1.17.0, openapi-typescript — 7.10.1, TypeScript — 5.9.3;
NSwag — 14.7.1, NJsonSchema — 11.6.1. Узкий override undici 7.30.1 относится
только к `@scalar/json-magic@0.13.5`. Нативный SQLite закреплён через
SQLitePCLRaw.lib.e_sqlite3 3.53.3, чтобы не использовать уязвимую транзитивную
2.1.11 ([advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)).
