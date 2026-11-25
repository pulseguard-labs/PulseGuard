# Referência de teste conservador — Sim101

Este é um roteiro de configuração para **simulação isolada**, não um preset importável nem recomendação financeira. Os valores devem ser conferidos na tela de propriedades. Não aplique em conta real.

## Configuração de simulação

| Propriedade | Valor de referência | Observação |
|---|---:|---|
| Conta | `Sim101` | Confirme no seletor que o gráfico usa a conta simulada. |
| `EnableAutomaticTrading` | `true`, somente durante o teste de ordens | Primeiro valide os sinais com `false`; habilite depois em Sim101. |
| `ConfirmAutomaticRisk` | `true` | Confirme somente após revisar todas as configurações de risco deste teste. |
| `AllowLiveTrading` | `false` | Não habilitar neste procedimento. |
| `AllowPropSimAccount` | `false` | Não necessário para `Sim101`. |
| `AccountStageSelected` | estágio correto para o limite de contratos | É uma seleção manual; mantenha o máximo correspondente em `1`. |
| `Contracts` | `1` | Não aumente para tentar compensar um stop. |
| `EmergencyStopTicks` | `32` (padrão atual) | Stop técnico curto relativo ao perfil original; reavalie a distância e o valor por tick do instrumento simulado. |
| `BreakevenAfterBricks` | `2` (padrão atual) | `BrickSizeTicks` também afeta essa distância; confirme a série/unidade antes do teste. |
| `MaxDailyProfit` / `MaxDailyLoss` | `$300` / `$150` (padrões atuais) | Travas internas da estratégia. |
| `MaxConsecutiveLosses` / `MaxTradesPerSession` | `2` / `8` (padrões atuais) | Travas adicionais por sessão. |
| `DailyLossLimitEnabled` / `DailyLossLimitAmount` / `DailyLossSafetyBuffer` | `true` / `$100` / `$20` para este exemplo | Valores didáticos do cenário Sim101; ajuste-os ao teste, não representam regras de broker/mesa. |
| `MaximumLossLimitEnabled` | `false`, salvo cenário de teste explicitamente definido | Se ativar, informe `MllRemainingRoom` e buffer; esses dados não são consultados automaticamente. |
| `RequireInstrument` / `RequireRenkoChart` | `false` por padrão | Ative apenas se o cenário exigir uma série/ativo específico e configure os valores. |
| `FlattenTime` | `16:40 ET` (padrão atual) | Confira calendário, horário da sessão e fuso do NinjaTrader. |

`EnableAutomaticTrading = true` aparece apenas como etapa opcional de teste em Sim101 depois de validar o modo de sinais. Se não for necessário testar submissão/gestão de ordens, mantenha `false`. O stop de 32 ticks não tem risco monetário universal: calcule a exposição com o valor por tick do instrumento, contratos, custos e slippage antes de simular.

## Sequência recomendada

1. Compile no NinjaScript Editor e selecione `Sim101`; confira visualmente a conta antes de habilitar a estratégia.
2. Comece com automação desligada. Confirme sinais, filtros, horário ET, limites e logs.
3. Defina um valor de DLL de teste, se for validar essa proteção. O valor informado não representa a regra da conta.
4. Só então, se precisar testar ordens, ative automação e confirmação de risco exclusivamente em Sim101, com um contrato e stop definido.
5. Verifique ordem de entrada, stop, breakeven, travas, flatten, rejeições e saída após desconexão/reconexão.
6. Ao concluir, desabilite a estratégia e restaure `EnableAutomaticTrading = false`.

Replay e Sim101 não garantem o mesmo comportamento de uma conta real e não demonstram rentabilidade. Não remova travas para fazer um teste passar.
