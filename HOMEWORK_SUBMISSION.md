# EPAM IT Marathon 2026 — посилання для здачі

Дата підготовки: **01.11.2026, Europe/Kyiv**. Репозиторій: [impoil/epam-marathon-2026](https://github.com/impoil/epam-marathon-2026). Нижче наведено наявні матеріали; відсутні результати не відновлювалися й не вигадувалися.

| ДЗ | Готові матеріали та результати | Статус / обмеження |
|---|---|---|
| №1 | **ПОТРІБНЕ ПОСИЛАННЯ** | Підтверджених матеріалів цього ДЗ у доступному репозиторії не знайдено |
| №2 | **ПОТРІБНЕ ПОСИЛАННЯ** | Підтверджених матеріалів цього ДЗ у доступному репозиторії не знайдено |
| №3 | **ПОТРІБНЕ ПОСИЛАННЯ** | Підтверджених матеріалів цього ДЗ у доступному репозиторії не знайдено; спільний Figma-макет не видається за власне виконане ДЗ |
| №4 — AWS / Terraform | [Вихідний код та звіт](https://github.com/impoil/epam-marathon-2026/tree/main/infra/homework4), [публічний JSON endpoint](https://xv3cbcohed.execute-api.eu-north-1.amazonaws.com/group-preorder) | Історичне розгортання й перевірки збережено; 01.11 GET → **200** |
| №5 — Backend | [Опис L1/L2/L3](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework5/README.md), [код](https://github.com/impoil/epam-marathon-2026/tree/main/backend/restaurant-backend-app), [перевірки](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework5/verification.md), [API routes](https://github.com/impoil/epam-marathon-2026/blob/main/backend/restaurant-backend-app/docs/ApiRoutes.md) | Реалізація трьох рівнів; історичні unit results збережено. Розгорнутий backend перевірено в межах №6 |
| №6 — Frontend / Green&Tasty | [Опис](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework6/README.md), [Angular source](https://github.com/impoil/epam-marathon-2026/tree/main/frontend/restaurant-frontend-app), [реальний завершений E2E та докази](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework6/e2e-completed.md), [PR #2](https://github.com/impoil/epam-marathon-2026/pull/2), [демонстрація](https://greentasty-epam-impoil.pages.dev/) | Історичний production E2E завершено; 47 попередніх frontend failures збережено. Поточне обмеження HTTPS-перевірки описано нижче |
| №7 — Manual QA | [Готова книга XLSX](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework7/EPAM_HW7_Manual_QA_US11.xlsx), [завантажити XLSX](https://github.com/impoil/epam-marathon-2026/raw/refs/heads/main/docs/homework7/EPAM_HW7_Manual_QA_US11.xlsx), [українське резюме](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework7/README_UA.md), [докази](https://github.com/impoil/epam-marathon-2026/tree/main/docs/homework7/evidence) | 49 cases: 13 Passed, 3 Failed, 24 Blocked, 9 Not Run; один підтверджений дефект. Власну Google Sheets URL не створено; готовий XLSX доступний через GitHub |
| №8 — Test Automation | [Коротке резюме здачі](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework8/SUBMISSION_UA.md), [код та запуск](https://github.com/impoil/epam-marathon-2026/tree/main/aqa/restaurant-aqa-framework), [Gherkin](https://github.com/impoil/epam-marathon-2026/tree/main/aqa/restaurant-aqa-framework/Features), [результати](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework8/RESULTS_UA.md), [JSON reports](https://github.com/impoil/epam-marathon-2026/tree/main/docs/homework8/results), [PR #3](https://github.com/impoil/epam-marathon-2026/pull/3), [AQA Quality Passed](https://github.com/impoil/epam-marathon-2026/actions/runs/41309402374) | **Submitted with documented limitations**. 34 acceptance + 2 diagnostics; offline перевірки успішні. Один безперервний fresh E2E **не підтверджено** |

## Green&Tasty і поточна доступність

- Frontend: [https://greentasty-epam-impoil.pages.dev/](https://greentasty-epam-impoil.pages.dev/).
- Backend: [GET /locations](https://7srgpvk8of.execute-api.eu-north-1.amazonaws.com/demo/locations) — **200** від 01.11.2026, без авторизації та business writes.
- Останній збережений production E2E: Group Submitted, Order SubmittedByCustomer, **$61**, [звіт №6](https://github.com/impoil/epam-marathon-2026/blob/main/docs/homework6/e2e-completed.md).

Під час фіналізації 01.11 frontend URL має DNS records, але поточні HTTP tools і вбудований браузер відхилили TLS certificate (`CERT_AUTHORITY_INVALID` / self-signed certificate). Тому нове успішне завантаження frontend **не заявляється**. Власнику слід перевірити URL у своєму браузері/іншій мережі; якщо проблема повториться, потрібне окреме усунення HTTPS-проблеми. Перевірку сертифікатів не обходили; Cloudflare/DNS/deployment не змінювали.

Business AQA зупинено source guards та suspended manual workflow. Offline Quality не є production E2E. Немає reservation cleanup API, справжнього test Waiter і двох недосяжних public-API fixtures; історичні Failed/Skipped та continuation залишено у звітах.

## Що потрібно додати власнику

Підтверджені посилання №1–3: **ПОТРІБНЕ ПОСИЛАННЯ**. Якщо організатори вимагають саме Google Sheets для №7, посилання на власну імпортовану книгу: **ПОТРІБНЕ ПОСИЛАННЯ**; GitHub XLSX уже є готовим результатом. Цей документ не заявляє відправлення форми організаторам.
