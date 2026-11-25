$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'version.json'
$versionSourcePath = Join-Path $root 'Indicator\PulseGuardVersion.cs'
$changelogPath = Join-Path $root 'CHANGELOG.md'
$semVerPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Manifesto não encontrado: $manifestPath"
}
if (-not (Test-Path -LiteralPath $versionSourcePath)) {
    throw "Fonte central de versões não encontrada: $versionSourcePath"
}
if (-not (Test-Path -LiteralPath $changelogPath)) {
    throw "CHANGELOG não encontrado: $changelogPath"
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    throw "version.json não é JSON válido: $($_.Exception.Message)"
}

if ($manifest.schema -ne 1) {
    throw "Schema não suportado em version.json: $($manifest.schema)"
}
if ($manifest.releaseNotesUrl -notmatch '^https://') {
    throw 'releaseNotesUrl deve ser uma URL HTTPS.'
}
try {
    [void][DateTime]::ParseExact(
        [string]$manifest.published,
        'yyyy-MM-dd',
        [System.Globalization.CultureInfo]::InvariantCulture
    )
} catch {
    throw 'published deve conter uma data válida no formato YYYY-MM-DD.'
}

$source = Get-Content -LiteralPath $versionSourcePath -Raw
$packageVersion = [string]$manifest.packageVersion
if ($packageVersion -notmatch $semVerPattern) {
    throw "Versão SemVer inválida em version.json: packageVersion = $packageVersion"
}
$packageConstant = [regex]::Match(
    $source,
    'public\s+const\s+string\s+Package\s*=\s*"([^"]+)"\s*;'
)
if (-not $packageConstant.Success -or $packageConstant.Groups[1].Value -ne $packageVersion) {
    throw "Divergência de versão do pacote entre PulseGuardVersion.cs e version.json."
}

foreach ($component in @('indicator', 'strategy', 'template')) {
    $property = $manifest.components.$component
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace($property.version)) {
        throw "Versão ausente em version.json: components.$component.version"
    }
    if ($property.version -notmatch $semVerPattern) {
        throw "Versão SemVer inválida em version.json: components.$component.version = $($property.version)"
    }

    $constantName = (Get-Culture).TextInfo.ToTitleCase($component)
    $constantPattern = 'public\s+const\s+string\s+' + [regex]::Escape($constantName) + '\s*=\s*"([^"]+)"\s*;'
    $match = [regex]::Match($source, $constantPattern)
    if (-not $match.Success) {
        throw "Constante não encontrada em PulseGuardVersion.cs: $constantName"
    }
    if ($match.Groups[1].Value -ne $property.version) {
        throw "Divergência de versão para $component`: C#=$($match.Groups[1].Value), JSON=$($property.version)"
    }

    if ($property.minRequired) {
        foreach ($required in $property.minRequired.PSObject.Properties) {
            if ($required.Value -notmatch $semVerPattern) {
                throw "Versão mínima SemVer inválida: components.$component.minRequired.$($required.Name)"
            }
        }
    }
}

$changelog = Get-Content -LiteralPath $changelogPath -Raw
$topEntry = [regex]::Match($changelog, '(?m)^## \[([^\]]+)\] - (\d{4}-\d{2}-\d{2})\s*$')
if (-not $topEntry.Success) {
    throw 'A primeira entrada do CHANGELOG deve seguir o formato "## [VERSÃO] - YYYY-MM-DD".'
}

$packageVersion = $topEntry.Groups[1].Value
if ($packageVersion -notmatch $semVerPattern) {
    throw "Versão SemVer inválida no topo do CHANGELOG: $packageVersion"
}
if ($packageVersion -ne $manifest.packageVersion) {
    throw "Divergência entre CHANGELOG ($packageVersion) e version.json ($($manifest.packageVersion))."
}
if ($topEntry.Groups[2].Value -ne $manifest.published) {
    throw "Divergência de data entre CHANGELOG ($($topEntry.Groups[2].Value)) e version.json ($($manifest.published))."
}

Write-Output 'Consistência aprovada: version.json, constantes C# e topo do CHANGELOG.'
