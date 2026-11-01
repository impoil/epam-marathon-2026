# Мінімальне наступне рішення: окремий AQA-контур

**Рекомендація: невеликий ізольований serverless stack у тому самому AWS account, з окремими DynamoDB tables, Lambda/API та Cognito pool; frontend запускати локально у CI.** Використати той самий backend artifact/source revision та Angular source, без правок production business logic. Це мінімальний варіант, який дає справжній fresh UI/API E2E й жорстку межу доступу до демонстраційних даних. У цьому аудиті нічого не розгорнуто, identities та AWS settings не створено/змінено.

## Порівняння варіантів

| Варіант | Зміни | Вартість | Вплив на демонстрацію / оцінка |
|---|---|---|---|
| Тільки offline/synthetic перевірки | Поточний AQA code + GitHub Quality | 0 нових AWS resources; локальні/CI minutes | Без business writes; корисно зараз, але не підтверджує fresh application E2E |
| Fixture/cleanup поверх діючого backend | Нові маркери/cleanup API або privileged controller, security review, можливо Lambda/IAM зміни | Найменше додаткових ресурсів, є requests/storage/controller costs | Прямо торкається shared demo data й waiter reservation lists. Поточна заборона та невідворотні помилки роблять цей варіант небажаним |
| Нові AQA data/Lambda/API, спільний Cognito | Клон data/runtime stack, нова test identity/group у чинному pool | Менше auth resources; спільні MAU/ліміти | Старі identities не обов’язково змінювати, але новий Waiter token може бути чинним і в demo API; auth boundary не ізольований |
| **Окремий data/runtime/auth stack** | Новий невеликий AQA stack, власний pool/client/roles; local CI frontend | Додаткові pay-per-use requests/compute/storage/auth, без постійної VM | Production stack/settings/identities не змінюються; IAM та token audience відділені. **Рекомендовано** |

Спільний account усе ще означає спільні billing/service quotas — це не окремий account-level sandbox. Перед погодженням перевірити наявні ліміти без їх збільшення; тестова concurrency=1, без навантажувальних тестів.

## Конкретний обсяг нового stack

1. Окремий Terraform root/state та resource prefix, наприклад `hw8-aqa`; не повторно застосовувати `infra/hw6-greentasty` до його чинного state. Існуючі production-файли не змінювати.
2. Той самий backend artifact у новій Lambda, новий REST API/stage, окремі runtime IAM role/log group. Environment variables посилаються **виключно** на AQA tables та pool. IAM Resource allowlist містить тільки їх ARNs; production table/pool доступ відсутній.
3. 10 tables з поточної schema `infra/hw6-greentasty/tables.json` та тими самими keys/GSIs; окремий невеликий `AqaRuns` registry для ownership/lease/cleanup journal. On-demand capacity; жодного доступу cleanup role до production ARNs.
4. Новий Cognito pool/client, groups Customer/Waiter, три спеціальні identities: Owner, Other Customer, Waiter. Жодну з трьох існуючих DPAPI identities не переводити в іншу роль.
5. Angular build з новим API_URL запускати на CI runner на визначеному loopback origin, наприклад `http://127.0.0.1:4173`; SPA fallback. CORS **нового** stack дозволяє тільки цей origin. Якщо створювати new module на базі наявного Terraform, підтримку loopback додати лише туди; validation/CORS production не послаблювати. Cloudflare Pages project/settings не змінюються, нового hosting-сервісу не потрібно.

Фізична schema й env mapping уже відокремлені у наявному deployment composition; тому зміна бізнесової Lambda logic для такого розділення не потрібна. Новий fixture controller — власний AQA інструмент, не публічний production endpoint.

## Fixture/cleanup contract, достатній для fresh E2E

Controller працює з короткоживучою окремою AQA IAM session; у CI майбутній OIDC trust обмежити конкретними repository/environment/ref. Його запуск та створення role потребують окремого погодження.

- **BeginRun:** registry видає run ID та lease, фіксує account/region, точний allowlist AQA tables і спеціальний TableId для цього run. Таблиця ресторану — run-owned fixture; інші runs не можуть її використовувати. Це дає корінь ownership навіть якщо POST reservation завершився network timeout до отримання ID.
- Normal reservation/group/guest/order дії виконуються через application API. Кожен returned ID і composite key дописується до registry. Утрачений response не спричиняє повторний POST: controller відновлює descendants тільки від зареєстрованого run-owned TableId/ReservationId та звіряє parent/customer/location на base-table reads.
- Два недосяжні стани (`participants=[]` і submitted empty cart) готуються **лише** controller у AQA tables, з register-before-write та точними ключами. Тести відрізняють synthetic precondition від реального UI lifecycle; production DynamoDB writes відсутні.
- **EndRun:** закрити lease, зупинити browser/pollers і дочекатися завершення in-flight requests. Зберегти assertions/artifacts. Controller виконує bounded cleanup тільки exact registered keys та підтверджених descendants. Умови DeleteItem перевіряють parent/customer/key; відсутній item — idempotent success, чужий/невідомий — відмова й journal.
- Cleanup охоплює GuestSessions, Orders (ключ **orderId + customerId**), GroupReservations, Reservations, Notifications та run-owned seed Table/Location/Dishes; waiter list змінюється тільки в AQA і тільки для поточних reservation IDs. Static AQA catalog/identity можна зберігати окремо від run-owned data.
- За помилки не виконувати broad scan/delete чи «force cleanup». Зберегти точний список unresolved keys; наступний BeginRun блокується до усунення залишків. Cleanup failure робить workflow Failed навіть після Passed business assertions. Повтор cleanup дозволений лише для того самого registry run/keys.

TTL — тільки страховка, не основне прибирання і не approval gate: AWS видаляє expired items асинхронно, зазвичай протягом кількох днів; видалення reservation не каскадує автоматично через application relationships. TTL-позначення всіх run-owned item ставити controller після припинення writes; права й ownership усе одно обов’язкові. [AWS TTL](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html).

Критерій готовності перед re-enable: dry-run cleanup manifest не містить production ARN/keys → cleanup одного isolated run → перевірка відсутності всіх descendants і звільнення slot → контрольований abort/timeout → повторний idempotent cleanup → тільки потім один безперервний fresh E2E. Старі 34 demo reservations не включаються в цей новий механізм автоматично.

## Справжня роль Waiter без зміни чинних personalities

Найменший безпечний шлях для рекомендованого контуру — **один новий user у новому AQA pool**, а не виправлення чинного account з local label `waiter`. План адміністратора після погодження:

1. Створити нового AQA user (`AdminCreateUser`, `MessageAction=SUPPRESS`), задати захищений пароль стандартним admin flow; не надсилати email і не використовувати self-signup як спосіб отримання staff role.
2. Додати тільки нового user до group `Waiter` із точним регістром; runtime не потребує прав змінювати membership. Отримати свіжі tokens після membership change. [Cognito groups/claims](https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-pools-user-groups.html), [AdminCreateUser](https://docs.aws.amazon.com/cognito-user-identity-pools/latest/APIReference/API_AdminCreateUser.html).
3. У **AQA Waiters table** створити mapping `userId=new Cognito sub`, `locationId=AQA location`, `reservations=[]`; без цього group role не забезпечує призначення столу/бронювання.
4. Перевірити `/me.role=Waiter`, `cognito:groups` у новому token та `reservation.waiterId == new sub`. Передати credentials тільки через майбутній `aqa-isolated` secret scope; не використовувати demo DPAPI identities як staff.

Якщо вже існує інший справжній Waiter з потрібним призначенням і власник надасть його credentials, можна обійтися без створення identity. Наявність такого account цим аудитом **не підтверджено**; користувачів/груп не шукали й не створювали в AWS. Додавати нового waiter до чинної demo location небажано: це може змінити least-busy assignment майбутніх бронювань.

## Вартість і наступне погодження

Точний кошторис без обсягу запусків, тривалості Lambda, log/storage retention та поточного plan/credits не визначено; «безкоштовність» не гарантується. Формула нового AQA контуру: API calls + Lambda requests/GB-seconds + DynamoDB reads/writes/storage/GSIs + logs + Cognito active-user usage + CI minutes. Клон в account не отримує окремий незалежний Free Tier. [API Gateway pricing](https://aws.amazon.com/api-gateway/pricing/), [Lambda pricing](https://aws.amazon.com/lambda/pricing/), [DynamoDB pricing](https://aws.amazon.com/dynamodb/pricing/), [Cognito pricing](https://aws.amazon.com/cognito/pricing/).

Немає EC2, NAT Gateway, нової CloudFront distribution чи окремого Cloudflare deployment. Наступне рішення для власника — погодити **лише цей ізольований stack, нові test identities та fixture controller** з budget/retention; до цього manual business E2E лишається suspended. Deployment plan, credentials provision, тариф, квоти й infrastructure writes у поточному аудиті не виконувалися.
