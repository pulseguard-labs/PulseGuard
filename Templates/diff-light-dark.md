# Diff controlado — claro v2 × escuro

O template escuro foi copiado do claro v2. A comparação deve mostrar mudanças somente nas serializações visuais abaixo; não há alteração de instrumento, barras, escala, ATM, quantidade, TIF, IDs, histórico ou propriedades de operação.

| Área | Propriedades com mudança visual |
|---|---|
| Candles e marcador de preço | `UpBrushSerialize`, `DownBrushSerialize`, `StrokeSerialize`, `Stroke2Serialize`, cores de `PriceMarker` |
| Gráfico | `ChartBackgroundSerialize`, `ChartTextSerialize`, watermark, grade H/V, eixo, crosshair, painel e marcadores |
| Realce de horário | `TimeHighlighter0/BrushSerialize` |
| Chart Trader | PnL, posição, botões Buy/Sell/Action e linhas de entrada, alvo e stop |

Validação automatizada: pendente de teste manual de importação no NinjaTrader 8. O XML é bem-formado; a aceitação do schema pelo aplicativo não foi executada neste ambiente.
