param(
    [string]$PublishDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\publish')
)

$requiredPaths = @(
    'ShadowGuard.exe',
    'shadowguard-cli.exe',
    'samples',
    'plugins',
    'docs\README.md',
    'docs\LICENSE',
    'docs\CHANGELOG.md',
    'docs\THIRD_PARTY_NOTICES.md',
    'docs\SECURITY.md'
)

$missing = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $requiredPaths) {
    $path = Join-Path $PublishDir $relativePath
    if (-not (Test-Path $path)) {
        $missing.Add($relativePath) | Out-Null
    }
}

if ($missing.Count -gt 0) {
    Write-Error ('Release payload is missing required files: ' + ($missing -join ', '))
    exit 1
}

Write-Host 'Release payload verification passed.'
foreach ($relativePath in $requiredPaths) {
    Write-Host ('OK: ' + $relativePath)
}
