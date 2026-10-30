# Green&Tasty: AWS backend и Cloudflare Pages frontend

**Актуально на 30 октября 2026:** Angular **опубликован пользователем** на **https://greentasty-epam-impoil.pages.dev**; чтение HTTPS страницы подтвердило **200**, Angular root и 3 script tags. Настоящий backend **https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo** работает. [Drift correction apply](aws-drift-apply.md) **1/1/1** успешен; контрольный remote plan с refresh — **0/0/0**, все 30 ресурсов no-op. PR #2 остаётся Draft.

Это текущий runbook размещения. Пользователи Cognito, seed и cleanup не выполнялись; авторизованный E2E — отдельный следующий этап. Настройки Cloudflare dashboard агент в этом ходе не менял, повторную публикацию не запускал.

## AWS backend и remote state

Контрольный remote plan: **0 Add / 0 Change / 0 Destroy, 30 no-op**. Используются существующий S3 backend, refresh, настоящие AWS credentials и обычная S3-блокировка; без `-target`, `-refresh=false` и skip account/credentials validation.

Источник: `fresh-deploy`, Account **071954286972**, workload region **eu-north-1**. Последняя проверка Free Plan на этапе создания backend: **FREE / ACTIVE**, **$140** credits; актуальный invoice этим runbook не измеряется. Регион явно задан provider/backend/CLI. Квота Lambda 5, используется прежний `reserved_concurrency=null`.

Подтверждённый production origin работающего Angular: **`https://greentasty-epam-impoil.pages.dev`**. Он задан в `frontend_origin`. Для будущего другого hostname/custom domain потребуется изменить переменную и отдельно согласовать новый plan.

Команда проверки из корня репозитория (не разрешение на apply):

```powershell
terraform '-chdir=infra/hw6-greentasty' init -input=false -reconfigure '-backend-config=backend.hcl'
terraform '-chdir=infra/hw6-greentasty' plan -input=false '-lock-timeout=60s' '-var-file=demo.tfvars.example' '-out=build/check.tfplan'
```

Backend сохраняет `use_lockfile=true`, lockfiles штатно освобождаются. Auth/account/variable validation не отключены. Binary plans, JSON state и backend.hcl остаются локально в ignored files. Исторические планы подготовки не применять повторно.

После первоначального partial apply ранее были созданы следующие восемь ресурсов (это инвентарь, а не новые действия):

1. `aws_lambda_function.api` — настоящий .NET 10 backend.
2. `aws_lambda_permission.gateway`.
3. `aws_api_gateway_rest_api.app`.
4. `aws_api_gateway_deployment.app`.
5. `aws_api_gateway_stage.app`.
6. `aws_api_gateway_method_settings.limits`.
7. `aws_api_gateway_gateway_response.cors["DEFAULT_4XX"]`.
8. `aws_api_gateway_gateway_response.cors["DEFAULT_5XX"]`.

Всего **30 managed resources**: 24 для AWS backend и 6 сохранённых неиспользуемых hosting resources. Внутри REST API — Cognito authorizer, настоящие 33 application methods и 28 OPTIONS; в DynamoDB — 13 GSI. Они не являются дополнительными отдельными Terraform addresses.

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

## Сохранённые ресурсы и state

Первоначальные 22 ресурса сохранены; дополнительно создан backend/API. В текущем state **30 managed resources**, контрольный remote plan показывает все 30 как no-op.

| Существующие ресурсы | Количество | Действие этого варианта |
| --- | ---: | --- |
| DynamoDB tables | 10 | Сохраняются, no-op; 13 GSI внутри |
| Cognito pool/client/Customer group | 3 | Сохраняются, no-op |
| IAM role/inline policy | 2 | Сохраняются, no-op |
| CloudWatch log group | 1 | Сохраняется, no-op |
| Private frontend S3 bucket/public block/SSE | 3 | Сохраняются unused, no-op |
| CloudFront function/OAC/response headers policy | 3 | Сохраняются unused, no-op |
| Lambda/permission, REST API/deployment/stage/settings, 2 gateway responses | 8 | Созданы, no-op |

Backend bucket/key остаются **`epam-hw4-state-freshdeploy-20261027` / `hw6-greentasty/terraform.tfstate`**, AES256. State штатно обновлён разрешённым apply; lockfile освобождён. HW4 state metadata, HW4 Lambda/API/routes/integrations/stages и HelloWorld до/после совпали. `infra/homework4` не менялся. State содержимое не включается в Git/отчёт.

### Только предложение отдельной очистки

Для работы backend ничего из уже созданного удалять **не обязательно**. После отдельного согласия и проверки отсутствия связей/данных можно удалить из конфигурации ровно шесть unused resources:

1. `aws_cloudfront_function.spa`.
2. `aws_cloudfront_origin_access_control.frontend`.
3. `aws_cloudfront_response_headers_policy.frontend`.
4. `aws_s3_bucket_server_side_encryption_configuration.frontend`.
5. `aws_s3_bucket_public_access_block.frontend`.
6. `aws_s3_bucket.frontend`.

Тогда убрать относящийся к bucket output и больше не нужный `spa.js`; заново проверить, что bucket пуст, нет связанных distributions и что это **не state bucket**. `force_destroy=false` сохраняется. Ожидаемый отдельный cleanup plan — **0 Add / 0 Change / 6 Destroy**. Сам cleanup diff/plan не создавался, удаление не запускалось и сейчас запрещено. `state rm`, `-target` и ручное удаление через console не нужны: Terraform должен сохранить историю управления.

## Cloudflare Pages с приватным GitHub monorepo

GitHub metadata подтверждает, что **`impoil/epam-marathon-2026` private**. Pages поддерживает private repositories через Cloudflare Workers & Pages GitHub App; доступ следует ограничивать этим repository. Менять visibility или выдавать AWS keys Cloudflare не нужно. Project и Angular publication уже выполнены пользователем; агент не менял Git App permissions/dashboard. [GitHub integration](https://developers.cloudflare.com/pages/configuration/git-integration/github-integration/), [private repositories и limits](https://developers.cloudflare.com/pages/platform/limits/).

Конфигурация monorepo для сопровождения опубликованного frontend (фактические dashboard settings в этом ходе не считывались):

| Поле | Значение |
| --- | --- |
| Project name | `greentasty-epam-impoil`, подтверждённый hostname `greentasty-epam-impoil.pages.dev` |
| Git repository | `impoil/epam-marathon-2026`, Git integration |
| Production branch для текущего demo | `feature/hw6-greentasty-aws-deployment`; merge PR #2 для этого не требуется |
| Root directory | **`frontend/restaurant-frontend-app`** |
| Framework preset | None/custom; явные команды ниже вместо preset defaults |
| Build command | **`npm ci && npm run build && node scripts/check-pages.mjs`** |
| Build output directory, относительно Root | **`dist/restaurant-frontend-app/browser`** |
| Node | `.node-version` фиксирует **24.22.1**; можно задать такой же `NODE_VERSION` в dashboard |
| `SKIP_DEPENDENCY_INSTALL` | **`1`**, поскольку `npm ci` уже включён в build command |
| Production `BACKEND_URL` | **`https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo`**, совпадает с настоящим Terraform output |
| Build watch include paths | `frontend/restaurant-frontend-app/*` (пути от repo root; `*` в Pages также охватывает вложенные пути) |
| Preview deployments | Отключить для demo или отдельно согласовать окружение/CORS; production origin не совпадает с preview URLs |

Git integration обычно запускает builds/deployments на push. Для текущих Terraform/documentation commits используется **`[CF-Pages-Skip]`**, чтобы не запускать лишний deployment уже опубликованного приложения. Смена production branch/settings и новая публикация в эту задачу не входят; PR #2 не объединяется. [Git integration/skip prefix](https://developers.cloudflare.com/pages/configuration/git-integration/github-integration/), [monorepos](https://developers.cloudflare.com/pages/configuration/monorepos/), [build watch paths](https://developers.cloudflare.com/pages/configuration/build-watch-paths/), [build image](https://developers.cloudflare.com/pages/configuration/build-image/).

`npm run build` обязателен: он запускает генератор production config из BACKEND_URL и bundle marker check. Непосредственный `ng build` обходит npm hooks. В build environment нет AWS credentials, JWT, паролей или Terraform state. BACKEND_URL — публичная URL-строка, она попадёт в JS bundle. Приватность GitHub source не делает опубликованный browser bundle приватным.

### SPA и static headers

Deploy только каталог **browser**, не весь monorepo, backend или `dist` целиком. Angular использует root base href. У Cloudflare Pages штатный SPA fallback: при отсутствии верхнего `404.html` запрос неизвестного asset path обслуживается root SPA. Поэтому отдельный blanket `_redirects` не добавлялся; routes `/guest/...` и `/reservations/<id>/group-order` обрабатывает Angular. Прямое открытие/Refresh этих маршрутов включить в следующий авторизованный E2E на уже опубликованном frontend. [SPA contract Pages](https://developers.cloudflare.com/pages/configuration/serving-pages/), [Angular guide](https://developers.cloudflare.com/pages/framework-guides/deploy-an-angular-site/).

`public/_headers` копируется Angular в output и задаёт no-referrer, nosniff, frame DENY; invite-token не должен уходить в Referer на внешние сайты. Это static frontend headers, а не CORS заголовки AWS API. Source maps/Functions/Workers не публикуются. `check-pages.mjs` проверяет SPA entry, отсутствие root 404.html и server/private artifacts, security headers, лимиты 20 000 файлов / 25 MiB на файл. Текущие 98 файлов и largest 11 620 373 bytes укладываются в Free limits. [Pages limits](https://developers.cloudflare.com/pages/platform/limits/).

## Проверки и ограничения

| Проверка на Windows | Результат |
| --- | --- |
| Terraform fmt/init/validate | Passed |
| Реальная validation переменной origin, изолированный provider-free module, только plan | **12 passed**, valid Pages/custom DNS; HTTP/wildcard/slash/path/query/fragment/credentials/port/upper-case/empty rejected |
| Текущий remote plan с настоящим S3 backend и refresh | **0/0/0**, 30 no-op, после разрешённой коррекции |
| Backend ZIP/source | Не менялись, SHA256 совпал |
| TypeScript app/spec | Passed |
| Production URL generator tests | **6 passed** |
| Production Angular build/bundle markers | Passed, initial **972.13 kB**, transfer **223.16 kB** |
| Pages artifact check | **98 files**, max **11 620 373 bytes**, no root 404/server/private artifacts |
| Полный frontend | **283 passed / 47 failed**, все имена failures совпадают с baseline |
| Browser production `?demo` | Storage пуст, protected route → `/login` |
| Опубликованная Angular страница | Пользователь подтвердил публикацию; read-only HTTP check **200**, Angular root |
| Live API/CORS | 8 smoke requests: публичные GET 200, защищённые/guest без token 401, OPTIONS 200; точный origin |
| Авторизованный deployed E2E / dashboard CI settings | Не проверены; users/seed ещё не созданы, dashboard не менялся |

Исторические TypeScript/build/bundle проверки в таблице выполнялись до публикации на локальном artifact с техническим URL. Они не описывают текущий published bundle и не запускались повторно в drift correction. Теперь реальный BACKEND_URL известен, Angular опубликован пользователем. Бизнес-логика ДЗ №5–6, npm dependencies и исходные homework tests не менялись; прежние Angular budget/CommonJS и Terraform key schema deprecation warnings сохранены.

## Новая стоимость

При Free Pages и **только static assets без Functions** хостинг frontend — **$0** в пределах правил/лимитов выбранного плана: static requests бесплатны, Free build quota 500/month, timeout 20 min, asset limits выше. Cloudflare account/plan/Git App permissions не подключались и не проверены; платные продукты не включались. [Static asset pricing](https://developers.cloudflare.com/pages/functions/pricing/), [limits](https://developers.cloudflare.com/pages/platform/limits/).

AWS модель прежней нагрузки без CloudFront/S3 frontend traffic: 10 000 REST/Lambda requests, 512 MB × 0.5 s, 50 000 WRU, 100 000 RRU, 0.01 GB DynamoDB, 0.1 GB logs/7-day retention, 3 MAU. По ранее полученным ставкам eu-north-1: **около $0.20/месяц до allowances/credits**, плюс API Internet egress, state storage/requests и налоги. Практический резерв **$1–3/месяц**; это не денежный cap. Отдельный custom domain в расчёт не входит; `pages.dev` не требует покупки домена.

Шесть оставленных пустых/неподключённых hosting resources не дают фиксированного compute charge: bucket пуст, function не обслуживает distribution. До отдельной очистки остаются managed resources и возможность небольших storage/API charges при использовании. Общая Lambda quota 5 и риск взаимного throttling с HW4 не изменяются. Исходные ставки и допущения: [AWS cost verification](aws-verification.md).

## Следующий этап

Backend и frontend уже развёрнуты; AWS CloudFront не требуется. На опубликованном сайте следующий отдельно согласуемый этап — тестовые Cognito users, DynamoDB seed и реальный E2E Customer/Owner/Guest. Cleanup шести hosting resources требует отдельного разрешения. Если production origin изменится, обновить `frontend_origin` и отдельно рассмотреть свежий plan; не применять исторические планы повторно.
