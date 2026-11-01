# Реальне виконання AQA — 01.11.2026

Це **історичний звіт** business-прогонів. Подальший [аудит надійності](RELIABILITY_AUDIT_UA.md) не повторював ці сценарії: business writes зупинено, додано окремі offline regressions. Жодний із Failed результатів нижче не замінено на Passed.

Об’єкт: власні Cloudflare Pages frontend та AWS API Gateway backend Green&Tasty. Наявний локальний SDK **10.0.403**, надані versions framework, встановлений Chrome headless. Нове глобальне ПЗ не встановлювалося. Production sources/deployment, AWS/Cognito configuration та історичні reservation/order не змінено.

| Прогін | Passed | Failed | Skipped | Тривалість | Оцінка |
|---|---:|---:|---:|---|---|
| Перший повний `hw8`, 32 tests | 19 | 11 | 2 | 2m 43.022s | **Failed**, збережено всі причини; не є підтвердженням готовності всього набору |
| Read-only UI після DNS/locator fix | 1 | 0 | 0 | 6.670s | Справжній login, participant cards/dishes/quantity/flags/subtotals/consolidated total; жодного business write |
| Actual Waiter preflight, 4 tests | 0 | 0 | 4 | 4.299s | **Blocked**, а не чотири успішні waiter tests |
| Дозволений повтор чотирьох UI scenarios | 2 | 2 | 0 | 37.609s | Review/Remove Passed; Submit assertions passed, teardown failed; journey interrupted before Guests |
| Продовження того самого E2E без нової reservation | 1 | 0 | 0 | 14.019s | Guest browser, review/remove/recalculate/final submit/reload пройдено; Owner cart UI submit виконано на попередньому етапі того самого scenario |

Поточна фінальна discovery знаходить **36 scenarios**: основний набір і додаткові read-only/continuation diagnostics. Повтор усього фінального набору не виконано. Після першого прогону додано окремий waiter visibility test, single-person/no-group test, точну перевірку fixture quantities/prices/flags та runtime Waiter preflight. Не виконані після змін API cases не позначено як реально Passed.

## Причини 11 невдач першого повного прогону

| Кількість | Причина | Стан |
|---:|---|---|
| 4 | Усі основні UI flows потрапляли через системний DNS на 10.0.0.43; app form відсутня, timeout | Public DNS + process-local Chromium override підтвердили справжній frontend з валідною TLS; окремий read-only UI пройдено |
| 6 | Fixture вважала table «available» гарантією точного slot, отримувала overlap 400 | Виправлено вибір конкретного slot через timeslots API; нові mutating повтори ще не виконані |
| 1 | Final aggregation test очікував доступ waiter до order, але отримав 403 | `/me` підтвердив Customer, у JWT немає Waiter group; окремі waiter scenarios блокуються до data creation |

Три waiter-labelled негативні checks у початкових 19 Passed фактично виконувалися Customer identity. Це **не підтверджує** права Waiter. Фінальний preflight виправляє некоректну передумову; початкові runner counts збережено незмінними. Failed assertion не перетворено на Passed: невиконувані waiter assertions винесено в явно blocked scenarios.

Два Skipped: zero-participant group недосяжна через public API, бо Owner session обов’язкова й захищена; submitted empty cart недосяжна, бо guest submit відхиляє empty cart. Вони потребують окремої контрольованої fixture API, яка тут не створювалася.

Перші три development smoke (по 2 tests, усі Failed: 12.237s/8.760s/10.882s) виявили різницю DTO/status codes, відсутній cancellation API, numeric OrderStatus і DNS. Окремий read-only до locator fix: 0/1/0, 11.934s. Ці історичні збої не приховані; raw TRX/HTML/traces лишаються в локальному ignored `TestResults/`. Невиконані кроки після помилки не зараховувалися як Passed.

## Дані та обмеження повтору

Створено **34 власних reservation records**: 6 у development smoke, 24 у першому повному прогоні та рівно 4 після окремого дозволу користувача. Точні IDs/майбутні timeslots — у локальних lifecycle manifests. Частина має власні group/order/owner-session; Guests відкритих groups точково видалено, включно з двома ранніми DTO partial failures, відновленими за власними manifests. Reservation cleanup API відсутній; записи **не скасовані й не мають TTL**. Демо 31.10.2026/$61 не змінено.

Автоматична safety review **відхилила другий повний прогін** через накопичення persistent data. Користувач після цього явно дозволив максимум 4 нових reservation. Виконано лише цей обмежений повтор із `AQA_MAX_RESERVATIONS=4`. Подальші продовження того самого interrupted scenario використовували `AQA_MAX_RESERVATIONS=0`; п’яту reservation не створено. Read-only і Waiter preflight також не створювали об’єктів.

У повторі Submit пройшов усі business assertions, але cleanup помилково довіряв ранньому Open snapshot. Виправлено читання фактичного group status перед cleanup і збереження підтвердженого Submitted. Journey виявив тестові synchronization errors: передчасний GET order (404), перехід до завершення асинхронного /me (header без cart), хибне очікування зникнення order card. За контрактом Group Owner submit лишає Order=Created до фінального submit; неправильний Count=0 замінено перевіркою справжньої UI confirmation і persisted Owner.isSubmitted. Жодний product assertion не вимкнено заради зеленого результату.

Два проміжні continuation failures збережено окремо: [profile wait](results/journey-continuation.json), 21.163s, і [Owner card expectation](results/journey-finished.json), 13.014s. Фінальне [continuation Passed](results/journey-completed.json) завершило ті самі дані автоматично: Owner UI cart submit з попереднього етапу, новий anonymous Guest через UI (2 страви), API Guest для remove, UI Review → Remove → Grand Total $47 → $30 → Submit → read-only після reload; API Order=SubmittedByCustomer та Group=Submitted. Це відновлений scenario у кілька автоматичних проходів, **не один безперервний green fresh run**. Waiter visibility не перевірено через некоректну роль.

## Перевірки та звіти

Locked restore, format verification, Release build (**0 warnings/errors**), Gherkin lint 4.2.4 і discovery виконано локально. Quality workflow без secrets підготовлено для PR. Manual E2E workflow не запускався в GitHub: потрібні налаштовані variables/Secrets/environment та коректний Waiter. Його успіх не заявляється.

Обезособлені per-test results: [full-first](results/full-first.json), [read-only](results/readonly-verified.json), [waiter preflight](results/waiter-verified.json), [чотири UI](results/ui-approved-four.json), [E2E continuation](results/journey-completed.json). TRX/TUnit HTML/Reqnroll HTML, screenshots/traces, lifecycle diagnostics зберігаються локально; CI завантажує їх після Failed зі збереженням failure status.

[Screenshot справжнього readonly стану](evidence/readonly-after-submit.png) знято під час попереднього read-only проходу до виправлення Owner locator. Стан продукту не змінювався; повтор після locator fix пройшов. $30 — новий тестовий order цього запуску, не історичний $61.

## Мінімальні наступні дії

1. Надати ізольоване AQA environment з cleanup API або контрольованим lifecycle перед наступним повним прогоном: додатковий дозволений бюджет 4 уже використано повністю.
2. Надати наявну справжню Waiter identity, призначену локації/бронюванню. Поточний account не має потрібної Cognito group; тести самі її не змінюють.
3. Перевірити один безперервний fresh E2E та повний фінальний API suite після налаштування середовища; зберегти звіт окремо від історичних Failed/continuation.
4. Узгодити fixture механізм для двох недосяжних станів і product-рішення щодо окремого UI total усіх carts, якщо суворо вимагати його за L1.
5. Налаштувати CI environment/Secrets і перевірити manual E2E; пройти public readiness audit перед зміною visibility.

План product-змін, якщо їх буде погоджено: cancellation/cleanup endpoint у **тестовій** середовищі; окремі позначені totals у UI; fixture API для недосяжних станів. Жодного такого frontend/backend/infrastructure deployment тут не виконано.
