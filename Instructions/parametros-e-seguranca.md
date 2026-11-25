# PulseGuard — parâmetros e segurança

Este documento descreve os controles de segurança e as opções específicas do gráfico. Os defaults não substituem a revisão dos riscos, dados, regras do broker ou limitações da plataforma.

## Modos e autorização

- `EnableAutomaticTrading = false`: padrão de somente sinais; não submete novas entradas.
- `ConfirmAutomaticRisk = false`: a automação também exige confirmação explícita de que os limites foram revisados.
- `AllowLiveTrading = false`: mantém a autorização de conta real desligada.
- `AllowPropSimAccount = false`: mantém desligada a exceção de conta simulada de mesa cujo nome não começa com `Sim`.
- Comece por Replay ou `Sim101`. Não habilite live para testar alterações.

## Travas de risco padrão

- `Contracts = 1`; limites de contratos por estágio começam em um contrato.
- Stop de emergência: `EmergencyStopTicks = 32`; breakeven após `BreakevenAfterBricks = 2`.
- Limites internos por sessão: lucro `$300`, perda `$150`, até duas perdas consecutivas e oito operações. `CooldownBarsAfterLoss = 3`.
- `DailyLossLimitEnabled = false` e `MaximumLossLimitEnabled = false`. São controles externos configuráveis e não substituem o limite interno de perda. O DLL/MLL informado pelo usuário não é consultado automaticamente.
- `IsExitOnSessionCloseStrategy` continua ativo. `FlattenTime = 16:40 ET`; valide o template de sessão e o horário limite aplicável.
- A automação é independente das travas: o padrão segue desligado mesmo que limites estejam preenchidos.

Esses valores refletem a implementação atual, não um dimensionamento recomendado para uma conta. O mesmo número de ticks pode representar riscos monetários muito diferentes entre instrumentos. Ajuste após calcular valor por tick, slippage, custos, tamanho de posição e condições de mercado; não aumente limites para contornar bloqueios.

## Ativo e série de barras

- `RequireInstrument = false` por padrão. Para habilitar, informe `RequiredInstrumentName`; a comparação usa o nome do instrumento principal.
- `RequireRenkoChart = false` por padrão. Quando ativada, a estratégia exige um tipo cujo nome contenha `Renko` e um `BarsPeriod.Value` igual a `ExpectedRenkoValue`.
- `ExpectedRenkoValue = 60` é apenas um valor configurável; não define nem transforma a série.
- `BrickSizeTicks = 60` é usado em cálculos internos de barreira e breakeven, independentemente da série. Confirme se seus ticks representam a unidade pretendida no ativo atual. Use qualquer outro ativo/série somente após revalidar filtros, stop e gestão.

## Tempo e sessões

As janelas de operação e zeragem são comparadas em horário de Nova Iorque (ET), com as regras sazonais correspondentes a `America/New_York`. No NinjaTrader, a conversão parte do fuso configurado em General Options (`GeneralOptions.TimeZoneInfo`) e usa o fuso Windows `Eastern Standard Time`. Não altere nem remova essa conversão ao mudar o ativo.

Confirme o horário da barra e o ET no HUD/Output, o Trading Hours template e a transição de horário de verão. `EntryStartTime`, `EntryEndTime`, `FlattenTime` e janelas de liquidez continuam valores de relógio em ET.

## Gestão de conta e ordens

- Selecione manualmente `AccountStageSelected` conforme a conta; a plataforma não infere o estágio da mesa.
- Os limites por estágio são configuráveis. Confirme-os diretamente com broker/mesa.
- O controle de limite diário externo usa parâmetros informados; não protege contra posições externas, saques ou regras não modeladas.
- Uma posição já existente na conta/instrumento impede nova entrada automática, para não misturar a gestão da estratégia com ordens manuais/ATM.
- Stop de emergência, flatten e travas dependem de dados, conexão, liquidez e execução. Não são garantia de preço, preenchimento ou proteção durante falhas.
- Revise CSV/Output após testes e não publique logs que revelem números de conta ou dados pessoais.

## Revisão de mudanças

Toda alteração que afete envio de ordens, stop, limites de conta, gestão de posição ou cálculo de horário deve incluir justificativa, revisão explícita de segurança e teste em Replay/Sim101. Compare os defaults antes/depois e mantenha a estratégia sem automação por padrão.

## Verificação informativa de versões

- `CheckForUpdates = true` habilita consultas assíncronas ao manifesto público. Na estratégia, `false` desliga as consultas do robô, do `MACDNG` incluído e do template; no indicador usado sozinho, a propriedade controla a própria consulta. Falhas de conexão resultam em “Não verificado” e não bloqueiam a estratégia.
- `ShowVersionPanel = true` exibe versões e status no dashboard da estratégia. Desligar essa opção oculta somente as linhas de versão.
- `TemplateVersion` deve ser preenchida manualmente com a versão do template aplicado. O NinjaTrader não oferece detecção segura automática a partir do XML atual.
- A consulta lê somente texto de `version.json`, usa TLS 1.2 e cache de 15 minutos; não baixa, instala, executa ou compila código e não altera as regras de ordens.
- Os logs registram somente o resultado resumido. Não inclua credenciais ou dados pessoais no manifesto, código ou relatórios.
