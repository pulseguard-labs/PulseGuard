# Modelo de preset — PulseGuard

> Modelo para documentar uma configuração. Não é uma configuração importável nem recomendação financeira. Preencha e valide cada campo para o cenário pretendido.

## Identificação

- **Nome do preset:**
- **Objetivo e público:**
- **Versão/commit do PulseGuard:**
- **Versão NinjaTrader 8:**
- **Responsável e data:**

## Contexto de teste

- **Ambiente:** Replay / Sim101 / outro simulador:
- **Instrumento (se aplicável):**
- **Tipo e valor de barras:**
- **Trading Hours template:**
- **Fuso configurado no NinjaTrader:**
- **Validações opcionais de instrumento/série:** desligadas / ligadas (descrever)

## Parâmetros

| Propriedade | Valor | Justificativa / unidade |
|---|---:|---|
| `EnableAutomaticTrading` | `false` inicialmente | Só alterar para um teste de ordens em simulador após revisar controles. |
| `ConfirmAutomaticRisk` | `false` inicialmente | Confirmar apenas depois de revisar limites. |
| `AllowLiveTrading` | `false` | Não habilitar em preset de teste. |
| `Contracts` |  | |
| `EmergencyStopTicks` |  | Validar valor monetário por tick no instrumento. |
| `BreakevenAfterBricks` |  | Confirmar a unidade usada por `BrickSizeTicks`. |
| `MaxDailyProfit` / `MaxDailyLoss` |  | |
| `DailyLossLimitEnabled` / `DailyLossLimitAmount` / `DailyLossSafetyBuffer` |  | Informar a origem dos limites de teste. |
| `MaximumLossLimitEnabled` / `MllRemainingRoom` / `MllSafetyBuffer` |  | Preencher apenas para cenário conhecido. |
| `RequireInstrument` / `RequiredInstrumentName` | `false` /  | |
| `RequireRenkoChart` / `ExpectedRenkoValue` | `false` /  | |
| `BrickSizeTicks` |  | Unidade independente do tipo/valor da série. |
| Horários de entrada/zeragem |  | Registrar em ET. |

## Risco e compatibilidade

- **Como foi calculado o risco monetário por contrato:**
- **Limite de posição/trades e travas ativas:**
- **Comportamento de stop, breakeven e flatten esperado:**
- **Dependências de ativo, série, feed ou broker:**
- **Como validar ET e horário de verão:**
- **Limitações conhecidas:**

## Validação

- [ ] Compilado no NinjaScript Editor, sem erros.
- [ ] Validado em Playback/Sim101; nenhuma conta real utilizada.
- [ ] Sinais e ordens confirmados nos modos relevantes.
- [ ] Limites, stop, gestão e flatten exercitados.
- [ ] Instrumento, série, sessão e conversão ET verificados.
- [ ] Resultado registrado em [`relatorio-teste-simulador.md`](relatorio-teste-simulador.md).

Não publique presets com credenciais, números de conta, dados pessoais ou configurações que habilitem live trading.
