. "$PSScriptRoot/common.ps1"
Invoke-DotNet test EcommerceERP.sln --configuration Release
Invoke-DotNet publish src/Ecommerce.Web --configuration Release --output artifacts/publish
# Never copy developer credentials to a deployment artifact.
Remove-Item artifacts/publish/appsettings.Local.json -ErrorAction SilentlyContinue
Write-Host 'Published to artifacts/publish. Configure production environment variables on IIS.'
