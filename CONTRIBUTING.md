# Участие в разработке

## Сообщения коммитов

Используем спецификацию [Conventional Commits 1.0.0](https://www.conventionalcommits.org/ru/v1.0.0/):

```text
<type>(<scope>): <description>
```

`scope` указывать необязательно. Допустимые области проекта:

- `api` — ASP.NET Core API;
- `web` — React-приложение;
- `tests` — общая тестовая инфраструктура;
- `ci` — GitHub Actions;
- `deps` — зависимости;
- `repo` — общие файлы и конфигурация репозитория.

Допустимые типы:

- `feat` — новая пользовательская возможность;
- `fix` — исправление ошибки;
- `refactor` — изменение кода без изменения поведения;
- `test` — добавление или исправление тестов;
- `docs` — документация;
- `build` — сборка и зависимости;
- `ci` — непрерывная интеграция;
- `perf` — улучшение производительности;
- `style` — форматирование без изменения поведения;
- `chore` — прочее обслуживание репозитория;
- `revert` — отмена предыдущего коммита.

Описание пишем на английском языке, в нижнем регистре, в повелительной форме,
без точки в конце. Один коммит должен выражать одно законченное изменение.

Примеры:

```text
feat(api): add meeting type endpoint
fix(web): prevent booking an occupied slot
test(api): add application smoke test
ci: run checks on every push
docs: document local setup
```

Несовместимое изменение отмечаем `!` после типа или области и поясняем в теле:

```text
feat(api)!: change booking response format

BREAKING CHANGE: booking identifiers are now returned as UUID strings.
```

Тело и footer отделяем пустой строкой. Для связи с задачей используем footer,
например `Refs: #42` или `Closes: #42`.

Commitlint проверяет все новые коммиты после принятия этого соглашения. Старые
коммиты до baseline не переписываются.
