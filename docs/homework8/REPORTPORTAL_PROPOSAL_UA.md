# ReportPortal — технічна пропозиція

Завдання із зірочкою **не реалізоване**: сервіс не розгортався, платні ресурси не створювалися. GitHub Actions — cloud CI provider; базові TRX/TUnit HTML/Reqnroll HTML/Playwright diagnostics реалізовано.

Після погодження hosting/вартості: окремий захищений instance, project `greentasty-aqa`, service API key у GitHub environment Secrets; endpoint/project/launch name — environment variables. Перевірити сумісність актуального ReportPortal .NET agent із Reqnroll.TUnit 3.3.4/TUnit 1.72.10. Якщо adapter відсутній — післятестовий importer TRX + Reqnroll messages через ReportPortal API. Result визначає runner, а не успіх upload.

Launch attributes: commit/ref/scope/environment/run ID; feature/scenario/step hierarchy. Attachments тільки після redaction, мінімальний retention і командний доступ. Не передавати invitation/session URLs, Authorization, passwords, AWS credentials або email identities. Blocked fixtures пояснюють Skip; справжні defects лишаються Failed.

Порядок після погодження: hosting → adapter/importer з одним failing non-production test → redaction/access/retention → інтеграція в manual workflow → звірка Passed/Failed/Skipped і artifacts → документація launch URL. Production backend/frontend не потрібно змінювати.
