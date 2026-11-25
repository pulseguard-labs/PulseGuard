# Levantamento de versionamento do PulseGuard

Levantamento inicial realizado antes das alterações de versionamento. O estado local está na branch `feat/versioning`, criada a partir de `refactor/pulseguard-structure`.

## Componentes NinjaScript encontrados

| Componente | Arquivo e classe | Inicialização observada |
|---|---|---|
| Indicador | [`Indicator/MACDNG.cs`](../Indicator/MACDNG.cs) — `MACDNG : Indicator`, no namespace `NinjaTrader.NinjaScript.Indicators` | `State.DataLoaded` |
| Estratégia | [`Strategy/PulseGuardStrategy.cs`](../Strategy/PulseGuardStrategy.cs) — `PulseGuardStrategy : Strategy` | `State.DataLoaded`; possui encerramento em `State.Terminated` |

Não foram encontrados outros fontes NinjaScript atuais em `Indicator/` ou `Strategy/`.

## Dashboard da estratégia

O dashboard pertence a `PulseGuardStrategy`. Ele é construído como controles WPF (`UserControl`, `Border` e `TextBlock`) e inserido em `UserControlCollection`; não usa `Draw.TextFixed` nem uma sobrescrita de `OnRender`. O método `RenderDashboard` prepara o conteúdo e as atualizações visuais são enfileiradas no dispatcher do `ChartControl`. A estratégia já remove o controle no estado `State.Terminated`, também pelo dispatcher.

`RenderDashboard` é chamado na inicialização em `State.Realtime`, em eventos de atualização e durante `OnBarUpdate`, com limitação de frequência existente. Há também um método `RenderDashboardLegacy` declarado, sem chamada encontrada nos fontes examinados. O indicador não possui dashboard próprio.

## Versões existentes

Não foram encontradas constantes ou classes de versão, metadados `BuildDate`/`Commit`, `version.json` nem `CHANGELOG.md`.

Os templates atuais não têm versão SemVer nos nomes. A documentação local identifica como atuais o tema claro `preset-template-60R-light-v2.xml/.xtpl` e o escuro `preset-template-60R-dark.xml/.xtpl`; existe também `preset-template-60R-light.xml` e um backup do export original. A documentação registra XML como fonte de verdade e `.xtpl` como cópia para instalação. O bloco de indicadores dos templates está vazio; a exportação real do NinjaTrader com o indicador ainda é uma pendência documentada em [`Templates/PENDENCIAS.md`](../Templates/PENDENCIAS.md).

## Repositório e publicação

- `origin`: `https://github.com/pulseguard-labs/PulseGuard.git`.
- O endpoint público do GitHub informa visibilidade `public` e branch padrão configurada como `main`.
- Na consulta do levantamento, o GitHub não listava branches, tags ou releases; a página de releases informava que ainda não havia releases.
- A cópia local tinha a branch `refactor/pulseguard-structure`, sem tags locais. A branch de trabalho `feat/versioning` foi criada a partir dela após concluir a leitura.
- A cópia local já continha alterações não commitadas em `README.md` e `.gitignore`; elas foram preservadas e não fazem parte deste levantamento.
- Ainda não existiam `version.json`, `CHANGELOG.md`, `scripts/`, `docs/` ou workflows de GitHub Actions.

## Referências do código

- Indicador: inicialização em `State.DataLoaded`.
- Estratégia: dashboard WPF declarado e renderizado em `PulseGuardStrategy`; criação do controle em `UserControlCollection`; remoção em `State.Terminated`.
- Nenhum mecanismo de consulta a versões remotas foi encontrado.
