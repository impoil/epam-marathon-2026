# Додатковий аудит надійності — 01.11.2026

**Business writes зупинено. Нових reservation/group/order/users не створено; наявні записи не редагувалися й не видалялися.** AWS/Cognito/Cloudflare, GitHub settings/visibility та merge не змінювалися. Перевірено вихідні файли, попередні TRX/JSON, GitHub metadata і локальні synthetic regressions. Історичні [business результати](RESULTS_UA.md) та їхні JSON залишено без зміни outcomes.

## Два останні основні UI failures

| Failure у `ui-approved-four` | Причина в тесті | Остаточне рішення без production-змін |
|---|---|---|
| Submit: business assertions Passed, AfterScenario Failed | Раннє читання group status повертало Open; teardown намагався видалити Guest уже Submitted group, отримував 400 | Підтверджений Submitted зберігається як незворотний локальний факт. Перед final submit ставиться `SubmissionAttempted`. За stale Open після спроби або невідомого результату cleanup **утримує** записи, не робить DELETE. Невідомий status — fail closed |
| Fresh journey: GET order → 404 | Click закінчився до async створення order; перший 404 повертався з poll як завершення | Response observer встановлюється **до** UI click; очікується POST /orders=201, потім bounded read polling дозволяє тимчасовий 404. Непідтверджений стан після deadline тепер кидає TimeoutException, а не повертає довільний 200 |

Наступні continuation failures також збережені: redirect відбувався раніше за async `/me` (повна navigation могла перервати profile load), а хибний Count=0 очікував зникнення Owner order card. Тест очікує Customer navigation link `Reservations` до переходу; Owner submit перевіряється справжнім confirmation heading та persisted `Owner.isSubmitted`. За backend-контрактом особистий submit Owner **не** переводить Order із Created до final group submit. Production frontend/backend для цих synchronization fixes не потрібні.

Раніше успішний continuation залишається доказом автоматичного проходження **кількох фаз того самого scenario**, не одного fresh run. Новий код **не перевірено повтором mutating E2E** через поточну заборону; відсутність fresh green run залишається відкритою.

## Assertions та lifecycle

| Виявлена прогалина | Зміна |
|---|---|
| Перевірка length + All могла прийняти дубль одного dishId замість іншого за однакової ціни/quantity | Окремі перевірки unique IDs, повної множини IDs, exact quantity та nonempty submitted result; перевіряється також HTTP submit response |
| Правильна загальна сума могла приховати компенсовані зміни unit prices або неправильну назву | Кожна final dish звіряється з submitted cart, включно з name/price; catalog/fixture inputs перевіряються до submit |
| UI Remove міг порівнювати дві однаково неправильні API/UI суми | Перевірка after snapshot дорівнює **before без target**: усі surviving carts/flags/items збережені, total зменшено рівно на target subtotal |
| Session ID Owner міг не потрапити до перевірки expected carts | Перевіряється повна множина session IDs fixture; read-only diagnostic не видається за незалежну fixture verification |
| 400 після edit/remove недостатньо для доведення відсутності side effects | Додано повторне читання final order: той самий orderId/status та незмінні items |
| Waiter role не доводить призначення reservation | Позитивний waiter scenario звіряє `/me.id` з `reservation.waiterId` до final submit |
| Cleanup зупинявся на першій невдалій Guest action | Помилки збираються; решта **власних** Guest IDs обробляється, journal зберігає failures; cleanup failure не стає Passed |
| Невідомий fixture state або UI action міг потрапити в default flow | Невідомі значення відхиляються до setup/action |
| Retired framework clients повторювали PUT/DELETE | Mutation retries прибрано; тільки legacy GET може повторювати transient 500. Retired samples не активовано |

На час suspension teardown **взагалі не робить business writes**, включно з cleanup/cancellation. Це свідоме дотримання поточної заборони, а не заява про прибрані reservations. Існуючі 34 записи та попередні Submitted snapshots збережені.

## Класифікація 36 BDD scenarios

| Класифікація | Кількість | Прийомка |
|---|---:|---|
| L1 | 10 | Review, totals, empty/draft, no/missing group, access |
| L2 | 13 | Submit/aggregation, prerequisites, access, actual assigned Waiter |
| L3 | 11 | Remove, ownership, cross-group/absent/repeated delete, один fresh E2E |
| Diagnostics | 2 | Read-only і continuation; поза `hw8`, рівнями та `e2e` |

[Повна inventory усіх 36 випадків](results/scenario-inventory.json) формується `audit-scenarios.py` без виконання hooks і звіряється з TUnit discovery. Додано `diagnostic/nonacceptance`; main `hw8` містить **34**, не 36 acceptance cases. Є один fresh E2E у L3, але discovery не підтверджує його проходження.

`summarize.py` зберігає фактичні runner counts, окремо показує acceptance/diagnostics та `fresh_acceptance_e2e_passed`. `--require-fresh-e2e` відмовляє для continuation Passed, навіть якщо тому помилково додати `e2e`. Fresh Owner flow відмовляє за існуючого order або вже submitted Owner cart; повторне використання дозволене лише явно позначеній continuation fixture, яка зараз також заблокована.

## Фактичні GitHub controls

Read-only GitHub REST API повернув HTTP 200: repository **private**, default branch **main**, **0 Environments**, **0 repository Secrets**. Actions enabled, `allowed_actions=all`, SHA pinning не обов’язковий; default token permissions — read, PR review approval через workflow token вимкнено. [Знімок перевірки](results/github-security-audit.json) містить тільки metadata, жодних token/secret values.

Окрема перевірка `.github/workflows/aqa-e2e.yml` на default branch `main` повернула **404**: workflow там відсутній. Отже, попередня конфігурація в PR була ризиком майбутнього ввімкнення, а не доказом доступного зараз запуску з default branch. Dispatch не виконувався.

Попередній `environment: aqa-production` не був перевіреним approval gate. Посилання на нестворене environment може створити його без необхідних protection rules; checkbox retention і concurrency=1 не обмежують накопичення між запусками. Старий budget 35 був per-process, не загальним бюджетом; відсутній/нечисловий value міг обходити client guard.

Тепер `aqa-e2e.yml` — fail-closed manual job: **без inputs, checkout, environment, Secrets, build, test process чи browser**; повертає Failed із поясненням suspension. 35 неможливо обрати, flags не ввімкнуть source stop. Client guard блокує бізнесові POST/PUT/PATCH/DELETE до transport; browser guard застосовано до Owner/Guest contexts, service workers заборонено. GET/HEAD і точний signin endpoint залишені для read-only діагностики. Legacy API clients також проходять guard. Невідомий/відсутній budget відхиляється; дозволені для майбутнього погодженого коду лише явні 0..4.

Це захист **поточної версії коду**, не спосіб забрати права repo admin: користувач із правом зміни workflow може повернути старий код; інші refs можуть містити старі workflows. Зараз repository/environment Secrets відсутні, їх не додавали. До re-enable потрібні перевірені branch/ref restrictions, reviewers/prevent-self-review/no-admin-bypass там, де тариф підтримує ці функції, credentials тільки ізольованого середовища та новий review. Для private repository доступність reviewers/Secrets залежить від GitHub plan; зміна visibility або тарифу заради обходу gate не пропонується. [Офіційні обмеження GitHub](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments).

## Безпечна верифікація цього аудиту

- Окремий verification project компілює **linked production test sources**, не live BDD assembly: **35 Passed / 0 Failed / 0 Skipped, 4.009s**, [окремий результат](results/reliability-offline.json). 34 pure/client cases та один synthetic browser sink, який перехоплює localhost requests до мережі. Немає Green&Tasty URL/credentials.
- Шість Python contract tests — **6 Passed / 0 Failed**: diagnostics не задовольняють fresh gate, Failed E2E не стає Passed, нуль результатів — помилка; окремий test не дозволяє непомітно повернути Secrets/test steps у suspended manual workflow. [Результат](results/reporting-contract.json).
- Основний framework: build/format/lint/discovery та inventory, **без запуску business scenarios**. CI Quality додатково запускає тільки offline unit/report checks; browser case виконується локально.

На **копії** історичного continuation TRX новий reporter зберіг `Passed=1`, але показав acceptance Passed=0 та `fresh_acceptance_e2e_passed=false`; fresh gate повернув nonzero. [Окремий classified report](results/reliability-continuation-classification.json). Оригінальний TRX та попередній business JSON не переписувалися.
- Перший локальний verification bootstrap помилково використовував ProjectReference на BDD assembly: 32 local cases Passed, 36 BDD cases Failed **до** environment initialization через missing UI_URL. HTTP до AWS не виконувався. Посилання прибрано, дані цієї невдалої локальної перевірки [збережені окремо](results/reliability-bootstrap-local.json). Це не новий live full run і не application failures.

Поточні два unreachable fixtures та реальна Waiter identity залишаються blockers. Рекомендований єдиний наступний infrastructure крок описано [окремо](AQA_ENVIRONMENT_PROPOSAL_UA.md); тут він лише спроєктований, без створення ресурсів.
