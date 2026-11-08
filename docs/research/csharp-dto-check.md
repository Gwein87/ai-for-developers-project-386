# Проверка генерации C# DTO

Проверено 8 ноября 2026 года для [«Проверить генерацию C# DTO из единого OpenAPI-контракта»](https://github.com/Gwein87/ai-for-developers-project-386/issues/10).
Это продолжение предыдущей проверки TypeSpec/OpenAPI/TypeScript в той же отдельной ветке codex/contract-toolchain-check. Пробный GET /probe ненормативный; приложение в main и миграции не менялись.

## Результат

Из одного TypeSpec-контракта получаются OpenAPI 3.0.0 JSON, TypeScript-типы и C# DTO.
Генерация C# выполняется NSwag.CodeGeneration.CSharp 14.7.1 с NJsonSchema.CodeGeneration.CSharp 11.6.1.
Генерируются только DTO: GenerateClientClasses=false, GenerateClientInterfaces=false, GenerateExceptionClasses=false, GenerateDtoTypes=true.

Финальный npm run check прошёл: ESLint, форматирование и анализаторы .NET, Knip, Commitlint, 5 frontend-тестов, 15 backend-тестов, production-сборка.
Ранее проверенные версии Node.js 24.19.0, npm 11.17.0, .NET SDK 10.0.302, TypeSpec 1.17.0 и openapi-typescript 7.10.1 сохранены.

## Воспроизведение

Из корня проверочной ветки:

```sh
npm ci
npm run contract:generate
npm run check
```

contract:generate последовательно генерирует JSON, .d.ts и ProbeDtos.g.cs.
C#-часть запускается scripts/generate-csharp-probe.mjs и отдельным xUnit-сценарием CSharpDtoToolchainTests с явным флагом обновления проверочного артефакта.
Обычный npm run check этот флаг не устанавливает: тест сравнивает результат с сохранённым файлом и не переписывает его.

C# генерируется дважды независимо и сравнивается побайтно; затем сравнивается с сохранённым артефактом.
LF сохраняется правилом .gitattributes.
После генерации детерминированная обработка убирает строки отключения/восстановления предупреждений из стандартного шаблона NSwag и пробелы на концах строк. Ручного редактирования DTO и новых исключений проверок нет.

## Проверенная компиляция и сериализация

Тест создаёт временный проект HexletCalendar.DtoSerializationProbe с net10.0, Nullable=enable и TreatWarningsAsErrors=true.
В него копируются именно сгенерированные классы. Проект компилируется, запускается отдельным процессом и:

- создаёт DTO со вложенным объектом и nullable строкой;
- сериализует и десериализует его System.Text.Json, сохраняя имена JSON-полей и значения;
- проверяет сгенерированные DataAnnotations для допустимого объекта, нижней границы id и обязательного часового пояса в timestamp;
- отклоняет неизвестное JSON-поле через JsonUnmappedMemberHandling.Disallow.

Полученный JSON дополнительно проверяется по исходной сгенерированной OpenAPI-схеме NJsonSchema.
Таким образом проверяется не только наличие файла C#, но и его реальная компиляция, выполнение, сериализация и соответствие JSON.

Генератор настроен на SystemTextJson, DataAnnotations, nullable reference types и nullable optional properties.
Пробный timestamp оставлен строкой через DateTimeType=string: это сохраняет исходную форму транспортного значения и позволяет проверять pattern до преобразования в момент времени.
В предметном коде после проверки его можно преобразовать в DateTimeOffset и Unix timestamp. Это не требует генерировать сущности EF Core из контракта.

## Закрытые объекты и неизвестные поля

Для emitter включён seal-object-schemas=true: в OpenAPI явно записывается additionalProperties=false.
Генератор C# тогда не добавляет JsonExtensionData для произвольных неизвестных полей.
Сам System.Text.Json всё равно требует настройки UnmappedMemberHandling=Disallow; одна генерация DTO не включает серверную политику автоматически.

Генерируемые атрибуты тоже не заменяют вызов валидации в ASP.NET Core.
В частности, nullable тип поля не доказывает наличие обязательного nullable поля в исходном JSON; полноту требуется проверять по транспортному контракту и отрицательным сценариям.
Проверка самого API, бизнес-ограничений, конкурентности SQLite и идемпотентности остаётся отдельной.

## Границы

Подтверждена совместимость для простых DTO с вложенными объектами, nullable, int32, строковым timestamp и обычной ошибкой.
Полиморфизм, все варианты OpenAPI и генерация контроллеров/клиентского SDK не проверялись и первой версии не требуются.
Версии, параметры генератора и схемы должны оставаться закреплёнными; изменения требуют повторной проверки.

Генерация C# согласована пользователем как обязательное требование.
Окончательные DTO реального приложения появятся после [согласования API](https://github.com/Gwein87/ai-for-developers-project-386/issues/7); этот пробный контракт не переносится в main.
Существующий override undici и ранее согласованное исключение minimatch не расширялись.

Источники: [NSwag CSharpClientGenerator](https://github.com/RicoSuter/NSwag/wiki/CSharpClientGenerator), [настройки C# генератора](https://github.com/RicoSuter/NSwag/wiki/CSharpGeneratorBaseSettings), [NSwag.CodeGeneration.CSharp 14.7.1](https://www.nuget.org/packages/NSwag.CodeGeneration.CSharp/14.7.1). Результаты запуска подтверждаются кодом теста и воспроизводимыми командами этой ветки.
