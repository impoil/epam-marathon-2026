# Окремий аудит готовності до публікації — 01.11.2026

**Результат: публікація всього repository поки потребує перевірок нижче.** GitHub metadata підтверджує `visibility=private`. Visibility, Git history, AWS infrastructure та PR #2 не змінювалися. Вимога homework-aqa.md про public repository ще не виконана; Draft PR не є її заміною.

| Область | Факти | Наступна дія |
|---|---|---|
| EPAM source/materials | У корені наданого archive та AQA subset немає явної LICENSE/NOTICE, що надає право відкритого розповсюдження | Отримати підтвердження умов EPAM для code, assets, homework descriptions та framework; не призначати чужому коду власну MIT ліцензію |
| Third-party dependencies/assets | Lock-файл фіксує packages; існуючі FE images/fonts/Figma/PDF/evidence мають окреме походження | Зібрати inventory та notices/license terms; права на навчальний доступ не вважати автоматично правом на публічну дистрибуцію |
| Секрети у Git | Початкова read-only перевірка: 856 tracked files, 922 reachable history blobs, 1691 text scans, 0 heuristic findings | Повторний scan після staging; окремий спеціалізований secret scan перед public. Нуль heuristic findings не є доказом відсутності секретів |
| Локальні джерела | `_local_hw8_sources` виключено `.git/info/exclude` і `.gitignore`; organizer archive не комітиться | Зберігати локально, не додавати force-add |
| Credentials | DPAPI залишається поза Git, `.env` ignored, URL/credentials не внесено до `.env.example` | Credentials для CI передати через Secrets; перевірити роль Waiter; не публікувати invitation/bearer values |
| Test artifacts | TestResults/bin/obj/generated bindings ignored; local diagnostics можуть містити sensitive values до redaction | Sanitization до upload, короткий retention, перегляд screenshot/trace; не комітити raw artifacts автоматично |
| AWS metadata | Старі docs уже містять account/pool/client IDs, endpoint URLs, ARNs і resource names | Це не passwords/access keys, але inventory корисний стороннім; вирішити, що обезособити перед public |
| Personal information | Git author metadata, Windows paths, screenshots, test names та локальні матеріали ДЗ №7 | Переглянути всі binary evidence/author emails, особливо не включені тут документи ДЗ №7 |
| Remote surface | Branch refs/history fetched, PR metadata доступні | Actions artifacts/logs, GitHub Secrets/settings, issues/attachments, unreachable objects і невитягнуті remote refs цим скриптом не перевірені |

Відсутність ліцензії не означає загального дозволу копіювати та поширювати код; GitHub окремо пояснює цей випадок у [Licensing a repository](https://docs.github.com/en/enterprise-cloud%40latest/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository). Для EPAM потрібні саме його умови. Нова ліцензія від автора ДЗ може поширюватися лише на власні доповнення в межах відповідних прав.

`scripts/audit-public.py --history --output <local-file>` тільки читає reachable Git objects. Виявляє AWS access-key patterns, JWT, GitHub tokens, private-key markers, literal guest bearers та tracked local/generated files; **ніколи не друкує matched values**. Обмеження: binary files/archives/images, blobs >=2 MB, false negatives та відсутність перевірки ліцензій/семантики PII. Комітований результат наведено в `results/public-audit.json`.

Мінімальний порядок перед public: підтвердити EPAM rights → перевірити licenses/notices/assets → перевірити файли та всю історію спеціалізованим scanner → переглянути screenshots/документи/author metadata → за потреби погодити remediation/rotation → налаштувати CI Secrets/environment → окреме рішення власника про visibility. History rewrite та visibility change тут не виконуються.
