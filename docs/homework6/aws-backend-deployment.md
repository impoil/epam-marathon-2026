# Green&Tasty: фактическое развёртывание AWS backend, 30.10.2026

Настоящий .NET backend создан и отвечает по HTTPS: **https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo**. Разрешённый saved-plan apply завершился успешно: **8 Add / 0 Change / 0 Destroy**. Всего в отдельном hw6 state теперь **30 managed resources**. Все исходные 22 ресурса сохранены; HW4 и HelloWorld не изменились.

**Проверка сходимости не пройдена:** контрольный remote plan показал **1 Add / 3 Change / 1 Destroy** вместо 0/0/0. Повторный apply не выполнялся. Ниже приведены точная причина и граница остановки. Это действующий backend с открытым блокером Terraform; развёртывание нельзя считать полностью завершённым по всем критериям пользователя.

## Разрешённый apply и происхождение пакета

- Ветка `feature/hw6-greentasty-aws-deployment`, локальный HEAD, origin branch и head Draft PR #2 перед apply: `b53da3743e95e949bae600548d17e3fb45a8293d`. Рабочая копия была чистой.
- STS подтвердил Account **071954286972**, действующую временную сессию `fresh-deploy`. Регион **eu-north-1** явно задан в provider, backend и AWS CLI. Нового `aws login` не потребовалось.
- `frontend_origin` уже имел точное значение **https://greentasty-epam-impoil.pages.dev**; пользователь подтвердил создание Pages project и назначение этого origin. `reserved_concurrency=null` сохранён.
- Настоящий Linux .NET ZIP: **1 799 567 bytes**, **21 файл**, SHA256 **`4bfb24e45ca476353ac1d58473db7e7f67e7ce6a0927cfcadde3c6645a7daec3`**. Повторно проверены содержимое ZIP, отсутствие credentials/JWT/private-key patterns и неожиданных secret/debug files. Lambda CodeSha256 соответствует этому ZIP.
- `terraform fmt -check`, `init -reconfigure` и `validate` прошли. Использован зафиксированный AWS provider **6.70.0**. Остались предупреждения deprecation DynamoDB `hash_key`; ошибок validate/plan/apply не было.
- Новый полноценный plan с refresh и **обычным S3 locking**, `-lock-timeout=60s`, `-var-file=demo.tfvars.example`: **8 create, 22 no-op, 0 update/delete/replacement/import**. Полные resource changes совпали с ранее проверенным Cloudflare-вариантом. CloudFront Distribution в плане отсутствовала.
- SHA256 применённого binary plan: **`ec9e3e0a1f1bc9a65be5b88cbcd026f9bc9c2bb522a94d25fb87b7c3d470e909`**. Перед apply повторно сверены SHA256 плана и ZIP.
- Выполнен ровно один apply этого нового плана с обычной S3-блокировкой: **30.10.2026 10:22:22–10:22:37 UTC**, exit code **0**, `8 added, 0 changed, 0 destroyed`.

Секреты, raw state, binary plans, их JSON и локальный backend config не публикуются. Они находятся только в ignored `infra/hw6-greentasty/build/backend-completion/` и `.terraform/`. В отчёт включены публичные URL/идентификаторы и безопасные сводные результаты.

## Созданные восемь ресурсов

| Terraform address | Фактическое значение |
| --- | --- |
| `aws_lambda_function.api` | `hw6-greentasty-api`, Active / Successful |
| `aws_lambda_permission.gateway` | `OnlyHw6RestApi`, только API Gateway этого API, account и stage |
| `aws_api_gateway_rest_api.app` | `7srgpvk8of`, REGIONAL |
| `aws_api_gateway_deployment.app` | `abmqyo` |
| `aws_api_gateway_stage.app` | `demo` |
| `aws_api_gateway_method_settings.limits` | `*/*`: 5 requests/s, burst 10 |
| `aws_api_gateway_gateway_response.cors["DEFAULT_4XX"]` | Точный Pages origin, `Cache-Control: no-store` |
| `aws_api_gateway_gateway_response.cors["DEFAULT_5XX"]` | Точный Pages origin, `Cache-Control: no-store` |

Внутри REST API проверены **33 application methods**, **28 OPTIONS**, **17 Cognito-protected methods**, один Cognito authorizer. OPTIONS — штатный MOCK preflight; все application methods используют AWS_PROXY к настоящей .NET Lambda. Это не подмена business endpoints моками.

## Фактические outputs

| Output | Значение |
| --- | --- |
| `backend_url` | `https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo` |
| `frontend_url` | `https://greentasty-epam-impoil.pages.dev` — подтверждённый origin; публикация Angular агентом не выполнялась |
| `frontend_bucket` | `hw6-greentasty-frontend-071954286972-eu-north-1` — сохранённый unused bucket |
| `user_pool_id` | `eu-north-1_r64xyLOtR` |
| `user_pool_client_id` | `2bafp42t5s2f6iqao13s2bu61u` — public client, без secret |

`distribution_id` отсутствует: CloudFront Distribution не создавалась. Для следующего отдельно разрешённого Pages build реальное значение `BACKEND_URL` — backend_url из таблицы. Настройки monorepo/SPA остаются в [Cloudflare runbook](cloudflare-pages.md).

## Проверки AWS и API

- Lambda: **dotnet10, x86_64, 512 MB, timeout 15 s**, handler `APIHandler::APIHandler.Function::FunctionHandler`; VPC отсутствует, reserved concurrency не выделена. Общая региональная квота осталась **5**, unreserved **5**.
- IAM runtime policy ограничена собственными DynamoDB tables/indices, Cognito pool и log group `hw6-greentasty`; managed policies отсутствуют. Разрешение invoke ограничено principal `apigateway.amazonaws.com`, SourceAccount `071954286972` и SourceArn `...:7srgpvk8of/demo/*/*`. CloudFront/S3 permissions не добавлялись.
- Cognito authorizer: **COGNITO_USER_POOLS**, только pool `eu-north-1_r64xyLOtR`, `Authorization` header, проверка audience `^2bafp42t5s2f6iqao13s2bu61u$`. Клиент без secret, token revocation включена, ID/access tokens 30 минут. Pool **LITE**, self-signup выключен, **0 users**.
- Stage: cache cluster, X-Ray, detailed metrics и data tracing выключены; API execution/access logging не включалось, чтобы не менять account-wide CloudWatch role. Lambda пишет в собственную log group с **retention 7 дней**.
- CORS совпадает для Lambda, всех OPTIONS и обоих gateway responses: только `https://greentasty-epam-impoil.pages.dev`; Content-Type/Authorization; GET/POST/PUT/DELETE/OPTIONS; Allow-Credentials отсутствует.
- CloudWatch после smoke tests: **3 REPORT**, без неожиданных runtime exceptions/timeouts/AccessDenied. Одна ожидаемая запись `[ERROR] Invalid guest session token` относится к проверке GET guest route без токена и HTTP 401.

Реальные HTTPS smoke tests выполнены без JWT и без записи business data:

| Запрос | Фактический ответ |
| --- | --- |
| GET `/me` | **401**, Cognito authorizer |
| GET `/reservations` | **401**, Cognito authorizer |
| GET `/guest/groups/verification-nonexistent/sessions` | **401**, Cognito authorizer |
| OPTIONS `/me` | **200**, точный origin и нужные headers/methods |
| OPTIONS `/signin` | **200**, точный origin и нужные headers/methods |
| GET `/locations` | **200**, `[]` из настоящего backend / пустой DynamoDB |
| GET `/dishes` | **200**, `[]` из настоящего backend / пустой DynamoDB |
| GET `/guest/sessions/verification-nonexistent` | **401**, проверка гостевого токена в .NET |

Каждый ответ содержит правильный Access-Control-Allow-Origin. Эти проверки подтверждают API routing, запуск .NET, чтение DynamoDB и отказ без авторизации. Авторизованный E2E бронирования/корзины ещё не выполнен: users/seed/публикация Angular запрещены на этом этапе.

## Контрольный plan: причина остановки

После успешного apply выполнен обычный remote plan с тем же tfvars, refresh и S3 locking. Он **не применялся**:

| Address | Планируемое действие |
| --- | --- |
| `aws_api_gateway_gateway_response.cors["DEFAULT_4XX"]` | update: удалить автоматически возвращаемый AWS `response_templates.application/json` |
| `aws_api_gateway_gateway_response.cors["DEFAULT_5XX"]` | Такое же update |
| `aws_api_gateway_deployment.app` | **replacement**, create-before-destroy из-за изменения `triggers.responses` |
| `aws_api_gateway_stage.app` | update deployment_id на новый deployment |
| Остальные **26** | no-op, включая все исходные **22** |

Итого: **1 Add / 3 Change / 1 Destroy**, одна замена. Причина подтверждена JSON plan и AWS get-gateway-responses: AWS возвращает стандартный template `{"message":$context.error.messageString}`, тогда как конфигурация не задаёт response_templates. Кроме того, `triggers.responses = sha256(jsonencode(aws_api_gateway_gateway_response.cors))` хеширует весь объект ресурса, включая поля, полученные от provider. При refresh это меняет trigger с `dde62071…` на `3b9bd17a…` и тянет replacement deployment/stage update.

Ни CORS origin, ни routes, ни код Lambda не изменились. Runtime работает; требование **0/0/0 не достигнуто**. Никакие изменения этого контрольного плана, удаления или повторный apply не выполнялись. Terraform-код после обнаружения расхождения не исправлялся; изменены только отчёты и комментарий о подтверждённом origin.

Следующий отдельный шаг: согласовать коррекцию Terraform — явно описать/нормализовать стандартный response template и строить deployment trigger из стабильных желаемых входов, а не полного provider resource. Затем проверить новый remote plan и отдельно согласовать любое необходимое изменение существующих API resources. Не применять текущий контрольный plan как автоматическое «доведение».

## State и защита существующих стеков

- Backend bucket прежний: `epam-hw4-state-freshdeploy-20261027`; только key **`hw6-greentasty/terraform.tfstate`**, **AES256**, `use_lockfile=true`. После apply объект записан **10:22:39 UTC**, размер **117 180 bytes**. State читается Terraform; 30 managed addresses, прежние 22 IDs сохранены. Lockfile после операций отсутствует.
- Для HW4 key **`homework4/group-preorder/terraform.tfstate`** metadata/VersionId/ETag до и после совпали.
- Совпали **все 7 нормализованных снимков**: HW4 state metadata, конфигурация Lambda `epam-hw4-group-preorder`, конфигурация Lambda `EPAM-Marathon-HelloWorld`, HW4 HTTP API `xv3cbcohed`, его routes, integrations, stages. Для Lambda совпали CodeSha256, RevisionId, LastModified и настройки.
- Первоначальные 22 ресурса (10 DynamoDB, Cognito pool/client/group, IAM role/policy, log group, 6 hosting resources) остались no-op как до apply, так и в контрольном плане. DynamoDB остаётся PAY_PER_REQUEST с прежними ограничениями.
- Шесть unused hosting resources — S3 bucket/public-access-block/SSE и CloudFront function/OAC/response-headers-policy — сохранены. Их очистка требует отдельного разрешения; distribution не создавалась.

## План, расходы и следующие этапы

Free Plan после apply: **FREE / ACTIVE**, API показывает **140 USD** remaining credits, expiration **26.04.2027 17:11:35 UTC**. Тариф и квоты не изменялись. Billing balance может обновляться с задержкой; это не доказательство нулевого invoice.

Фиксированные compute-компоненты не добавлены: нет NAT/EC2/ALB/provisioned concurrency. По ранее полученным 29.10.2026 региональным ставкам и той же малой модели (10 000 requests, 512 MB × 0.5 s, 50 000 WRU, 100 000 RRU, 0.01 GB DynamoDB, 0.1 GB logs, 3 MAU) остаётся **около $0.20/месяц до allowances/credits**, плюс egress, state и налоги; ориентир резерва **$1–3/месяц**, не spending cap. Восьми smoke requests, ~117 KB state и небольшим логам соответствуют лишь usage/storage charges; фактическую сумму invoice текущие API не показывают. Основание и допущения: [региональные ставки](aws-verification.md#стоимость-по-полученным-aws-price-list-ставкам), [API Gateway pricing](https://aws.amazon.com/api-gateway/pricing/), [Lambda pricing](https://aws.amazon.com/lambda/pricing/).

Пользователи Cognito, seed, cleanup, публикация Angular и merge PR #2 не выполнялись. После разрешения Terraform-блокера следующие отдельно согласуемые этапы — тестовые users/seed, production BACKEND_URL в Cloudflare Pages, публикация Angular и настоящий E2E Customer/Owner/Guest. Существование Pages project и origin подтверждено пользователем; настройки Cloudflare dashboard в этом ходе агент не менял.

Отчёт сохраняется в той же feature-ветке. Для обновления Git используется префикс commit message **`[CF-Pages-Skip]`**, чтобы существующая Pages Git integration пропустила deployment: [официальная документация Cloudflare](https://developers.cloudflare.com/pages/configuration/git-integration/github-integration/). PR #2 остаётся Draft и не объединён.
