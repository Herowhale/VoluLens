$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "set-local-env.ps1")

$installRoot = Join-Path $PSScriptRoot "..\.dotnet"
$sdkVersion = "8.0.423"
$cacheRoot = Join-Path $PSScriptRoot "..\.cache\downloads"
$archivePath = Join-Path $cacheRoot "volulens-dotnet-sdk-$sdkVersion-win-x64.zip"
$downloadUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdkVersion/dotnet-sdk-$sdkVersion-win-x64.zip"

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null

if (-not (Test-Path (Join-Path $installRoot "sdk\$sdkVersion"))) {
    & curl.exe --fail --location --retry 3 --continue-at - --output $archivePath $downloadUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to download .NET SDK $sdkVersion."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $installRoot -Force
    Remove-Item -LiteralPath $archivePath -Force
}

$dotnet = Join-Path $installRoot "dotnet.exe"
if (-not (Test-Path $dotnet)) {
    throw ".NET SDK installation did not produce dotnet.exe."
}

& $dotnet --info
