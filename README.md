# PulseGuard

PulseGuard is an open-source NinjaTrader 8 (NT8) NinjaScript project for MACD-based signals and configurable strategy/risk management. It is not financial advice or a promise of performance.

## Architecture

- [`Indicator/MACDNG.cs`](Indicator/MACDNG.cs) — reusable `MACDNG` indicator in `NinjaTrader.NinjaScript.Indicators`; it is not tied to an instrument or strategy.
- [`Strategy/PulseGuardStrategy.cs`](Strategy/PulseGuardStrategy.cs) — `PulseGuardStrategy` in `NinjaTrader.NinjaScript.Strategies`.
- [`Instructions/`](Instructions/) — installation, testing, parameter and safety guides.
- [`Presets/`](Presets/) — documented simulator-only reference configuration.
- [`Templates/`](Templates/) — reusable preset and simulator-report templates.
- [`Tests/`](Tests/) — practical Sim101 validation checklist.
- [`Videos/`](Videos/) — recommended community video learning path; YouTube links are placeholders until verified URLs are available.
- [`.github/`](.github/) — issue and pull request templates for community collaboration.

The strategy's instrument and Renko type/value validations are optional and disabled by default. `BrickSizeTicks` remains an independent input to internal barrier and breakeven calculations; changing or disabling chart validation does not infer a suitable value.

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
