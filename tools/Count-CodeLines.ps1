param(
    [string]$SourceRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'ShadowGuard')
)

$includeExtensions = @('.cs', '.xaml')
$excludeSegments = @('\obj\', '\bin\')
$results = New-Object System.Collections.Generic.List[object]
$total = 0

function Get-CodeLineCount {
    param([string]$Path)

    $count = 0
    $inBlockComment = $false
    $inXmlComment = $false

    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()

        if ($inBlockComment) {
            if ($trimmed.Contains('*/')) {
                $inBlockComment = $false
            }
            continue
        }

        if ($inXmlComment) {
            if ($trimmed.Contains('-->')) {
                $inXmlComment = $false
            }
            continue
        }

        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($trimmed.StartsWith('//')) {
            continue
        }

        if ($trimmed.StartsWith('/*')) {
            if (-not $trimmed.Contains('*/')) {
                $inBlockComment = $true
            }
            continue
        }

        if ($trimmed.StartsWith('<!--')) {
            if (-not $trimmed.Contains('-->')) {
                $inXmlComment = $true
            }
            continue
        }

        if ($trimmed.StartsWith('*') -or $trimmed.StartsWith('*/')) {
            continue
        }

        $count++
    }

    return $count
}

function Test-IsExcludedPath {
    param([string]$Path)

    foreach ($segment in $excludeSegments) {
        if ($Path.Contains($segment)) {
            return $true
        }
    }

    return $false
}

Get-ChildItem -Path $SourceRoot -Recurse -File |
    Where-Object {
        ($includeExtensions -contains $_.Extension) -and -not (Test-IsExcludedPath -Path $_.FullName)
    } |
    Sort-Object FullName |
    ForEach-Object {
        $count = Get-CodeLineCount -Path $_.FullName
        $relativePath = Resolve-Path -Relative $_.FullName
        $total += $count
        $results.Add([PSCustomObject]@{
            File = $relativePath
            CodeLines = $count
        }) | Out-Null
    }

$results | Format-Table -AutoSize
Write-Host ('TOTAL=' + $total)
