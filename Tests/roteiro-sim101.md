# Roteiro de validação — Sim101

Este roteiro valida o PulseGuard em conta simulada. Não use conta real. Siga a sequência e registre resultados em [`../Templates/relatorio-teste-simulador.md`](../Templates/relatorio-teste-simulador.md).

## 1. Preparação e segurança

- [ ] Confirme versão do NinjaTrader e commit PulseGuard.
- [ ] Compile `MACDNG` e `PulseGuardStrategy` no NinjaScript Editor; resolva erros antes de continuar.
- [ ] Confirme visualmente que a conta selecionada é `Sim101` ou ambiente de Playback.
- [ ] Confirme `AllowLiveTrading = false` e `AllowPropSimAccount = false`.
- [ ] Comece com `EnableAutomaticTrading = false` e `ConfirmAutomaticRisk = false`.
- [ ] Registre instrumento, tipo/valor de barras, Trading Hours e fuso configurado.
- [ ] Confirme valor por tick, unidade de `BrickSizeTicks`, número de contratos e exposição monetária potencial.

## 2. Modo somente sinais

- [ ] Adicione a estratégia ao gráfico e aguarde dados suficientes.
- [ ] Confirme carregamento de `MACDNG`, médias, painel e Output sem erros.
- [ ] Confirme que sinais/setas/alertas não submetem ordens enquanto automação estiver desligada.
- [ ] Registre filtros que bloquearam sinais e confirme motivo explícito.

## 3. Validações opcionais

- [ ] Com `RequireInstrument = false` e `RequireRenkoChart = false`, confirme que ativo e série não são bloqueados por validações específicas.
- [ ] Ative `RequireInstrument`, informe um nome de teste e confirme correspondência e bloqueio em caso divergente.
- [ ] Ative `RequireRenkoChart`, configure tipo/valor esperado e confirme correspondência e bloqueio em caso divergente.
- [ ] Restaure os valores que serão usados no restante do teste e anote-os.

## 4. Travas e gestão

- [ ] Confirme o limite de contratos do estágio selecionado e teste uma quantidade acima do limite somente em simulação.
- [ ] Verifique as travas internas de lucro/perda, perdas consecutivas, operações por sessão e cooldown.
- [ ] Se testar DLL/MLL, use valores de simulação documentados; não apresente os valores como regras do broker/mesa.
- [ ] Confira stop de emergência, breakeven, saídas técnicas e gerenciamento de posições long/short.
- [ ] Verifique que posição existente na conta/instrumento impede entrada automática adicional.
- [ ] Teste rejeição de ordem, desconexão/reconexão e bloqueios; confirme mensagens visíveis e ausência de entradas não autorizadas.

## 5. Fuso horário e sessão

- [ ] Compare timestamp da barra, relógio local, fuso de origem e ET no HUD/Output.
- [ ] Confirme janelas e flatten em `America/New_York` (ET), incluindo datas de horário padrão e de verão dos EUA.
- [ ] Verifique o Trading Hours template e a zeragem antes do limite de sessão.
- [ ] Registre horários observados e diferenças inesperadas.

## 6. Teste opcional de ordens

Somente depois de concluir e registrar as etapas anteriores:

- [ ] Reconfirme `Sim101` no seletor de conta antes de habilitar a estratégia.
- [ ] Configure contratos mínimos, stop revisado, limites e buffers para o cenário simulado.
- [ ] Ative `ConfirmAutomaticRisk = true` após revisar os valores.
- [ ] Ative `EnableAutomaticTrading = true` apenas para validar submissão/gestão em Sim101.
- [ ] Mantenha `AllowLiveTrading = false` e `AllowPropSimAccount = false`.
- [ ] Confirme entradas, stop, saídas, travas, flatten e logs no Control Center.
- [ ] Desabilite a estratégia e restaure `EnableAutomaticTrading = false` ao terminar.

## 7. Verificação de versão (informativa)

Execute esta seção apenas em Playback/Sim101 e sem alterar os padrões de segurança. A consulta deve afetar somente o texto exibido/logado, nunca ordens.
O manifesto está publicado na branch padrão atual `feat/versioning`; confirme que o status online é “Atualizado”.

- [ ] Online, manifesto válido e versões iguais: status “Atualizado”.
- [ ] Em ambiente de teste isolado, manifesto com versão superior: status “Atualização disponível” e orientação para consultar Releases.
- [ ] Sem internet: status “Não verificado”; estratégia e gestão continuam funcionando normalmente.
- [ ] Manifesto malformado ou com campos obrigatórios ausentes: status “Não verificado”, sem exceção propagada.
- [ ] `CheckForUpdates = false`: status “Desligado” e nenhuma requisição de rede.
- [ ] Adicione estratégia e indicador juntos: confirmar consulta compartilhada/cache sem requisições repetidas.
- [ ] Reabra a workspace/gráfico: confirmar uma checagem por carregamento e sem repetição por barra/tick.
- [ ] Inspecione o dashboard em temas claro e escuro, com `ShowVersionPanel` ligado e desligado.
- [ ] Compare ordens, horários e latência antes/depois; nenhuma ordem pode ser criada, removida ou atrasada pela checagem.
- [ ] Informe manualmente `TemplateVersion`; confirme que o painel não afirma detectar o template automaticamente.

## Critérios de encerramento

- [ ] Nenhum erro de compilação ou estado inesperado sem triagem.
- [ ] Limites, stop e gestão atuaram conforme configurados.
- [ ] Conversão para ET manteve comportamento coerente com o fuso de origem.
- [ ] Relatório preenchido com resultados observados, evidências sanitizadas e limitações.

Backtests e simulações não garantem resultados futuros nem execução/preço em conta real. Stops e travas não protegem contra todas as falhas de conexão, liquidez ou plataforma.
