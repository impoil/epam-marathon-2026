# Homework #8 — Submitted with documented limitations

Реалізовано **L1 Review**, **L2 Submit / aggregation** та **L3 Remove / E2E** для Group Pre-Order. Технології: .NET 10, C#, Playwright, Reqnroll/Gherkin, TUnit, Screenplay; GitHub Actions Quality.

Виявлено **36 сценаріїв: 34 acceptance (L1 10, L2 13, L3 11) та 2 diagnostics**. Діагностичний continuation не підтверджує один безперервний fresh E2E.

Перевірені build/format/lint/discovery; локально **35 C# verification + 6 Python checks Passed**. [AQA Quality для 68863da — Passed](https://github.com/impoil/epam-marathon-2026/actions/runs/41309402374), включно з 34 offline cases та report contracts. Це підтвердження offline-частини, не повного application E2E.

Історичні результати не приховано: full run **19 Passed / 11 Failed / 2 Skipped**; погоджені чотири UI checks **2 Passed / 2 Failed**; успішний continuation — кілька фаз тієї самої reservation, не fresh run. [Деталі й первинні outcomes](RESULTS_UA.md), [аудит коду/cleanup/CI](RELIABILITY_AUDIT_UA.md), [JSON reports](results/).

Обмеження: справжня Waiter identity не підтверджена; два стани не створюються public API; немає reservation cleanup API/TTL. **Business writes і manual E2E заблоковано**. Нові тестові дані або AQA infrastructure для здачі не створюються. [Код, конфігурація та інструкції](../../aqa/restaurant-aqa-framework/README.md). Наявна пропозиція ізольованого контуру залишається лише аналізом.
