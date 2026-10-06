. "$PSScriptRoot/common.ps1"
$env:ASPNETCORE_ENVIRONMENT='Development'
Invoke-DotNet run --project src/Ecommerce.Web --launch-profile Ecommerce.Web
