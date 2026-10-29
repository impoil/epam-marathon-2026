# AWS deployment Green&Tasty: частичное создание и блокер CloudFront

**Историческая попытка 29.10.2026.** Пользователь выбрал внешний Cloudflare Pages вместо AWS distribution; новый вариант подготовки от 30.10.2026 не требует разблокировки CloudFront CreateDistribution и даёт **8/0/0**, сохраняя все 22 ресурса. Актуальная инструкция: [Cloudflare runbook](cloudflare-pages.md). Новый apply не выполнялся.

**29 октября 2026. Разрешённый apply выполнен один раз и остановился на AWS account verification. Создано 22 из 32 согласованных Terraform resources. Повторный apply не выполнялся.**

## Проверки перед apply

- Чистая ветка `feature/hw6-greentasty-aws-deployment`, PR #2 head и локальный commit совпали: `e4f51d90d3ea9f95cd50aad7a4c28742cd5f31d3`. PR draft, не объединён.
- STS: Account **071954286972**, временный профиль **fresh-deploy**, workload region **eu-north-1**. Free Plan **FREE / ACTIVE**, **$140** credits; после частичного apply API возвращает тот же баланс. Тариф и квоты не менялись.
- Настоящий .NET ZIP: **1 799 567 bytes**, SHA256 **`4bfb24e45ca476353ac1d58473db7e7f67e7ce6a0927cfcadde3c6645a7daec3`**. Проверены 21 файл, отсутствие credentials/JWT/private-key patterns и неожиданных config/debug/test файлов. Пакет не пересобирался и не заменялся.
- Свежий полноценный remote plan с обычным S3 locking: **32 Add / 0 Change / 0 Destroy**, imports/replacements 0. Все `resource_changes` совпали с ранее согласованным планом, а не только итоговые числа. Backend key и package hash проверены.
- `demo.tfvars.example`: `reserved_concurrency=null`; plan value `-1`, без reservation при региональной quota 5.
- SHA256 применённого binary plan: **`72ea960d7bf71bcb6459aa95d0426aac3df99593534fdf57625bcdbb7a7eda71`**. Init/fmt/validate прошли; предупреждения deprecated DynamoDB hash_key не являются ошибкой создания.

## Единственная попытка apply

Запущен именно сохранённый и проверенный план, с обычным locking:

```powershell
terraform '-chdir=infra/hw6-greentasty' apply -input=false '-lock-timeout=60s' -no-color build/deployment/approved.tfplan
```

Начало **23:46:01 Europe/Kyiv**, завершение **23:46:47**, exit code **1**. Terraform закончил уже начатые независимые операции и сохранил частичный результат в remote state. После ошибки выполнялись только read-only проверки и локальная подготовка отчёта.

Ошибка AWS:

> CloudFront CreateDistributionWithTags: 403 AccessDenied — Your account must be verified before you can add new CloudFront resources. To verify your account, please contact AWS Support.

Request ID: **`3c7b6fe9-ebc3-48a3-8e34-ecdb07147285`**, UTC **2026-10-29 21:46**. AWS направляет в [Support Center](https://console.aws.amazon.com/support/home#/). Роль/credentials корректны; сообщение относится к верификации аккаунта для CloudFront distribution. Проверка доступности read API и успешный Terraform plan не выявляют это ограничение Create API заранее.

Не выполнялись попытки обхода, смена архитектуры/тарифа, quota increase, дополнительный apply, destroy, import, миграция state, создание пользователей, seed или публикация Angular. Дополнительных managed resources вне согласованных 32 нет.

## Что создано и проверено

| Группа | Количество | Фактическая проверка AWS CLI |
| --- | ---: | --- |
| DynamoDB | 10 | Все ACTIVE, PAY_PER_REQUEST; 13 GSI ACTIVE, лимиты таблиц 10 reads/s и 5 writes/s; ItemCount 0 |
| Cognito | 3 | Pool Lite, Customer group, public App Client без secret; self-signup выключен, password minimum 14, ID/access 30 min, token revocation включена; users 0 |
| S3 frontend | 3 | Bucket + public access block + SSE; все public block flags true, AES256, objects 0 |
| IAM | 2 | Новая execution role и inline policy; attached managed policies 0, resource scope — только hw6 tables/indices, pool и log group |
| CloudWatch | 1 | `/aws/lambda/hw6-greentasty-api`, retention 7 дней, stored bytes 0 |
| CloudFront вспомогательные | 3 | SPA function LIVE, OAC с signing=always, response headers policy; distribution отсутствует |
| **Итого** | **22** | Все записаны в отдельный remote state |

**Backend Lambda и API Gateway не созданы.** Они зависят от CloudFront domain для точного CORS origin. Поэтому пока нельзя проверить runtime .NET, живой API authorizer/CORS, получить API/CloudFront URL или выполнить E2E. S3 bucket policy с OAC/CloudFront ARN тоже ещё не создана; bucket остаётся закрытым public access block. Старый endpoint HW4 не используется как новый backend.

## Фактические outputs

| Output | Значение |
| --- | --- |
| `backend_url` | Не существует: REST API ещё не создан |
| `frontend_url` | Не существует: CloudFront distribution не создана |
| `frontend_bucket` | `hw6-greentasty-frontend-071954286972-eu-north-1` |
| `distribution_id` | Не существует |
| `user_pool_id` | `eu-north-1_r64xyLOtR` |
| `user_pool_client_id` | `2bafp42t5s2f6iqao13s2bu61u` — public client ID, не secret |

Это реальные outputs частичного state; placeholders или URL других стеков не подставлялись.

## Remote state, остаток плана и HW4

- State существует в **`s3://epam-hw4-state-freshdeploy-20261027/hw6-greentasty/terraform.tfstate`**, **61 712 bytes**, SSE **AES256**. Содержимое state не публикуется.
- Применение использовало backend `use_lockfile=true`; после завершения lock снят, в текущем prefix присутствует только `terraform.tfstate`, активного `.tflock` нет. Старый key не подключался и не мигрировал.
- После ошибки получен только **read-only remaining plan с `-lock=false`**, чтобы не выполнять новые S3-записи: **10 Add / 0 Change / 0 Destroy**, существующие 22 — no-op. Ожидание 0/0/0 пока не достигнуто из-за частичного apply.
- Метаданные HW4 state (VersionId/ETag/LastModified), конфигурации HW4 Lambda и HelloWorld, HW4 API/routes/integrations/stages **до и после совпали**. Исходный `infra/homework4` не изменён.

Оставшиеся **10** — строго часть первоначального списка:

1. `aws_cloudfront_distribution.frontend`.
2. `aws_s3_bucket_policy.frontend`.
3. `aws_lambda_function.api`.
4. `aws_lambda_permission.gateway`.
5. `aws_api_gateway_rest_api.app`.
6. `aws_api_gateway_deployment.app`.
7. `aws_api_gateway_stage.app`.
8. `aws_api_gateway_method_settings.limits`.
9. `aws_api_gateway_gateway_response.cors["DEFAULT_4XX"]`.
10. `aws_api_gateway_gateway_response.cors["DEFAULT_5XX"]`.

## Стоимость и дальнейшее действие

Сейчас нет hw6 Lambda/API/CDN distribution traffic, пользователей Cognito, seed, frontend objects и provisioned capacity. У созданных ресурсов нет фиксированного compute charge; остаются небольшие S3 state storage/version/request charges. Баланс Free Plan по API после попытки — **$140**, но это не финальный invoice. Модель полного малого демо из [AWS verification](aws-verification.md) остаётся **$0.43–1.19/месяц до льгот/credits плюс API egress**, практический резерв **$1–5**.

Пользователю нужно обратиться в AWS Support для верификации аккаунта/разблокировки CloudFront, приложив точное сообщение и Request ID. Обращение автоматически не отправлялось; Paid plan upgrade не запрашивался. После подтверждения AWS потребуется свежая проверка STS/Free Plan и plan с обычным locking **из этого же hw6 state**, ожидаемо **10 Add / 0 Change / 0 Destroy**; только затем можно отдельно продолжать остановленный apply. Повторно создавать 22 ресурса, чистить state или делать destroy не нужно. Пользователи/seed/Angular остаются следующим этапом после завершения и проверки инфраструктуры.
