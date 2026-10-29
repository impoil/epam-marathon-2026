# AWS: проверка аккаунта и remote plan Green&Tasty

**Исторический отчёт pre-apply.** После него отдельно разрешённый apply создал 22 ресурса и остановился на CloudFront account verification; актуальный remaining plan **10 Add / 0 Change / 0 Destroy** и реальные outputs: [deployment report](aws-deployment.md).

Проверено **29 октября 2026**, Windows, профиль `fresh-deploy`, регион ресурсов `eu-north-1`. Финальный plan: **23:26:34 Europe/Kyiv / 21:26:34 UTC**. Временная AWS-сессия восстановлена; предыдущий блокер истёкшей сессии снят.

**Результат: 32 Add / 0 Change / 0 Destroy, 0 replacements.** Это полноценный remote Terraform plan, не offline/targeted план. `apply`, `destroy`, import, миграция state, изменение квот, создание пользователей, запись seed, upload и изменение настроек существующих AWS-ресурсов не выполнялись. PR #2 остаётся draft и не объединён.

## Identity, регион и Free Plan

| Проверка | Результат |
| --- | --- |
| `sts get-caller-identity` | Account **071954286972**, assumed role **AccountFullAccessRole** |
| Credentials source, `aws configure list` | **login**, временная сессия AWS CLI; значения ключей/токенов не включены в отчёт |
| Region в AWS CLI profile | Не задан; все региональные команды явно передают `--region eu-north-1` |
| Provider/backend plan | `eu-north-1`, `fresh-deploy`, `allowed_account_ids=[071954286972]` |
| Free Plan API, us-east-1 | **FREE / ACTIVE** |
| Remaining credits | **140 USD**, агрегированное значение `accountPlanRemainingCredits` |
| Expiration плана | **26.04.2027 20:11:35 Europe/Kyiv**, значение API `2027-04-26T20:11:35.07+03:00` |
| `get-free-tier-usage` | `freeTierUsages=[]`; это не подтверждение нулевого текущего счёта |

Не создавались постоянные access keys, credentials не переносились в Terraform/env-файлы. Terraform backend/provider использовали native profile login. Отдельные credit IDs, правила применимости/срок каждого рекламного кредита и итоговая сумма invoice этим API не возвращаются. Cost Explorer и платные billing queries не вызывались. [Описание Free Plan API](https://docs.aws.amazon.com/aws-cost-management/latest/APIReference/API_freetier_GetAccountPlanState.html).

## Существующие ресурсы

Инвентарь охватывает перечисленные сервисы в Stockholm и глобальные S3/CloudFront/IAM namespace, **не все сервисы во всех AWS-регионах**.

| Сервис | Обнаружено |
| --- | --- |
| Lambda | 2: `epam-hw4-group-preorder`, `EPAM-Marathon-HelloWorld`; Python 3.14, по 128 MB |
| API Gateway REST | 0 |
| API Gateway HTTP | 1: `epam-hw4-group-preorder`, API ID `xv3cbcohed` |
| DynamoDB / Cognito user pools | 0 / 0 |
| S3 | 1: `epam-hw4-state-freshdeploy-20261027` |
| CloudFront distributions/functions/custom headers policies/OAC | Не обнаружены |
| EC2 instances / NAT Gateways / ALB/NLB | 0 / 0 / 0 |
| SQS queues / EventBridge rules на default bus | Не обнаружены |
| CloudWatch Logs | 3 группы: HW4 Lambda и API (retention 7 дней), HelloWorld (retention не задан) |
| IAM roles с префиксом `hw6-greentasty` | 0 |
| Планируемое имя frontend S3 bucket | `HeadBucket` вернул ожидаемый 404 — конфликта не обнаружено |

Старые ресурсы, включая retention HelloWorld, оставлены как есть. Реальные Lambda/API endpoints не вызывались ради smoke/load test.

**Квота Lambda:** account settings возвращает `ConcurrentExecutions=5`, `UnreservedConcurrentExecutions=5`, FunctionCount=2. У существующих Lambda reservation отсутствует. Cognito quota user pools/account — 1000.

AWS требует оставить 100 unreserved executions перед выделением reserved concurrency. Поэтому default `reserved_concurrency=2` здесь использовать нельзя, хотя Terraform способен построить для него план 32/0/0. Финальный вариант явно применяет **`demo.tfvars.example` с `reserved_concurrency=null`**; provider планирует `reserved_concurrent_executions=-1`. Никаких PutFunctionConcurrency или quota increases не было. [Требование AWS по concurrency](https://docs.aws.amazon.com/lambda/latest/dg/lambda-concurrency.html).

Это устраняет известный барьер reservation при подготовке, но новый Lambda делит региональный пул 5 с HW4/HelloWorld: при нагрузке возможен взаимный throttling. Изоляция ресурсов/state не означает отдельную account quota. Для демо оставить низкий API limit (5 rps/burst 10), не проводить load test; строгая изоляция concurrency требует отдельного решения и разрешения.

## S3 backend и неизменность ДЗ №4

- `HeadBucket` выполнен с `--expected-bucket-owner 071954286972`; местоположение **eu-north-1**.
- Versioning **Enabled**, default encryption **AES256**, BlockPublicAcls/IgnorePublicAcls/BlockPublicPolicy/RestrictPublicBuckets **true**. Bucket policy запрещает insecure transport для bucket и objects. Настройки не менялись.
- Новый root: `infra/hw6-greentasty`; новый key **`hw6-greentasty/terraform.tfstate`**. Старый key **`homework4/group-preorder/terraform.tfstate`** не подключался к новому root и не загружался как содержимое state; читались только его object metadata.
- Remote init использует `encrypt=true`, `use_lockfile=true`, тот же expected account. Для текущих read-only plans использован **`-lock=false`**, поэтому PutObject/DeleteObject для `.tflock` не требовались.
- После init и двух успешных plan запрос `list-object-versions` для `hw6-greentasty/` не вернул ни Versions, ни DeleteMarkers. State/lock objects и их исторических версий для hw6 нет.
- До/после совпадают **VersionId, ETag и LastModified старого state**, а также **CodeSha256, RevisionId, LastModified, runtime, memory и timeout** HW4 Lambda. Исходный `infra/homework4` побайтово не изменён относительно `origin/main`.
- Managed prior state нового плана пуст; все 32 managed actions — `create`. В конфигурации нет remote-state dependency/import/reference на ресурсы HW4. Единственная общая инфраструктура — существующий S3 bucket хранения разных state keys и региональные account quotas.

## Полноценный remote plan

Terraform **1.17.0**, AWS provider **6.70.0**, native `fresh-deploy`. Команды PowerShell из корня репозитория:

```powershell
terraform '-chdir=infra/hw6-greentasty' init -input=false -reconfigure '-backend-config=backend.hcl'
terraform '-chdir=infra/hw6-greentasty' plan -input=false -lock=false '-var-file=demo.tfvars.example' '-out=build/hw6.tfplan'
terraform '-chdir=infra/hw6-greentasty' show -json build/hw6.tfplan
```

Нет `-target`, `-refresh=false`, skip account/credentials validation или fake credentials. Plan JSON подтверждает `complete=true`, `applyable=true`; все resource actions — create. Backend region/account/profile проверены по локальным backend metadata и plan configuration. Numeric null нужно передавать через tfvars, а не `-var=reserved_concurrency=null`.

Локальный ignored binary: `infra/hw6-greentasty/build/hw6.tfplan`.

- Plan SHA256: `f3ca9569685dd29c8f686968db8425c82b3c84a155ba595389fa943cab2397fb`.
- Настоящий .NET ZIP SHA256: `4bfb24e45ca476353ac1d58473db7e7f67e7ce6a0927cfcadde3c6645a7daec3`.
- ZIP 1 799 567 bytes; код приложения и ZIP в этом продолжении не менялись.

`complete/applyable` отражают корректность плана, а не успешность будущих AWS Create API. Не проверялись созданием сервисов Free Plan restrictions, CloudFront/Cognito provisioning, cold start или реальный E2E. Сохраняется provider warning: DynamoDB `hash_key` deprecated в пользу `key_schema`; schema validation и plan успешны. Binary plan, raw JSON, state и backend.hcl не коммитятся. Перед будущим разрешённым apply требуется новый plan с обычным state locking и повторная проверка аккаунта/credits/квот.

## Точный список создания

Ни один ресурс этого списка ещё не создан. Это **32 managed Terraform resources**; 13 GSI находятся внутри 10 table resources. Один Cognito authorizer, API path resources, 33 app methods и OPTIONS описаны inline в OpenAPI REST API и не представлены отдельными Terraform addresses.

| # | Terraform address | Планируемое имя / назначение |
| --- | --- | --- |
| 1 | `aws_api_gateway_deployment.app` | Связь / конфигурация нового hw6 ресурса; ID после apply |
| 2 | `aws_api_gateway_gateway_response.cors["DEFAULT_4XX"]` | DEFAULT_4XX |
| 3 | `aws_api_gateway_gateway_response.cors["DEFAULT_5XX"]` | DEFAULT_5XX |
| 4 | `aws_api_gateway_method_settings.limits` | demo |
| 5 | `aws_api_gateway_rest_api.app` | hw6-greentasty |
| 6 | `aws_api_gateway_stage.app` | demo |
| 7 | `aws_cloudfront_distribution.frontend` | Связь / конфигурация нового hw6 ресурса; ID после apply |
| 8 | `aws_cloudfront_function.spa` | hw6-greentasty-spa |
| 9 | `aws_cloudfront_origin_access_control.frontend` | hw6-greentasty |
| 10 | `aws_cloudfront_response_headers_policy.frontend` | hw6-greentasty |
| 11 | `aws_cloudwatch_log_group.lambda` | /aws/lambda/hw6-greentasty-api |
| 12 | `aws_cognito_user_group.customer` | Customer |
| 13 | `aws_cognito_user_pool.app` | hw6-greentasty |
| 14 | `aws_cognito_user_pool_client.app` | hw6-greentasty |
| 15 | `aws_dynamodb_table.app["Dishes"]` | hw6-greentasty-Dishes |
| 16 | `aws_dynamodb_table.app["Feedback"]` | hw6-greentasty-Feedback |
| 17 | `aws_dynamodb_table.app["GroupReservations"]` | hw6-greentasty-GroupReservations |
| 18 | `aws_dynamodb_table.app["GuestSessions"]` | hw6-greentasty-GuestSessions |
| 19 | `aws_dynamodb_table.app["Locations"]` | hw6-greentasty-Locations |
| 20 | `aws_dynamodb_table.app["Notifications"]` | hw6-greentasty-Notifications |
| 21 | `aws_dynamodb_table.app["Orders"]` | hw6-greentasty-Orders |
| 22 | `aws_dynamodb_table.app["Reservations"]` | hw6-greentasty-Reservations |
| 23 | `aws_dynamodb_table.app["Tables"]` | hw6-greentasty-Tables |
| 24 | `aws_dynamodb_table.app["Waiters"]` | hw6-greentasty-Waiters |
| 25 | `aws_iam_role.api` | hw6-greentasty-api |
| 26 | `aws_iam_role_policy.api` | hw6-greentasty-runtime |
| 27 | `aws_lambda_function.api` | hw6-greentasty-api |
| 28 | `aws_lambda_permission.gateway` | hw6-greentasty-api |
| 29 | `aws_s3_bucket.frontend` | hw6-greentasty-frontend-071954286972-eu-north-1 |
| 30 | `aws_s3_bucket_policy.frontend` | Связь / конфигурация нового hw6 ресурса; ID после apply |
| 31 | `aws_s3_bucket_public_access_block.frontend` | Связь / конфигурация нового hw6 ресурса; ID после apply |
| 32 | `aws_s3_bucket_server_side_encryption_configuration.frontend` | Связь / конфигурация нового hw6 ресурса; ID после apply |


У всех новых ресурсов namespace/tag Project `hw6-greentasty`, где применимы tags. DynamoDB/GSI — PAY_PER_REQUEST, без provisioned capacity/streams/PITR. Lambda .NET 10 x64/512 MB/15 s, без VPC и provisioned concurrency. IAM policy ссылается на новые tables/indices, user pool и log group; некоторые ARN/JSON ещё unknown до создания. NAT, EC2, ALB, новый state bucket, SES/SQS и reporting Lambdas отсутствуют.

## Стоимость по полученным AWS Price List ставкам

Тарифы получены read-only через бесплатный `pricing get-products`, endpoint us-east-1; filters: regionCode eu-north-1, Europe для CDN. Это **USD без налогов и без учёта free allowances/credits**, не точный будущий invoice. Для небольшого demo применяются первые платные tiers; бесплатные ступени консервативно не вычитаются.

| Компонент | Ставка | Явное допущение в месяц | Расчёт, USD |
| --- | --- | --- | ---: |
| REST API | $3.50 / 1 млн | 10 000 запросов | 0.035000 |
| Lambda requests | $0.20 / 1 млн | 10 000 | 0.002000 |
| Lambda x64 duration | $0.0000166667 / GB-s | 512 MB × 0.5 s × 10 000 = 2 500 GB-s | 0.041667 |
| DynamoDB writes | $0.67 / 1 млн WRU | 50 000 WRU, включая GSI/transaction multipliers | 0.033500 |
| DynamoDB reads | $0.1345 / 1 млн RRU | 100 000 RRU | 0.013450 |
| DynamoDB storage | $0.269 / GB-month платной ступени | 0.01 GB | 0.002690 |
| S3 Standard storage | $0.023 / GB-month | 0.1 GB | 0.002300 |
| S3 PUT/LIST | $0.005 / 1 000 | 1 000 | 0.005000 |
| S3 GET | $0.0004 / 1 000 | 10 000 | 0.004000 |
| CloudFront HTTPS Europe | $0.012 / 10 000 | 100 000 | 0.120000 |
| CloudFront Functions | $0.10 / 1 млн платной ступени | 100 000 | 0.010000 |
| CloudFront egress Europe → Internet | $0.085 / GB | 1–10 GB | 0.085000–0.850000 |
| CloudWatch Standard logs ingest | $0.54 / GB | 0.1 GB | 0.054000 |
| CloudWatch log storage | $0.028 / GB-month | 0.1 × 7/30 GB при равномерных логах и retention 7 дней | 0.000653 |
| Cognito Lite | $0.0055 / MAU платной ступени | 3 MAU | 0.016500 |
| **Итого по этой модели** | | | **0.425760–1.190760** |

У DynamoDB каталог содержит бесплатную storage-ступень до 25 GB, у CloudFront Functions — первые 2 млн executions бесплатно, у Cognito/прочих сервисов возможны дополнительные allowances. Выше использованы платные marginal rates для консервативной модели. Предполагаются стандартные DynamoDB tables, европейские viewers, отсутствие SMS/email/WAF, paid plan subscriptions, reports, provisioned concurrency и прочих не включённых в plan функций.

В subtotal не включены переменный API Gateway Internet data transfer, дополнительные invalidations сверх бесплатных, дополнительные S3 state versions/requests, налоги и существующий HW4/HelloWorld. Практический резерв для малого демо остаётся **$1–5/месяц**; это не spending cap. Throttled requests, polling открытых вкладок, cold-start duration и объём assets могут менять результат. При текущем **FREE/ACTIVE / $140 credits** eligible usage может покрываться credits/allowances, но эти $140 общие для аккаунта. Баланс нельзя считать бюджетом только hw6. Перевод в Paid plan не выполнялся.

Разовое создание не имеет фиксированного setup fee; seed/первый upload до 1 000 PUT и короткий smoke обычно укладываются в **<$0.10 API/write charges**, плюс выдача frontend/другой трафик. При простое начисляются только фактически занятые storage и редкие обращения; для выбранных объёмов это доли цента без новых логов. Read-only подготовка не создала платных compute/storage ресурсов, но S3 read requests сами по себе тарифицируются в рамках общих условий.

Источники: [Price List API](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/price-changes.html), [Lambda](https://aws.amazon.com/lambda/pricing/), [API Gateway](https://aws.amazon.com/api-gateway/pricing/), [DynamoDB](https://aws.amazon.com/dynamodb/pricing/), [S3](https://aws.amazon.com/s3/pricing/), [CloudFront](https://aws.amazon.com/cloudfront/pricing/), [CloudWatch](https://aws.amazon.com/cloudwatch/pricing/), [Cognito](https://aws.amazon.com/cognito/pricing/).

## Что остаётся перед deployment

Нужно отдельное разрешение на AWS-запись/apply. После него — свежий plan с locking и тем же `demo.tfvars.example`, проверка Free Plan service restrictions/квот, deployment, Cognito users/seed, Angular build с настоящим output URL, upload и реальный E2E из [runbook](../../infra/hw6-greentasty/README.md). Пока нет существующего hw6 API URL и выполненного облачного E2E. Подготовленный план не является разрешением на эти операции.
