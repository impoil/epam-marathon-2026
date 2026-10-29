# Домашнє завдання EPAM №4 — Group Pre-Order Summary

Незалежний навчальний застосунок, натхненний Green Tasty. Усі його файли містяться
в `infra/homework4/`; вихідний застосунок та `infra/terraform/` не використовуються.
**Домашнє завдання №4 успішно розгорнуто й перевірено 27.10.2026.** Lambda, HTTP API Gateway,
IAM та CloudWatch Logs працюють в AWS; Terraform state зберігається в захищеному S3
backend. Інфраструктура залишається активною для демонстрації. S3 state bucket
існує окремо й не керується цим Terraform root.

## Результат розгортання 27 жовтня 2026

Розгорнуто в обліковому записі `071954286972`, регіон `eu-north-1`, профіль `fresh-deploy`.
AWS CLI 2.38.6, Terraform 1.17.0; providers AWS 6.70.0 та archive 2.8.1.
Перед apply виконано перевірки STS та S3 `--expected-bucket-owner`. Новий збережений
план в основному репозиторії показав **10 Add / 0 Change / 0 Destroy** і збігся
з узгодженою діагностичною конфігурацією, включно з IAM та Lambda ZIP.
Застосовано лише цей `homework4.tfplan`: **10 added, 0 changed, 0 destroyed**.
Створено рівно 10 керованих ресурсів.
Перевірний plan після apply: **0 Add / 0 Change / 0 Destroy**, відхилень немає.

Фактичний endpoint:
<https://xv3cbcohed.execute-api.eu-north-1.amazonaws.com/group-preorder>

Виконано один GET: HTTP **200**, коректний JSON, `currency=USD`, `group_total=64`.
Незалежний перерахунок рядків підтвердив підсумок; учасників 4, обрали страви 3,
очікується вибір 1, порцій 5. CloudWatch підтвердив один access log зі статусом 200
і Lambda REPORT без помилок. В обох групах журналів строк зберігання становить 7 днів.
Lambda активна, Python 3.14 ARM64, 128 МБ, timeout 3 секунди; завантажений код
збігається зі збереженим планом. IAM execution role має лише узгоджену
inline policy запису власних журналів; підключених managed policies немає.
Додаткових ресурсів проєкту за його префіксом і тегом не виявлено.

Віддалений state існує в `epam-hw4-state-freshdeploy-20261027` за ключем
`homework4/group-preorder/terraform.tfstate`, шифрування AES256, versioning увімкнено.
S3 backend використовує `allowed_account_ids=["071954286972"]`, `encrypt=true`,
`use_lockfile=true`; активного блокування після перевірки немає. TLS Deny policy збережено.
Історія версій state і видалених об'єктів блокування залишається в S3.

Вихідна бізнес-логіка та склад ресурсів не змінені. Облікові дані до проєкту не
додано; локальні backend, plan, ZIP і службові звіти ігноруються Git.
У GitHub публікуються вихідні файли, provider lockfile, приклад backend і README.
Інфраструктура залишається активною для демонстрації; автоматичне видалення
не налаштовано. Подальше видалення потребує окремого дозволу й рев'ю
destroy plan; до завершення очищення зберігайте той самий віддалений state.

## Як працює застосунок

`GET /group-preorder` повертає зведення фіксованого навчального групового замовлення.
API Gateway HTTP API передає запит Python Lambda у форматі payload 2.0.
Функція не використовує AWS SDK, базу даних, зовнішню мережу чи сторонні бібліотеки.
Кожна відповідь обчислюється заново; стан між запитами не зберігається.

| Учасник | Замовлення | Кількість порцій | Проміжний підсумок | Статус |
| --- | --- | ---: | ---: | --- |
| Owner | Fresh Strawberry Mint Salad × 1; Roasted Sweet Potato & Lentils Salad × 1 | 2 | $27 | `selected` |
| Anna | Fresh Strawberry Mint Salad × 1 | 1 | $17 | `selected` |
| Oleh | Roasted Sweet Potato & Lentils Salad × 2 | 2 | $20 | `selected` |
| Maria | Поки нічого не обрано | 0 | $0 | `pending` |

Ціна першої страви — $17, другої — $10. Груповий підсумок **$64** обчислюється як
`27 + 17 + 20 + 0`, а не записаний константою в реалізації.
Учасників — 4, обрали страви — 3, очікується вибір — 1, порцій — 5.
Статус `selected` означає лише наявність страв, а не оплату чи підтвердження.

JSON містить `schema_version`, `currency`, масив `participants`, лічильники
`participant_count`, `participants_with_orders`, `participants_pending`,
`total_quantity` та `group_total`. Для кожного учасника повертаються ідентифікатор,
ім'я, роль, статус, `orders`, кількість і `subtotal`. Кожен рядок замовлення містить
ідентифікатор і назву страви, `unit_price`, `quantity` та `line_total`.
Грошові поля JSON подано числом у USD, наприклад `64.0`. Уся арифметика виконується
в цілих центах; переведення в USD відбувається лише під час формування відповіді.

Фрагмент відповіді для Oleh:

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

Відповідь має `Content-Type: application/json` та `Cache-Control: no-store`.
Лише `GET /group-preorder` зареєстрований в API Gateway: немає маршруту `$default`,
маршрутів `ANY`, POST/PUT/PATCH/DELETE та Lambda Function URL. Стадія `$default`
існує, але це не маршрут для всіх запитів. Lambda додатково відхиляє методи,
відмінні від GET, кодом 405 та інші шляхи кодом 404. API Gateway відхиляє запити
без відповідного маршруту ще до Lambda. Параметри query та тіло запиту не змінюють
замовлення. CORS не ввімкнено: робота через curl можлива, доступ зі стороннього браузерного
origin не заявляється. Endpoint публічний і без авторизації: тут лише вигадані
навчальні дані, а не персональні дані справжніх відвідувачів.

## Розгорнута інфраструктура

Регіон зафіксовано: **eu-north-1**. Ім'я за замовчуванням — `epam-hw4-group-preorder`;
змінна `name` дає змогу ізолювати додатковий екземпляр.

- Lambda: **Python 3.14**, ARM64, **128 МБ**, **3 секунди**, без VPC, layers,
  provisioned concurrency та опублікованих версій. ZIP містить лише
  `lambda_function.py`; обробник — `lambda_function.handler`.
- IAM execution role з trust policy лише для Lambda та inline policy лише на
  створення log streams / запис подій у власну групу журналів. Функція не
  отримує прав на S3, DynamoDB чи зміну інфраструктури.
- HTTP API, Lambda proxy integration, один GET route та стадія `$default`
  з автоматичним розгортанням і обмеженням **1 запит/секунду**, burst **2**.
- Resource-based permission на виклик Lambda лише API Gateway цього API,
  лише для `GET /group-preorder`.
- Дві CloudWatch Log Groups — для Lambda та access logs API — зі строком зберігання **7 днів**.
  Access logs містять ідентифікатор запиту, маршрут, статус і технічні відомості,
  але не тіло відповіді чи замовлення.

`integration_method = "POST"` у Terraform — внутрішній протокол виклику Lambda
з API Gateway, а не публічний POST-маршрут.
HTTP API використовує доставлення access logs сервісом AWS; Lambda role не отримує
прав на API logs. У цільовому обліковому записі працюють необхідні службові
механізми CloudWatch delivery: access log успішного GET підтверджено після розгортання.
Локальний `validate` сам по собі не перевіряє доставлення журналів.

Загалом визначено 10 керованих ресурсів і локальне джерело даних `archive_file`.
S3 bucket для state **навмисно не входить** до цього проєкту. Не створюються CloudFront,
NAT Gateway, EC2, база даних, custom domain, додаткові API чи вихідний backend.

## Локальна перевірка без AWS

Потрібні Terraform **>= 1.10, < 2**, Python **3.14**, доступ до офіційних
адрес реєстру/релізів Terraform лише для завантаження інструментів і providers.
Версії providers зафіксовано у відстежуваному файлі `.terraform.lock.hcl`.
Python не має pip-залежностей.

Із `infra/homework4/`:

```bash
terraform fmt
terraform init -backend=false -input=false -lockfile=readonly
terraform validate
python3.14 -m unittest -v
```

Або `bash validate.sh`: скрипт перевіряє Python 3.14, запускає `fmt -check`,
`init -backend=false`, `validate` та модульні тести. Він також знаходить інструменти,
встановлені локально в ігнорованому каталозі `.tools/`, або приймає шляхи через
`TERRAFORM_BIN` та `PYTHON_BIN`. Кожен крок має завершитися успішно; код помилки
не приховується. Для початкового створення lockfile використано звичайний
`terraform init -backend=false -input=false`; подальші перевірки забороняють
неявну зміну lockfile.

`init -backend=false` завантажує providers і не звертається до S3. `validate`
перевіряє конфігурацію та схеми providers, не виконуючи запитів до AWS API.
Тести перевіряють підсумок $64, рядки замовлень, статуси, змінювані ціни й кількості,
точність арифметики в центах, порожню групу, відсутність мутацій даних, помилки
вхідних даних і HTTP-відповіді. Вони не використовують облікові дані AWS та мережу.

Ці автономні перевірки самі по собі не перевіряють AWS runtime, IAM-дозволи й
публічну URL-адресу. Додатково 27.10.2026 виконано справжнє розгортання Lambda ARM64,
один GET до робочого endpoint і перевірку CloudWatch Logs; результати наведено вище.

Результати початкової автономної перевірки 27 жовтня 2026 (до розгортання):

| Перевірка | Результат |
| --- | --- |
| `terraform fmt`, потім `fmt -check` | Успішно |
| `terraform init -backend=false` | Успішно; backend не підключався |
| Повторний init із `-lockfile=readonly` | Успішно; зафіксовані версії використано повторно |
| `terraform validate` | `Success! The configuration is valid.` |
| Python unit tests | **14 passed**, 0 failed, 0 skipped |
| Перевірка меж змін | Лише нові файли всередині `infra/homework4/` |

Інструменти початкової автономної перевірки: Terraform **1.14.0**, CPython **3.14.8**; providers
`hashicorp/aws` **6.70.0** та `hashicorp/archive` **2.8.1**. Terraform ZIP перевірено
за SHA-256 з офіційного маніфесту релізу; providers встановлено з перевіркою
підпису HashiCorp. Повний локальний журнал — `.build/validation.log` (ігнорується
Git). На цьому початковому автономному етапі облікові дані AWS не використовувалися та
state не створювався. Під час подальшого розгортання використано тимчасову login-сесію,
а чинний state збережено в захищеному S3 backend.

## Перевірка контрольних сум providers для Windows/Linux

До lockfile додано офіційні `h1` для Windows AMD64; версії providers залишилися
AWS 6.70.0 та archive 2.8.1, хеші Linux AMD64 збережено. Пакети перевірено за
підписами HashiCorp. Для узгодженого оновлення хешів платформ використовується:

```powershell
terraform providers lock -platform=windows_amd64 -platform=linux_amd64
```

Звичайний init виконуйте з `-lockfile=readonly`. `zh` перевіряє вихідний ZIP,
а перевірка розпакованого provider cache використовує відповідний платформі `h1`.
Не виправляйте невідповідність вимкненням перевірки чи видаленням довірених контрольних сум.

## Постійний стан: обов'язковий окремий S3 bucket

Codex Cloud є тимчасовим середовищем: локальний `terraform.tfstate` може зникнути разом із задачею.
Для справжнього розгортання обов'язковий віддалений S3 backend. У `backend.tf` уже оголошено
частковий `backend "s3" {}`: не видаляйте його й не переходьте на local backend.
Після локального `init -backend=false` не можна одразу запускати команди розгортання.

Використовується наявний bucket `epam-hw4-state-freshdeploy-20261027` в обліковому записі
`071954286972`, регіон `eu-north-1`; створювати його повторно не потрібно. Перевірено
versioning, SSE-S3, усі чотири Block Public Access, Bucket owner enforced та TLS Deny.
Під час підготовки іншого середовища bucket створюється окремо, а не цим Terraform root.
Вимоги до захисту backend:

1. Виберіть свій обліковий запис AWS та `eu-north-1`, задайте унікальне ім'я bucket.
2. Увімкніть **Block all public access** — усі чотири налаштування. Object Ownership —
   **Bucket owner enforced**, ACL вимкнено.
3. Увімкніть **S3 Versioning** для відновлення попереднього state.
4. Установіть Default encryption **SSE-S3 (AES-256)**. `encrypt = true` додатково
   вимагає server-side encryption під час запису backend. Для цієї задачі окремий
   платний customer-managed KMS key не потрібен.
5. Bucket policy має забороняти запити без TLS (`aws:SecureTransport = false`).
   Доступ дозволяється лише виділеним deployment identities/адміністраторам,
   а не публічно й не execution role нашої Lambda.
6. Обмежте IAM: `s3:ListBucket` на bucket із необхідними префіксами;
   `s3:GetObject` / `s3:PutObject` на
   `homework4/group-preorder/terraform.tfstate`;
   `s3:GetObject` / `s3:PutObject` / `s3:DeleteObject` на
   `homework4/group-preorder/terraform.tfstate.tflock`.
   Для звичайного керування state право видалення самого state не потрібне.
7. Не вмикайте Object Lock, який забороняє видалення `.tflock`. Для блокування state
   використовується **нативний S3 lockfile**, `use_lockfile = true` (Terraform 1.10+).
   Таблиця блокування DynamoDB не потрібна. Не використовуйте `-lock=false`.

Скопіюйте `backend.hcl.example` в ігнорований `backend.hcl`, замініть ім'я bucket
і єдиний елемент `allowed_account_ids` на справжній 12-значний account ID.
Фіксовані `region`, `key`, `encrypt` та `use_lockfile` залиште. Жодних облікових даних
у HCL. `allowed_account_ids` стосується саме S3 backend і перевіряє обліковий запис
identity, що здійснює виклик. Це не перевірка власника bucket у кожному S3-запиті.
Параметр `expected_bucket_owner` не підтримується Terraform S3 backend; для перевірки
власника окремо використовуйте AWS CLI `--expected-bucket-owner` перед підключенням.
Для одного екземпляра використовуйте той самий bucket/key у всіх майбутніх задачах.
Додатковому незалежному екземпляру потрібен інший key, а не спільний state.

Versioning і locking розв'язують різні задачі: перше дає історію, друге запобігає
одночасному запису. State містить чутливі дані й не має потрапляти до Git, повідомлень,
публічних артефактів чи загальнодоступних резервних копій. Не публікуйте `.terraform/`:
там зберігаються метадані backend. І state, і plan-файли можуть містити секрети.
Lockfile `.terraform.lock.hcl` містить лише версії та контрольні суми providers,
а не секрети чи Terraform state.

Якщо хтось раніше випадково виконав розгортання з local state, спочатку збережіть
його захищену копію й окремо узгодьте міграцію через `init -migrate-state`.
Не втрачайте цей файл і не створюйте другий набір ресурсів із порожнім state.
Розгортання використовує віддалений S3 state; окремий локальний state розгортання не застосовувався.

## Тимчасова авторизація та відтворюваний робочий процес

**Усі дії виконуються вручну в узгодженому обліковому записі; apply запускається лише для перевіреного збереженого плану.**
Перед роботою у вашому AWS Free Plan перевірте в Billing активний план, залишок
кредитів, строк їхньої дії, доступність Lambda/HTTP API та квоти в `eu-north-1`.
Назва Free Plan не означає безстрокової безоплатності чи нульової вартості всіх
ресурсів. Не переводьте обліковий запис на платний план без окремого рішення власника.

Для доступу користувача перевага надається короткій сесії IAM Identity Center
(AWS CLI v2 SSO). Для автоматизації — короткочасна STS-сесія через довірений
OIDC/федеративний механізм, якщо його надає ваша організація/платформа.
Execution role Lambda та deployment identity — різні ролі: останній також потрібні
обмежені права створювати й видаляти перелічені ресурси, `iam:PassRole`
лише для execution role та права backend, описані вище.

Не використовуйте root credentials, довготривалі IAM access keys, ключі зі стороннього
проєкту, секрети в чаті, `backend.hcl`, `.tfvars`, коді чи shell history.
Не припускайте, що HTTP-proxy placeholder секрету є придатними необробленими обліковими даними AWS
для SigV4. Налаштування авторизації має підтримуватися AWS SDK/CLI.
Альтернативний приклад інтерактивної SSO-сесії:

```bash
aws configure sso --profile homework4
aws sso login --profile homework4
export AWS_PROFILE=homework4
export AWS_REGION=eu-north-1
aws sts get-caller-identity
```

Для поточного робочого процесу Windows перевірено AWS CLI 2.38.6, Terraform 1.17.0 та профіль
`fresh-deploy`, який використовує браузерний `aws login`. Секрети вводяться користувачем
лише на сторінці AWS; Terraform читає тимчасову сесію безпосередньо. Якщо сесія
спливла, оновіть її командою `aws login --profile fresh-deploy --region eu-north-1`.

Перед init, plan та apply виконайте PowerShell-перевірки облікового запису й власника bucket:

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

`backend.hcl` має містити `allowed_account_ids = ["071954286972"]`,
`region = "eu-north-1"`, `encrypt = true`, `use_lockfile = true` та ключ
`homework4/group-preorder/terraform.tfstate`. Не вимикайте перевірки облікових даних,
контрольних сум, підписів чи TLS. `terraform validate` не перевіряє параметри S3 backend:
для цього потрібні успішний remote init і перевірка фактичної конфігурації backend.

Перевірте Account та ARN перед змінами. Не виводьте значення токенів/змінних.
Авторизуйтеся лише в окремій задачі для дозволеного розгортання; не публікуйте
знімок середовища з активними обліковими даними чи кешами SSO. Після завершення закрийте
сесію (`aws sso logout` у виділеному робочому середовищі; команда зачіпає його SSO
sessions), видаліть лише власні тимчасові облікові дані/артефакти плану та
дайте STS-сесії спливти. Не видаляйте наосліп спільні файли облікових даних користувача.

Нижче наведено робочий процес, використаний для розгортання. Повторювати apply можна лише після
окремого узгодження нового збереженого плану; для поточного розгортання
підсумковий перевірний plan уже показав відсутність змін.
Із цього каталогу:

```powershell
terraform init -reconfigure "-backend-config=backend.hcl" -input=false -lockfile=readonly
if ($LASTEXITCODE -ne 0) { throw "Terraform init failed" }
terraform validate
if ($LASTEXITCODE -ne 0) { throw "Terraform validate failed" }
git check-ignore -- homework4.tfplan backend.hcl
if ($LASTEXITCODE -ne 0) { throw "Local deployment files must be ignored" }
terraform plan "-out=homework4.tfplan" -input=false -lock-timeout=30s
if ($LASTEXITCODE -ne 0) { throw "Terraform plan failed" }
# Перевірити plan: лише узгоджені ресурси, account/region, IAM і вартість.
# Виконувати наступну команду лише після дозволу саме на цей план:
terraform apply -input=false homework4.tfplan
if ($LASTEXITCODE -ne 0) { throw "Terraform apply failed" }
terraform output -raw group_preorder_url
```

`homework4.tfplan`, `backend.hcl`, `.terraform/`, ZIP і файли в `.build/` ігноруються
Git. Не додавайте їх примусово: state і plan можуть містити чутливі
дані. Не запускайте apply без імені узгодженого збереженого плану.

`-reconfigure` підключає вже підготовлений віддалений backend; це не міграція
наявного local state. У новій локальній Windows-сесії відновіть тимчасову
авторизацію та використовуйте той самий bucket/key і backend config. Запуск розгортання
з іншого середовища також має використовувати цей віддалений state.
Не зберігайте команди розгортання в автоматичному сценарії запуску/встановлення середовища.
Робоча URL-адреса й результат виконаного smoke test наведені на початку README.
Для окремо узгодженої повторної перевірки використовуйте GET за output URL і
порівняйте підсумок $64 та статуси; непідтримувані маршрути мають відхилятися.
Не надсилайте справжні персональні дані до цього публічного demo API.

## Витрати й обмеження

- Оплачуватися можуть Lambda invocations/duration, API Gateway requests,
  CloudWatch ingestion/storage, S3 storage/requests/version history і вихідний
  трафік. Фактичне покриття Free Plan перевіряється у вашому обліковому записі.
- Lambda ARM64/128 МБ/3 секунди, HTTP API, ліміт 1 request/sec і короткий строк зберігання
  зменшують витрати. Throttling — best effort, **не жорсткий грошовий ліміт**.
  Публічний GET також можна викликати багаторазово; за підозри на зловживання
  вимкніть endpoint/видаліть навчальний стек після перевірки.
- Не створюються постійно запущені сервери, NAT Gateway та бази даних.
  Reserved concurrency не задано, щоб не вимагати резервування квоти нового
  облікового запису; враховуйте загальну Lambda concurrency quota свого облікового запису.
- Через Billing Console заздалегідь налаштуйте невеликі бюджетні сповіщення та повідомлення
  про кредити. Це не створюється цим Terraform; сповіщення не зупиняють витрати.
  Не додавайте платні допоміжні послуги без перевірки їхньої ціни.
- Зараз демо залишається активним для демонстрації. Після її завершення узгодьте
  очищення окремо. Строк зберігання 7 днів не видаляє самі log groups; Terraform видалить
  обидві групи під час очищення. Старі версії S3 state зберігаються окремо.

## Безпечне видалення надалі — лише після окремого дозволу

Інфраструктура зараз активна. Наведені нижче команди описують подальше очищення й під час
завершення домашнього завдання №4 не виконуються. Спочатку відновіть профіль `fresh-deploy`,
виконайте наведені вище перевірки STS та власника S3 для облікового запису `071954286972` і
підключіть **той самий S3 backend**. Не видаляйте ресурси вручну в обхід Terraform.

Після окремого дозволу на підготовку очищення з `infra/homework4/`:

```powershell
$env:AWS_PROFILE = "fresh-deploy"
$env:AWS_REGION = "eu-north-1"
terraform init -reconfigure "-backend-config=backend.hcl" -input=false -lockfile=readonly
if ($LASTEXITCODE -ne 0) { throw "Terraform init failed" }
terraform plan -destroy "-out=homework4-destroy.tfplan" -input=false -lock-timeout=30s
if ($LASTEXITCODE -ne 0) { throw "Destroy plan failed" }
# Перевірити: лише десять керованих ресурсів ДЗ №4, без сторонніх ресурсів.
# Отримати окремий дозвіл на застосування саме цього destroy plan.
# Наступну команду не запускати до отримання цього дозволу:
terraform apply -input=false homework4-destroy.tfplan
if ($LASTEXITCODE -ne 0) { throw "Cleanup failed; preserve remote state" }
```

Це видалить API, Lambda, execution role/policy, permission та обидві групи журналів.
Bucket state не керується цим стеком і залишиться. Не видаляйте state чи bucket
до підтвердженого завершення очищення. Після перевірки порожнього state та відсутності
ресурсів вирішіть окремо, скільки зберігати захищену історію для аудиту/відновлення.
Якщо bucket виділений лише цьому завданню й більше не потрібен, його видалення — окрема
операція власника: потрібно видалити всі версії та delete markers, переконатися у відсутності
активного `.tflock` та інших проєктів. Не застосовуйте рекурсивне видалення до спільного
bucket і не видаляйте активний lock; force-unlock припустимий лише після перевірки,
що жоден процес Terraform більше не працює, та окремого узгодження.

Офіційні довідкові матеріали:

- [S3 backend, versioning і locking](https://developer.hashicorp.com/terraform/language/backend/s3)
- [Python runtime Lambda](https://docs.aws.amazon.com/lambda/latest/dg/lambda-python.html)
- [HTTP API Lambda integration payload 2.0](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html)
- [Тимчасовий доступ AWS CLI через IAM Identity Center](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sso.html)
- [AWS Free Tier / Free Plan](https://aws.amazon.com/free/)
