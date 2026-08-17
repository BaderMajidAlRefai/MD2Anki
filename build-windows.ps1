<#
    Builds MD2Anki for Windows.

    Produces a self-contained folder in dist/MD2Anki-win-x64 containing the
    Avalonia front end plus the PyInstaller-packaged FastAPI backend under
    backend/. The .NET publish picks the backend up automatically through the
    Content item in ui/ui.csproj, so the backend must be built first.
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [switch]$SkipBackend
)

$ErrorActionPreference = 'Stop'

# pip, PyInstaller and dotnet all write progress to stderr, which Windows
# PowerShell turns into a terminating error while ErrorActionPreference is
# Stop. Run them with it relaxed and judge success by the exit code instead.
function Invoke-Tool {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ToolArguments = @()
    )

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $FilePath @ToolArguments
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE"
    }
}

$root = $PSScriptRoot
$venv = Join-Path $root '.venv'
$venvPython = Join-Path $venv 'Scripts\python.exe'
$output = Join-Path $root "dist\MD2Anki-$Runtime"

if (-not $SkipBackend) {
    if (-not (Test-Path $venvPython)) {
        Write-Host '==> Creating virtual environment'
        Invoke-Tool 'python' @('-m', 'venv', $venv)
    }

    Write-Host '==> Installing backend build dependencies'
    Invoke-Tool $venvPython @('-m', 'pip', 'install', '--upgrade', 'pip')
    Invoke-Tool $venvPython @('-m', 'pip', 'install', '-r', (Join-Path $root 'requirements-build.txt'))

    Write-Host '==> Building backend (PyInstaller)'
    Invoke-Tool $venvPython @(
        '-m', 'PyInstaller', '--noconfirm',
        '--distpath', (Join-Path $root 'dist'),
        '--workpath', (Join-Path $root 'build'),
        (Join-Path $root 'Obsidian2Anki.Backend.spec')
    )
}

$backend = Join-Path $root 'dist\Obsidian2Anki.Backend'
if (-not (Test-Path $backend)) {
    throw "Backend output not found at $backend. Run without -SkipBackend first."
}

Write-Host '==> Publishing front end (dotnet)'
if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

Invoke-Tool 'dotnet' @(
    'publish', (Join-Path $root 'ui\ui.csproj'),
    '--configuration', 'Release',
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $output
)

Write-Host ''
Write-Host "==> Build complete: $output"
