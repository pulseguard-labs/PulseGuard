# Contribuindo com PulseGuard

Obrigado por contribuir. Mudanças pequenas, reproduzíveis e com contexto de plataforma facilitam a revisão. Abra uma issue antes de mudanças grandes ou que alterem ordens/risco.

## Fluxo de contribuição

1. Abra uma issue usando o template de bug ou feature. Para bugs, informe versão NT8, instrumento, tipo/valor da barra, Trading Hours, fuso, reprodução e logs sanitizados.
2. Crie uma branch a partir da branch padrão atual: `feature/<resumo>`, `fix/<resumo>` ou `docs/<resumo>`.
3. Mantenha cada PR focado. Não inclua arquivos locais, logs, CSVs, exports, DLLs, credenciais ou dados proprietários.
4. Execute os testes relevantes; compile NinjaScript no NinjaTrader 8 e prefira Playback/Sim101, sem usar uma conta live. Registre resultados com os modelos de `Templates/` e o roteiro em `Tests/`.
5. Abra PR usando o template. Descreva comportamento anterior/novo, testes, limitações de compilação e riscos; solicite revisão antes de integrar.

## Regras de segurança que não são negociáveis

- Mantenha `EnableAutomaticTrading = false` por padrão e não relaxe confirmação de risco, controles de conta, limites de contratos, stop de emergência, travas diárias ou lógica de gestão sem justificativa e revisão explícitas.
- Não codifique regras de uma firma, ativo obrigatório, série obrigatória ou tamanho de barra na identidade comercial. Validações específicas devem continuar opcionais/configuráveis.
- Preserve `MACDNG` como indicador reutilizável e no namespace `NinjaTrader.NinjaScript.Indicators`.
- Preserve a conversão de fuso de barras para ET (`America/New_York`), inclusive horário de verão. Mudanças temporais precisam de testes em fusos de origem distintos e nas transições sazonais.
- Não trate stop/trava como garantia de execução ou de preço. Não use logs da estratégia para publicar identificadores de conta ou dados pessoais.
- Nunca valide alterações de envio de ordens em conta real.

## Revisão e testes

O PR deve demonstrar compilação limpa no NinjaScript Editor compatível e, quando aplicável, testes em histórico, Playback ou Sim101. Teste bloqueios, ordens, stop, breakeven, limites, flatten, sessão e conversão ET quando forem afetados. Indique claramente qualquer validação que não pôde ser realizada.

Revisores devem verificar escopo, compatibilidade NinjaTrader, defaults de segurança, dados sensíveis e alterações no horário ET. Mudanças de risco/ordens requerem revisão explícita antes de merge.

Ao publicar uma versão, atualize as constantes centralizadas, `version.json` e o topo do `CHANGELOG.md`; use SemVer e execute `scripts/check-versions.ps1` e `scripts/test-versioning.ps1`. Consulte [`docs/RELEASE.md`](docs/RELEASE.md) para o fluxo completo. O verificador remoto é somente informativo e nunca instala atualizações.

Novos presets devem separar valores de instrumento/série das regras gerais da estratégia, documentar unidades e ser testados em simulação. Vídeos comunitários devem usar links públicos verificados e não mostrar dados de conta ou habilitar live trading.
