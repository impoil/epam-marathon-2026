# Ізольовані перевірки коду AQA

Цей проект **не запускає Green&Tasty E2E**. Він компілює linked source files із framework без ProjectReference на live BDD assembly/Reqnroll hooks. Secrets, Cognito identities, UI_URL/API_URL та AWS credentials не потрібні.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet bin/Release/net10.0/restaurant-aqa-verification.dll --report-trx --results-directory TestResults/offline --treenode-filter '/*/*/*/*[Category=offline]'
python run_reporting_checks.py
```

34 cases перевіряють блокування business requests **до** transport, missing/invalid/35 budget, sticky/uncertain Submitted cleanup, exact consolidation (зокрема same-price duplicates і компенсовані unit-price помилки), незмінність surviving carts та bounded readiness polling. Шість Python cases використовують synthetic TRX і перевіряють frozen workflow contract. CI запускає саме їх; результати — `TestResults/`.

Окремо локально на **вже встановленому Chrome**:

```powershell
dotnet bin/Release/net10.0/restaurant-aqa-verification.dll --report-trx --results-directory TestResults/browser --treenode-filter '/*/*/*/*[Category=offline-browser]'
```

Один browser case використовує тільки synthetic localhost route sink: GET/signin проходять до sink, POST orders та signin на інший origin блокуються. Жодний запит не пересилається Green&Tasty. Browser installation/production credentials цей тест не вимагає.

Сумарно локально перевірено 35 C# cases; це окрема кількість від **36 BDD scenarios** основного framework. Offline Passed не є application acceptance/fresh E2E Passed.
