# Fresh Deploy: оценка без подключения к AWS

Цель: отдельный настоящий .NET backend в account `071954286972`, `eu-north-1`, без изменений работающего `infra/homework4`. AWS API/CLI не вызывались, ресурсы не создавались. Доступность аккаунта, квоты, фактические цены и поддержка runtime в регионе не проверены.

**Технически отдельное малозатратное демо возможно, но текущая конфигурация не является готовым безопасным Fresh Deploy всего Green&Tasty.** Нужен отдельный reviewable IaC/пакет развёртывания. Статический JSON, Python Lambda и reuse ресурсов ДЗ №4 не подходят.

## Минимальное демо четырёх операций

- Реальная Lambda `APIHandler::APIHandler.Function::FunctionHandler`, совместимый .NET 10 runtime/пакет, IAM role и CloudWatch Logs. Начать с 256–512 MB и проверить cold start; текущие 128 MB не проверены нагрузкой.
- Новый API Gateway REST API: четыре маршрута из ДЗ, Cognito user-pool authorizer, Lambda invoke permissions, CORS, throttling. Для подготовки данных можно ограниченно добавить существующие guest/cart endpoints; публично открывать все management routes не требуется.
- Новый Cognito User Pool и App Client, тестовый Owner и второй пользователь для проверки 403. Одной Lambda Function URL недостаточно для существующего контракта `RequestContext.Authorizer.Claims.sub`.
- Пять новых DynamoDB tables: Reservations (id), GroupReservations (groupReservationId), GuestSessions (guestSessionId), Orders (orderId + customerId), Dishes (id). Индексы: GroupReservations.reservationId, GuestSessions.groupReservationId, Orders.GSI_ReservationId; token/cart endpoints дополнительно требуют индексы токенов. Полные схемы уже перечислены в backend deployment_resources.json.
- Связанные seed-данные reservation/group/owner session/guest sessions/dishes/order; настоящие JWT и проверки всех четырёх операций. Существующий GET owner id возвращает 404, если у старой группы нет owner session; старым данным нужен контролируемый backfill, GET не создаёт данные скрыто.
- Все env variables из lambda_config.json: Registry конструирует и другие handlers/services при cold start, поэтому надо задать также корректные конфигурационные значения для их зависимостей. Для ограниченного demo неиспользуемые S3/SQS/report ресурсы могут оставаться отключёнными за счёт отсутствия маршрутов; их env значения всё равно должны быть определены.

Для реального end-to-end сценария с созданием бронирований через API понадобятся также Tables, Locations, Waiters и их индексы, тестовые стол/локация/официант. Seed-only demo проверяет ДЗ, но не весь жизненный цикл ресторана.

## Полный Green&Tasty

Кроме минимальных компонентов: все 13 DynamoDB tables и 14 GSI из backend конфигурации, S3 buckets изображений/профилей и при необходимости Swagger, SQS очередь отчётов, дополнительные `reports-handler` и `report-sender` Lambda, SES с верифицированным отправителем для рассылки, триггеры/расписания отчётов, Cognito группы/пользователи, IAM policies, корректная упаковка native dependencies для PDF/Excel. Frontend для публичного API URL не обязателен; для полного пользовательского интерфейса понадобится отдельный хостинг.

Текущие определения содержат таблицы/API/политики и lambdas, но не полный самодостаточный стек Cognito/SQS/всех S3 ресурсов. В aliases указан `us-west-2`, общие имена вроде `Orders`, `GuestSessions` и placeholder имени Swagger bucket. `groupReservationsTable` и `guestSessionTable` в Lambda config заданы фиксированными именами. Нельзя применять эти шаблоны вслепую в Fresh Deploy: нужен новый namespace, физические имена/env mappings и регион, отдельный state/stack и проверка ownership ресурсов.

## Что необходимо перед публичным запуском

1. Создать отдельный IaC-план только для нового namespace (например `hw5-dotnet-*`), с review списка create/change/delete. Ничего не импортировать из `infra/homework4`.
2. Закрыть лишние routes. Например, существующий `POST /waiters` в deployment_resources.json имеет `authorization_type: NONE`, а AddWaiterHandler не проверяет администратора. Полный исходный API требует отдельного аудита прав; эти старые операции не входят в scope ДЗ №5.
3. Ограничить существующую общую IAM policy с `Resource: "*"` новыми таблицами/индексами/buckets/queues/user pool. Для условных Put в TransactWriteItems нужны права PutItem на обе новые таблицы.
4. Обновить известные уязвимые NuGet dependencies и проверить обработку изображений/PDF; проверить пакет и runtime в `eu-north-1`, Linux/native dependencies, cold start и memory. Локальная сборка сама по себе этого не доказывает.
5. Выполнить настоящие smoke tests с Cognito JWT: чужие reservation → 403, отсутствие ресурсов, submit и оба статуса в DynamoDB, delete и новый total. Согласовать удаление тестового стека после сдачи.

## Стоимость и сложность

Для короткого демо подходят Lambda по запросу без provisioned concurrency, API Gateway с лимитами, DynamoDB PAY_PER_REQUEST без постоянных provisioned capacity, короткое хранение logs. VPC/NAT Gateway, EC2, ALB и платный домен не нужны. Добавить бюджет/оповещение и лимит concurrency; free tier заранее не предполагается.

Ориентир при сотнях/тысячах тестовых запросов и малых данных — единицы USD за месяц, не гарантия цены или верхнего лимита. Текущий полный шаблон использует provisioned RCU/WCU для большинства таблиц/GSI, поэтому даже простой может стоить заметно больше; разумный бюджет полного учебного стека — десятки USD/месяц до уточнения AWS Calculator. Пользователи Cognito, email/SMS, логи, S3 и внешние вызовы оплачиваются отдельно по фактическому использованию.

Оценка трудоёмкости: ограниченный demo с отдельным IaC и seed — примерно 0,5–1 рабочий день после доступа и утверждения плана; полный стек, исправление старых проблем доступа/зависимостей, наполнение данными и smoke tests — несколько рабочих дней. Это оценка по репозиторию, без cloud discovery. Развёртывание требует отдельного разрешения; в этом ДЗ оно не выполнялось.

## Пределы текущей проверки

Unit tests подтверждают handlers/services, JSON, DI и содержание одной условной DynamoDB-транзакции с реальной SDK-сериализацией и mock transport. Они не заменяют интеграционные тесты настоящих DynamoDB/Cognito/API Gateway. Group/session GSI имеют eventual consistency: немедленный ответ DELETE локально исключает удалённую session, но отдельный GET или submit сразу после изменения участника может кратковременно видеть старый индекс. Перед production-нагрузкой нужны отдельная стратегия согласованности snapshot и проверка конкурентных guest edits/delete/submit; атомарность записи order+group уже обеспечена.
