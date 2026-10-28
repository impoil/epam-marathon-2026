# Проверка Homework #5

Среда: Codex Cloud, Linux, официальный .NET SDK **10.0.403**, target framework **net10.0**. Проверка выполнена 28 октября 2026 года. AWS API/CLI не вызывались.

| Проверка | Результат |
|---|---|
| Исходная версия `77d1e2aa4e95af77700b3090f39d0b78d474b0da`, `dotnet test` | 141 passed, 0 failed, 0 skipped |
| Итоговая solution, `dotnet build --configuration Release --no-restore` | Успешно, 0 errors; существующие warnings |
| Итоговая solution, `dotnet test --configuration Release --no-build --no-restore` | 248 passed, 0 failed, 0 skipped |
| Только новые `FullyQualifiedName~Homework5` | 107 passed, 0 failed, 0 skipped |

Полная solution включает APIHandler, APIHandler.Tests, ReportSender и ReportsHandler. Ошибок старых тестов не обнаружено. Изменён один старый тестовый fixture: добавлены mock dependencies конструктора GroupReservationService; сами старые проверки не удалялись и не ослаблялись.

Новые тесты покрывают четыре handler-а, их сервисы, SDK-сериализацию условной транзакции, mapping HTTP ошибок, точную структуру JSON, настоящую регистрацию в RouteRegistry и DI с mock AWS repositories, соответствие Cognito authorizer в API Gateway config.

Обязательные сценарии: успех; отсутствие reservation/group/session/order; чужой владелец и пустой Cognito subject; поддельный userId в body; отсутствие submitted sessions/блюд; draft sessions исключены из submit; одинаковые dishId суммируются; некорректные количества и overflow; принадлежность group/reservation/order; удаление owner session запрещено; новый total; Closed/Submitted group и non-Created order; повторный submit/delete; storage failure без изменения загруженных данных. Ответ DELETE не зависит от появления удалённой session в устаревшем GSI.

Warnings исходных зависимостей включают уязвимости SixLabors.ImageSharp 3.1.12, System.Security.Cryptography.Pkcs 8.0.1 и System.Security.Cryptography.Xml 8.0.4, а также nullable, deprecated SDK calls и две старые xUnit Theory без параметров. Они не скрывались и не превращались в skipped tests. Обновление пакетов и live integration tests указаны как предварительные условия публичного развёртывания в [FreshDeploy.md](FreshDeploy.md).

Перед commit выполнены `git diff --check`, просмотр diff, проверка добавленных файлов на секреты/credentials и запрещённые build/state-файлы. В PR входят только C# backend, его API Gateway config, tests и документация. `infra/homework4` не изменён; SDK, NuGet cache, build artifacts, credentials и Terraform state в commit не входят.
