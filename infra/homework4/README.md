# EPAM Homework #4 — Group Pre-Order Summary

Независимое учебное приложение, вдохновлённое Green Tasty. Все его файлы находятся
в `infra/homework4/`; исходное приложение и `infra/terraform/` не используются.
**Homework #4 успешно развёрнуто и проверено 27.10.2026.** Lambda, HTTP API Gateway,
IAM и CloudWatch Logs работают в AWS; Terraform state хранится в защищённом S3
backend. Инфраструктура остаётся активной для демонстрации. S3 state bucket
существует отдельно и не управляется этим Terraform root.

## Результат deployment 27 октября 2026

Развёрнуто в аккаунте `071954286972`, регион `eu-north-1`, профиль `fresh-deploy`.
AWS CLI 2.38.6, Terraform 1.17.0; providers AWS 6.70.0 и archive 2.8.1.
Перед apply выполнены STS и S3 `--expected-bucket-owner` проверки. Новый сохранённый
план в основном репозитории показал **10 Add / 0 Change / 0 Destroy** и совпал
с согласованной диагностической конфигурацией, включая IAM и Lambda ZIP.
Применён только этот `homework4.tfplan`: **10 added, 0 changed, 0 destroyed**.
Создано ровно 10 управляемых ресурсов.
Проверочный plan после apply: **0 Add / 0 Change / 0 Destroy**, drift отсутствует.

Фактический endpoint:
<https://xv3cbcohed.execute-api.eu-north-1.amazonaws.com/group-preorder>

Выполнен один GET: HTTP **200**, корректный JSON, `currency=USD`, `group_total=64`.
Независимый пересчёт строк подтвердил итог; участников 4, выбрали блюда 3,
ожидается выбор 1, порций 5. CloudWatch подтвердил один access log со статусом 200
и Lambda REPORT без ошибок. У обеих групп логов retention 7 дней.
Lambda активна, Python 3.14 ARM64, 128 МБ, timeout 3 секунды; загруженный код
совпадает с сохранённым планом. IAM execution role имеет только согласованную
inline policy записи собственных логов; attached managed policies отсутствуют.
Дополнительных ресурсов проекта по его префиксу и тегу не обнаружено.

Remote state существует в `epam-hw4-state-freshdeploy-20261027` под ключом
`homework4/group-preorder/terraform.tfstate`, шифрование AES256, versioning включён.
S3 backend использует `allowed_account_ids=["071954286972"]`, `encrypt=true`,
`use_lockfile=true`; активного lock после проверки нет. TLS Deny policy сохранена.
История версий state и удалённых lock-объектов остаётся в S3.

Исходная бизнес-логика и состав ресурсов не изменены. Credentials в проект не
добавлены; локальные backend, plan, ZIP и служебные отчёты игнорируются Git.
В GitHub публикуются исходные файлы, provider lockfile, пример backend и README.
Инфраструктура остаётся активной для демонстрации; автоматическое удаление
не настроено. Последующее удаление требует отдельного разрешения и review
destroy plan; до завершения cleanup сохраняйте тот же remote state.

## Как работает приложение

`GET /group-preorder` возвращает сводку фиксированного учебного группового заказа.
API Gateway HTTP API передаёт запрос Python Lambda в формате payload 2.0.
Функция не использует AWS SDK, базу данных, внешнюю сеть или сторонние библиотеки.
Каждый ответ рассчитывается заново; состояние между запросами не сохраняется.

| Участник | Заказ | Количество порций | Подытог | Статус |
| --- | --- | ---: | ---: | --- |
| Owner | Fresh Strawberry Mint Salad × 1; Roasted Sweet Potato & Lentils Salad × 1 | 2 | $27 | `selected` |
| Anna | Fresh Strawberry Mint Salad × 1 | 1 | $17 | `selected` |
| Oleh | Roasted Sweet Potato & Lentils Salad × 2 | 2 | $20 | `selected` |
| Maria | Пока ничего не выбрано | 0 | $0 | `pending` |

Цена первого блюда — $17, второго — $10. Групповой итог **$64** вычисляется как
`27 + 17 + 20 + 0`, а не записан константой в реализации.
Участников — 4, выбрали блюда — 3, ожидается выбор — 1, порций — 5.
Статус `selected` означает только наличие блюд, а не оплату или подтверждение.

JSON содержит `schema_version`, `currency`, массив `participants`, счётчики
`participant_count`, `participants_with_orders`, `participants_pending`,
`total_quantity` и `group_total`. Для каждого участника возвращаются идентификатор,
имя, роль, статус, `orders`, количество и `subtotal`. Каждая строка заказа содержит
идентификатор и название блюда, `unit_price`, `quantity` и `line_total`.
Денежные поля JSON выражены числом в USD, например `64.0`. Вся арифметика выполняется
в целых центах; перевод в USD происходит только при формировании ответа.

Фрагмент ответа для Oleh:

```json
{
  "participant_id": "oleh",
  "name": "Oleh",
  "role": "guest",
  "status": "selected",
  "orders": [
    {
      "dish_id": "sweet-potato-lentils-salad",
      "dish_name": "Roasted Sweet Potato & Lentils Salad",
      "unit_price": 10.0,
      "quantity": 2,
      "line_total": 20.0
    }
  ],
  "total_quantity": 2,
  "subtotal": 20.0
}
```

Ответ имеет `Content-Type: application/json` и `Cache-Control: no-store`.
Только `GET /group-preorder` зарегистрирован в API Gateway: отсутствуют `$default`
route, маршруты `ANY`, POST/PUT/PATCH/DELETE и Lambda Function URL. Стадия `$default`
существует, но это не catch-all маршрут. Lambda дополнительно отклоняет методы,
отличные от GET, кодом 405 и другие пути кодом 404. API Gateway отклоняет запросы
без совпадающего маршрута ещё до Lambda. Параметры query и тело запроса не изменяют
заказ. CORS не включён: работа через curl возможна, доступ из чужого браузерного
origin не заявляется. Endpoint публичный и без авторизации: здесь только фиктивные
учебные данные, не персональные данные настоящих посетителей.

## Развёрнутая инфраструктура

Регион закреплён: **eu-north-1**. Имя по умолчанию — `epam-hw4-group-preorder`;
переменная `name` позволяет изолировать дополнительный экземпляр.

- Lambda: **Python 3.14**, ARM64, **128 МБ**, **3 секунды**, без VPC, layers,
  provisioned concurrency и опубликованных версий. ZIP содержит только
  `lambda_function.py`; обработчик — `lambda_function.handler`.
- IAM execution role с trust policy только для Lambda и inline policy только на
  создание log streams / запись событий в собственную группу логов. Функция не
  получает прав на S3, DynamoDB или изменение инфраструктуры.
- HTTP API, Lambda proxy integration, один GET route и автоматически развёртываемая
  стадия `$default` с ограничением **1 запрос/секунду**, burst **2**.
- Resource-based permission на вызов Lambda только API Gateway этого API,
  только для `GET /group-preorder`.
- Две CloudWatch Log Groups — для Lambda и access logs API — с retention **7 дней**.
  Access logs содержат идентификатор запроса, маршрут, статус и технические сведения,
  но не тело ответа или заказа.

`integration_method = "POST"` в Terraform — внутренний протокол вызова Lambda
из API Gateway, а не публичный POST-маршрут.
HTTP API использует доставку access logs сервисом AWS; Lambda role не выдаются
права на API logs. В целевом аккаунте работают необходимые служебные
механизмы CloudWatch delivery: access log успешного GET подтверждён после deployment.
Локальный `validate` сам по себе не проверяет доставку логов.

Всего определено 10 управляемых ресурсов плюс локальный `archive_file` data source.
S3 bucket для state **намеренно не входит** в этот проект. Не создаются CloudFront,
NAT Gateway, EC2, база данных, custom domain, дополнительные API или исходный backend.

## Локальная проверка без AWS

Требуются Terraform **>= 1.10, < 2**, Python **3.14**, доступ к официальным
Terraform registry/release destinations только для загрузки инструментов и providers.
Версии providers фиксируются отслеживаемым файлом `.terraform.lock.hcl`.
У Python нет pip-зависимостей.

Из `infra/homework4/`:

```bash
terraform fmt
terraform init -backend=false -input=false -lockfile=readonly
terraform validate
python3.14 -m unittest -v
```

Или `bash validate.sh`: скрипт проверяет Python 3.14, запускает `fmt -check`,
`init -backend=false`, `validate` и unit tests. Он также находит инструменты,
установленные локально в игнорируемой `.tools/`, либо принимает пути через
`TERRAFORM_BIN` и `PYTHON_BIN`. Каждый шаг должен завершиться успешно; код ошибки
не скрывается. Для первоначального создания lockfile использован обычный
`terraform init -backend=false -input=false`; дальнейшие проверки запрещают
неявное изменение lockfile.

`init -backend=false` скачивает providers и не обращается к S3. `validate`
проверяет конфигурацию и схемы providers, не выполняя AWS API-запросы.
Тесты проверяют итог $64, строки заказов, статусы, изменяемые цены и количества,
точность арифметики в центах, пустую группу, отсутствие мутаций данных, ошибки
входных данных и HTTP-ответы. Они не используют AWS credentials и сеть.

Эти offline-проверки сами по себе не проверяют AWS runtime, IAM-разрешения и
публичный URL. Дополнительно 27.10.2026 выполнены настоящий deployment Lambda ARM64,
один GET к рабочему endpoint и проверка CloudWatch Logs; результаты приведены выше.

Результаты первоначальной offline-проверки 27 октября 2026 (до deployment):

| Проверка | Результат |
| --- | --- |
| `terraform fmt`, затем `fmt -check` | Успешно |
| `terraform init -backend=false` | Успешно; backend не подключался |
| Повторный init с `-lockfile=readonly` | Успешно; зафиксированные версии переиспользованы |
| `terraform validate` | `Success! The configuration is valid.` |
| Python unit tests | **14 passed**, 0 failed, 0 skipped |
| Проверка границ изменений | Только новые файлы внутри `infra/homework4/` |

Инструменты первоначальной offline-проверки: Terraform **1.14.0**, CPython **3.14.8**; providers
`hashicorp/aws` **6.70.0** и `hashicorp/archive` **2.8.1**. Terraform ZIP проверен
по SHA-256 из официального release manifest; providers установлены с проверкой
подписи HashiCorp. Полный локальный журнал — `.build/validation.log` (игнорируется
Git). На этом первоначальном offline-этапе AWS credentials не использовались и
state не создавался. При последующем deployment использована временная login-сессия,
а действующий state сохранён в защищённом S3 backend.

## Проверка Windows/Linux provider checksums

В lockfile добавлены официальные `h1` для Windows AMD64; версии providers остались
AWS 6.70.0 и archive 2.8.1, Linux AMD64 hashes сохранены. Пакеты проверены по
подписям HashiCorp. Для согласованного обновления platform hashes используется:

```powershell
terraform providers lock -platform=windows_amd64 -platform=linux_amd64
```

Обычный init выполняйте с `-lockfile=readonly`. `zh` проверяет исходный ZIP,
а проверка распакованного provider cache использует подходящий платформе `h1`.
Не исправляйте mismatch отключением проверки или удалением доверенных checksums.

## Постоянное состояние: обязательный отдельный S3 bucket

Codex Cloud эфемерен: локальный `terraform.tfstate` может исчезнуть вместе с задачей.
Для настоящего деплоя обязателен удалённый S3 backend. В `backend.tf` уже объявлен
частичный `backend "s3" {}`: не удаляйте его и не переходите на local backend.
После локального `init -backend=false` нельзя сразу запускать deployment commands.

Используется существующий bucket `epam-hw4-state-freshdeploy-20261027` в аккаунте
`071954286972`, регион `eu-north-1`; повторно создавать его не нужно. Проверены
versioning, SSE-S3, все четыре Block Public Access, Bucket owner enforced и TLS Deny.
При подготовке другого окружения bucket создаётся отдельно, не этим Terraform root.
Требования к защите backend:

1. Выберите свой AWS-аккаунт и `eu-north-1`, задайте уникальное имя bucket.
2. Включите **Block all public access** — все четыре настройки. Object Ownership —
   **Bucket owner enforced**, ACL отключены.
3. Включите **S3 Versioning** для восстановления предыдущего state.
4. Установите Default encryption **SSE-S3 (AES-256)**. `encrypt = true` дополнительно
   требует server-side encryption при записи backend. Для этой задачи отдельный
   платный customer-managed KMS key не нужен.
5. Bucket policy должна запрещать запросы без TLS (`aws:SecureTransport = false`).
   Доступ разрешается только выделенным deployment identities/администраторам,
   а не публично и не execution role нашей Lambda.
6. Ограничьте IAM: `s3:ListBucket` на bucket с необходимыми префиксами;
   `s3:GetObject` / `s3:PutObject` на
   `homework4/group-preorder/terraform.tfstate`;
   `s3:GetObject` / `s3:PutObject` / `s3:DeleteObject` на
   `homework4/group-preorder/terraform.tfstate.tflock`.
   Для обычного управления state право удаления самого state не требуется.
7. Не включайте Object Lock, запрещающий удаление `.tflock`. Для state locking
   используется **нативный S3 lockfile**, `use_lockfile = true` (Terraform 1.10+).
   DynamoDB locking table не нужна. Не используйте `-lock=false`.

Скопируйте `backend.hcl.example` в игнорируемый `backend.hcl`, замените имя bucket
и единственный элемент `allowed_account_ids` на настоящий 12-значный account ID.
Фиксированные `region`, `key`, `encrypt` и `use_lockfile` оставьте. Никаких credentials
в HCL. `allowed_account_ids` относится именно к S3 backend и проверяет аккаунт
вызывающей identity. Это не проверка владельца бакета в каждом S3-запросе.
Параметр `expected_bucket_owner` не поддерживается Terraform S3 backend; для проверки
владельца отдельно используйте AWS CLI `--expected-bucket-owner` перед подключением.
Для одного экземпляра используйте один и тот же bucket/key во всех будущих задачах.
Дополнительному независимому экземпляру нужен другой key, а не общий state.

Versioning и locking решают разные задачи: первое даёт историю, второе предотвращает
одновременную запись. State чувствителен и не должен попадать в Git, сообщения,
публичные artifacts или общедоступные backups. Не публикуйте `.terraform/`:
там сохраняется backend metadata. И state, и plan-файлы могут содержать секреты.
Lockfile `.terraform.lock.hcl` содержит только версии и контрольные суммы providers,
а не секреты или Terraform state.

Если кто-то ранее случайно сделал deployment с local state, сначала сохраните
его защищённую копию и отдельно согласуйте миграцию через `init -migrate-state`.
Не теряйте этот файл и не создавайте второй набор ресурсов с пустым state.
Deployment использует remote S3 state; отдельный local deployment state не применялся.

## Временная авторизация и воспроизводимый workflow

**Все действия выполняются вручную в согласованном аккаунте; apply запускается только для проверенного сохранённого плана.**
Перед работой в вашем AWS Free Plan проверьте в Billing активный план, остаток
кредитов, срок их действия, доступность Lambda/HTTP API и квоты в `eu-north-1`.
Название Free Plan не означает бессрочную бесплатность или нулевую стоимость всех
ресурсов. Не переключайте аккаунт на платный план без отдельного решения владельца.

Для человеческого доступа предпочтительна короткая сессия IAM Identity Center
(AWS CLI v2 SSO). Для автоматизации — короткоживущая STS-сессия через доверенный
OIDC/федеративный механизм, если его предоставляет ваша организация/платформа.
Execution role Lambda и deployment identity — разные роли: последней нужны также
ограниченные права создавать и удалять перечисленные ресурсы, `iam:PassRole`
только для execution role и права backend, описанные выше.

Не используйте root credentials, долгоживущие IAM access keys, ключи из чужого
проекта, секреты в chat, `backend.hcl`, `.tfvars`, коде или shell history.
Не предполагайте, что HTTP-proxy placeholder секрета является пригодным raw AWS
credential для SigV4. Настройка авторизации должна поддерживаться AWS SDK/CLI.
Альтернативный пример интерактивной SSO-сессии:

```bash
aws configure sso --profile homework4
aws sso login --profile homework4
export AWS_PROFILE=homework4
export AWS_REGION=eu-north-1
aws sts get-caller-identity
```

Для текущего Windows workflow проверены AWS CLI 2.38.6, Terraform 1.17.0 и профиль
`fresh-deploy`, использующий браузерный `aws login`. Секреты вводятся пользователем
только на странице AWS; Terraform читает временную сессию напрямую. Если сессия
истекла, обновите её командой `aws login --profile fresh-deploy --region eu-north-1`.

Перед init, plan и apply выполните PowerShell-проверки аккаунта и владельца бакета:

```powershell
$env:AWS_PROFILE = "fresh-deploy"
$env:AWS_REGION = "eu-north-1"
$identityJson = aws sts get-caller-identity --profile fresh-deploy --region eu-north-1 --output json
if ($LASTEXITCODE -ne 0) { throw "AWS authentication failed" }
$identity = $identityJson | ConvertFrom-Json
if ($identity.Account -ne "071954286972") { throw "Unexpected AWS account" }
aws s3api head-bucket --bucket epam-hw4-state-freshdeploy-20261027 --expected-bucket-owner 071954286972 --profile fresh-deploy --region eu-north-1
if ($LASTEXITCODE -ne 0) { throw "Unexpected bucket owner or unavailable bucket" }
```

`backend.hcl` должен содержать `allowed_account_ids = ["071954286972"]`,
`region = "eu-north-1"`, `encrypt = true`, `use_lockfile = true` и ключ
`homework4/group-preorder/terraform.tfstate`. Не отключайте проверки credentials,
checksums, подписей или TLS. `terraform validate` не проверяет параметры S3 backend:
для этого требуется успешный remote init и проверка эффективной конфигурации backend.

Проверьте Account и ARN перед изменениями. Не выводите значения токенов/переменных.
Авторизуйтесь только в отдельной задаче для разрешённого деплоя; не публикуйте
снимок среды с активными credentials или SSO caches. После завершения завершите
сессию (`aws sso logout` в выделенной рабочей среде; команда затрагивает её SSO
sessions), удалите только собственные временные credentials/plan artifacts и
дайте STS-сессии истечь. Не удаляйте вслепую общие пользовательские credential files.

Ниже workflow, использованный для deployment. Повторять apply можно только после
отдельного согласования нового сохранённого плана; для текущего deployment
финальный проверочный plan уже показал отсутствие изменений.
Из этого каталога:

```powershell
terraform init -reconfigure "-backend-config=backend.hcl" -input=false -lockfile=readonly
if ($LASTEXITCODE -ne 0) { throw "Terraform init failed" }
terraform validate
if ($LASTEXITCODE -ne 0) { throw "Terraform validate failed" }
git check-ignore -- homework4.tfplan backend.hcl
if ($LASTEXITCODE -ne 0) { throw "Local deployment files must be ignored" }
terraform plan "-out=homework4.tfplan" -input=false -lock-timeout=30s
if ($LASTEXITCODE -ne 0) { throw "Terraform plan failed" }
# Проверить plan: ровно согласованные ресурсы, account/region, IAM и стоимость.
# Выполнять следующую команду только после разрешения именно этого плана:
terraform apply -input=false homework4.tfplan
if ($LASTEXITCODE -ne 0) { throw "Terraform apply failed" }
terraform output -raw group_preorder_url
```

`homework4.tfplan`, `backend.hcl`, `.terraform/`, ZIP и файлы в `.build/` игнорируются
Git. Не добавляйте их принудительно: state и plan могут содержать чувствительные
данные. Не запускайте apply без имени согласованного сохранённого плана.

`-reconfigure` подключает уже подготовленный удалённый backend; это не миграция
существующего local state. В новой локальной Windows-сессии восстановите временную
авторизацию и используйте тот же bucket/key и backend config. Запуск deployment
из другой среды также должен использовать этот remote state.
Не сохраняйте deploy commands в автоматическом startup/install сценарии среды.
Рабочий URL и результат выполненного smoke test приведены в начале README.
Для отдельно согласованной повторной проверки используйте GET по output URL и
сравните итог $64 и статусы; неподдерживаемые маршруты должны отклоняться.
Не отправляйте реальные персональные данные в этот публичный demo API.

## Расходы и ограничения

- Оплачиваемыми могут быть Lambda invocations/duration, API Gateway requests,
  CloudWatch ingestion/storage, S3 storage/requests/version history и исходящий
  трафик. Фактическое покрытие Free Plan проверяется в вашем аккаунте.
- Lambda ARM64/128 МБ/3 секунды, HTTP API, лимит 1 request/sec и короткий retention
  уменьшают расходы. Throttling — best effort, **не жёсткий денежный лимит**.
  Публичный GET тоже можно многократно вызывать; при подозрении на злоупотребление
  отключите endpoint/удалите учебный stack после проверки.
- Не создаются постоянно работающие серверы, NAT Gateway и базы данных.
  Reserved concurrency не задана, чтобы не требовать резервирования квоты нового
  аккаунта; учитывайте общую Lambda concurrency quota вашего аккаунта.
- Через Billing Console заранее настройте небольшие budget alerts и уведомления
  по кредитам. Это не создаётся данным Terraform; alerts не останавливают расходы.
  Не добавляйте платные вспомогательные услуги без проверки их цены.
- Сейчас demo остаётся активным для демонстрации. После её завершения согласуйте
  cleanup отдельно. Retention 7 дней не удаляет сами log groups; Terraform удалит
  обе группы при cleanup. Старые S3 state versions сохраняются отдельно.

## Безопасное удаление в дальнейшем — только после отдельного разрешения

Инфраструктура сейчас активна. Команды ниже описывают последующий cleanup и при
финализации Homework #4 не выполняются. Сначала восстановите профиль `fresh-deploy`,
выполните приведённые выше STS и S3 owner checks для аккаунта `071954286972` и
подключите **тот же S3 backend**. Не удаляйте ресурсы вручную в обход Terraform.

После отдельного разрешения на подготовку cleanup из `infra/homework4/`:

```powershell
$env:AWS_PROFILE = "fresh-deploy"
$env:AWS_REGION = "eu-north-1"
terraform init -reconfigure "-backend-config=backend.hcl" -input=false -lockfile=readonly
if ($LASTEXITCODE -ne 0) { throw "Terraform init failed" }
terraform plan -destroy "-out=homework4-destroy.tfplan" -input=false -lock-timeout=30s
if ($LASTEXITCODE -ne 0) { throw "Destroy plan failed" }
# Проверить: только десять managed resources Homework #4, без посторонних ресурсов.
# Получить отдельное разрешение на применение именно этого destroy plan.
# Следующую команду не запускать до этого разрешения:
terraform apply -input=false homework4-destroy.tfplan
if ($LASTEXITCODE -ne 0) { throw "Cleanup failed; preserve remote state" }
```

Это удалит API, Lambda, execution role/policy, permission и обе группы логов.
Bucket state не управляется этим stack и останется. Не удаляйте state или bucket
до подтверждённого завершения cleanup. После проверки пустого state и отсутствия
ресурсов решите отдельно, сколько хранить защищённую историю для аудита/восстановления.
Если bucket выделен только этому заданию и больше не нужен, его удаление — отдельная
операция владельца: нужно удалить все версии и delete markers, убедиться в отсутствии
активного `.tflock` и других проектов. Не применяйте рекурсивное удаление к общему
bucket и не удаляйте активный lock; force-unlock допустим лишь после проверки,
что никакой Terraform процесс больше не работает, и отдельного согласования.

Официальные справочные материалы:

- [S3 backend, versioning и locking](https://developer.hashicorp.com/terraform/language/backend/s3)
- [Python runtime Lambda](https://docs.aws.amazon.com/lambda/latest/dg/lambda-python.html)
- [HTTP API Lambda integration payload 2.0](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html)
- [Временный доступ AWS CLI через IAM Identity Center](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sso.html)
- [AWS Free Tier / Free Plan](https://aws.amazon.com/free/)
