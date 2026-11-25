# Auditoria — template Renko 60

> Auditoria estática do arquivo `preset-template-60R-light.xml`, sem execução no NinjaTrader 8. Valores e fusos que dependem da instalação permanecem a validar manualmente.

| Elemento | Valor atual | Problema | Ação proposta | Risco |
|---|---|---|---|---|
| `FixedScaleMaxSerialize` / `FixedScaleMinSerialize` (escala direita) | `26288.25` / `26048.25` | Valores são de uma sessão/preço específico, embora `YAxisRangeType` seja `Automatic`. | Zerar os dois valores, preservando as tags e a escala automática. | Baixo; requer teste visual. |
| `<From>` / `<DaysBack>` | `2026-02-07T00:00:00` / `3` | A data inicial fixa não é reutilizável entre sessões; `DaysBack` já define a janela. | Manter `DaysBack=3` e neutralizar `From` com o padrão vazio já usado no arquivo (`<From />`). | Baixo, mas o comportamento de carregamento deve ser validado no NT8. |
| `<BarsSeriesId>` / `<Id>` | GUIDs específicos | Identificadores exportados de uma instância de gráfico. | Manter: não há evidência no XML de um valor genérico aceito pelo schema. | Médio se alterado sem export real. |
| `<Instrument>` / `<Label>` | `MNQ DEC26` / `MNQ 12-26` | O template fica vinculado ao contrato e o rótulo usa outra convenção. | Manter o instrumento para não inventar enum/valor; alinhar o `Label` a `MNQ DEC26`. Para rollover, selecionar o contrato no NT8 após aplicar o template. | Baixo. |
| `DataSeriesWatermark` | `@INSTRUMENT_FULL` | Duplica visualmente o nome/rótulo da série, sobretudo com watermark de 70 pt. | Manter, pois é propriedade existente e útil; reduzir sua opacidade no claro e definir cor apropriada no escuro. | Baixo. |
| `TimeHighlighters` / blocos | Contador `1`, mas blocos `0..9`; ativo `14:30–17:00`, demais `09:00–17:00` | Nove blocos são redundantes/inativos pelo contador. O XML não informa o fuso do gráfico. | Remover apenas `TimeHighlighter1..9`; manter o bloco 0. Registrar fuso como “A validar”; não assumir ET nem horário local. | Baixo, condicionado a teste de importação. |
| `BackgroundImagePath` / `LoadBackgroundImage` | caminho local `C:\Program Files\...\earth.jpg` / `false` | Caminho não é portável e não tem efeito com carregamento desativado. | Limpar o caminho e manter `false`. | Baixo. |
| Cores de candles e grade | altas `#FF228B22`, baixas `#FFDC143C`, grade `#FFC0C0C0` | As cores têm contraste adequado em claro, mas a grade pode ser mais discreta; verde/vermelho puros não são a única pista de direção. | Claro: tornar grade mais suave. Escuro: trocar candles por azul/laranja de luminosidade distinta e ajustar contornos. | Baixo; aparência pendente de validação manual. |
| Eixos, marcadores, Chart Trader | vários `DEFAULT`; marcador com verde/azul | Parte do visual depende do tema local; o original já fornece serializações explícitas para itens críticos. | Claro: preservar `DEFAULT` para evitar suposições. Escuro: definir somente cores em campos que já existem e já aceitam serialização no arquivo. | Médio se o NT8 rejeitar algum valor explícito. |
| Escala / espaçamento | automática, margens `6`/`6`, `BarDistance=9`, margem direita `8` | Escala está correta, porém com limites fixos residuais; espaçamento não evidencia defeito. | Apenas neutralizar limites; manter margens e espaçamentos. | Baixo. |
| `<Indicators />` | vazio | Não há exportação real de MACD a incorporar. | Manter vazio e solicitar export do NT8 em `PENDENCIAS.md`. | Nenhum. |
| ATM / quantidade / TIF | `100-200` / `1` / `Day` | São parâmetros operacionais, não visuais. | Não alterar. | Alto se alterado sem validação do usuário. |

## Ações seguras adotadas nas próximas tarefas

Serão aplicadas somente as ações de baixo risco acima: neutralização de data/escala fixa, alinhamento do rótulo, remoção dos nove realçadores inativos, limpeza do caminho de imagem inativo e ajustes visuais em propriedades já presentes. Instrumento, GUIDs, ATM, quantidade, TIF, barras Renko e propriedades operacionais permanecerão inalterados.
