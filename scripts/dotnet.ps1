$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "set-local-env.ps1")

$dotnet = Join-Path $workspaceRoot ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    throw "Local .NET SDK not found. Run scripts\install-dotnet.ps1 first."
}

& $dotnet @args
exit $LASTEXITCODE

