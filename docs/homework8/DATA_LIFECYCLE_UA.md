# Ізоляція та життєвий цикл

Кожна mutating fixture має `AQA-HW8-<guid>`, власні API reservation/group/session/order IDs, дату `UtcNow+7..59 days` і конкретний вільний 90-хвилинний timeslot. Guest names мають run-prefix. Catalog/table/identities лише читаються. Reservation 31.10.2026 та order $61 не використовуються; business writes до них не виконувалися.

Fixture запам’ятовується **до** першого write. Teardown знає тільки IDs власних create calls. У відкритій group видаляються тільки власні Guests; 200 і вже відсутній 404 прийнятні, інша помилка робить cleanup неуспішним. Submitted groups не змінюються. Журнал містить create/remove/retention/status без invitation/bearer. Read-only diagnostic teardown не виконує business writes.

Мінімальний HW6 deployment не експонує `DELETE /reservations` і не має hard-delete reservation/group/owner-session/order. Початковий smoke отримав gateway 403; відсутність маршруту підтверджено існуючим `infra/hw6-greentasty/routes.json`. **Записи не скасовані й не мають автоматичного TTL.** Обов’язковий `AQA_RETAIN_TEST_DATA=true` явно погоджує збереження. `AQA_CANCEL_RESERVATIONS=true` передбачено лише для іншого deployment зі штатним cancellation API; тут його успішність не перевірена.

Залишені записи перелічено в локальних `TestResults/<run>/AQA-HW8-*-lifecycle.json`. Дві ранні Guest sessions створилися до помилки DTO і не потрапили до fixture cleanup; конкретні записи потребують lifecycle review. Фінальний client реєструє tokens для redaction одразу після JSON decode. Точні кількості та прогінні результати наведено у RESULTS_UA.

Масові DynamoDB scan/delete, status resets, повторний seed та infrastructure cleanup не виконуються. Наступна безпечна альтернатива: окрема AQA-середа з TTL і захищеним fixture cleanup API, або whitelist-маніфест поточних IDs з ручним переглядом. Це **пропозиція**, resources/TTL не створено. Перед повтором потрібен перегляд накопичення та майбутніх зайнятих slots. CI/local concurrency=1; fixtures незалежні.
