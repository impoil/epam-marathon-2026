# Реальне виконання AQA — 01.11.2026

Об’єкт: власні Cloudflare Pages frontend та AWS API Gateway backend Green&Tasty. Наявний локальний SDK **10.0.403**, надані versions framework, встановлений Chrome headless. Нове глобальне ПЗ не встановлювалося. Production sources/deployment, AWS/Cognito configuration та історичні reservation/order не змінено.

| Прогін | Passed | Failed | Skipped | Тривалість | Оцінка |
|---|---:|---:|---:|---|---|
| Перший повний `hw8`, 32 tests | 19 | 11 | 2 | 2m 43.022s | **Failed**, збережено всі причини; не є підтвердженням готовності всього набору |
| Read-only UI після DNS/locator fix | 1 | 0 | 0 | 6.670s | Справжній login, participant cards/dishes/quantity/flags/subtotals/consolidated total; жодного business write |
| Actual Waiter preflight, 4 tests | 0 | 0 | 4 | 4.299s | **Blocked**, а не чотири успішні waiter tests |

Поточна фінальна discovery знаходить **35 scenarios**: основний набір і додатковий read-only diagnostic. Повтор усього фінального набору не виконано. Після першого прогону додано окремий waiter visibility test, single-person/no-group test, точну перевірку fixture quantities/prices/flags та runtime Waiter preflight. Їх не позначено як реально Passed без запуску.

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

Створено **30 власних reservation records**: 6 у development smoke та 24 у першому повному прогоні. Точні IDs/майбутні timeslots — у локальних lifecycle manifests. Частина має власні group/order/owner-session; Guests відкритих groups точково видалено, крім двох ранніх DTO partial failures. Reservation cleanup API відсутній; записи **не скасовані й не мають TTL**. Демо 31.10.2026/$61 не змінено. Нова й історична тестові дані не змішуються.

Автоматична safety review **відхилила другий повний прогін** через накопичення persistent data без cleanup API. Запитано окремий дозвіл на максимум 4 нових reservation для основних UI flows; без відповіді нові business writes не виконуються. Поточний read-only повтор і Waiter preflight не створили додаткових об’єктів.

## Перевірки та звіти

Locked restore, format verification, Release build (**0 warnings/errors**), Gherkin lint 4.2.4 і discovery виконано локально. Quality workflow без secrets підготовлено для PR. Manual E2E workflow не запускався в GitHub: потрібні налаштовані variables/Secrets/environment та коректний Waiter. Його успіх не заявляється.

Обезособлені per-test results з тривалістю й причиною: [full-first](results/full-first.json), [read-only](results/readonly-verified.json), [waiter preflight](results/waiter-verified.json). TRX/TUnit HTML/Reqnroll HTML, screenshots/traces, lifecycle diagnostics зберігаються локально; CI завантажує їх навіть після Failed, зберігаючи фінальний failure status.

[Screenshot справжнього readonly стану](evidence/readonly-after-submit.png) знято під час попереднього read-only проходу до виправлення Owner locator. Стан продукту не змінювався; повтор після locator fix пройшов. $30 — новий тестовий order цього запуску, не історичний $61.

## Мінімальні наступні дії

1. Погодити обмежений UI повтор із контролем залишених записів або надати ізольоване AQA environment з cleanup API.
2. Надати наявну справжню Waiter identity, призначену локації/бронюванню. Поточний account не має потрібної Cognito group; тести самі її не змінюють.
3. Запустити фінальні UI mutations та API cases з виправленими fixtures і зберегти новий звіт окремо від історичного Failed.
4. Узгодити fixture механізм для двох недосяжних станів і product-рішення щодо окремого UI total усіх carts, якщо суворо вимагати його за L1.
5. Налаштувати CI environment/Secrets і перевірити manual E2E; пройти public readiness audit перед зміною visibility.

План product-змін, якщо їх буде погоджено: cancellation/cleanup endpoint у **тестовій** середовищі; окремі позначені totals у UI; fixture API для недосяжних станів. Жодного такого frontend/backend/infrastructure deployment тут не виконано.
