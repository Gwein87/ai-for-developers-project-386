# Трекер задач: GitHub

Задачи и спецификации этого репозитория хранятся в GitHub Issues. Для всех
операций используй GitHub CLI `gh`.

## Основные операции

- Создание: `gh issue create --title "..." --body-file <file>`.
- Чтение: `gh issue view <number> --json number,title,body,labels,comments`.
- Поиск: `gh issue list --state open --json number,title,body,labels,comments`.
- Комментарий: `gh issue comment <number> --body-file <file>`.
- Добавление и удаление меток: `gh issue edit <number> --add-label "..."` и
  `gh issue edit <number> --remove-label "..."`.
- Закрытие: сначала добавь комментарий с объяснением, затем выполни
  `gh issue close <number>`.
- Дочерняя задача: `gh issue create --parent <parent> ...` или
  `gh issue edit <parent> --add-sub-issue <child>`.
- Зависимость: `gh issue edit <child> --add-blocked-by <blocker>`.

Репозиторий определяется по `git remote`. Перед первым созданием задачи в
сессии проверь цель командой:

```shell
gh repo view --json nameWithOwner
```

Создавай задачи только в репозитории пользователя. Для этого проекта ожидаемая
цель — `Gwein87/ai-for-developers-project-386`.

## Pull request как источник запросов

**Рассматривать PR как запросы для triage: нет.**

Изменение этого значения на «да» включает внешние PR в очередь triage.

## Публикация в трекер задач

Если навык требует опубликовать результат в трекере задач, создай GitHub Issue.

## Получение задачи

Если навык требует получить связанную задачу, прочитай соответствующий GitHub
Issue вместе с метками и комментариями.

## Операции Wayfinder

Карта работы хранится в одной задаче с меткой `wayfinder:map`, а отдельные
работы оформляются как её дочерние задачи.

- Тип дочерней задачи задаётся меткой `wayfinder:<type>`, где `<type>` —
  `research`, `prototype`, `grilling` или `task`.
- Заблокированную задачу не начинай, пока все её блокирующие задачи не закрыты.
- При получении задачи назначь её на себя командой
  `gh issue edit <number> --add-assignee @me`.
- При завершении добавь комментарий с результатом и закрой задачу.
