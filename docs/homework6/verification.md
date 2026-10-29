# Homework #6 verification

Основа: feature/homework-6-frontend-starter, `86f1781bbf302e63bfaae974f7c17ad0e86d5a5d`.
Перед разработкой проверены commit, homework-frontend.md и homework-tests/.
GitHub branch/commit и локальные объекты проверены по SHA. Архив повторно не импортировался.

## Итоговые проверки

| Проверка | Результат |
| --- | --- |
| tsc -p tsconfig.app.json --noEmit | Passed |
| tsc -p tsconfig.spec.json --noEmit | Passed |
| BACKEND_URL=https://example.com npm run build | Passed |
| postbuild / check-bundle.mjs | Passed |
| npm run test:hw -- --browsers=ChromeHeadless | 78 passed |
| npm run test:starter -- --browsers=ChromeHeadless | 58 passed |
| npm test -- --watch=false --browsers=ChromeHeadless | 282 passed, 47 failed |
| 47 failing names vs saved clean-main baseline | Identical; no additions or omissions |
| Original activated homework specs vs source | All 7 files byte-identical |
| Scoped ESLint new card/page/service and safety spec | 0 errors, 0 warnings |
| Playwright development browser-check.cjs | Passed |
| Playwright production-browser-check.cjs | Passed |
| git diff --check | Passed |

Node 24.16.0, Chrome Headless 154.0.0.0. npm доступен; новых пакетов не установлено.
Production initial: 647.89 kB / 133.98 kB transfer; group-order lazy chunk: 23.30 kB / 5.95 kB transfer.
Существующие budget/CommonJS warnings остаются; пороги не ослаблялись.

## Baseline и регрессии

Сохранённая проверка чистого main `04c7998`: 195 passed, 47 failed.
Starter `86f1781`: 221 passed, 47 failed.
Текущая реализация: **282 passed, 47 failed** — 61 дополнительный passing test относительно starter.

Имена всех 47 падений автоматически извлечены из текущего полного Karma log,
отсортированы и сравнены с `/workspace/.cloud-setup/logs/frontend-failures.txt`
(сохранённый отчёт проверки main в этой среде). Добавленных/исчезнувших имён — 0.
Список для ревью: [baseline-failures.txt](baseline-failures.txt).
Падения baseline не исправлялись и не отключались в рамках этого ДЗ.

Все активированные оригинальные homework specs сохранены без правок:
practice (карточка), level-2 (страница/сервис), level-3 (sync страницы/сервиса),
level-3-bonus (Remove страницы/сервиса). Helpers сохранены.
Новые safety-тесты проверяют loading, последовательность запросов, один таймер при retry,
сохранение полного снимка при ошибке status, остановку на Submitted/Closed,
Owner protection, повторный DELETE/Submit, retry, unsubscribe при destroy.

## Production и визуальная проверка

Production checker просмотрел JS/CSS/HTML всех chunks: нет backend placeholder,
demo issuer, demo email или demo invite-token. Production browser на свежем storage:
`?demo` не создаёт сессию, Owner route перенаправляется на /login. Обращения landing-page
к example.com заменены локальными fixtures; реального API в этой проверке нет.

Development browser проверил 1440×1000 и 375×812, отсутствие overflow и broken images,
диалоги и Escape/focus restoration, Remove (включая ошибку/retry), изменение totals,
Submit pending/error/retry/read-only, silent refresh error/recovery, initial error/retry.
Необработанных ошибок и запросов к deployed API — 0.
UI failure cases используют инъекцию failed Observable, не реальные сетевые сбои .NET.
Скриншоты и воспроизводимые scripts связаны из [README.md](README.md).

## Сохранность репозитория и границы проверки

Backend, infrastructure, ДЗ №4/№5, организаторские homework-tests/ и макеты не изменялись.
Зависимости/lock integrity сохранены. Версия 1.2.0; lockfile меняет только root version в двух местах.
Generated production environment, node_modules, dist, credentials и Terraform state не коммитятся.

AWS не использовался. Доступ к production .NET, Cognito sign-in с реальными аккаунтами,
создание заказов в DynamoDB и доставка в restaurant order не проверялись.
Это отдельный E2E этап после deployment. PR предназначен для ревью; merge не выполняется.
