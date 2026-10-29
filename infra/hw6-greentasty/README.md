# Реальный AWS deployment Green&Tasty — подготовка ДЗ №5–6

29 октября 2026, Windows. Основа — `main ab483fc`, PR #1 объединён. **Deployment не выполнялся.** AWS-ресурсы не создавались/не менялись; apply/destroy, seed upload и создание пользователей не запускались. `infra/homework4` и его state не изменены, ничего из старого стека не импортируется.

## Готовность и блокеры

Готовы настоящий .NET ZIP, отдельный Terraform root, production build workflow Angular, генератор тестовых данных и E2E checklist. Статические JSON и моки не заменяют endpoints. `routes.json`/`tables.json` — определения инфраструктуры, а не ответы API.

После восстановления AWS CLI выполнены проверки через настоящий аккаунт: STS подтвердил **071954286972**, временную роль `AccountFullAccessRole`, источник credentials `login`. В профиле отсутствует явный region; CLI, provider и backend используют **eu-north-1** явно. Free Plan **FREE / ACTIVE**, остаток **$140**, срок плана **26.04.2027 20:11:35 Europe/Kyiv**. Free Tier usage API вернул пустой список — это не подтверждение нулевого текущего счёта.

**Полноценный remote plan от 29.10.2026 23:26 Europe/Kyiv: 32 Add / 0 Change / 0 Destroy, replacements 0**, без target, refresh=false или обхода STS. S3 backend подтверждён и подключён; отдельный key `hw6-greentasty/terraform.tfstate`, `encrypt=true`, `use_lockfile=true`. Для строго read-only plan использован `-lock=false`; ни state, ни lock object в S3 не созданы. План сохранён локально в ignored `build/hw6.tfplan`; binary plan/state в Git не включены. Полный перечень, AWS-инвентарь, смета и доказательства изоляции: [AWS verification](../../docs/homework6/aws-verification.md).

Важное ограничение: фактическая региональная квота Lambda — **5**. Окончательный план использует **`-var-file=demo.tfvars.example`**, где `reserved_concurrency=null` (в плане `-1`, без резерва). Исходный default 2 тоже даёт 32/0/0, но непригоден для запуска при требовании AWS оставить 100 unreserved executions. Квота не менялась. Новый стек будет делить concurrency с ДЗ №4/HelloWorld; это ограничение изоляции на уровне аккаунта. Реальный BACKEND_URL появится после отдельно разрешённого создания API.

## Минимальный полноценный стек

Angular → HTTPS CloudFront → приватный S3/OAC. Angular обращается к региональному REST API Gateway `/demo` → существующий .NET backend → DynamoDB/Cognito. Используется встроенный домен/сертификат CloudFront. REST выбран для совместимости с `APIGatewayProxyRequest` и `RequestContext.Authorizer.Claims`.

| Компоненты | Количество / назначение |
| --- | --- |
| DynamoDB | 10 таблиц: Reservations, GroupReservations, GuestSessions, Orders, Dishes, Locations, Tables, Waiters, Feedback, Notifications |
| GSI | 13 индексов внутри таблиц, имена/типы из backend; Orders: `orderId + customerId` |
| Cognito | Новый User Pool Lite, public App Client без secret, группа Customer: 3 resources |
| Выполнение | Lambda, IAM role/inline policy, log group: 4 resources |
| API | REST API, Lambda permission, deployment, stage, settings, 2 gateway responses: 7 resources |
| Hosting | S3 bucket/public access block/SSE/policy; CloudFront distribution/OAC/function/headers policy: 8 resources |

Все имена изолированы префиксом `hw6-greentasty`. PAY_PER_REQUEST на таблицах и GSI; максимум каждого — 10 read units/s, 5 write units/s. Feedback требуется для чтения рейтинга локаций, Notifications — настоящему CreateOrder. Provisioned capacity/PITR/streams не включены.

Lambda: .NET 10, Linux x64, 512 MB, timeout 15 s; handler `APIHandler::APIHandler.Function::FunctionHandler`. ZIP около 1.8 MiB содержит DLL/deps/runtimeconfig в корне. `dotnet/APIHandler.csproj` компилирует **реальные исходники backend**; отдельный composition root включает 33 необходимых метода. Исходные бизнес-сервисы и DynamoDB-транзакция используются напрямую.

VPC/NAT, EC2, ALB, provisioned concurrency, SQS/SES/report Lambdas и Swagger hosting не создаются. Owner — Customer, владеющий бронью (`customerId == sub`), отдельной роли Owner нет. Registration, управление рестораном/официантами, uploads и reports исключены и из API Gateway, и из .NET-пакета. Это демо Customer/Owner/guest flow, не полный административный Green&Tasty.

## Безопасность и исправления

- Cognito authorizer нового pool проверяет ID JWT и `aud` нового App Client. Owner handlers дополнительно проверяют владение. Guest routes используют собственный случайный bearer-token, SHA256 hash которого хранится в DynamoDB; public invite проверяет состояние группы/лимит участников.
- Исправлены IDOR чтения чужой брони и получения чужого invite-token. Защищённый invite-token response теперь включает PascalCase `invite` metadata. Owner Review использует его, сохраняя fallback для старого backend: владелец может открыть Review при полной/Submitted/Closed группе, публичный invite при этом остаётся закрыт.
- Исправлены case-sensitive Authorization, 500 при malformed guest token и Scan локаций без namespace override. Missing/invalid guest credentials возвращают 401.
- Корзина Owner синхронизируется из настоящих Orders в owner GuestSession. Для группы личный submit подтверждает только её корзину, сохраняя Order Created до общего Submit. Edit снимает подтверждение; legacy unsubmit не может снова открыть Submitted/Closed группу. Одиночные брони сохраняют прежнее поведение. Синхронизация Orders/owner session выполняет отдельные записи, поэтому после частичного отказа нужен повтор/проверка; общий финальный Submit остаётся условной атомарной транзакцией Orders/GroupReservations. Конкурентные изменения корзин при общем Submit требуют отдельного нагрузочного/интеграционного теста.
- Runtime IAM: чтение только новых таблиц/индексов; запись только Reservations, GroupReservations, GuestSessions, Orders, Waiters, Notifications; AdminGetUser/ListUsers/AdminListGroupsForUser только нового pool. Транзакция DynamoDB авторизуется через PutItem/ConditionCheckItem. Нет management/S3/SQS/SES/IAM grants или доступа к ресурсам ДЗ №4.
- CORS в Lambda/OPTIONS/Gateway errors — точный HTTPS origin новой CloudFront distribution, без credentials. CORS не заменяет authorization.
- Throttling API 5 requests/s, burst 10. Окончательный remote plan учитывает измеренную Lambda quota 5 и использует `demo.tfvars.example` без reserved concurrency. Default 2 оставлен как отдельный ограничительный вариант, требующий подходящей квоты; не применять его в текущем аккаунте. Лимиты best effort, не hard spending cap; общая квота может приводить к взаимному throttling с ДЗ №4. Не менять квоты автоматически.
- Lambda logs retention 7 дней; request path/query/headers/body не логируются, invite URL содержит bearer capability. REST execution/data tracing и CloudFront access/query logs выключены. Общая account-level CloudWatch role для REST API не меняется. Старые handlers могут писать ID/описания ошибок — использовать только синтетические данные.
- Cognito: self-signup выключен; admin-only recovery, пароль ≥14, ID/access tokens 30 минут. Нет SMS/автоматических писем/паролей в Terraform. MFA выключена для учебного демо.
- S3 закрыт, AES256, TLS-only, OAC только своей distribution; HTTPS redirect, nosniff, frame DENY, no-referrer. DynamoDB/Cognito защищены от случайного удаления, S3 force_destroy=false.
- npm audit **32 → 0**: lockfile обновлён в существующих semver-диапазонах, без `audit fix` и новых app dependencies. Frontend 1.2.1. Большой diff lockfile обусловлен security update; `npm ci` прошёл.
- Полный legacy backend сохраняет ImageSharp 3.1.12 с известными уязвимостями: исправленная 3.2.0 потребовала лицензию в Release. В deployment-проекте ImageSharp/NPOI/QuestPDF и соответствующие uploads/reports отсутствуют; этих DLL нет в ZIP. NuGet audit **deployable project: 0 findings**. Xml/Pkcs полного backend обновлены до 10.0.14. Полный legacy deployment требует отдельного решения по ImageSharp.

## Проверки

.NET SDK 10.0.403, Node 24.22.1, npm CLI 12.2.1, Terraform 1.17.0, AWS provider 6.70.0, Chrome Headless 155, Windows.

| Проверка | Результат |
| --- | --- |
| Полный backend test project | 268 passed, 0 failed |
| Реальный deployment composition/отсутствие management routes | 5 passed |
| Linux Release publish/ZIP | Passed |
| Deployable NuGet audit, direct + transitive | 0 findings |
| npm ci / audit | Passed / 0 findings |
| TypeScript app/spec | Passed |
| Production config node:test | 6 passed |
| Angular production build/bundle marker check | Passed; initial 972.13 kB / transfer 223.16 kB |
| test:hw / test:starter | 79 / 58 passed |
| Полный frontend | 283 passed / 47 failed; все 47 имён совпадают с сохранённым baseline, новых failures нет |
| Browser production `?demo` | Storage пуст, protected route перенаправляет на `/login` |
| Terraform fmt/init backend=false/validate | Passed, lockfile Windows/Linux |
| Remote init/plan | Passed: 32 Add / 0 Change / 0 Destroy, profile `demo.tfvars.example` |
| Deployed E2E | Не выполнен: deployment не разрешён и не выполнялся |

Сохраняются nullable/obsolete .NET warnings и CommonJS/style/bundle warnings Angular. Initial bundle близок к error budget 1 MB; бюджет не повышен. Исходные homework tests не переписаны и не отключены.

Angular скомпилирован с техническим **несуществующим** `https://0000000000.execute-api.eu-north-1.amazonaws.com/demo`: исключительно проверка компиляции, не готовый к публикации frontend artifact. Реальный URL нельзя получить без создания нового API. `scripts/build-frontend.ps1` берёт настоящий Terraform output и отвергает этот технический адрес; публикация потребует повторной сборки.

```powershell
# Из корня репозитория, установлен .NET 10 SDK.
./infra/hw6-greentasty/scripts/package.ps1
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
dotnet test infra/hw6-greentasty/dotnet-tests/Deployment.Tests.csproj
dotnet restore infra/hw6-greentasty/dotnet/APIHandler.csproj --locked-mode
terraform -chdir=infra/hw6-greentasty init -backend=false
terraform -chdir=infra/hw6-greentasty fmt -check
terraform -chdir=infra/hw6-greentasty validate
```

Dependencies фиксируются `dotnet/packages.lock.json`. ZIP/publish, seed, state/plan, backend.hcl и login material не коммитятся.

## AWS-проверки и получение плана

1. Перед будущим deployment повторно проверить `aws sts get-caller-identity --profile fresh-deploy --region eu-north-1`; при истечении сессии завершить `aws login --profile fresh-deploy`. Текущая проверка успешна. При другой identity/ошибке остановиться. Credentials/JWT не присылать.
2. `scripts/inspect-aws.ps1 -OutputPath build/aws-inventory.json` — только чтение, остановка при первой ошибке. Проверить Free Plan, остаток/срок credits в Billing, доступность сервисов, инвентарь и concurrency. Free Tier API обслуживается в us-east-1; ресурсы — eu-north-1. Cost Explorer API ради проверки не используется.
3. Bucket `epam-hw4-state-freshdeploy-20261027` подтверждён с `--expected-bucket-owner 071954286972`: Stockholm, versioning Enabled, AES256, все четыре public access block flags, TLS Deny policy. Настройки bucket не менялись. Отдельный key: **`hw6-greentasty/terraform.tfstate`**. Старый `homework4/group-preorder/terraform.tfstate` не использовать. Его VersionId/ETag/LastModified и конфигурация Lambda ДЗ №4 до/после plan совпали.
4. Пример скопирован в ignored `backend.hcl`. Windows remote init: `terraform '-chdir=infra/hw6-greentasty' init -input=false -reconfigure '-backend-config=backend.hcl'`. Кавычки вокруг аргумента с `.hcl` важны для PowerShell. Не использовать migrate-state/import/force-unlock и не запускать команды в ДЗ №4. Backend bucket не создавать.
5. `terraform '-chdir=infra/hw6-greentasty' plan -input=false -lock=false '-var-file=demo.tfvars.example' '-out=build/hw6.tfplan'`. Null для numeric variable передаётся через tfvars, а не `-var=reserved_concurrency=null`. Здесь `-lock=false` сохраняет строго read-only подготовку: S3 lock object тоже запись. Других операций над hw6 state в это время быть не должно. Plan/state не публиковать. Получено только создание hw6 resources; при будущих change/delete, неожиданном ресурсе/расходе остановиться.
6. Перед отдельно разрешённым apply получить новый plan с обычным state locking. Эти инструкции не разрешают apply, seed, users или upload. Budget/уведомление в Billing рассмотреть отдельно; budget не является hard cap и в этом PR не создаётся.

## Последовательность после отдельного разрешения

Согласованный plan → новый стек → тестовые Cognito users → справочники → Angular с настоящим URL → upload → E2E. Скриптов автоматического apply/upload в PR нет.

Создать в новом pool двух Customer (Owner и чужой Customer для проверки 403), добавить их в Customer group. Для Waiters подготовить синтетического Cognito пользователя-официанта и взять настоящий `sub`. Через admin/console workflow использовать suppress delivery, permanent пароль ≥14, email/given_name/family_name/email_verified=true. Пароли не помещать в CLI history/Git/Terraform. Access keys не создавать. Для Owner/гостей нужны разные browser profiles.

`scripts/seed.ps1 -WaiterSub <реальный-sub>` по умолчанию **только создаёт локальный** `build/seed.json`: локация, стол capacity=6, официант и блюда $17/$10. Изображения `/assets/img/...` берутся из настоящего frontend. Sample manifest подготовлен с синтетическим UUID, перед записью нужен реальный sub. Только отдельно разрешённый `-Execute` вставляет записи в новые таблицы с `attribute_not_exists`, без перезаписи. При частичном seed/повторном запуске сначала разобраться с состоянием. Reservations/groups/sessions/orders создаются настоящими endpoints, не seed.

`scripts/build-frontend.ps1` читает реальный `backend_url` из hw6 Terraform output и запускает npm ci/build. HTTPS обязателен; URL credentials/query/fragment запрещены, TS генерируется через JSON escaping. Demo markers/placeholder проверяются после build. Production providers пустые, `?demo` не авторизует.

После разрешения загрузить Angular в новый private bucket: hashed js/css — immutable cache, index.html/unhashed файлы — no-cache; при необходимости CloudFront invalidation. Проверить Content-Type, SPA deep links и отсутствие source maps. Старый bucket ДЗ №4 не использовать.

## Реальный E2E checklist

1. Войти через `/signin` настоящим Cognito Customer, проверить `/me`. Без JWT owner endpoints → 401; истёкший/чужой app-client JWT не проходит authorizer; management routes → 403/404.
2. Создать в UI бронь на **3 участников**, минимум за несколько часов с учётом ограничений pre-order. Проверить настоящие Reservations, GroupReservations Open и owner GuestSession.
3. Owner добавить salad ×1 ($17), создать/подтвердить личный pre-order через `/orders`, чтобы существовали Order Created и owner-session IsSubmitted=true; проверить owner session ID.
4. Invite открыть в отдельном профиле без Cognito, создать guest session, добавить main ×2 ($20), подтвердить cart. Второму гостю добавить/подтвердить salad ×1. Не выводить bearer/JWT в отчёт.
5. Owner Review после заполнения группы показывает $54; polling ≤15 s/Refresh обновляет настоящие данные, draft не учитываются. Проверить loading/error/429/retry и mobile view.
6. Удалить второго гостя через диалог → total $37, owner остаётся, токен удалённой session больше не работает. Проверить запрет удаления owner и повторного удаления.
7. Другим Customer проверить 403 для чужих reservation/invite-token/session-id/sessions/status/remove/submit. Guest bearer не работает как Owner JWT; bearer одной session не открывает другую; malformed/missing header → 401, lowercase header работает.
8. Submit group → salad ×1 + main ×2, Group Submitted и Order SubmittedByCustomer. Проверить настоящий `GET /orders/{reservationId}`, group status и DynamoDB read. Повторный submit и изменения закрытой группы отклоняются.
9. Перезагрузить Owner Review → итоговый read-only статус. Старый public invite не допускает join. В CloudWatch отсутствуют JWT/password/invite URL; сохранять только синтетические IDs/request IDs/статусы.
10. Проверить Billing/Free Plan после теста; согласовать срок хранения. Load testing и удаление стека отдельно не разрешены.

## Расходы

Ориентир на 29.10.2026, USD без налогов, только новый стек. Ставки eu-north-1 и Europe CDN получены через настоящий AWS Price List API; подробная модель: [AWS verification](../../docs/homework6/aws-verification.md). FREE/ACTIVE и $140 credits подтверждены, но ниже консервативная стоимость до free allowances/credits, а не обещание размера счёта.

| Сценарий | Оценка |
| --- | --- |
| Создание | Фиксированного setup fee нет; короткий seed/smoke обычно < $0.10 API/write charges плюс трафик frontend |
| Простой | Нет compute/provisioned capacity fees; при малых данных storage обычно < $0.10/месяц |
| Небольшое демо | При явно заданной нагрузке: расчёт **$0.43–1.19/месяц** до льгот/credits, плюс переменный API egress; практический резерв **$1–5/месяц** |
| Нежелательный трафик | Может существенно превысить $5; throttling не является денежным потолком, CDN egress/rejected API requests тоже учитываются |

Пример без free quota: 10 000 × 0.5 GB × 0.5 s = 2 500 GB-s, около $0.042 Lambda compute плюс requests; REST API порядка $3.50/млн ($0.035 за 10 000). Остальное — DynamoDB/GSI writes, storage/log ingestion и CDN egress. Это оценка по публичным ставкам, точную региональную смету подтвердить в Calculator до apply. Малое число Cognito Lite users может покрываться MAU free quota; право аккаунта проверить отдельно.

Источники: [Lambda](https://aws.amazon.com/lambda/pricing/), [API Gateway](https://aws.amazon.com/api-gateway/pricing/), [DynamoDB](https://aws.amazon.com/dynamodb/pricing/), [Cognito](https://aws.amazon.com/cognito/pricing/), [CloudFront](https://aws.amazon.com/cloudfront/pricing/), [S3](https://aws.amazon.com/s3/pricing/), [CloudWatch](https://aws.amazon.com/cloudwatch/pricing/), [Free Plan API](https://docs.aws.amazon.com/cli/latest/reference/freetier/get-account-plan-state.html), [.NET Lambda](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html).

Авторизация, Free Plan, S3 backend и create-only remote plan теперь подтверждены. Перед запуском остаются отдельное разрешение, обновление плана с locking, осознанный выбор profile без reservation при общей quota 5, проверка apply-time ограничений сервисов Free Plan, реальный URL, Linux Lambda cold start/latency и настоящий E2E. Terraform plan не создаёт ресурсы и не проверяет все Create API ограничения. Полный legacy backend (uploads/reports/waiter/admin) безопасным этим PR не объявляется.
