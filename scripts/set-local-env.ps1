$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot ".cache\dotnet-cli"
$env:NUGET_PACKAGES = Join-Path $workspaceRoot ".packages\nuget"
$env:TEMP = Join-Path $workspaceRoot ".cache\temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null
New-Item -ItemType Directory -Force -Path $env:NUGET_PACKAGES | Out-Null
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
