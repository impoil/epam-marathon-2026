# Homework #5 — Green&Tasty Backend (.NET)

Реализация всех трёх уровней в исходном C# backend: Lambda → handlers → services → repositories → DynamoDB. Спецификация: [homework-backend.md](../../homework-backend.md). Python Lambda и стек `infra/homework4` не изменены.

| Уровень | Операция | Реализация |
|---|---|---|
| 1 | `GET /guest/groups/{groupReservationId}/sessions` | Snapshot всех участников, включая Owner, блюда, quantity, decimal total и isSubmitted |
| 2 | `GET /reservations/{reservationId}/guest-session-id` | Только собственная owner session владельца reservation |
| 2 | `POST /group-reservations/{groupReservationId}/submit` | reservationId из JSON; только submitted sessions; агрегация dishId; Order=SubmittedByCustomer и Group=Submitted атомарно |
| 3 | `DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}` | Переиспользован существующий метод; Owner-only, принадлежность группе, защита owner session, обновлённый snapshot |

Все четыре операции используют доверенный Cognito `sub`, а не userId из тела или query. Отсутствие аутентификации → 401, другой владелец → 403. Полные request/response и ошибки: [ApiRoutes.md](../../backend/restaurant-backend-app/docs/ApiRoutes.md).

Примеры ответов:

```json
{"participants":[{"id":"guest-1","type":"Guest","name":"Andrii","items":[{"dish":{"id":"dish1","name":"Caesar Salad","price":180},"quantity":2}],"total":360,"isSubmitted":true}],"total":360}
```

```json
{"guestSessionId":"owner-session-123"}
```

POST с телом `{"reservationId":"reservation-123"}` возвращает:

```json
[{"dishId":"dish1","quantity":3},{"dishId":"dish2","quantity":2}]
```

После удаления последнего guest при пустой owner session ответ содержит owner с `items:[]`, `total:0`; удаление самой owner session запрещено. Повторный submit → 400, повторное удаление отсутствующей session → 404.

## Основные изменения

- [GuestSessionService](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GuestSessions/GuestSessionService.cs): общая сборка snapshot для GET/DELETE, поиск собственной owner session, валидация до удаления; target исключается локально, без зависимости от задержки GSI.
- [GroupReservationService](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GroupReservations/GroupReservationService.cs): агрегация, проверки, detached copies перед записью; существующий `CreateGroupOwnerSessionAsync` подключён к созданию группы. Подсчёт вместимости учитывает Owner один раз.
- [GroupReservationRepository](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Repositories/GroupReservations/GroupReservationRepository.cs): одна условная `TransactWriteItems` для order и group, защита от повторного/конкурентного submit. Существующие права `dynamodb:PutItem` покрывают условные Put в этой транзакции.
- [RouteRegistry](../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs) и [API Gateway config](../../backend/restaurant-backend-app/deployment_resources.json): все четыре маршрута с Cognito authorizer; исправлено `guest/group` → `guest/groups`. Registry допускает проверку DI с моками без подключения к AWS.
- [Unit tests](../../backend/restaurant-backend-app/Tests/Homework5): handlers, сервисы, реальные DynamoDB mappings с mock-клиентом, маршруты и DI.

## Проверка

Нужен официальный .NET SDK 10: проекты уже на `net10.0`. Проверено в Codex Cloud SDK `10.0.403`. Установка на компьютер пользователя не выполнялась.

```bash
cd backend/restaurant-backend-app
dotnet build restaurant-backend-app.sln --configuration Release
dotnet test restaurant-backend-app.sln --configuration Release --no-build --no-restore
dotnet test Tests/APIHandler.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~Homework5
```

Для restricted cloud, где домашняя директория read-only, задать `DOTNET_CLI_HOME` и `NUGET_PACKAGES` на доступные для записи каталоги. Тесты AWS используют только mocks; credentials и AWS-подключение не нужны.

Исходный commit `77d1e2aa4e95af77700b3090f39d0b78d474b0da`: **141 passed, 0 failed, 0 skipped**. Финальный результат приведён в [verification.md](verification.md). Проверяются успех, отсутствующие ресурсы, 400/401/403/404, пустые и draft sessions, одинаковые dishId, статусы, удаление и total, повторные операции, ошибки хранилища без изменения загруженных объектов.

NuGet сообщает о существующих уязвимостях ImageSharp и System.Security.Cryptography.Pkcs/Xml; также остаются исходные nullable/xUnit warnings. Тесты исходной версии не падают. Обновление этих зависимостей требует отдельной проверки изображений и отчётов до публичного развёртывания.

## Готовность к сдаче и Fresh Deploy

Код и unit-проверка пригодны для проверки ДЗ. Публичный AWS URL не создавался, работа в AWS не проверялась. Оценка для `071954286972 / eu-north-1`, перечень компонентов и ограничения: [FreshDeploy.md](FreshDeploy.md).
