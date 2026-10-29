# Перевірка домашнього завдання №5

Середовище: Codex Cloud, Linux, офіційний .NET SDK **10.0.403**, цільова платформа **net10.0**. Перевірку виконано 28 жовтня 2026 року. AWS API/CLI не викликалися.

| Перевірка | Результат |
|---|---|
| Вихідна версія `77d1e2aa4e95af77700b3090f39d0b78d474b0da`, `dotnet test` | 141 passed, 0 failed, 0 skipped |
| Підсумкове рішення, `dotnet build --configuration Release --no-restore` | Успішно, 0 errors; наявні попередження |
| Підсумкове рішення, `dotnet test --configuration Release --no-build --no-restore` | 248 passed, 0 failed, 0 skipped |
| Лише нові `FullyQualifiedName~Homework5` | 107 passed, 0 failed, 0 skipped |

Повне рішення містить APIHandler, APIHandler.Tests, ReportSender та ReportsHandler. Збоїв старих тестів не виявлено. Змінено один старий тестовий fixture: додано mock-залежності конструктора GroupReservationService; самі старі перевірки не видалялися й не послаблювалися.

Нові тести охоплюють чотири handlers, їхні сервіси, SDK-серіалізацію умовної транзакції, зіставлення HTTP-помилок, точну структуру JSON, справжню реєстрацію в RouteRegistry та DI з mock AWS repositories, відповідність Cognito authorizer у конфігурації API Gateway.

Обов'язкові сценарії: успіх; відсутність reservation/group/session/order; сторонній власник і порожній Cognito subject; підроблений userId у body; відсутність submitted sessions/страв; draft sessions виключені із submit; однакові dishId підсумовуються; некоректні кількості й overflow; належність group/reservation/order; видалення owner session заборонено; новий total; Closed/Submitted group та non-Created order; повторний submit/delete; помилка сховища без зміни завантажених даних. Відповідь DELETE не залежить від появи видаленої session у застарілому GSI.

Попередження вихідних залежностей охоплюють вразливості SixLabors.ImageSharp 3.1.12, System.Security.Cryptography.Pkcs 8.0.1 та System.Security.Cryptography.Xml 8.0.4, а також nullable, застарілі виклики SDK і дві старі xUnit Theory без параметрів. Їх не приховували й не перетворювали на пропущені тести. Оновлення пакетів та інтеграційні тести з реальним середовищем зазначено як передумови публічного розгортання у [FreshDeploy.md](FreshDeploy.md).

Перед комітом виконано `git diff --check`, перегляд diff, перевірку доданих файлів на секрети/облікові дані та заборонені файли збірки/стану. До PR входять лише C# backend, його конфігурація API Gateway, тести та документація. `infra/homework4` не змінено; SDK, кеш NuGet, артефакти збірки, облікові дані та Terraform state до коміту не входять.
