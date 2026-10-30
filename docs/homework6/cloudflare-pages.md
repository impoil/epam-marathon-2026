# Green&Tasty: AWS backend и Cloudflare Pages frontend

**Обновление после отдельно разрешённого apply 30.10.2026:** пользователь создал Pages project и подтвердил origin `https://greentasty-epam-impoil.pages.dev`. Созданы оставшиеся **8** AWS resources; backend **https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo** работает. Контрольный plan **1/3/1**; повторный apply не выполнялся. Фактические проверки и блокер: [deployment report](aws-backend-deployment.md). Ниже сохранена история подготовки до apply; инструкции monorepo/SPA по-прежнему применимы, Angular агентом не публиковался.

Подготовлено **30 октября 2026** на Windows в `feature/hw6-greentasty-aws-deployment`, PR #2. Cloudflare project не создавался, frontend не публиковался. AWS apply/destroy, state rm/import/force-unlock/migration, users/seed и изменения квот не выполнялись.

## Результат remote plan

**8 Add / 0 Change / 0 Destroy, 22 no-op**, replacements/imports 0. План получен по существующему S3 backend, с refresh и настоящими AWS credentials; без `-target`, `-refresh=false` и skip account/credentials validation. `complete=true`, `applyable=true` не являются разрешением на apply и не гарантируют успех всех Create API.

Источник: `fresh-deploy`, Account **071954286972**, workload region **eu-north-1**. Free Plan остаётся **FREE / ACTIVE**, API показывает **$140** credits. Регион явно задан provider/backend/CLI. Квота Lambda 5, используется прежний `reserved_concurrency=null`.

Предварительный origin выбран пользователем: **`https://greentasty-epam-impoil.pages.dev`**. Он **ещё не зарезервирован и не является работающим сайтом**. Финальный apply должен использовать подтверждённый фактический production origin; при другом назначенном URL заменить только `frontend_origin` и пересмотреть свежий plan.

Read-only команда из корня репозитория:

```powershell
terraform '-chdir=infra/hw6-greentasty' init -input=false -reconfigure '-backend-config=backend.hcl'
terraform '-chdir=infra/hw6-greentasty' plan -input=false -lock=false '-var-file=demo.tfvars.example' '-out=build/cloudflare-preparation/cloudflare.tfplan'
```

Backend сохраняет `use_lockfile=true`. Только текущий plan использовал `-lock=false`, чтобы не писать даже временный S3 lock в рамках запрета AWS-записей. Auth/account/variable validation не отключены; до/после проверено отсутствие изменений state. Нельзя выполнять параллельные операции над этим state. Будущий разрешённый apply потребует нового плана с обычным locking.

SHA256 этого плана: **`9be60c1464733c4dc56188a434b2e6ad2540b4a8f5d98a262507aafaa8acdb20`**. Binary plan, JSON state и backend.hcl остаются локально в ignored files.

Создаются только:

1. `aws_lambda_function.api` — настоящий .NET 10 backend.
2. `aws_lambda_permission.gateway`.
3. `aws_api_gateway_rest_api.app`.
4. `aws_api_gateway_deployment.app`.
5. `aws_api_gateway_stage.app`.
6. `aws_api_gateway_method_settings.limits`.
7. `aws_api_gateway_gateway_response.cors["DEFAULT_4XX"]`.
8. `aws_api_gateway_gateway_response.cors["DEFAULT_5XX"]`.

После отдельно разрешённого применения получится 30 managed resources: 24 для AWS backend и 6 сохранённых неиспользуемых hosting resources. Внутри REST API остаются Cognito authorizer, настоящие 33 application methods и OPTIONS; в DynamoDB — 13 GSI. Они не являются дополнительными отдельными Terraform addresses.

## Минимальное изменение архитектуры

Browser → **Cloudflare Pages (Angular static SPA)** → **AWS REST API `/demo`** → **.NET Lambda** → **Cognito/DynamoDB**. Backend, аутентификация и состояние корзин остаются настоящими в AWS. Cloudflare Functions/Workers, proxy API, моки и статические JSON вместо backend не добавлялись.

Из конфигурации удалены только декларации **не созданных** `aws_cloudfront_distribution.frontend` и `aws_s3_bucket_policy.frontend`. Их не было в state, поэтому удаление деклараций не вызывает destroy. Существующие resource addresses не переименованы и не обёрнуты в count/for_each; moved/import/state operations не нужны.

Единственные backend wiring changes:

- Обязательная переменная `frontend_origin`, без default/wildcard; validation допускает один lower-case HTTPS DNS origin без path, slash в конце, query, fragment, port и credentials.
- `local.origin` API Gateway и Lambda `CORS_ALLOWED_ORIGIN` используют эту переменную. OPTIONS и DEFAULT_4XX/DEFAULT_5XX получают тот же origin; `Content-Type,Authorization`, существующие методы и отсутствие Allow-Credentials сохранены. CORS не подменяет Cognito/guest authorization.
- `frontend_url` теперь означает **сконфигурированный внешний origin**, а не доказательство существования Pages project. Output `distribution_id` удалён, поскольку distribution не существует. `frontend_bucket` сохранён для инвентаря/будущей очистки и явно помечен unused.
- IAM не требует изменений: execution role не имеет CloudFront/S3 grants и ссылается на новые DynamoDB tables/indices, Cognito pool и log group. `build-frontend.ps1` уже читает только `backend_url`; CloudFront ему не нужен.

Cognito использует существующий `/signin` flow USER_PASSWORD_AUTH, а не Hosted UI callback на CloudFront; менять pool/client/roles не требуется. Производственные CORS headers будут точными для одного выбранного origin; preview `https://<hash>.<project>.pages.dev`, localhost или другой custom domain автоматически не разрешаются. Не добавлять `*.pages.dev` ради previews.

Backend business source и настоящий Linux .NET ZIP не менялись. ZIP SHA256: **`4bfb24e45ca476353ac1d58473db7e7f67e7ce6a0927cfcadde3c6645a7daec3`**. В active application source/scripts других ссылок на CloudFront/distribution_id не найдено. В Terraform CloudFront остаётся только как три сохранённых вспомогательных ресурса; refresh читает их metadata, но backend от них не зависит.

## Сохранённые 22 ресурса и state

State успешно прочитан Terraform и сопоставлен с последним partial apply: **22 managed resources**, тот же набор адресов. Remote plan refresh показывает все 22 как no-op, что также подтверждает согласованность state и AWS в области этого root.

| Существующие ресурсы | Количество | Действие этого варианта |
| --- | ---: | --- |
| DynamoDB tables | 10 | Сохраняются, no-op; 13 GSI внутри |
| Cognito pool/client/Customer group | 3 | Сохраняются, no-op |
| IAM role/inline policy | 2 | Сохраняются, no-op |
| CloudWatch log group | 1 | Сохраняется, no-op |
| Private frontend S3 bucket/public block/SSE | 3 | Сохраняются unused, no-op |
| CloudFront function/OAC/response headers policy | 3 | Сохраняются unused, no-op |

Backend bucket/key остаются **`epam-hw4-state-freshdeploy-20261027` / `hw6-greentasty/terraform.tfstate`**, AES256, 61 712 bytes. VersionId/ETag/LastModified hw6 state до/после совпали; lock object не появился. До/после также сравниваются HW4 state metadata, HW4 Lambda/API/routes/integrations/stages и HelloWorld; изменений нет. `infra/homework4` не менялся. State содержимое не включается в Git/отчёт.

### Только предложение отдельной очистки

Для работы backend ничего из уже созданного удалять **не обязательно**. После отдельного согласия и проверки отсутствия связей/данных можно удалить из конфигурации ровно шесть unused resources:

1. `aws_cloudfront_function.spa`.
2. `aws_cloudfront_origin_access_control.frontend`.
3. `aws_cloudfront_response_headers_policy.frontend`.
4. `aws_s3_bucket_server_side_encryption_configuration.frontend`.
5. `aws_s3_bucket_public_access_block.frontend`.
6. `aws_s3_bucket.frontend`.

Тогда убрать относящийся к bucket output и больше не нужный `spa.js`; заново проверить, что bucket пуст, нет связанных distributions и что это **не state bucket**. `force_destroy=false` сохраняется. После завершения backend ожидаемый отдельный cleanup plan — **0 Add / 0 Change / 6 Destroy**; если объединять с ещё не созданным backend, получится **8/0/6**, что сейчас не предлагается применять. Сам cleanup diff/plan не создавался, удаление не запускалось. `state rm`, `-target` и ручное удаление через console не нужны: Terraform должен сохранить историю управления.

## Cloudflare Pages с приватным GitHub monorepo

GitHub metadata подтверждает, что **`impoil/epam-marathon-2026` private**. Pages поддерживает private repositories через Cloudflare Workers & Pages GitHub App: на будущем этапе разрешить доступ **только к этому repository**. Менять visibility или выдавать AWS keys Cloudflare не нужно. Подключение GitHub App/создание проекта сейчас не выполнялось. [GitHub integration](https://developers.cloudflare.com/pages/configuration/git-integration/github-integration/), [private repositories и limits](https://developers.cloudflare.com/pages/platform/limits/).

Настройки для **будущего разрешённого** создания проекта:

| Поле | Значение |
| --- | --- |
| Project name | `greentasty-epam-impoil`, проверить фактически назначенный production hostname |
| Git repository | `impoil/epam-marathon-2026`, Git integration |
| Production branch для текущего demo | `feature/hw6-greentasty-aws-deployment`; merge PR #2 для этого не требуется |
| Root directory | **`frontend/restaurant-frontend-app`** |
| Framework preset | None/custom; явные команды ниже вместо preset defaults |
| Build command | **`npm ci && npm run build && node scripts/check-pages.mjs`** |
| Build output directory, относительно Root | **`dist/restaurant-frontend-app/browser`** |
| Node | `.node-version` фиксирует **24.22.1**; можно задать такой же `NODE_VERSION` в dashboard |
| `SKIP_DEPENDENCY_INSTALL` | **`1`**, поскольку `npm ci` уже включён в build command |
| Production `BACKEND_URL` | **Настоящий `terraform output -raw backend_url` после разрешённого backend apply**; сейчас API ещё не создан |
| Build watch include paths | `frontend/restaurant-frontend-app/*` (пути от repo root; `*` в Pages также охватывает вложенные пути) |
| Preview deployments | Отключить для demo или отдельно согласовать окружение/CORS; production origin не совпадает с preview URLs |

Git integration обычно запускает builds/deployments на push. Пока публикация не разрешена, проект не создавать; в будущем сначала согласовать создание/первый deploy и branch controls. После перехода production branch на main потребуется отдельное изменение настройки, а не самостоятельный merge PR. [Monorepos](https://developers.cloudflare.com/pages/configuration/monorepos/), [build watch paths](https://developers.cloudflare.com/pages/configuration/build-watch-paths/), [build image/version and install control](https://developers.cloudflare.com/pages/configuration/build-image/).

`npm run build` обязателен: он запускает генератор production config из BACKEND_URL и bundle marker check. Непосредственный `ng build` обходит npm hooks. В build environment нет AWS credentials, JWT, паролей или Terraform state. BACKEND_URL — публичная URL-строка, она попадёт в JS bundle. Приватность GitHub source не делает опубликованный browser bundle приватным.

### SPA и static headers

Deploy только каталог **browser**, не весь monorepo, backend или `dist` целиком. Angular использует root base href. У Cloudflare Pages штатный SPA fallback: при отсутствии верхнего `404.html` запрос неизвестного asset path обслуживается root SPA. Поэтому отдельный blanket `_redirects` не добавлялся; routes `/guest/...` и `/reservations/<id>/group-order` обрабатывает Angular. Проверить прямое открытие и Refresh после будущей публикации. [SPA contract Pages](https://developers.cloudflare.com/pages/configuration/serving-pages/), [Angular guide](https://developers.cloudflare.com/pages/framework-guides/deploy-an-angular-site/).

`public/_headers` копируется Angular в output и задаёт no-referrer, nosniff, frame DENY; invite-token не должен уходить в Referer на внешние сайты. Это static frontend headers, а не CORS заголовки AWS API. Source maps/Functions/Workers не публикуются. `check-pages.mjs` проверяет SPA entry, отсутствие root 404.html и server/private artifacts, security headers, лимиты 20 000 файлов / 25 MiB на файл. Текущие 98 файлов и largest 11 620 373 bytes укладываются в Free limits. [Pages limits](https://developers.cloudflare.com/pages/platform/limits/).

## Проверки и ограничения

| Проверка на Windows | Результат |
| --- | --- |
| Terraform fmt/init/validate | Passed |
| Реальная validation переменной origin, изолированный provider-free module, только plan | **12 passed**, valid Pages/custom DNS; HTTP/wildcard/slash/path/query/fragment/credentials/port/upper-case/empty rejected |
| Remote plan с настоящим S3 backend | **8/0/0**, 22 no-op |
| Backend ZIP/source | Не менялись, SHA256 совпал |
| TypeScript app/spec | Passed |
| Production URL generator tests | **6 passed** |
| Production Angular build/bundle markers | Passed, initial **972.13 kB**, transfer **223.16 kB** |
| Pages artifact check | **98 files**, max **11 620 373 bytes**, no root 404/server/private artifacts |
| Полный frontend | **283 passed / 47 failed**, все имена failures совпадают с baseline |
| Browser production `?demo` | Storage пуст, protected route → `/login` |
| Cloudflare-native CI / live CORS / deployed E2E | Не выполнялись: нет Pages project и backend API |

Локальная компиляция использовала технический несуществующий `https://0000000000.execute-api.eu-north-1.amazonaws.com/demo`; artifact не публиковался и не поставляется как production frontend. Настоящий BACKEND_URL появится после отдельного разрешённого AWS apply. Бизнес-логика ДЗ №5–6, npm dependencies и исходные homework tests не менялись. Сохраняются прежние Angular budget/CommonJS и Terraform deprecated hash_key warnings.

## Новая стоимость

При Free Pages и **только static assets без Functions** хостинг frontend — **$0** в пределах правил/лимитов выбранного плана: static requests бесплатны, Free build quota 500/month, timeout 20 min, asset limits выше. Cloudflare account/plan/Git App permissions не подключались и не проверены; платные продукты не включались. [Static asset pricing](https://developers.cloudflare.com/pages/functions/pricing/), [limits](https://developers.cloudflare.com/pages/platform/limits/).

AWS модель прежней нагрузки без CloudFront/S3 frontend traffic: 10 000 REST/Lambda requests, 512 MB × 0.5 s, 50 000 WRU, 100 000 RRU, 0.01 GB DynamoDB, 0.1 GB logs/7-day retention, 3 MAU. По ранее полученным ставкам eu-north-1: **около $0.20/месяц до allowances/credits**, плюс API Internet egress, state storage/requests и налоги. Практический резерв **$1–3/месяц**; это не денежный cap. Отдельный custom domain в расчёт не входит; `pages.dev` не требует покупки домена.

Шесть оставленных пустых/неподключённых hosting resources не дают фиксированного compute charge: bucket пуст, function не обслуживает distribution. До отдельной очистки остаются managed resources и возможность небольших storage/API charges при использовании. Общая Lambda quota 5 и риск взаимного throttling с HW4 не изменяются. Исходные ставки и допущения: [AWS cost verification](aws-verification.md).

## Перед окончательным apply

Нужен **подтверждённый production HTTPS origin**, планируемый сейчас `https://greentasty-epam-impoil.pages.dev`. Если имя занято/Cloudflare назначит другой hostname или будет custom domain, обновить `frontend_origin` и получить новый plan. Не применять сохранённый provisional plan как окончательно согласованный. Публикация реального backend без AWS CloudFront **возможна**; свежий 8/0/0 plan это подтверждает на уровне Terraform. После отдельного разрешения и apply проверить AWS runtime/API/CORS, затем отдельно согласовать users/seed/Pages publish и настоящий E2E. Никакие из этих AWS/Cloudflare операций текущей подготовкой не выполнялись.
