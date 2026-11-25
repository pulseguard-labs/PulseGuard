# PulseGuard — instalação e testes

Instruções para instalar `MACDNG` e `PulseGuardStrategy` no NinjaTrader 8 (NT8). Os fontes deste repositório não incluem DLLs nem exports compilados.

## Instalação

1. Desabilite a estratégia nos gráficos e faça backup de qualquer script existente que será atualizado.
2. Copie `Indicator/MACDNG.cs` para `Documents\NinjaTrader 8\bin\Custom\Indicators\MACDNG.cs`.
3. Copie `Strategy/PulseGuardStrategy.cs` para `Documents\NinjaTrader 8\bin\Custom\Strategies\PulseGuardStrategy.cs`.
4. Se a instalação já tiver `MACDNG`, atualize a implementação existente em vez de criar outra. Deve haver apenas uma classe `MACDNG` em `NinjaTrader.NinjaScript.Indicators`.
5. Arquive a cópia anterior da estratégia após fazer backup e confirmar que nenhuma workspace ou automação ainda depende dela.
6. Abra **New > NinjaScript Editor**, compile todos os scripts e confirme que não há erros. A compilação NinjaScript pode gerar assinaturas/wrappers que não fazem parte do fonte principal do indicador.
7. Só depois de compilar sem erros, adicione `PulseGuardStrategy` a um gráfico de simulação e confira o painel, indicador, médias e Output.

O caminho de dados pode variar conforme a instalação. Use o diretório `bin\Custom` da instalação ativa do NinjaTrader.

## Configuração inicial

- Comece em `Sim101` ou Market Replay e mantenha `EnableAutomaticTrading = false`.
- `RequireInstrument = false` e `RequireRenkoChart = false` são os padrões. Assim, a estratégia não impõe um ativo, tipo de barra ou valor de série específico. Se habilitadas, essas opções validam o nome configurado e o tipo/valor informados pelo usuário.
- `BrickSizeTicks` participa dos cálculos internos de barreira, espaço e breakeven. Desabilitar as validações não altera nem converte esse parâmetro. Confira a unidade e o valor monetário por tick do instrumento.
- Confira `Trading Hours`, fuso configurado no NinjaTrader, contratos, estágio de conta, limites e os horários antes de cada teste.
- Configure os limites externos informados pelo broker/mesa de forma independente. O código não consulta suas regras nem saldo automaticamente.

Parâmetros e travas estão descritos em [parâmetros e segurança](parametros-e-seguranca.md). Uma referência não operacional para Sim101 está em [exemplo conservador de simulador](../Presets/exemplo-simulador-conservador.md).

## Plano de validação

1. **Compilação:** compile indicador e estratégia juntos no NinjaScript Editor e resolva todos os erros.
2. **Modo de sinais:** automação desativada; confirme que setas/alertas aparecem sem novas ordens.
3. **Validação opcional:** teste com `RequireInstrument` e `RequireRenkoChart` desligados; depois habilite-os e confirme bloqueio explícito quando o nome do ativo ou série não corresponder.
4. **Risco:** em simulação, teste limite de contratos, perda/lucro diário, perdas consecutivas, stop de emergência, breakeven, cooldown, rejeição de ordens e bloqueios de conta.
5. **Gestão:** teste posições long/short, saídas técnicas, posição já existente na conta, zeragem pelo horário e encerramento de sessão.
6. **Fuso:** compare horário da barra, relógio local, Output e ET. Repita em datas representativas de horário padrão e horário de verão dos EUA.
7. **Estabilidade:** recarregue dados, alterne gráficos e examine o Log/Trace antes de habilitar qualquer automação.

Registre instrumento, tipo/valor de barra, Trading Hours, fuso e versão NT8 utilizados no teste. Não use conta real para validar mudanças.

## Cópia instalada do NinjaTrader

Não sincronize arquivos enquanto a compilação não estiver limpa. Faça backup fora de `bin\Custom`, atualize a única cópia de cada classe, compile novamente e verifique se não restaram classes/arquivos duplicados. Não remova uma estratégia antiga até confirmar que layouts e automações não dependem dela.

## Limitações

A compilação no editor NinjaScript e o comportamento em Replay/Sim101 dependem da instalação local, feed e dados disponíveis; uma compilação externa não substitui essa validação. Resultados de backtest/simulação não garantem resultados futuros.
