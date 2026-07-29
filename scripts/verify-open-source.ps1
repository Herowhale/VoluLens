[CmdletBinding()]
param(
    [string]$Root = '',
    [switch]$RequireRelease
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent $PSScriptRoot
}
$rootPath = [IO.Path]::GetFullPath($Root)
$required = @(
    '.gitignore', 'LICENSE', 'README.md', 'CONTRIBUTING.md', 'RELEASE_GUIDE.md',
    'VoluLens.sln', 'global.json', 'NuGet.Config',
    'src\VoluLens.App\VoluLens.App.csproj',
    'src\VoluLens.Core\VoluLens.Core.csproj',
    'tests\VoluLens.App.Tests\VoluLens.App.Tests.csproj',
    'tests\VoluLens.Core.Tests\VoluLens.Core.Tests.csproj',
    'src\VoluLens.App\Assets\LUCIDE-LICENSE.txt',
    '.github\workflows\build.yml',
    'docs\screenshots\README.md'
)

$missing = $required | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $rootPath $_))
}
if ($missing) {
    throw "Missing required files: $($missing -join ', ')"
}

$forbiddenDirectoryNames = @(
    '.dotnet', '.packages', '.cache', '.superpowers', 'WebView2Data', 'bin', 'obj', '.vs'
)
$forbiddenDirectories = Get-ChildItem -LiteralPath $rootPath -Directory -Recurse -Force |
    Where-Object { $_.Name -in $forbiddenDirectoryNames }
if ($forbiddenDirectories) {
    throw "Forbidden directories: $($forbiddenDirectories.FullName -join ', ')"
}

$forbiddenFiles = Get-ChildItem -LiteralPath $rootPath -File -Recurse -Force |
    Where-Object {
        $_.Name -like 'storage_analysis*.json' -or
        $_.Name -like 'storage_scan*.json' -or
        $_.Name -like 'storage_report_server*.log'
    }
if ($forbiddenFiles) {
    throw "Forbidden local files: $($forbiddenFiles.FullName -join ', ')"
}

$publicTextExtensions = @(
    '.md', '.txt', '.json', '.xml', '.yml', '.yaml', '.cs', '.csproj', '.sln', '.ps1', '.html', '.xaml'
)
$knownUser = 'Hero' + [char]39 + 'whale'
$shortUser = 'HERO' + [char]39 + 'W~1'
$workspaceName = '电脑内存' + '分析'
$personalPatternParts = @($knownUser, $shortUser, $workspaceName) |
    ForEach-Object { [regex]::Escape($_) }
$personalPattern = $personalPatternParts -join '|'
$personalMatches = Get-ChildItem -LiteralPath $rootPath -File -Recurse -Force |
    Where-Object { $_.Extension -in $publicTextExtensions } |
    Select-String -Pattern $personalPattern
if ($personalMatches) {
    $personalPaths = ($personalMatches.Path | Sort-Object -Unique) -join ', '
    throw "Personal path data found: $personalPaths"
}

if ($RequireRelease) {
    $zipPath = Join-Path $rootPath 'release-assets\VoluLens-win-x64.zip'
    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "Release ZIP is missing: $zipPath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name } |
            ForEach-Object { $_.FullName -replace '\\', '/' })
        $expected = @('Assets/LUCIDE-LICENSE.txt', 'README.txt', 'VoluLens.App.exe')
        if (@(Compare-Object $expected $entries).Count -ne 0) {
            throw "Unexpected ZIP entries: $($entries -join ', ')"
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host 'Open-source package verification passed.'
