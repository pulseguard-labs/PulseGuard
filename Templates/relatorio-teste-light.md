# Relatório de teste — PulseGuard

> Preencha com resultados observados. Não invente medições. Use somente Playback/Sim101; remova dados de conta e informações proprietárias antes de compartilhar.

## Identificação do teste

- **Data/hora e responsável:**
- **Versão/commit PulseGuard:**
- **Versão NinjaTrader 8:**
- **Ambiente:** Playback / Sim101 / outro simulador:
- **Instrumento:**
- **Tipo/valor de barras:**
- **Trading Hours template:**
- **Fuso configurado no NinjaTrader:**

## Configuração sob teste

- **Preset ou parâmetros relevantes:** `preset-template-60R-light-v0.1.0` — pendente de teste manual.
- **`EnableAutomaticTrading`:** `false`
- **`ConfirmAutomaticRisk`:**
- **`AllowLiveTrading`:** `false`
- **Validações de instrumento e barras:** desligadas / ligadas
- **Contratos e limite:**
- **Stop e breakeven:**
- **Travas de lucro/perda e buffers:**
- **Horários de entrada/flatten em ET:**

## Casos executados

| Caso | Esperado | Observado | Resultado |
|---|---|---|---|
| Compilação do indicador e estratégia | Sem erros no NinjaScript Editor | | Passou / Falhou / N/A |
| Modo somente sinais | Sinais sem novas ordens | | Passou / Falhou |
| Instrumento/série compatíveis ou validações desligadas | Sem bloqueio indevido | | Passou / Falhou |
| Instrumento/série incompatíveis com validação ligada | Bloqueio com motivo explícito | | Passou / Falhou / N/A |
| Limite de contratos e travas diárias | Entradas bloqueadas no limite | | Passou / Falhou |
| Stop, breakeven e gestão da posição | Ordens/saídas conforme a configuração | | Passou / Falhou |
| Horário de sessão, ET e flatten | Comparações coerentes com ET | | Passou / Falhou |
| Rejeição/desconexão/recarregamento | Erro visível e comportamento seguro | | Passou / Falhou / N/A |

## Evidências e divergências

- **Resumo de sinais/ordens observados:**
- **Resumo de logs (sanitizados):**
- **Captura de gráfico anexada:** sim / não
- **Divergências, bloqueios inesperados ou erros:**
- **Reprodução mínima:**

## Conclusão

- **Resultado geral:** Aprovado para continuar testes / Reprovado / Inconclusivo
- **Riscos ou limitações pendentes:**
- **Próxima ação:**

Este relatório registra apenas um teste histórico ou simulado. Não demonstra rentabilidade, execução futura ou proteção garantida em uma conta real.
