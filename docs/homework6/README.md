# Домашнє завдання №6 — Frontend Angular

Реалізовано рівні 1–3 та бонус Remove Guest. Основа: `origin/feature/homework-6-frontend-starter`, коміт `86f1781`. Starter-файли не імпортувалися повторно. Backend, інфраструктура та ДЗ №4/№5 не змінювалися.

## Запуск

У Codex Cloud спочатку виконайте `source /workspace/.cloud-setup/env.sh`. Залежності вже встановлені; повторне встановлення не потрібне. На новій машині використовуйте `npm ci` з наявним lockfile.

Із `frontend/restaurant-frontend-app`:

```bash
npm start -- --host 127.0.0.1 --port 4200
```

Відкрийте `http://localhost:4200/reservations/demo-reservation/group-order?demo`.
Демо працює лише в режимі development; перезавантаження скидає стан mock group. Дата й час форматуються в часовому поясі браузера.

## Реалізація

- **Рівень 1.** Окрема захищена сторінка Owner Review, standalone-компоненти, signals/computed, семантичні main/h1/ul/li/article, SCSS із кольорами наявної системи. Шапка бронювання, My Pre-Order, картки гостей, страви/кількість/вартість/Subtotal, текстові статуси, consolidated pre-order та Grand Total. Настільний екран і 375 px.
- **Рівень 2.** Invite-token → invite → sessions, Owner session ID та group status. Власна картка визначається за ID сесії. `consolidatePreOrder()` враховує виключно submitted sessions: оригінальний сценарій перевіряє **$540, а не $760**. Попередження про непідтверджені кошики, loading/empty/error/404/retry, Submit через наявний app-modal, блокування повторного запиту, сповіщення про успіх і режим лише для читання після Submitted/Closed. Помилку API показує наявний interceptor, повторного сповіщення немає.
- **Рівень 3.** Типізований HttpClient-сервіс відповідає маршрутам .NET із ДЗ №5. Cognito **ID token** передається на Owner endpoints, зокрема `/guest/groups/`; анонімні invites та guest-session payload/Authorization зберігаються. Один таймер на 15 секунд, ручний Refresh, Updated at, тихі помилки через SKIP_ERROR_TOAST, збереження останнього повного знімка даних. Спільне блокування запобігає перекриттю циклів refresh і станам гонитви між refresh та Submit/DELETE. Таймер і незавершені підписки припиняються під час знищення компонента; синхронізація зупиняється за Submitted/Closed.
- **Remove Guest.** Посилання лише у гостей, підтвердження з іменем та aria-label, DELETE через той самий HTTP-сервіс (mock у демо, .NET для реальних ID), оновлення учасників і суми з відповіді. Owner захищений і в UI/обробнику, і в mock/.NET. Повторний DELETE блокується; після submit видалення недоступне. Діалоги мають утримання фокуса, Escape і відновлення фокуса.

У режимі development demo-токен блокується перед зверненням до недемонстраційного API. Production використовує порожні homework providers; demo login і mock backend виключені з графа збірки. Перевірка збірки не допускає demo issuer/email/invite-token і заповнювачів. Локальна браузерна перевірка production підтвердила: `?demo` не створює auth/user, а захищений маршрут переспрямовує на `/login`.

Версія застосунку — 1.2.0 відповідно до домовленості в CLAUDE.md. У package-lock.json змінено лише два рядки версії кореневого пакета; залежності та контрольні суми integrity не змінювалися.

## Перевірки

Виконано 29 жовтня 2026, Node 24.16.0, Chrome Headless 154:

| Перевірка | Результат |
| --- | --- |
| TypeScript app / spec, --noEmit | Passed |
| Production build, BACKEND_URL=https://example.com | Passed |
| Перевірка production-збірки | Passed, маркери demo/placeholder відсутні |
| test:hw | **78 passed, 0 failed** |
| test:starter | **58 passed, 0 failed** |
| Повний набір тестів frontend | **282 passed, 47 failed** |
| Порівняння з baseline 221 passed / 47 failed | Ті самі 47 імен, **0 нових збоїв** |
| ESLint для нових компонентів/сервісу | 0 errors, 0 warnings |
| Playwright: настільний екран / 375 px / сценарії | Passed |
| Playwright: production ?demo | Passed |

Початковий пакет production-збірки: **647.89 kB**, обсяг передавання **133.98 kB**. Чанк Owner Review із відкладеним завантаженням: **23.30 kB**, обсяг передавання **5.95 kB**. Збереглися наявні попередження про загальний бюджет розміру збірки/стилів і CommonJS; пороги не змінювалися.

```bash
npm exec -- tsc -p tsconfig.app.json --noEmit
npm exec -- tsc -p tsconfig.spec.json --noEmit
BACKEND_URL=https://example.com npm run build
npm run test:hw -- --browsers=ChromeHeadless
npm run test:starter -- --browsers=ChromeHeadless
npm test -- --watch=false --browsers=ChromeHeadless
```

У цьому середовищі CHROME_BIN уже налаштовано на Chromium із потрібними прапорцями для контейнера. Серверу та Karma потрібен дозвіл на локальні порти.

Оригінальні тести practice, level-2, level-3 та level-3-bonus активовані в src/app і побайтово збігаються з homework-tests. Застарілий page spec level-1 не активований, картку та допоміжні функції збережено. Додано окремі тести станів гонитви, повторних спроб, скасування запитів і зупинки таймера. Starter-перевірки авторизації уточнено відповідно до контракту .NET: `/guest/groups/` отримує Owner token, а гостьовий token зберігається для `/guest/sessions/`; додано перевірки заборони demo → real API.

Повний список вихідних збоїв: [baseline-failures.txt](baseline-failures.txt). Це попередні тести FileReader/zone.js, відсутні providers/required inputs і застарілий заголовок застосунку. Їх не вимкнено й не послаблено. Докладніше: [verification.md](verification.md).

## Візуальна перевірка

Перевірено справжнім Chromium через Playwright: **1440×1000** та **375×812**, часовий пояс Europe/Kyiv. Немає горизонтального переповнення, зображень, що не завантажилися, чи необроблених JS-помилок. Перевірено Remove/Cancel/Escape, повернення фокуса, зміну суми **$50.50 → $26.50** після видалення submitted guest, Submit, pending/read-only, помилки завантаження/Submit/Remove/refresh і повторні спроби. Демо не надсилало запитів до розгорнутого API.

Помилки для візуальних сценаріїв інжектуються як failed Observable через Angular development debug API. Це перевірка UI, а не E2E зі справжнім .NET.

| Знімок екрана | Стан |
| --- | --- |
| [Настільний екран](screenshots/01-owner-review-desktop.png) | Owner Review |
| [Мобільний екран 375 px](screenshots/02-owner-review-mobile-375.png) | Owner Review |
| [Remove на мобільному екрані](screenshots/03-remove-guest-mobile-375.png) / [настільному екрані](screenshots/04-remove-guest-desktop.png) | Підтвердження видалення |
| [Гостя видалено](screenshots/05-guest-removed.png) | Учасників і Grand Total оновлено |
| [Помилка Refresh](screenshots/06-refresh-error.png) | Останні дані збережено |
| [Підтвердження Submit](screenshots/07-submit-confirmation.png) | Підтвердження надсилання |
| [Помилка Submit](screenshots/08-submit-error.png) | Помилка й можливість повторної спроби |
| [Sent на настільному екрані](screenshots/09-sent-to-restaurant.png) / [мобільному екрані](screenshots/10-sent-to-restaurant-mobile-375.png) | Лише читання |
| [Помилка завантаження на мобільному екрані](screenshots/11-load-error-mobile-375.png) | Try again |

Для повторення з кореня репозиторію (Playwright і Chromium уже встановлені в Cloud; не додаються до залежностей застосунку):

```bash
node docs/homework6/browser-check.cjs
node docs/homework6/production-browser-check.cjs
```

Першій команді потрібен запущений dev server. Друга сама запускає локальний статичний сервер для вже зібраної production-збірки. Можна задати HW6_FRONTEND_URL та HW6_CHROMIUM.

## Готовність до розгортання в AWS

Frontend готовий до збірки з реальним BACKEND_URL і використання наявних Cognito/.NET endpoints. AWS-ресурси не створювалися, розгортання та merge не виконувалися.

**Реальний E2E залишається окремим етапом після розгортання backend:** увійти як реальний Owner, створити бронювання та власний order, відкрити invite в іншому браузері, підтвердити гостьовий кошик, перевірити polling/DELETE/Submit і підсумковий restaurant order. Потрібні розгорнуті endpoints ДЗ №5 та реальні облікові записи Cognito. `https://example.com` використано лише для перевірки збірки. Перед реальною інтеграцією демо необхідно завершити через Sign out.

Код і перевірки підготовлено за допомогою Codex; результат перевірено оригінальними тестами, додатковими регресійними тестами та локальним браузером.
