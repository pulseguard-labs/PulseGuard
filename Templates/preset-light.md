# Modelo de preset — PulseGuard

> Modelo para documentar uma configuração. Não é uma configuração importável nem recomendação financeira. Preencha e valide cada campo para o cenário pretendido.

## Identificação

- **Nome do preset:** `preset-template-60R-light-v2`
- **Objetivo e público:** Template visual claro para gráfico Renko 60 no NinjaTrader 8; uso somente em Playback/Sim101.
- **Versão/commit do PulseGuard:** A validar
- **Versão NinjaTrader 8:**
- **Responsável e data:** A validar / 2026-11-25

## Contexto de teste

- **Ambiente:** Replay / Sim101 / outro simulador:
- **Instrumento (se aplicável):** MNQ DEC26
- **Tipo e valor de barras:** Renko (`BarsPeriodTypeSerialize=11`), valor 60; base Minute 1.
- **Trading Hours template:** Não especificado (`TradingHoursSerializable` vazio).
- **Fuso configurado no NinjaTrader:** A validar.
- **Validações opcionais de instrumento/série:** desligadas / ligadas (descrever)

## Parâmetros

| Propriedade | Valor | Justificativa / unidade |
|---|---:|---|
| `EnableAutomaticTrading` | `false` inicialmente | Só alterar para um teste de ordens em simulador após revisar controles. |
| `ConfirmAutomaticRisk` | `false` inicialmente | Confirmar apenas depois de revisar limites. |
| `AllowLiveTrading` | `false` | Não habilitar em preset de teste. |
| `Contracts` | `1` | Quantidade do Chart Trader; não alterada. |
| `EmergencyStopTicks` |  | Validar valor monetário por tick no instrumento. |
| `BreakevenAfterBricks` |  | Confirmar a unidade usada por `BrickSizeTicks`. |
| `MaxDailyProfit` / `MaxDailyLoss` |  | |
| `DailyLossLimitEnabled` / `DailyLossLimitAmount` / `DailyLossSafetyBuffer` |  | Informar a origem dos limites de teste. |
| `MaximumLossLimitEnabled` / `MllRemainingRoom` / `MllSafetyBuffer` |  | Preencher apenas para cenário conhecido. |
| `RequireInstrument` / `RequiredInstrumentName` | `false` /  | |
| `RequireRenkoChart` / `ExpectedRenkoValue` | `false` /  | |
| `BrickSizeTicks` |  | Unidade independente do tipo/valor da série. |
| Horários de entrada/zeragem |  | Registrar em ET. |

### Propriedades do template de gráfico

| Propriedade | Valor |
|---|---|
| ATM | `100-200` |
| TIF | `Day` |
| Escala Y | `Automatic`; margens 6/6; limites fixos neutralizados para `0` |
| Janela histórica | `DaysBack=3`; `From` vazio |
| Time highlighter ativo | 14:30–17:00 no fuso do gráfico — A validar (não assumir ET/local) |
| Indicadores | Nenhum (`<Indicators />`) |

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
