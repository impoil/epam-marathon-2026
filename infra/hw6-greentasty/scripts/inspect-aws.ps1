# Read-only. Stops on the first AWS error; never logs credentials, tokens or state contents.
param([string]$OutputPath = 'aws-inventory.json')
$ErrorActionPreference = 'Stop'
function Read-Aws([string[]]$Arguments) {
    $result = & aws @Arguments --profile fresh-deploy --region eu-north-1 --output json --no-cli-pager
    if ($LASTEXITCODE -ne 0) { throw "AWS read failed: $($Arguments[0]) $($Arguments[1])" }
    return ($result | ConvertFrom-Json)
}
$identity = Read-Aws @('sts', 'get-caller-identity')
if ($identity.Account -ne '071954286972') { throw 'Unexpected AWS account. Stop.' }
$report = [ordered]@{ CheckedAtUtc = [DateTime]::UtcNow.ToString('o'); Identity = $identity; EffectiveRegion = 'eu-north-1' }
# Free Plan API is served in us-east-1; the resource inventory below stays in Stockholm.
$freePlan = & aws freetier get-account-plan-state --profile fresh-deploy --region us-east-1 --output json --no-cli-pager
if ($LASTEXITCODE -ne 0) { throw 'Free Plan state could not be confirmed. Stop.' }
$report.FreePlan = $freePlan | ConvertFrom-Json
$report.Lambda = Read-Aws @('lambda', 'list-functions', '--query', 'Functions[].{Name:FunctionName,Runtime:Runtime,Memory:MemorySize,State:State}')
$report.LambdaQuota = Read-Aws @('lambda', 'get-account-settings')
$report.RestApis = Read-Aws @('apigateway', 'get-rest-apis', '--query', 'items[].{id:id,name:name}')
$report.HttpApis = Read-Aws @('apigatewayv2', 'get-apis', '--query', 'Items[].{id:ApiId,name:Name}')
$report.DynamoDB = Read-Aws @('dynamodb', 'list-tables')
$report.Cognito = Read-Aws @('cognito-idp', 'list-user-pools', '--max-results', '60')
$report.S3 = Read-Aws @('s3api', 'list-buckets', '--query', 'Buckets[].Name')
$report.CloudFront = Read-Aws @('cloudfront', 'list-distributions', '--query', 'DistributionList.Items[].{Id:Id,Domain:DomainName,Enabled:Enabled}')
$report.NatGateways = Read-Aws @('ec2', 'describe-nat-gateways', '--query', 'NatGateways[].{Id:NatGatewayId,State:State}')
$report.Instances = Read-Aws @('ec2', 'describe-instances', '--query', 'Reservations[].Instances[].{Id:InstanceId,State:State.Name,Type:InstanceType}')
$report.LoadBalancers = Read-Aws @('elbv2', 'describe-load-balancers', '--query', 'LoadBalancers[].{Name:LoadBalancerName,Type:Type}')
$bucket = 'epam-hw4-state-freshdeploy-20261027'
$owner = '071954286972'
$null = Read-Aws @('s3api', 'head-bucket', '--bucket', $bucket, '--expected-bucket-owner', $owner)
$report.StateBucketLocation = Read-Aws @('s3api', 'get-bucket-location', '--bucket', $bucket, '--expected-bucket-owner', $owner)
$report.StateEncryption = Read-Aws @('s3api', 'get-bucket-encryption', '--bucket', $bucket, '--expected-bucket-owner', $owner)
$report.StateVersioning = Read-Aws @('s3api', 'get-bucket-versioning', '--bucket', $bucket, '--expected-bucket-owner', $owner)
$report.StatePublicAccess = Read-Aws @('s3api', 'get-public-access-block', '--bucket', $bucket, '--expected-bucket-owner', $owner)
$report.StatePolicy = Read-Aws @('s3api', 'get-bucket-policy', '--bucket', $bucket, '--expected-bucket-owner', $owner)
$report.Hw6StateMetadata = Read-Aws @('s3api', 'list-objects-v2', '--bucket', $bucket, '--prefix', 'hw6-greentasty/', '--expected-bucket-owner', $owner, '--query', 'Contents[].{Key:Key,Size:Size}')
$report | ConvertTo-Json -Depth 12 | Set-Content $OutputPath
Write-Output "Read-only inventory saved to $OutputPath. Review before remote init; no resources changed."
