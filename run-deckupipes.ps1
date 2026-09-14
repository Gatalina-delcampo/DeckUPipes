$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\DeckUPipes.App\DeckUPipes.App.csproj'
Write-Host "Launching DeckUPipes from $project" -ForegroundColor Cyan
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "DeckUPipes exited with code $LASTEXITCODE"
}
