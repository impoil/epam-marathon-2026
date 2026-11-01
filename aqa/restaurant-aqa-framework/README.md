# ДЗ №8 — Напрям 1, Group Pre-Order

**Підхід: BDD + Screenplay; .NET 10, Reqnroll 3.3.4, TUnit 1.72.10, Playwright 1.63.0.** Інтегровано наданий EPAM AQA Framework у спільний Green&Tasty repository. Production-код та infrastructure не змінено.

[Фактичні результати](../../docs/homework8/RESULTS_UA.md) · [покриття](../../docs/homework8/COVERAGE_UA.md) · [дані](../../docs/homework8/DATA_LIFECYCLE_UA.md) · [аудит публікації](../../docs/homework8/PUBLIC_READINESS_UA.md).

## Налаштування й запуск

Перевірте `dotnet --list-sdks`: runtime 8 не є SDK 10. Наявний локальний SDK можна передати через `-Dotnet`, глобальна інсталяція не потрібна. Тести використовують динамічні майбутні timeslots, власні об’єкти й три надані identity credentials. Waiter має бути справжнім Waiter у `/me`; тести не створюють Cognito users/groups.

```powershell
cd aqa/restaurant-aqa-framework
Copy-Item .env.example .env
# Заповніть власні URL, table ID та credentials. .env ніколи не комітиться.
dotnet restore --locked-mode
dotnet build
# Або вже встановлений Chrome: $env:PLAYWRIGHT_CHANNEL='chrome'
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

Для mutating прогону потрібні **обидва** дозволи `AQA_ALLOW_WRITES=true` і `AQA_RETAIN_TEST_DATA=true`. У мінімальному deployment немає reservation cleanup API. Власні Guests відкритої group видаляються, решта записів реєструється в lifecycle manifest. Не запускайте весь набір багаторазово без перегляду журналу. Це документований fallback homework-aqa.md, а не повне прибирання.

```powershell
dotnet run --no-build -- --report-trx --treenode-filter '/*/*/*/*[Category=hw8]'
dotnet run --no-build -- --report-trx --treenode-filter '/*/*/*/*[Category=l1]'
dotnet run --no-build -- --report-trx --treenode-filter '/*/*/*/*[Category=l2]'
dotnet run --no-build -- --report-trx --treenode-filter '/*/*/*/*[Category=l3]'
dotnet run --no-build -- --report-trx --treenode-filter '/*/*/*/*[Category=e2e]'
```

Так само доступні `ui`, `api`, `smoke`. `readonly` — окремий diagnostic зі шляхом `AQA_READONLY_MANIFEST`; не створює й не змінює business data та не входить до `hw8`. Не запускайте executable без фільтра: diagnostic потребує manifest.

Windows DPAPI: `scripts/run-local.ps1 -CredentialFile <protected-clixml> -Dotnet <existing-dotnet.exe> -Filter '/*/*/*/*[Category=hw8]'`. CLIXML: масив `{ Role: owner|other|waiter, Credential: PSCredential }`. Файл не копіюється в repository, паролі передаються тільки environment процесу й прибираються в `finally`. Альтернатива — CI Secrets або process environment.

## Архітектура

| Шар | Власна реалізація ДЗ №8 |
|---|---|
| `Features/Group*.feature` | Три рівні, role outlines, позитивні й негативні правила |
| `StepDefinitions/GroupPreOrderStepDefinitions.cs` | Тонкі кроки: Actor/tasks/questions/assertions |
| `Screenplay/Tasks/` | Prepare/Probe/UseGroupPreOrderUi/ResumeOwnGroupReadOnly |
| `Screenplay/Questions/TheGroupChecks.cs` | Фактичні checks; порожній набір не може пройти |
| `Ui/GroupPreOrderPage.cs` | Review, confirm/remove dialogs, Owner cart, guest invitation/cart |
| `Api/GroupPreOrderApi.cs` | HTTP без mutation retries; diagnostics без secret values |
| `Api/Seeding/GroupFixture.cs` | Run IDs, власні об’єкти, очікувані quantities/flags, cleanup |
| `Hooks/` | Ізольовані browser contexts, Waiter preflight, lifecycle, screenshots/traces |

Збережено Actor, abilities, memory, stages, оригінальні API/UI clients, tasks/questions та tool manifest. Організаторські features перенесено до `ReferenceFeatures/*.feature.example`: зразки не запускають signup/seed/admin сценарії на поточному deployment. Global pre/post-run sweep видалено, бо він міг зачепити дані інших запусків. Missing/pending steps — `Error`, аналізатори — warnings as errors. `packages.lock.json` фіксує transitive dependencies.

## Обраний контракт

- Cognito Owner endpoints: anonymous/guest bearer → 401, other Customer/actual Waiter → 403. Відмова не змінює snapshot/group status.
- Missing group/session → 404. Participant іншої незалежної group → 400. Видалити Owner → 400. Повторний DELETE → 404.
- Повторний final submit → 400/`GroupReservationSubmitted`. Edit/remove після фіналу → 400.
- API review total включає **всі** carts; UI **consolidated Grand Total** — тільки submitted carts. Draft dishes видимі у participant card і не надсилаються ресторану. Відмінність від суворого трактування L1 описана в coverage.
- Нова group має порожню Owner session. Zero participants недосяжні через public API; `participants: []/total: 0` — explicit blocked. Останній **Guest** видаляється, Owner зберігається. Submitted empty cart також недосяжна: public submit відхиляє порожню cart. Потрібна ізольована fixture API, без DynamoDB writes.
- Фактичний guest join: 200, `SessionId`/`SessionToken`; invite metadata — PascalCase. Order `status=3` — `SubmittedByCustomer`; group status — рядок `Submitted`. Це контракт наявного backend, а не вимкнення assertions.

## Звіти й CI

TRX, TUnit HTML, Reqnroll HTML/messages, screenshots, traces та `AQA-HW8-*-lifecycle.json`: `TestResults/` або `ARTIFACTS_DIR`. `python scripts/summarize.py <directory> --require-results` створює summary JSON/HTML з реальних TRX і причинами Failed/Skipped; нуль результатів — помилка. [TUnit HTML](https://tunit.dev/docs/guides/html-report/) і [Reqnroll formatters](https://docs.reqnroll.net/latest/reporting/reqnroll-formatters.html) — офіційні джерела.

Trace viewer: `pwsh bin/Debug/net10.0/playwright.ps1 show-trace <trace.zip>`. Traces редагуються до attachment: passwords, JWT, invitation та guest bearers замінюються. CI додатково запускає `sanitize-artifacts.py`; ручний перегляд screenshots усе ще потрібен. Артефакти мають retention 7 днів і автоматично не комітяться.

`aqa-quality.yml`: PR та feature push — locked restore, format, build, pinned Gherkin lint, discovery, tracked secret/generated file check. Без secrets, браузерів і business writes.

`aqa-e2e.yml`: **тільки workflow_dispatch**, фіксований scope, checkbox retention, environment `aqa-production`, concurrency 1, без auto-retry. Немає schedule/`pull_request_target`/PR trigger. `continue-on-error` лише забезпечує artifacts; фінальний крок повертає Failed. Variables: `AQA_UI_URL`, `AQA_API_URL`, `AQA_TESTTABLE_ID`; secrets: `AQA_{OWNER,OTHER,WAITER}_{USERNAME,PASSWORD}`. Environment required reviewers/allowed refs налаштовує власник окремо.

Локальний DNS 01.11.2026 повертав 10.0.0.43 замість Cloudflare. `AQA_UI_DNS_ADDRESS` дозволяє **свіжу перевірену** public DNS answer лише в процесі Chromium. Hostname/SNI/origin/TLS validation збережено. IP не переноситься в CI; TLS bypass у фінальному коді відсутній.
