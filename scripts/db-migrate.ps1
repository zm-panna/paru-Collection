. "$PSScriptRoot/common.ps1"
Invoke-DotNet run --project src/Ecommerce.Web --no-launch-profile -- --migrate
