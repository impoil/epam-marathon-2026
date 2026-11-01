# Default: produce a local manifest only. -Execute requires separate user approval.
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F-]{36}$')][string]$WaiterSub,
    [switch]$Execute
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function S([string]$Value) { return @{ S = $Value } }
function N([string]$Value) { return @{ N = $Value } }
$location = '60000000-0000-4000-8000-000000000001'
$table = '60000000-0000-4000-8000-000000000002'
$dish1 = '60000000-0000-4000-8000-000000000003'
$dish2 = '60000000-0000-4000-8000-000000000004'
$seed = @(
    @{ TableName='hw6-greentasty-Locations'; Key='id'; Item=@{id=(S $location); address=(S 'Green&Tasty demo, Stockholm'); description=(S 'EPAM homework demonstration'); totalCapacity=(N '6'); averageOccupancy=(S 'Low'); rating=(N '0'); imageUrl=(S '/assets/img/available-tables.jpg'); specialtyDishIds=@{L=@((S $dish1),(S $dish2))}} },
    @{ TableName='hw6-greentasty-Tables'; Key='id'; Item=@{id=(S $table);locationId=(S $location);capacity=(N '6');tableNumber=(N '1')} },
    @{ TableName='hw6-greentasty-Waiters'; Key='userId'; Item=@{userId=(S $WaiterSub);locationId=(S $location);createdAt=(S ([DateTime]::UtcNow.ToString('o')));reservations=@{L=@()}} }
)
foreach ($dish in @(@{id=$dish1;name='Demo salad';price='17'},@{id=$dish2;name='Demo main course';price='10'})) {
    $seed += @{ TableName='hw6-greentasty-Dishes'; Key='id'; Item=@{id=(S $dish.id);name=(S $dish.name);description=(S 'Demo menu item');price=(N $dish.price);dishType=(N '1');state=(N '1');locationId=(S $location);weight=(S '250 g');calories=(S '250');proteins=(S '10');fats=(S '8');carbohydrates=(S '30');vitamins=(S 'A, C');imageUrl=(S '/assets/img/dish.png')} }
}
New-Item -ItemType Directory -Force (Join-Path $root 'build') | Out-Null
$path = Join-Path $root 'build/seed.json'
$seed | ConvertTo-Json -Depth 12 | Set-Content $path
if (!$Execute) { Write-Output "Local seed manifest only: $path"; return }
$identity = & aws sts get-caller-identity --profile fresh-deploy --region eu-north-1 --output json --no-cli-pager | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $identity.Account -ne '071954286972') { throw 'AWS account verification failed' }
foreach ($record in $seed) {
    $itemPath = Join-Path $root 'build/seed-item.json'
    $record.Item | ConvertTo-Json -Depth 12 | Set-Content $itemPath
    & aws dynamodb put-item --profile fresh-deploy --region eu-north-1 --table-name $record.TableName --item "file://$itemPath" --condition-expression "attribute_not_exists($($record.Key))" --no-cli-pager
    if ($LASTEXITCODE -ne 0) { throw 'Seed stopped; existing data will not be overwritten. Inspect partial inserts before continuing.' }
}
Write-Output 'Seed inserted. Create reservations, sessions and orders only through the real application API.'
