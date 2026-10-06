. "$PSScriptRoot/common.ps1"
Invoke-DotNet restore EcommerceERP.sln
Invoke-DotNet build EcommerceERP.sln --configuration Release --no-restore
Invoke-DotNet test EcommerceERP.sln --configuration Release --no-build --logger trx
Invoke-DotNet ef migrations has-pending-model-changes --project src/Ecommerce.Infrastructure --startup-project src/Ecommerce.Web --configuration Release --no-build
