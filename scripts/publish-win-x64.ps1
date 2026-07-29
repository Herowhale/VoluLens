$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "set-local-env.ps1")

$dotnet = Join-Path $workspaceRoot ".dotnet\dotnet.exe"
$project = Join-Path $workspaceRoot "src\VoluLens.App\VoluLens.App.csproj"
$output = Join-Path $workspaceRoot "artifacts\publish\win-x64"
$publishRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot "artifacts\publish"))
$output = [IO.Path]::GetFullPath($output)

if (-not $output.StartsWith(
    $publishRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output must remain under artifacts\publish."
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

& $dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $output

if ($LASTEXITCODE -ne 0) {
    throw "VoluLens publish failed."
}

$executable = Join-Path $output "VoluLens.App.exe"
if (-not (Test-Path $executable)) {
    throw "Published executable was not created."
}

Get-Item -LiteralPath $executable
