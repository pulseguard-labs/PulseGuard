# Processo de release do PulseGuard

## SemVer por componente

Use `MAJOR.MINOR.PATCH`, com sufixo de pré-lançamento opcional:

- **MAJOR**: mudança incompatível, inclusive quebra de compatibilidade de parâmetros.
- **MINOR**: recurso novo compatível.
- **PATCH**: correção compatível.

`Indicator/PulseGuardVersion.cs` é a fonte central das versões instaladas, incluindo `Package` e cada componente. `version.json` publica essas versões para a checagem informativa do NinjaScript. `BuildDate` e `Commit` permanecem como `dev` até existir um processo de build que os preencha.

## Publicar uma versão

1. Atualize as constantes `Package` e dos componentes alterados (`Indicator`, `Strategy` e/ou `Template`) em `Indicator/PulseGuardVersion.cs`.
2. Atualize `packageVersion` e os componentes correspondentes em `version.json`, incluindo `minRequired` quando uma versão mínima for necessária.
3. Atualize a primeira entrada do `CHANGELOG.md` com a versão do pacote e a data de publicação.
4. Execute `./scripts/check-versions.ps1` e `./scripts/test-versioning.ps1`.
5. Revise e integre as alterações na branch pública configurada (`main`).
6. Crie a tag do pacote no formato `v<MAJOR>.<MINOR>.<PATCH>` e publique a release com as notas correspondentes ao `CHANGELOG.md`.

O verificador do NinjaTrader lê somente o texto de `version.json` na branch `main`. Ele só verá a nova versão depois que esse manifesto estiver publicado nessa branch. A página de releases é informativa; o checker não baixa arquivos, não instala código e não executa conteúdo remoto.

## Instalação no NinjaTrader 8

Copie `Indicator/MACDNG.cs`, `Indicator/PulseGuardVersion.cs` e `Indicator/PulseGuardUpdateChecker.cs` para `Documents\NinjaTrader 8\bin\Custom\Indicators\`, e `Strategy/PulseGuardStrategy.cs` para `Documents\NinjaTrader 8\bin\Custom\Strategies\`. Mantenha uma única cópia de cada fonte e compile no NinjaScript Editor.

O cliente usa as assemblies do .NET Framework 4.8 `System.Net.Http.dll` e `System.Web.Extensions.dll` para configurar TLS 1.2 por handler e interpretar JSON. Se o compilador NinjaScript informar que uma delas não foi encontrada, adicione a DLL correspondente nas referências do NinjaScript Editor; não instale pacotes de terceiros. O verificador não altera `ServicePointManager` nem a configuração global de TLS.

## Templates

Os templates de gráfico são arquivos XML e não executam código. Os pares `.xml`/`.xtpl` ativos levam a versão SemVer no nome. A propriedade `TemplateVersion` da estratégia é preenchida manualmente, pois o NinjaTrader pode regravar o XML e não há detecção automática segura. Como os templates atuais não incluem uma exportação real do indicador, não foi criado XML de indicador manualmente; consulte [`../Templates/PENDENCIAS.md`](../Templates/PENDENCIAS.md).

## Validação de release

O workflow `.github/workflows/version-check.yml` executa o validador de consistência e os testes do parser/SemVer em pushes e pull requests. A compilação oficial no NinjaScript Editor e os testes em Playback/Sim101 continuam sendo validações manuais; não são substituídos pelo CI.
