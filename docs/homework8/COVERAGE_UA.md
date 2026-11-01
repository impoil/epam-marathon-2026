# Покриття Напряму 1

| Рівень | Сценарії | Спосіб |
|---|---|---|
| L1 | Новий reservation/invite, Owner і Guests, dishes/quantity/catalog price, submitted/draft, empty cart, subtotals | API fixtures + UI Review |
| L1 | Порожня Owner cart до join; draft cart change → зміна загальної суми | Окремі API scenarios |
| L1 | Missing group, other Customer/Guest/Waiter/anonymous | Outlines; actual Waiter preflight |
| L1 | Zero participants, total=0 | Explicit Skip: public API завжди створює Owner і забороняє видалення |
| L2 | Однаковий dishId від Owner/Guest → quantity 3; draft інша страва quantity 7 виключена | UI Submit + API order/status/total |
| L2 | Group Submitted, Order SubmittedByCustomer (3), повторний submit, edit/remove після фіналу | Точні API assertions; UI read-only після reload |
| L2 | No submitted carts, missing order/group/reservation, порожній reservationId | Окремі приклади, messages і незмінний стан після відмов |
| L2 | Submitted empty cart | Explicit Skip: fixture недосяжна через public API |
| L2 | Офіціант бачить той самий склад | Окремий requires-waiter scenario; local identity насправді Customer |
| L3 | Submitted Guest remove, exact subtotal subtraction, решта participants, persist read | API + UI confirmation/recalculation |
| L3 | Останній Guest, Owner protection, missing participant, repeated DELETE, old guest bearer | Окремі API scenarios |
| L3 | Participant іншої незалежної group → 400; обидві group незмінні | Дві власні fixtures |
| L3 | Other Customer/Guest/Waiter/anonymous remove denied | Outlines і перевірка незмінності |
| L3 E2E | Нова API reservation; Owner UI cart/submit; anonymous browser invitation/join/cart/submit; review; remove іншого Guest; recalculate; final submit; reload read-only | Playwright у двох browser contexts; додатковий Guest для remove підготовлений API |

Очікувані quantities/flags порівнюються з виконаними діями, names/prices — з незалежним catalog read. Довільний 4xx не зараховується як правильна відмова. Порожній набір checks не може пройти. Немає mutation retries та global sweep.

Точні вимоги homework-aqa.md мають пріоритет. Cleanup fallback взято з його загальних вимог: якщо API прибирання немає, дозволені унікальні дані. Це не створює TTL і не усуває потребу в контрольованій тестовій середовищі.

API review total включає draft, UI показує **consolidated Grand Total** лише submitted carts. L2 вимагає submitted aggregation. За суворого трактування L1 окремий UI total усіх carts відсутній. Мінімальна пропозиція: окремі підписи «All participant carts total» та «Submitted pre-order total» і відповідні UI assertions. Product frontend не змінено.

Перегляд замовлення офіціантом реалізовано API, як дозволено запитом, але фактичний результат потребує справжньої Waiter role. Відмова Customer account не підтверджує перевірку офіціанта.
