# Homework #6 — Frontend Angular

Реализованы уровни 1–3 и бонус Remove Guest. Основа: `origin/feature/homework-6-frontend-starter`, коммит `86f1781`. Starter-файлы не импортировались повторно. Backend, инфраструктура и ДЗ №4/№5 не изменялись.

## Запуск

В Codex Cloud сначала выполните `source /workspace/.cloud-setup/env.sh`. Зависимости уже установлены; повторная установка не нужна. На новой машине используйте `npm ci` с существующим lockfile.

Из `frontend/restaurant-frontend-app`:

```bash
npm start -- --host 127.0.0.1 --port 4200
```

Откройте `http://localhost:4200/reservations/demo-reservation/group-order?demo`.
Demo работает только в development; перезагрузка сбрасывает mock group. Даты и время форматируются в часовом поясе браузера.

## Реализация

- **Уровень 1.** Отдельная защищённая страница Owner Review, standalone-компоненты, signals/computed, семантические main/h1/ul/li/article, SCSS с цветами существующей системы. Шапка бронирования, My Pre-Order, гостевые карточки, блюда/количество/стоимость/Subtotal, текстовые статусы, consolidated pre-order и Grand Total. Desktop и 375 px.
- **Уровень 2.** Invite-token → invite → sessions, Owner session ID и group status. Собственная карточка определяется по ID сессии. `consolidatePreOrder()` учитывает исключительно submitted sessions: оригинальный сценарий проверяет **$540, а не $760**. Предупреждение о неподтверждённых корзинах, loading/empty/error/404/retry, Submit через существующий app-modal, блокировка повторного запроса, toast успеха и read-only после Submitted/Closed. Ошибку API показывает существующий interceptor, повторного toast нет.
- **Уровень 3.** Типизированный HttpClient-сервис соответствует маршрутам .NET из ДЗ №5. Cognito **ID token** передаётся на Owner endpoints, включая `/guest/groups/`; анонимные invites и guest-session payload/Authorization сохраняются. Один таймер на 15 секунд, ручной Refresh, Updated at, тихие ошибки через SKIP_ERROR_TOAST, сохранение последнего полного снимка. Общая блокировка исключает перекрывающиеся циклы refresh и гонки refresh с Submit/DELETE. Таймер и незавершённые подписки прекращаются при уничтожении компонента; синхронизация останавливается при Submitted/Closed.
- **Remove Guest.** Ссылка только у гостей, подтверждение с именем и aria-label, DELETE через тот же HTTP-сервис (mock в demo, .NET для реальных ID), обновление участников и суммы из ответа. Owner защищён и в UI/обработчике, и в mock/.NET. Повторный DELETE блокируется; после submit удаление недоступно. Диалоги имеют focus trap, Escape и восстановление фокуса.

В development demo-токен блокируется перед обращением к не-демо API. Production использует пустые homework providers; demo login и mock backend исключены из графа сборки. Проверка bundle не допускает demo issuer/email/invite-token и placeholders. Локальная браузерная проверка production подтвердила: `?demo` не создаёт auth/user и защищённый route перенаправляет на `/login`.

Версия приложения — 1.2.0 по соглашению CLAUDE.md. В package-lock.json изменены только две строки версии корневого пакета; зависимости и integrity не менялись.

## Проверки

Выполнены 29 октября 2026, Node 24.16.0, Chrome Headless 154:

| Проверка | Результат |
| --- | --- |
| TypeScript app / spec, --noEmit | Passed |
| Production build, BACKEND_URL=https://example.com | Passed |
| Production bundle checker | Passed, demo/placeholder markers отсутствуют |
| test:hw | **78 passed, 0 failed** |
| test:starter | **58 passed, 0 failed** |
| Полный frontend suite | **282 passed, 47 failed** |
| Сравнение с baseline 221 passed / 47 failed | Те же 47 имён, **0 новых падений** |
| Scoped ESLint новых компонентов/сервиса | 0 errors, 0 warnings |
| Playwright: desktop / 375 px / сценарии | Passed |
| Playwright: production ?demo | Passed |

Initial production bundle: **647.89 kB**, transfer **133.98 kB**. Lazy chunk Owner Review: **23.30 kB**, transfer **5.95 kB**. Остались прежние предупреждения общего size/style budget и CommonJS; пороги не менялись.

```bash
npm exec -- tsc -p tsconfig.app.json --noEmit
npm exec -- tsc -p tsconfig.spec.json --noEmit
BACKEND_URL=https://example.com npm run build
npm run test:hw -- --browsers=ChromeHeadless
npm run test:starter -- --browsers=ChromeHeadless
npm test -- --watch=false --browsers=ChromeHeadless
```

В этой среде CHROME_BIN уже настроен на Chromium с необходимыми container flags. Серверу и Karma требуется разрешение на локальные порты.

Оригинальные тесты practice, level-2, level-3 и level-3-bonus активированы в src/app и побайтно совпадают с homework-tests. Устаревший page spec level-1 не активирован, карточка и helpers сохранены. Добавлены отдельные тесты гонок, retry, отмены запросов и остановки таймера. Starter-проверки авторизации уточнены по контракту .NET: `/guest/groups/` получает Owner token, а гостевой token сохраняется у `/guest/sessions/`; добавлены проверки запрета demo → real API.

Полный список исходных падений: [baseline-failures.txt](baseline-failures.txt). Это прежние тесты FileReader/zone.js, отсутствующие providers/required inputs и устаревший app title. Они не отключены и не ослаблены. Подробнее: [verification.md](verification.md).

## Визуальная проверка

Проверены реальным Chromium через Playwright: **1440×1000** и **375×812**, timezone Europe/Kyiv. Нет горизонтального переполнения, битых изображений и необработанных JS-ошибок. Проверены Remove/Cancel/Escape, возврат фокуса, изменение суммы **$50.50 → $26.50** после удаления submitted guest, Submit, pending/read-only, ошибки загрузки/Submit/Remove/refresh и retry. Demo не отправил запросов к развёрнутому API.

Ошибки для визуальных сценариев инжектируются как failed Observable через Angular development debug API. Это проверка UI, а не E2E с настоящим .NET.

| Скриншот | Состояние |
| --- | --- |
| [Desktop](screenshots/01-owner-review-desktop.png) | Owner Review |
| [Mobile 375 px](screenshots/02-owner-review-mobile-375.png) | Owner Review |
| [Remove mobile](screenshots/03-remove-guest-mobile-375.png) / [desktop](screenshots/04-remove-guest-desktop.png) | Подтверждение удаления |
| [Guest removed](screenshots/05-guest-removed.png) | Участники и Grand Total обновлены |
| [Refresh error](screenshots/06-refresh-error.png) | Последние данные сохранены |
| [Submit confirmation](screenshots/07-submit-confirmation.png) | Подтверждение отправки |
| [Submit error](screenshots/08-submit-error.png) | Ошибка и возможность retry |
| [Sent desktop](screenshots/09-sent-to-restaurant.png) / [mobile](screenshots/10-sent-to-restaurant-mobile-375.png) | Read-only |
| [Load error mobile](screenshots/11-load-error-mobile-375.png) | Try again |

Для повторения из корня репозитория (Playwright и Chromium уже установлены в Cloud; не добавляются в зависимости приложения):

```bash
node docs/homework6/browser-check.cjs
node docs/homework6/production-browser-check.cjs
```

Первой команде нужен работающий dev server. Вторая сама запускает локальный статический сервер для уже собранного production bundle. Можно задать HW6_FRONTEND_URL и HW6_CHROMIUM.

## Готовность к AWS deployment

Frontend готов к сборке с реальным BACKEND_URL и использованию существующих Cognito/.NET endpoints. AWS-ресурсы не создавались, deployment и merge не выполнялись.

**Реальный E2E остаётся отдельным этапом после backend deployment:** войти реальным Owner, создать бронь и собственный order, открыть invite в другом браузере, подтвердить гостевую корзину, проверить polling/DELETE/Submit и итоговый restaurant order. Нужны развернутые endpoints ДЗ №5 и реальные Cognito-аккаунты. `https://example.com` использован только для проверки сборки. Demo перед реальной интеграцией необходимо завершить через Sign out.

Код и проверки подготовлены с помощью Codex; результат проверен оригинальными тестами, дополнительными regression-тестами и локальным браузером.
