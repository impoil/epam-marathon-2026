# Green&Tasty: исправление Terraform drift, 30.10.2026

**Применено после отдельного разрешения пользователя:** [итоговый отчёт](aws-drift-apply.md), один apply **1/1/1**, контрольный remote plan **0/0/0**, 30 no-op. Angular опубликован пользователем в Cloudflare Pages. Ниже сохранены диагностика и план **до** этого разрешения/apply; утверждения «не применён» относятся к предыдущему этапу.

Конфигурация исправлена локально и проверена с настоящим AWS backend. Новый полноценный remote plan: **1 Add / 1 Change / 1 Destroy**, **28 no-op**, **1 replacement**, **0 imports**. План **не применялся**. Два последовательных plan с refresh дали полностью одинаковые resource changes и новый trigger hash.

Однократная замена deployment необходима для перехода от старого hash provider-managed objects к стабильному hash желаемой конфигурации. После отдельно разрешённого применения ожидается **0 Add / 0 Change / 0 Destroy**, но фактический post-apply 0/0/0 **ещё не проверен**. Без разрешённого apply и без ручного изменения state записать новый trigger в существующий deployment невозможно.

## Диагностика и минимальная коррекция

Исходная рабочая копия была чистой: локальный HEAD, origin ветки `feature/hw6-greentasty-aws-deployment` и head Draft PR #2 совпали — `fcea10b7b8ec4fde278a249b13e92154e01d1435`. STS подтвердил действующую сессию `fresh-deploy` и Account **071954286972**. Регион provider/backend/CLI — **eu-north-1**. Повторная авторизация не требовалась.

Изменён только **`infra/hw6-greentasty/api.tf`** и документация:

1. Общий `local.gateway_responses` описывает желаемые DEFAULT_4XX/DEFAULT_5XX parameters и templates. Ресурсы получают значения из этого же local; прежние `for_each` keys/addresses сохранены.
2. Явно задан фактический стандартный template AWS: `response_templates = { "application/json" = "{\"message\":$context.error.messageString}" }`. Значение `$context.error.messageString` уже содержит JSON quotes: дополнительное экранирование/кавычки вокруг подстановки не добавляются. Оба gateway responses теперь **no-op**; templates и parameters побайтно совпадают с прочитанными AWS значениями.
3. `triggers.responses` теперь равен `sha256(jsonencode(local.gateway_responses))`. В hash больше не входят resource IDs, служебные/вычисляемые provider fields, status_code или значения, добавляемые refresh.
4. `triggers.definition = sha256(jsonencode(local.api_definition))` сохранён: изменения routes, authorization, integrations и OPTIONS продолжают создавать новый deployment. Значение definition hash в текущем плане не изменилось.
5. Добавлен явный `depends_on = [aws_api_gateway_gateway_response.cors]`: после удаления implicit reference из hash порядок gateway response updates → deployment сохранён. Один только depends_on не инициирует redeploy; изменения desired parameters/templates отслеживает responses hash. `create_before_destroy=true` сохранён.

CORS остаётся точным **https://greentasty-epam-impoil.pages.dev**. Изменение origin меняет одновременно OPTIONS/API definition и gateway responses. Прежние методы/headers, `Cache-Control: no-store`, отсутствие Allow-Credentials, Cognito authorizer и IAM не изменены. `reserved_concurrency=null` сохранён через `demo.tfvars.example`.

Не использовались `ignore_changes`, `-target`, `-refresh=false`, `-lock=false`, import/moved/state rm/state push/force-unlock или редактирование Terraform state. Новых компонентов или переписывания backend нет.

## Точный состав нового плана

| Ресурс | Действие | Причина |
| --- | --- | --- |
| `aws_api_gateway_deployment.app` | **create + delete**, одна replacement | Изменился только `triggers.responses`; provider не обновляет deployment snapshot на месте |
| `aws_api_gateway_stage.app` | **update in-place** | Переключить `deployment_id` с текущего `abmqyo` на новый snapshot |
| Остальные **28** ресурсов | **no-op** | Включая REST API, Lambda/permission, gateway responses, limits, IAM, Cognito, DynamoDB и все 6 retained hosting resources |

Hash `definition` до/после: `4f3e41b8bc845a389159144f05eb398312ea0c98fb56430bed267d33fb720348`.

Hash `responses`: `dde62071e050aa8a15992690950baee9aa0694c879f331a0380c4957a1b33d8f` → **`877ea07daefa5391a8f625a96f2b9d2003c25a694e4e9ee249ebd85145e36a8d`**. Новое значение полностью известно на plan и одинаково в повторной проверке.

SHA256 первого нового binary plan: **`f6ee295ed5808801b9ef913cbaf4e265ef2cd45c6ef2e69070117c791fa3f255`**. Binary plan/JSON и raw state остаются в ignored `infra/hw6-greentasty/build/drift-correction/` и не публикуются. Свежесть этого сохранённого плана нужно проверить перед любым будущим отдельно разрешённым применением.

## Влияние будущего apply на работающий API

План пересоздаёт **deployment snapshot**, а не REST API. Останутся те же API ID **`7srgpvk8of`**, stage **`demo`** и backend URL **https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo**. Lambda, её ZIP, Cognito pool/client, DynamoDB и данные не меняются.

`create_before_destroy` обеспечивает создание нового snapshot до удаления старого; существующий stage переключается на новый deployment. Маршруты, integration и ответы при этом сохраняют текущую конфигурацию. План не предусматривает удаления API/stage или намеренного перерыва обслуживания. Изменение всё равно проходит через AWS control plane и propagation; фактическую доступность во время перехода ещё не проверяли, нулевую длительность возможного сбоя заранее не гарантируем.

**Остановлено перед apply**, как запросил пользователь. AWS ресурсы и state в этой работе не изменены; временные S3 lockfiles штатно созданы/освобождены только для plan. После отдельного разрешения потребуется свежий просмотр плана, применение согласованной коррекции и обычный контрольный plan с refresh. Только результат реального post-apply плана позволит подтвердить 0/0/0.

## Проверки и сохранность

| Проверка | Результат |
| --- | --- |
| `terraform fmt -check` | Passed |
| `terraform init -reconfigure`, существующий backend | Passed, без migration |
| `terraform validate` | Passed, AWS provider 6.70.0 |
| Первый remote plan, реальные credentials, refresh, S3 locking | **1/1/1**, 28 no-op |
| Второй remote plan, те же параметры и refresh | **1/1/1**, полные resource changes идентичны |
| Локальные plan-only contract checks | **4 passed / 0 failed**, без providers/AWS resources |
| Metadata hw6 remote state до/после | VersionId, ETag, LastModified и размер совпали; state не переписан |
| HW4 и HelloWorld | Все 7 нормализованных снимков до/после совпали |
| API stage и gateway responses до/после | Совпали, текущий deployment всё ещё `abmqyo` |
| S3 lockfile после plan | Отсутствует |
| Новые application build/test runs | Не требуются: backend/frontend source и deployment package не менялись |

Локальный contract fixture получен из production locals, с фактическими ID из remote plan, без provider/backend/resources. Четыре plan-only проверки: исходные hashes совпадают с настоящим remote plan; повтор тех же входов сохраняет hashes; смена origin меняет оба hashes; добавление route меняет definition hash. Это проверка формулы и будущих triggers, **не** подмена настоящего backend и **не** свидетельство фактического post-apply 0/0/0. Fixture/results локальны в ignored build.

Backend: bucket `epam-hw4-state-freshdeploy-20261027`, key **`hw6-greentasty/terraform.tfstate`**, AES256, `use_lockfile=true`, 30 managed resources. Отдельный HW4 key **`homework4/group-preorder/terraform.tfstate`** не менялся. Снимки HW4 state, Lambda `epam-hw4-group-preorder`, Lambda `EPAM-Marathon-HelloWorld`, HTTP API `xv3cbcohed`, routes/integrations/stages совпали. `infra/homework4` не редактировался.

Воспроизводимая команда из корня репозитория (не разрешение на apply):

```powershell
terraform '-chdir=infra/hw6-greentasty' init -input=false -reconfigure '-backend-config=backend.hcl'
terraform '-chdir=infra/hw6-greentasty' fmt -check
terraform '-chdir=infra/hw6-greentasty' validate
terraform '-chdir=infra/hw6-greentasty' plan -input=false '-lock-timeout=60s' '-var-file=demo.tfvars.example' '-out=build/drift-correction/correction.tfplan'
```

Остались прежние предупреждения provider о DynamoDB `hash_key` deprecated; schema migration в задачу не включалась. Новых runtime/AWS provisioning проверок или действий не было. Cognito users, DynamoDB seed, cleanup, Angular publish и merge не выполнялись. Commit использует **`[CF-Pages-Skip]`**, PR #2 остаётся Draft.

Основание: [AWS gateway response templates](https://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-gatewayResponse-definition.html), [deployment snapshot, triggers и ordering — provider 6.70.0](https://github.com/hashicorp/terraform-provider-aws/blob/v6.70.0/website/docs/r/api_gateway_deployment.html.markdown). История первоначального успешного создания ресурсов: [backend deployment report](aws-backend-deployment.md).
