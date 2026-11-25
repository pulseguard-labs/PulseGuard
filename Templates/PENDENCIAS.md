# Pendências

## Exportação do MACD

O bloco `<Indicators />` dos dois templates está vazio. Não foi criado XML de indicador manualmente. Para fornecer um export válido do NinjaTrader 8:

1. Abra um gráfico e adicione o MACD com 12/26/9 no painel 2.
2. Configure a linha MACD como `#FF00B300`, a Avg como `#FFFF0000`, o histograma Diff oculto/transparente e a zero line sólida preta.
3. Clique com o botão direito no gráfico e escolha **Templates → Save as**.
4. Envie o arquivo exportado `.xml` ou `.xtpl` resultante, sem dados de conta ou informações pessoais.

Com esse export, será possível copiar somente o bloco de indicador efetivamente serializado pelo NT8 e ajustar as cores do tema escuro com segurança.

## Testes manuais pendentes

- Importar cada `.xtpl` no NinjaTrader 8 e aplicar em um gráfico MNQ Renko 60 em Playback/Sim101.
- Confirmar a abertura após `From` vazio, escala automática e limites fixos zerados.
- Confirmar a aparência, contraste e legibilidade do claro e do escuro, inclusive Chart Trader.
- Confirmar o fuso configurado no NT8 e se 14:30–17:00 do único `TimeHighlighter` corresponde ao horário pretendido, incluindo horário de verão.
- Confirmar o comportamento de rollover: o instrumento exportado é MNQ DEC26; o contrato deve ser selecionado no NT8 depois de aplicar o template.
