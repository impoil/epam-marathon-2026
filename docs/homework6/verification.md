# Перевірка домашнього завдання №6

Основа: feature/homework-6-frontend-starter, `86f1781bbf302e63bfaae974f7c17ad0e86d5a5d`.
Перед розробкою перевірено коміт, homework-frontend.md та homework-tests/.
Гілку/коміт GitHub і локальні об'єкти перевірено за SHA. Архів повторно не імпортувався.

## Підсумкові перевірки

| Перевірка | Результат |
| --- | --- |
| tsc -p tsconfig.app.json --noEmit | Passed |
| tsc -p tsconfig.spec.json --noEmit | Passed |
| BACKEND_URL=https://example.com npm run build | Passed |
| postbuild / check-bundle.mjs | Passed |
| npm run test:hw -- --browsers=ChromeHeadless | 78 passed |
| npm run test:starter -- --browsers=ChromeHeadless | 58 passed |
| npm test -- --watch=false --browsers=ChromeHeadless | 282 passed, 47 failed |
| 47 імен тестів зі збоями порівняно зі збереженим baseline чистого main | Ідентичні; без доданих чи пропущених |
| Оригінальні активовані тести ДЗ порівняно з джерелом | Усі 7 файлів побайтово ідентичні |
| ESLint для нових картки/сторінки/сервісу та safety spec | 0 errors, 0 warnings |
| Playwright development browser-check.cjs | Passed |
| Playwright production-browser-check.cjs | Passed |
| git diff --check | Passed |

Node 24.16.0, Chrome Headless 154.0.0.0. npm доступний; нових пакетів не встановлено.
Початковий пакет production-збірки: 647.89 kB / 133.98 kB передавання; чанк group-order із відкладеним завантаженням: 23.30 kB / 5.95 kB передавання.
Наявні попередження про бюджет розміру збірки та CommonJS залишаються; пороги не послаблювалися.

## Baseline і регресії

Збережена перевірка чистого main `04c7998`: 195 passed, 47 failed.
Starter `86f1781`: 221 passed, 47 failed.
Поточна реалізація: **282 passed, 47 failed** — 61 додатковий успішний тест порівняно зі starter.

Імена всіх 47 тестів зі збоями автоматично отримано з поточного повного журналу Karma,
відсортовано й порівняно з `/workspace/.cloud-setup/logs/frontend-failures.txt`
(збережений звіт перевірки main у цьому середовищі). Доданих/відсутніх імен — 0.
Список для рев'ю: [baseline-failures.txt](baseline-failures.txt).
Збої baseline не виправлялися й не вимикалися в межах цього ДЗ.

Усі активовані оригінальні тести ДЗ збережено без правок:
practice (картка), level-2 (сторінка/сервіс), level-3 (синхронізація сторінки/сервісу),
level-3-bonus (Remove сторінки/сервісу). Допоміжні функції збережено.
Нові safety-тести перевіряють loading, послідовність запитів, один таймер під час повторної спроби,
збереження повного знімка даних у разі помилки status, зупинку за Submitted/Closed,
захист Owner, повторний DELETE/Submit, повторну спробу, скасування підписок під час destroy.

## Production і візуальна перевірка

Перевірка production-збірки охопила JS/CSS/HTML усіх чанків: немає backend placeholder,
demo issuer, demo email чи demo invite-token. Браузерна перевірка production з порожнім сховищем:
`?demo` не створює сесію, Owner route переспрямовує на /login. Звернення цільової сторінки
до example.com замінено локальними fixtures; реального API в цій перевірці немає.

Браузерна перевірка development охопила 1440×1000 та 375×812, відсутність переповнення й зображень, що не завантажилися,
діалоги та Escape/відновлення фокуса, Remove (включно з помилкою/повторною спробою), зміну сум,
Submit pending/error/retry/read-only, silent refresh error/recovery, initial error/retry.
Необроблених помилок і запитів до розгорнутого API — 0.
Сценарії помилок UI використовують інжекцію failed Observable, а не реальні мережеві збої .NET.
Посилання на знімки екрана й відтворювані скрипти наведено в [README.md](README.md).

## Збереження репозиторію та межі перевірки

Backend, інфраструктура, ДЗ №4/№5, організаторські homework-tests/ і макети не змінювалися.
Залежності/контрольні суми lockfile збережено. Версія 1.2.0; lockfile змінює лише root version у двох місцях.
Згенероване production environment, node_modules, dist, облікові дані та Terraform state не додаються до коміту.

AWS не використовувався. Доступ до production .NET, Cognito sign-in із реальними обліковими записами,
створення замовлень у DynamoDB і передавання в restaurant order не перевірялися.
Це окремий етап E2E після розгортання. PR призначений для рев'ю; merge не виконується.
