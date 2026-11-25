# Templates de gráfico PulseGuard

Estes arquivos são templates visuais para NinjaTrader 8, destinados exclusivamente a Playback/Sim101. Não são recomendação financeira e não prometem rentabilidade.

## Arquivos

- `preset-template-60R-light-v0.1.0.xml` e `.xtpl`: tema claro aprimorado, baseado na revisão visual v2.
- `preset-template-60R-dark-v0.1.0.xml` e `.xtpl`: tema escuro equivalente.
- `preset-template-60R-light.xml`: arquivo legado sem versão, mantido apenas como referência; não é o template ativo.
- `backup/preset-template-60R-light.original.xml`: cópia byte a byte do original.
- `AUDITORIA.md`, `PENDENCIAS.md` e `diff-light-dark.md`: achados, pendências e diff controlado.

Os `.xml` são a fonte de verdade para versionamento e revisão de diff. Os `.xtpl` têm conteúdo idêntico ao XML correspondente e usam a extensão que o NinjaTrader 8 procura em `Documents\NinjaTrader 8\templates\Chart\`.

## Instalação e uso

1. Copie o `.xtpl` desejado para `Documents\NinjaTrader 8\templates\Chart\`.
2. No NinjaTrader 8, abra um gráfico em Playback/Sim101 e aplique o template pelo menu **Templates**.
3. Para alternar de tema, aplique o arquivo claro ou escuro ao mesmo gráfico.
4. Faça o checklist manual em `PENDENCIAS.md` antes de considerar o template validado.

O XML é a fonte de verdade; o `.xtpl` correspondente deve permanecer idêntico. Os nomes SemVer representam a versão do componente template, não uma exigência de instrumento ou de série de barras. ATM `100-200`, quantidade `1` e TIF `Day` foram preservados. Não há indicadores no template até que seja fornecido um export real do MACD. A estratégia apresenta `TemplateVersion` como informado manualmente; não há detecção automática do template aplicado.
