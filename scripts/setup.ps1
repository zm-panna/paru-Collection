param([string]$Server='localhost',[string]$Database='ParuEcommerceERP',[string]$SqlUser='')
. "$PSScriptRoot/common.ps1"
if ($Database -notmatch '^[A-Za-z][A-Za-z0-9_]{0,100}$') { throw 'Use only letters, digits and underscore in database name.' }
$LocalPath = Join-Path $ProjectRoot 'src/Ecommerce.Web/appsettings.Local.json'
if (-not (Test-Path $LocalPath)) {
 $Builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
 $Builder['Data Source']=$Server; $Builder['Initial Catalog']=$Database; $Builder['Encrypt']=$true; $Builder['TrustServerCertificate']=$true
 if ($SqlUser) {
  $Builder['User ID']=$SqlUser
  $SqlSecret=Read-Host 'SQL login password' -AsSecureString
  $Builder['Password']=(New-Object System.Net.NetworkCredential('', $SqlSecret)).Password
 } else { $Builder['Integrated Security']=$true }
 $AdminEmail=Read-Host 'Website admin email (different from SQL login)'
 $AdminSecret=Read-Host 'Website admin password (12+ chars, uppercase/lowercase/digit/symbol)' -AsSecureString
 $ModelName=Read-Host 'Ollama model name from ollama list (blank to configure later)'
 $Config=@{ConnectionStrings=@{Default=$Builder.ConnectionString};Seed=@{IncludeDemoOrders=$true;AdminEmail=$AdminEmail;AdminPassword=(New-Object System.Net.NetworkCredential('', $AdminSecret)).Password};Ollama=@{BaseUrl='http://localhost:11434/';Model=$ModelName}}
 $Config | ConvertTo-Json -Depth 6 | Set-Content -Path $LocalPath -Encoding UTF8
 $Config=$null; $Builder=$null; $AdminSecret=$null; $SqlSecret=$null
 Write-Host 'Local configuration saved. Keep this file private.'
} else { Write-Host 'Using existing local configuration; existing data is preserved.' }
Invoke-DotNet tool restore
Invoke-DotNet restore EcommerceERP.sln
Invoke-DotNet build EcommerceERP.sln --no-restore
& "$PSScriptRoot/db-migrate.ps1"
& "$PSScriptRoot/db-seed.ps1"
Write-Host 'Ready. Run .\scripts\start-local.ps1 and open http://localhost:5080'
