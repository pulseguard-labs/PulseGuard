# PulseGuard

> **Aviso de Risco:** Operar no mercado financeiro envolve alto risco de perda e volatilidade imprevisível. **Não há garantia de resultados**, e **resultados passados não garantem resultados futuros**. Este código tem caráter educacional e experimental — teste sempre em simulador e leia o [`DISCLAIMER.md`](./DISCLAIMER.md) completo antes de qualquer uso.

PulseGuard is an open-source NinjaTrader 8 (NT8) NinjaScript project for MACD-based signals and configurable strategy/risk management. It is not financial advice or a promise of performance.

## Architecture

- [`Indicator/MACDNG.cs`](Indicator/MACDNG.cs) — reusable `MACDNG` indicator in `NinjaTrader.NinjaScript.Indicators`; it is not tied to an instrument or strategy.
- [`Indicator/PulseGuardVersion.cs`](Indicator/PulseGuardVersion.cs) and [`version.json`](version.json) — installed and published component versions.
- [`Indicator/PulseGuardUpdateChecker.cs`](Indicator/PulseGuardUpdateChecker.cs) — asynchronous, informational version check; it never downloads or installs software.
- [`Strategy/PulseGuardStrategy.cs`](Strategy/PulseGuardStrategy.cs) — `PulseGuardStrategy` in `NinjaTrader.NinjaScript.Strategies`.
- [`Instructions/`](Instructions/) — installation, testing, parameter and safety guides.
- [`Presets/`](Presets/) — documented simulator-only reference configuration.
- [`Templates/`](Templates/) — NinjaTrader 8 chart templates, their review trail, and simulator-only test documentation.
- [`Tests/`](Tests/) — practical Sim101 validation checklist.
- [`Videos/`](Videos/) — recommended community video learning path; YouTube links are placeholders until verified URLs are available.
- [`.github/`](.github/) — issue and pull request templates for community collaboration.

The strategy's instrument and Renko type/value validations are optional and disabled by default. `BrickSizeTicks` remains an independent input to internal barrier and breakeven calculations; changing or disabling chart validation does not infer a suitable value.

## Chart templates

The chart-template source of truth is XML. The matching `.xtpl` files have identical content and are provided for NinjaTrader 8 installation in `Documents\NinjaTrader 8\templates\Chart\`.

- [`preset-template-60R-light-v0.1.0.xml`](Templates/preset-template-60R-light-v0.1.0.xml) / [`preset-template-60R-light-v0.1.0.xtpl`](Templates/preset-template-60R-light-v0.1.0.xtpl) — revised light theme.
- [`preset-template-60R-dark-v0.1.0.xml`](Templates/preset-template-60R-dark-v0.1.0.xml) / [`preset-template-60R-dark-v0.1.0.xtpl`](Templates/preset-template-60R-dark-v0.1.0.xtpl) — equivalent dark theme, with high-contrast blue/orange candles.
- [`backup/preset-template-60R-light.original.xml`](Templates/backup/preset-template-60R-light.original.xml) — byte-for-byte preservation copy of the original exported template.
- [`AUDITORIA.md`](Templates/AUDITORIA.md), [`PENDENCIAS.md`](Templates/PENDENCIAS.md), and [`diff-light-dark.md`](Templates/diff-light-dark.md) — static audit, manual-test requirements, and controlled visual diff.

The templates retain the exported MNQ DEC26 instrument, Renko value 60, ATM `100-200`, quantity `1`, and TIF `Day`. They do not enable live trading or contain accounts, credentials, or automatic trading settings. Apply and validate them only in Playback/Sim101; after rollover, select the appropriate contract in NinjaTrader 8.

The active time highlighter is 14:30–17:00 in the time zone configured in NinjaTrader 8. Its intended ET/local interpretation, daylight-saving behavior, XML import acceptance, chart scale, and Chart Trader appearance remain pending manual validation. No MACD XML is embedded: see [`Templates/PENDENCIAS.md`](Templates/PENDENCIAS.md) for the required real NT8 export workflow.

## Component versioning

The version check reads only the public `version.json` manifest on `main`; until that manifest is published there, the status remains **Não verificado**. The check is asynchronous, uses a shared 15-minute cache, and does not change order behavior. The strategy's `CheckForUpdates` setting controls its own, embedded-indicator, and template checks; a standalone indicator has its own setting. The template version is entered manually because chart-template XML cannot be safely inspected at runtime. See [`docs/RELEASE.md`](docs/RELEASE.md) for SemVer, validation, and NinjaTrader assembly-reference requirements.

## Quick start

1. Back up existing NinjaTrader scripts. Install `Indicator/MACDNG.cs` under `Documents\NinjaTrader 8\bin\Custom\Indicators\` and `Strategy/PulseGuardStrategy.cs` under `Documents\NinjaTrader 8\bin\Custom\Strategies\`.
2. Keep only one `MACDNG` definition, then compile all scripts in **New > NinjaScript Editor**.
3. Add `PulseGuardStrategy` to a chart in Playback or `Sim101`; keep automatic trading disabled while reviewing the signals and settings.
4. Follow [installation and testing](Instructions/instalacao-e-testes.md), review [parameters and safety](Instructions/parametros-e-seguranca.md), and complete the [Sim101 test checklist](Tests/roteiro-sim101.md) before considering any automated simulation.

## Safety and time handling

Automatic trading, risk confirmation, live trading authorization, and prop-simulation exceptions default to disabled. The strategy includes configurable account/contract controls, daily limits, emergency stop, breakeven, position management, and session flattening. Validate all values for the selected instrument, account, and bar series.

Bar timestamps are converted from the NinjaTrader-configured time zone to Eastern Time (ET), using the Windows time-zone rules for `America/New_York`, including daylight-saving changes. Do not remove or bypass this conversion when contributing.

Stops and risk controls depend on platform, connectivity, liquidity, broker behavior, and order execution. They cannot guarantee a fill or a price. Futures and other financial instruments involve substantial risk, including loss of capital. Backtests and simulation do not guarantee future results. Use the software at your own risk; independently review code, settings, and applicable rules. Never test live automation with funds you cannot afford to lose.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md), open an issue before substantial changes, and use the pull request template. Contributions that change order handling, risk controls, or ET conversion require explicit safety review and simulation testing.

This project is distributed under the [MIT License](LICENSE).
