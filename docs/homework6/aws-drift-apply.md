# Green&Tasty: drift исправлен и применён, 30.10.2026

Единственный отдельно разрешённый apply коррекции завершился успешно: **1 Add / 1 Change / 1 Destroy**, остальные **28 no-op**. Контрольный полноценный remote plan с refresh: **0 Add / 0 Change / 0 Destroy**, все **30 managed resources — no-op**. Блокер с gateway response templates / deployment trigger устранён.

| Сервис | Фактический адрес / статус |
| --- | --- |
| Backend | **https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo**, URL сохранён |
| Angular frontend | **https://greentasty-epam-impoil.pages.dev**, опубликован **пользователем**; проверка HTTPS → **200**, Angular root и 3 script tags |
| API deployment | `abmqyo` → **`5y05is`**, stage `demo` переключён, прежний snapshot удалён |
| Lambda | `hw6-greentasty-api`, конфигурация и CodeSha256 не изменились |
| Terraform | 30 ресурсов, отдельный key `hw6-greentasty/terraform.tfstate` |

## Проверка и единственный apply

- До начала HEAD/origin/head Draft PR #2 совпали: **`cb7049c1d7276089a6e70f7b347cab8f73b89258`**, рабочая копия чистая.
- STS подтвердил действующую сессию `fresh-deploy`, Account **071954286972**. Регион CLI/provider/backend — **eu-north-1**. Дополнительный login не потребовался.
- Существующий S3 state прочитан; 30 addresses, прежний bucket/key, AES256. Выполнены `fmt -check`, `init -reconfigure` и `validate` с AWS provider 6.70.0, без migration/state editing.
- Свежий saved plan с refresh, обычным S3 locking и `-lock-timeout=60s`, **`demo.tfvars.example`**: ровно **1/1/1, 28 no-op**. `frontend_origin=https://greentasty-epam-impoil.pages.dev`, `reserved_concurrency=null` сохранены. Полные resource changes совпали с рассмотренной пользователем коррекцией.
- Разрешённые изменения только: replacement **`aws_api_gateway_deployment.app`** и update deployment_id **`aws_api_gateway_stage.app`**. Gateway responses, REST API, Lambda, IAM, Cognito, DynamoDB, limits и 6 retained hosting resources — no-op.
- SHA256 нового применённого плана: **`87ce4c1cf9b3099137e4027a7e8e6b4b66dbcc48262b74654104142383a7c7f9`**. Перед apply hash и чистая рабочая копия повторно проверены.
- Выполнен **один apply**, **30.10.2026 11:07:32–11:07:37 UTC**, exit code **0**. По журналу: сначала создан `5y05is`, затем обновлён stage, затем удалён старый snapshot. Отдельная команда destroy, повторный apply, quota changes и state migration не выполнялись.
- Контрольный remote plan с тем же tfvars, refresh и обычным locking: **`No changes. Your infrastructure matches the configuration.`** В JSON все 30 resource changes — no-op.

Новая формула trigger хранится в state; явные стандартные gateway templates соответствуют AWS. История причины и изменения кода: [коррекция drift](terraform-drift-correction.md). Первоначальное создание настоящего backend: [deployment report](aws-backend-deployment.md).

## Проверки API после переключения stage

Все запросы выполнены без JWT и без business writes, с Origin **https://greentasty-epam-impoil.pages.dev**:

| Запрос | Результат |
| --- | --- |
| GET `/locations` | **200**, пустой массив из настоящего .NET/DynamoDB |
| GET `/dishes` | **200**, пустой массив из настоящего .NET/DynamoDB |
| GET `/me` | **401**, Cognito authorizer |
| GET `/reservations` | **401**, Cognito authorizer |
| GET `/guest/groups/verification-nonexistent/sessions` | **401**, Cognito authorizer |
| GET `/guest/sessions/verification-nonexistent` | **401**, проверка guest token в .NET |
| OPTIONS `/me` | **200**, корректный preflight |
| OPTIONS `/signin` | **200**, корректный preflight |

Во всех 8 ответах Access-Control-Allow-Origin точно равен production Pages origin. Allow-Headers содержит Content-Type/Authorization, методы GET/POST/PUT/DELETE/OPTIONS; Allow-Credentials не включён. Таким образом проверены Lambda responses, OPTIONS и gateway authorization responses.

CloudWatch после начала apply: **3 REPORT**, неожиданных exceptions/timeouts/AccessDenied нет. Единственная ERROR-запись — ожидаемая **`Invalid guest session token`** при отрицательной проверке guest route. Lambda log retention остаётся **7 дней**. Успешный реальный backend smoke test не заменяет авторизованный E2E: тестовые пользователи и данные ещё не созданы.

## Сохранность ресурсов и ограничения

- **Все 12 защищённых снимков до/после совпали:** 7 снимков HW4 state/Lambda/API/routes/integrations/stages и HelloWorld; hw6 Lambda, REST API, Cognito authorizers, gateway responses и Lambda account settings. Для Lambda совпали CodeSha256/RevisionId/LastModified и Environment. Квоты не менялись.
- У всех ресурсов, кроме заменённого deployment, сохранены прежние resource IDs. Terraform outputs до/после полностью совпали. В API остался один deployment `5y05is`, старого `abmqyo` нет.
- HW4 key **`homework4/group-preorder/terraform.tfstate`** не изменён. HW6 state штатно записан в прежний отдельный key, **AES256**, **117 170 bytes**, LastModified **11:07:40 UTC**. Содержимое state не публикуется; lockfile освобождён.
- Шесть вспомогательных CloudFront/S3 hosting resources сохранены без cleanup; CloudFront Distribution отсутствует. Данные DynamoDB, Cognito users, IAM и quotas apply не менял.
- **Angular уже опубликован пользователем.** Агент в этом этапе только прочитал страницу по HTTPS; Cloudflare build/deploy/settings не менялись. Для следующих Git updates используется `[CF-Pages-Skip]`, чтобы не инициировать лишний deployment. [Актуальный Pages runbook](cloudflare-pages.md).
- Пользователи Cognito, seed, cleanup и merge PR #2 не выполнялись. Следующий отдельный этап — разрешённые тестовые users/seed и настоящий Customer/Owner/Guest E2E на опубликованном frontend. Прежние provider deprecation warnings для DynamoDB key schema не являются drift и не устранялись в этом apply.

Raw plans, state, AWS responses и логи локальны в ignored **`infra/hw6-greentasty/build/drift-apply/`**; в Git включён только этот обезличенный отчёт и актуализация документации. PR #2 остаётся **Draft**.
