# CareNestAI — Firebase Hosting Deploy Script
# Run once: firebase login
# Then run: .\deploy.ps1

$firebase = "C:\Users\aliak\AppData\Roaming\npm\firebase.ps1"
$appDir = "$PSScriptRoot\app"

Write-Host "Building Flutter web..." -ForegroundColor Cyan
Set-Location $appDir
flutter build web --release
if ($LASTEXITCODE -ne 0) { Write-Host "Flutter build failed!" -ForegroundColor Red; exit 1 }

Write-Host "Deploying to Firebase Hosting..." -ForegroundColor Cyan
Set-Location $PSScriptRoot
& $firebase deploy --only hosting

Write-Host "Done! App is live." -ForegroundColor Green
