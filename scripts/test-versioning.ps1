$ErrorActionPreference = 'Stop'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath $framework)) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$references = @(
    (Join-Path $framework 'mscorlib.dll'),
    (Join-Path $framework 'System.dll'),
    (Join-Path $framework 'System.Core.dll'),
    (Join-Path $framework 'System.Net.Http.dll'),
    (Join-Path $framework 'System.Web.Extensions.dll')
)
$helper = Join-Path (Split-Path -Parent $PSScriptRoot) 'Indicator\PulseGuardUpdateChecker.cs'

foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) {
        throw "Assembly do .NET Framework não encontrada: $reference"
    }
}

Add-Type -Path $helper -ReferencedAssemblies $references
$checker = [NinjaTrader.NinjaScript.PulseGuardUpdateChecker]

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Label) {
    if ($Expected -ne $Actual) {
        throw "$Label`: esperado '$Expected', recebido '$Actual'."
    }
}

function Assert-Throws([scriptblock]$Action, [string]$Label) {
    $threw = $false
    try {
        & $Action
    } catch {
        $threw = $true
    }
    if (-not $threw) {
        throw "$Label`: era esperada uma exceção."
    }
}

Assert-Equal 1 ($checker::CompareSemVer('1.10.0', '1.9.0')) 'ordem numérica SemVer'
Assert-Equal -1 ($checker::CompareSemVer('1.0.0-rc.1', '1.0.0')) 'pré-lançamento antes da versão final'
Assert-Equal -1 ($checker::CompareSemVer('1.0.0-alpha.2', '1.0.0-alpha.10')) 'identificadores numéricos de pré-lançamento'
Assert-Equal 0 ($checker::CompareSemVer('1.0.0+build.1', '1.0.0+build.2')) 'metadados de build não alteram precedência'
Assert-Throws { $checker::CompareSemVer('01.0.0', '1.0.0') } 'SemVer com zero à esquerda'

$validManifest = @'
{
  "schema": 1,
  "packageVersion": "0.1.0",
  "components": {
    "indicator": { "version": "0.1.0", "minRequired": {} },
    "strategy": { "version": "0.1.0", "minRequired": { "indicator": "0.1.0" } },
    "template": { "version": "0.1.0", "minRequired": {} }
  },
  "releaseNotesUrl": "https://github.com/pulseguard-labs/PulseGuard/releases",
  "published": "2026-11-25"
}
'@
$manifest = $checker::ParseManifest($validManifest)
Assert-Equal 1 $manifest.Schema 'schema do manifesto'
Assert-Equal '0.1.0' $manifest.PackageVersion 'versão do pacote no manifesto'
Assert-Equal '0.1.0' $manifest.Components['strategy'].Version 'versão da estratégia no manifesto'
Assert-Equal '0.1.0' $manifest.Components['strategy'].MinRequired['indicator'] 'requisito mínimo do indicador'
Assert-Equal 'Atualizado' ([NinjaTrader.NinjaScript.PulseGuardUpdateChecker]::CheckAgainstManifest(
    $manifest, [NinjaTrader.NinjaScript.PulseGuardComponent]::Strategy, '0.1.0', '0.1.0').Status.ToString()) 'status atualizado'

$updateManifest = $checker::ParseManifest($validManifest.Replace(
    '"strategy": { "version": "0.1.0"', '"strategy": { "version": "0.2.0"'))
Assert-Equal 'AtualizacaoDisponivel' ([NinjaTrader.NinjaScript.PulseGuardUpdateChecker]::CheckAgainstManifest(
    $updateManifest, [NinjaTrader.NinjaScript.PulseGuardComponent]::Strategy, '0.1.0', '0.1.0').Status.ToString()) 'status de atualização disponível'

$incompatibleManifest = $checker::ParseManifest($validManifest.Replace(
    '"indicator": "0.1.0" } },', '"indicator": "0.2.0" } },'))
Assert-Equal 'Incompativel' ([NinjaTrader.NinjaScript.PulseGuardUpdateChecker]::CheckAgainstManifest(
    $incompatibleManifest, [NinjaTrader.NinjaScript.PulseGuardComponent]::Strategy, '0.1.0', '0.1.0').Status.ToString()) 'compatibilidade mínima do indicador'
Assert-Equal 'NaoVerificado' ([NinjaTrader.NinjaScript.PulseGuardUpdateChecker]::CheckAgainstManifest(
    $manifest, [NinjaTrader.NinjaScript.PulseGuardComponent]::Template, 'informado manualmente', '0.1.0').Status.ToString()) 'versão do template não informada'
$disabled = [NinjaTrader.NinjaScript.PulseGuardUpdateChecker]::CheckAsync(
    [NinjaTrader.NinjaScript.PulseGuardComponent]::Strategy, '0.1.0', '0.1.0', $false
).GetAwaiter().GetResult()
Assert-Equal 'Desligado' $disabled.Status.ToString() 'checagem desligada'

Assert-Throws { $checker::ParseManifest('{ invalid json') } 'JSON malformado'
Assert-Throws {
    $checker::ParseManifest('{"schema":1,"components":{},"releaseNotesUrl":"https://example.com","published":"2026-11-25"}')
} 'componente obrigatório ausente'
Assert-Throws {
    $checker::ParseManifest($validManifest.Replace('"schema": 1', '"schema": 2'))
} 'schema não suportado'

Write-Output 'Testes aprovados: comparação SemVer e parser do manifesto.'
