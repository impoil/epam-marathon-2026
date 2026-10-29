# Домашнє завдання №5 — Green&Tasty Backend (.NET)

Реалізація всіх трьох рівнів у вихідному C# backend: Lambda → handlers → services → repositories → DynamoDB. Специфікація: [homework-backend.md](../../homework-backend.md). Python Lambda та стек `infra/homework4` не змінені.

| Рівень | Операція | Реалізація |
|---|---|---|
| 1 | `GET /guest/groups/{groupReservationId}/sessions` | Знімок даних усіх учасників, включно з Owner, страви, quantity, decimal total та isSubmitted |
| 2 | `GET /reservations/{reservationId}/guest-session-id` | Лише власна owner session власника reservation |
| 2 | `POST /group-reservations/{groupReservationId}/submit` | reservationId із JSON; лише submitted sessions; агрегація dishId; Order=SubmittedByCustomer та Group=Submitted атомарно |
| 3 | `DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}` | Повторно використано наявний метод; Owner-only, належність до групи, захист owner session, оновлений знімок даних |

Усі чотири операції використовують довірений Cognito `sub`, а не userId із тіла чи query. Відсутність автентифікації → 401, інший власник → 403. Повні request/response та помилки: [ApiRoutes.md](../../backend/restaurant-backend-app/docs/ApiRoutes.md).

Приклади відповідей:

```json
{"participants":[{"id":"guest-1","type":"Guest","name":"Andrii","items":[{"dish":{"id":"dish1","name":"Caesar Salad","price":180},"quantity":2}],"total":360,"isSubmitted":true}],"total":360}
```

```json
{"guestSessionId":"owner-session-123"}
```

POST із тілом `{"reservationId":"reservation-123"}` повертає:

```json
[{"dishId":"dish1","quantity":3},{"dishId":"dish2","quantity":2}]
```

Після видалення останнього guest за порожньої owner session відповідь містить owner з `items:[]`, `total:0`; видалення самої owner session заборонено. Повторний submit → 400, повторне видалення відсутньої session → 404.

## Основні зміни

- [GuestSessionService](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GuestSessions/GuestSessionService.cs): спільне формування знімка даних для GET/DELETE, пошук власної owner session, валідація до видалення; target виключається локально, без залежності від затримки GSI.
- [GroupReservationService](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GroupReservations/GroupReservationService.cs): агрегація, перевірки, detached copies перед записом; наявний `CreateGroupOwnerSessionAsync` підключено до створення групи. Підрахунок місткості враховує Owner один раз.
- [GroupReservationRepository](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Repositories/GroupReservations/GroupReservationRepository.cs): одна умовна `TransactWriteItems` для order та group, захист від повторного/конкурентного submit. Наявні права `dynamodb:PutItem` охоплюють умовні Put у цій транзакції.
- [RouteRegistry](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs) та [конфігурація API Gateway](../../backend/restaurant-backend-app/deployment_resources.json): усі чотири маршрути з Cognito authorizer; виправлено `guest/group` → `guest/groups`. Registry дає змогу перевіряти DI з моками без підключення до AWS.
- [Модульні тести](../../backend/restaurant-backend-app/Tests/Homework5): handlers, сервіси, реальні зіставлення DynamoDB з mock-клієнтом, маршрути та DI.

## Перевірка

Потрібен офіційний .NET SDK 10: проєкти вже на `net10.0`. Перевірено в Codex Cloud SDK `10.0.403`. Установлення на комп'ютер користувача не виконувалося.

```bash
cd backend/restaurant-backend-app
dotnet build restaurant-backend-app.sln --configuration Release
dotnet test restaurant-backend-app.sln --configuration Release --no-build --no-restore
dotnet test Tests/APIHandler.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~Homework5
```

Для обмеженого хмарного середовища, де домашній каталог доступний лише для читання, слід спрямувати `DOTNET_CLI_HOME` та `NUGET_PACKAGES` на каталоги, доступні для запису. Тести AWS використовують лише моки; облікові дані та підключення до AWS не потрібні.

Вихідний коміт `77d1e2aa4e95af77700b3090f39d0b78d474b0da`: **141 passed, 0 failed, 0 skipped**. Підсумковий результат наведено в [verification.md](verification.md). Перевіряються успіх, відсутні ресурси, 400/401/403/404, порожні та draft sessions, однакові dishId, статуси, видалення й total, повторні операції, помилки сховища без зміни завантажених об'єктів.

NuGet повідомляє про наявні вразливості ImageSharp та System.Security.Cryptography.Pkcs/Xml; також залишаються вихідні попередження nullable/xUnit. Тести вихідної версії не мають збоїв. Оновлення цих залежностей потребує окремої перевірки зображень і звітів до публічного розгортання.

## Готовність до здачі та Fresh Deploy

Код і модульні перевірки придатні для перевірки ДЗ. Публічна AWS URL-адреса не створювалася, робота в AWS не перевірялася. Оцінка для `071954286972 / eu-north-1`, перелік компонентів та обмеження: [FreshDeploy.md](FreshDeploy.md).
