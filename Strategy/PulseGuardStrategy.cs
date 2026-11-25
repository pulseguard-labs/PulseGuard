#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	[TypeConverter(typeof(AccountStageConverter))]
	public enum AccountStage
	{
		Evaluation,
		Funded,
		Real
	}

	public class AccountStageConverter : EnumConverter
	{
		public AccountStageConverter() : base(typeof(AccountStage))
		{
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			string stage = value as string;
			if (stage == "Conta de Avaliação")
				return AccountStage.Evaluation;
			if (stage == "Conta Financiada")
				return AccountStage.Funded;
			if (stage == "Conta Real")
				return AccountStage.Real;
			if (stage == "Live")
				return AccountStage.Real;
			return base.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture,
			object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is AccountStage)
			{
				switch ((AccountStage)value)
				{
					case AccountStage.Evaluation:
						return "Conta de Avaliação";
					case AccountStage.Funded:
						return "Conta Financiada";
					case AccountStage.Real:
						return "Conta Real";
				}
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
		{
			return true;
		}

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			return new StandardValuesCollection(new AccountStage[]
			{
				AccountStage.Evaluation, AccountStage.Funded, AccountStage.Real
			});
		}
	}

	public enum PulseGuardDashboardMode
	{
		Compact,
		Detailed
	}

	public class PulseGuardStrategy : Strategy
	{
		private const double EntryZoneBuyLow = -41.6666666667;
		private const double EntryZoneBuyHigh = -15.0;
		private const double EntryZoneSellLow = 15.0;
		private const double EntryZoneSellHigh = 41.6666666667;
		private const double ExitFailureBuy = -45.0;
		private const double ExitFailureSell = 45.0;

		private MACDNG macd;
		private EMA emaFast;
		private SMA smaMedium;
		private EMA emaMacro;
		private SessionIterator sessionIterator;
		private DateTime sessionBegin;
		private bool sessionInitialized;
		private bool dailyLocked;
		private bool orderErrorHalt;
		private bool connectionErrorHalt;
		private bool entryOrderPending;
		private bool entryCancelRequested;
		private bool exitOrderPending;
		private bool configurationValid;
		private string configurationBlockReason = "";
		private string orderErrorReason = "";
		private string connectionErrorReason = "";
		private Order pendingEntryOrder;
		private Account monitoredAccount;
		private int lastProcessedTradeCount;
		private int cooldownUntilBar = -1;
		private int currentEntryScore;
		private int cachedContextScore;
		private int activeSessionContractLimit;
		private int sessionTradeCount;
		private int sessionWins;
		private int sessionLosses;
		private double currentSessionPnl;
		private double accountRealizedPnl;
		private double accountUnrealizedPnl;
		private int accountOpenPositionCount;
		private bool accountHasPositionOnInstrument;
		private int accountSessionTradeCount;
		private int accountSessionWins;
		private int accountSessionLosses;
		private UserControl dashboardControl;
		private Border dashboardCard;
		private TextBlock[] dashboardRows;
		private DateTime lastDashboardUpdateUtc = DateTime.MinValue;
		private readonly object dashboardUpdateLock = new object();
		private bool dashboardUpdateQueued;
		private bool dashboardHideQueued;
		private string[] pendingDashboardRows;
		private Brush[] pendingDashboardRowBrushes;
		private Brush pendingDashboardAccentBrush;
		private int cachedDashboardBarNumber = -1;
		private string cachedTrendText = "Aguardando dados suficientes.";
		private string cachedScoreText = "Score de contexto: ainda calculando.";
		private bool cachedTrendBullish;
		private bool cachedTrendBearish;
		private bool protectiveStopConfigured;
		private bool breakEvenStopActive;
		private bool currentSignalReady;
		private string lastStatusMessage = "Inicializando estratégia.";
		private string lastEntryFilterReason = "Nenhum sinal recente.";
		private string dailyLockReason = "";
		private Brush fastEmaBrush;
		private string fastEmaBrushSerializable;
		private Brush mediumSmaBrush;
		private string mediumSmaBrushSerializable;
		private Brush macroEmaBrush;
		private string macroEmaBrushSerializable;
		private Brush panelBackgroundBrush;
		private string panelBackgroundBrushSerializable;
		private DateTime firstEntryFillTime;
		private TimeZoneInfo barTimeZone;
		private TimeZoneInfo easternTimeZone;
		private readonly object fileLock = new object();
		private readonly object csvQueueLock = new object();
		private string logFilePath;
		private Task csvWriteTask = Task.FromResult(0);
		private volatile PulseGuardUpdateResult strategyVersionResult;
		private volatile PulseGuardUpdateResult indicatorVersionResult;
		private volatile PulseGuardUpdateResult templateVersionResult;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "PulseGuard combines MACDNG and moving averages. Signals only by default; optional automation requires validation.";
				Name						= "PulseGuardStrategy";
				Calculate					= Calculate.OnEachTick;
				EntriesPerDirection			= 1;
				EntryHandling				= EntryHandling.UniqueEntries;
				IsExitOnSessionCloseStrategy	= true;
				ExitOnSessionCloseSeconds	= 960;
				IncludeCommission			= true;
				BarsRequiredToTrade			= 205;
				RealtimeErrorHandling		= RealtimeErrorHandling.StopCancelClose;
				StartBehavior				= StartBehavior.WaitUntilFlat;
				StopTargetHandling			= StopTargetHandling.PerEntryExecution;

				EnableAutomaticTrading		= false;
				ConfirmAutomaticRisk		= false;
				AllowLiveTrading			= false;
				AllowPropSimAccount		= false;
				RequireRenkoChart			= false;
				ExpectedRenkoValue			= 60;
				RequireInstrument			= false;
				RequiredInstrumentName		= "";

				FastPeriod					= 12;
				SlowPeriod					= 26;
				SignalPeriod				= 9;
				NormalizationPeriod			= 50;
				ZoneLookbackBars			= 3;

				FastEmaPeriod				= 9;
				MediumSmaPeriod				= 21;
				MacroEmaPeriod				= 200;
				MinAverageDistanceTicks		= 4;
				NoCrossLookbackBars			= 5;

				BrickSizeTicks				= 60;
				BarrierLookbackBars			= 10;
				ScoreBarrierLookbackBars	= 15;
				MinimumEntryScore			= 65;
				HighConfluenceScore			= 80;
				UseQualityScore				= true;
				MaxBarsAfterMacdEvent		= 1;
				AllowMacdRejectionTrigger	= true;
				MacdRejectionMaxGap			= 6;
				MaxSameDirectionBricksBeforeEntry = 3;
				MaxDistanceFromFastEmaBricks = 2;
				AllowRecentFastMediumCross	= true;
				FreshTriggerScoreBonus		= 20;

				Contracts					= 1;
				AccountStageSelected		= AccountStage.Funded;
				MaxContractsEvaluation		= 1;
				MaxContractsFunded			= 1;
				MaxContractsReal			= 1;
				DailyLossLimitEnabled		= false;
				DailyLossLimitAmount		= 0;
				DailyLossSafetyBuffer		= 0;
				MaximumLossLimitEnabled	= false;
				MllRemainingRoom			= 0;
				MllSafetyBuffer				= 0;
				EmergencyStopTicks			= 32;
				BreakevenAfterBricks		= 2;
				MaxDailyProfit				= 300;
				MaxDailyLoss				= 150;
				MaxConsecutiveLosses		= 2;
				MaxTradesPerSession			= 8;
				CooldownBarsAfterLoss		= 3;

				EntryStartTime				= 93000;
				EntryEndTime				= 160000;
				FlattenTime				= 164000;
				MinimumProfitableHoldSeconds = 6;
				GenerateSignalsOutsideSession = false;
				FastEmaBrush				= Brushes.DodgerBlue;
				MediumSmaBrush				= Brushes.Goldenrod;
				MacroEmaBrush				= Brushes.MediumPurple;
				FastEmaWidth				= 2;
				MediumSmaWidth				= 2;
				MacroEmaWidth				= 3;
				MostrarPainel				= true;
				ModoPainel					= PulseGuardDashboardMode.Compact;
				TamanhoFontePainel			= 14;
				OpacidadeFundoPainel		= 94;
				CorFundoPainel				= new SolidColorBrush(Color.FromRgb(13, 20, 32));
				CheckForUpdates				= true;
				ShowVersionPanel			= true;
				TemplateVersion				= "informado manualmente";

				EnableSoundAlerts			= true;
				EnableCsvLog				= true;
				MacroSlopeMinTicks			= 1;
				HealthyDistanceMinBricks	= 1;
				HealthyDistanceMaxBricks	= 10;
				MacdAccelerationThreshold	= 5;
				MacdExtremeThreshold		= 58;
				MacdFlatRangeThreshold		= 1;
				ShortAverageSlopeMinTicks	= 1;
				MainLiquidityStart1			= 93000;
				MainLiquidityEnd1			= 113000;
				MainLiquidityStart2			= 140000;
				MainLiquidityEnd2			= 154500;
				SecondaryLiquidityStart1	= 83000;
				SecondaryLiquidityEnd1		= 93000;
				SecondaryLiquidityStart2	= 130000;
				SecondaryLiquidityEnd2		= 140000;
			}
			else if (State == State.Configure)
			{
				// Entries and technical exits are evaluated on closed bars;
				// daily risk and the emergency stop are monitored intrabar.
			}
			else if (State == State.DataLoaded)
			{
				barTimeZone = NinjaTrader.Core.Globals.GeneralOptions.TimeZoneInfo;
				easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
				macd = MACDNG(FastPeriod, SlowPeriod, SignalPeriod, true, NormalizationPeriod,
					40.0, 60.0, 0.25, CheckForUpdates, 40, 20, -20, -40);
				emaFast = EMA(FastEmaPeriod);
				smaMedium = SMA(MediumSmaPeriod);
				emaMacro = EMA(MacroEmaPeriod);
				emaFast.Plots[0].Brush = FastEmaBrush;
				emaFast.Plots[0].Width = FastEmaWidth;
				smaMedium.Plots[0].Brush = MediumSmaBrush;
				smaMedium.Plots[0].Width = MediumSmaWidth;
				emaMacro.Plots[0].Brush = MacroEmaBrush;
				emaMacro.Plots[0].Width = MacroEmaWidth;
				sessionIterator = new SessionIterator(Bars);
				lastProcessedTradeCount = SystemPerformance.AllTrades.Count;
				logFilePath = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "PulseGuard", "strategy_events.csv");
				configurationValid = ValidateChartConfiguration();
				activeSessionContractLimit = GetConfiguredContractLimit();

				if (ChartControl != null)
				{
					AddChartIndicator(emaFast);
					AddChartIndicator(smaMedium);
					AddChartIndicator(emaMacro);
					AddChartIndicator(macd);
					CheckComponentVersions();
				}
			}
			else if (State == State.Realtime)
			{
				lastProcessedTradeCount = SystemPerformance.AllTrades.Count;
				SubscribeAccountUpdates();
				RefreshAccountSnapshot();
				PrintTimeDiagnostic();
				RenderDashboard();
			}
			else if (State == State.Terminated)
			{
				UnsubscribeAccountUpdates();
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync((Action)(() =>
					{
						if (dashboardControl != null)
						{
							UserControlCollection.Remove(dashboardControl);
							dashboardControl = null;
							dashboardRows = null;
							dashboardCard = null;
						}
					}));
				}
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < BarsRequiredToTrade || CurrentBar < NormalizationPeriod + SlowPeriod + SignalPeriod)
			{
				UpdateStatus("Aguardando barras suficientes para aquecimento dos indicadores.");
				return;
			}

			if (!sessionInitialized || (Bars.IsFirstBarOfSession
				&& (State == State.Historical || IsFirstTickOfBar)))
				ResetForSession();

			if (Position.MarketPosition == MarketPosition.Flat)
				exitOrderPending = false;

			ProcessCompletedTrades();
			UpdateDailyRisk();
			RenderDashboard();

			int signalBar = State == State.Historical ? 0 : 1;
			bool closedBarAvailable = State == State.Historical || IsFirstTickOfBar;

			if (IsInFlattenWindow(Time[0]))
			{
				dailyLocked = true;
				dailyLockReason = "Horário de operação encerrado. Posições em zeragem.";
				lastStatusMessage = dailyLockReason;
				CancelPendingEntry();
				FlattenPosition("Fim da janela operacional");
				if (GenerateSignalsOutsideSession && Position.MarketPosition == MarketPosition.Flat
					&& !entryOrderPending && closedBarAvailable)
					EvaluateClosedBarSignal(signalBar, false);
				else
					UpdateStatus("Sessão encerrada: novas entradas bloqueadas.");
				return;
			}

			if (dailyLocked)
			{
				CancelPendingEntry();
				if (Position.MarketPosition != MarketPosition.Flat)
					FlattenPosition("Estratégia bloqueada por limite de risco ou horário");
				else if (GenerateSignalsOutsideSession && !IsWithinEntryWindow(Time[signalBar])
					&& !entryOrderPending && closedBarAvailable)
					EvaluateClosedBarSignal(signalBar, false);
				UpdateStatus(GetBlockedStatus());
				return;
			}

			if (orderErrorHalt || connectionErrorHalt || !configurationValid)
			{
				if (GenerateSignalsOutsideSession && !IsWithinEntryWindow(Time[signalBar])
					&& !entryOrderPending && Position.MarketPosition == MarketPosition.Flat
					&& configurationValid && closedBarAvailable)
					EvaluateClosedBarSignal(signalBar, false);
				UpdateStatus(GetBlockedStatus());
				return;
			}

			if (Position.MarketPosition != MarketPosition.Flat)
			{
				if (State == State.Historical || IsFirstTickOfBar)
					ManagePositionAtBarClose(State == State.Historical ? 0 : 1);
				UpdateStatus(EnableAutomaticTrading ? "AUTOMÁTICO — posição gerida pela estratégia" : "SOMENTE SINAIS — posição ativa da estratégia");
				return;
			}

			if ((State != State.Historical && !IsFirstTickOfBar) || entryOrderPending)
				return;

			EvaluateClosedBarSignal(signalBar, true);
		}

		private bool ValidateChartConfiguration()
		{
			StringBuilder errors = new StringBuilder();
			if (RequireRenkoChart)
			{
				bool isRenko = BarsPeriod.BarsPeriodType.ToString().IndexOf("Renko", StringComparison.OrdinalIgnoreCase) >= 0;
				if (!isRenko || BarsPeriod.Value != ExpectedRenkoValue)
					AddConfigurationError(errors, "Exige Renko " + ExpectedRenkoValue + "; atual "
						+ BarsPeriod.BarsPeriodType + " " + BarsPeriod.Value);
			}

			string masterInstrumentName = Instrument == null || Instrument.MasterInstrument == null
				? "N/D" : Instrument.MasterInstrument.Name;
			if (RequireInstrument)
			{
				if (string.IsNullOrWhiteSpace(RequiredInstrumentName))
					AddConfigurationError(errors, "Informe o nome do instrumento a validar");
				else if (!string.Equals(masterInstrumentName, RequiredInstrumentName, StringComparison.OrdinalIgnoreCase))
					AddConfigurationError(errors, "Exige " + RequiredInstrumentName + "; instrumento atual " + masterInstrumentName);
			}

			int configuredContractLimit = GetConfiguredContractLimit();
			if (Contracts > configuredContractLimit)
				AddConfigurationError(errors, "Contratos " + Contracts + " excedem o limite "
					+ configuredContractLimit + " do estágio " + AccountStageSelected);

			if (BrickSizeTicks <= 0 || TickSize <= 0)
				AddConfigurationError(errors, "Brick size ou tick size inválido");
			if (FastPeriod >= SlowPeriod)
				AddConfigurationError(errors, "MACD Fast deve ser menor que Slow");
			if (ZoneLookbackBars < 1 || BarrierLookbackBars < 1 || ScoreBarrierLookbackBars < 1)
				AddConfigurationError(errors, "Lookback de zona/barreira deve ser maior que zero");
			if (MaxBarsAfterMacdEvent < 0 || MaxSameDirectionBricksBeforeEntry < 1
				|| MaxDistanceFromFastEmaBricks < 1 || MacdRejectionMaxGap < 0)
				AddConfigurationError(errors, "Parâmetros anti-atraso inválidos");
			if (MaxContractsEvaluation < 1 || MaxContractsFunded < 1 || MaxContractsReal < 1)
				AddConfigurationError(errors, "Limite máximo de contratos deve ser maior que zero");
			if (DailyLossLimitEnabled && DailyLossLimitAmount <= 0)
				AddConfigurationError(errors, "Limite diário de perda ativo exige valor maior que zero");
			if (DailyLossSafetyBuffer < 0)
				AddConfigurationError(errors, "Buffer diário não pode ser negativo");
			if (MaximumLossLimitEnabled && MllRemainingRoom <= 0)
				AddConfigurationError(errors, "Limite máximo de perda ativo exige margem restante maior que zero");
			if (MllSafetyBuffer < 0)
				AddConfigurationError(errors, "Buffer de perda máxima não pode ser negativo");
			if (FlattenTime >= 164500 || FlattenTime <= EntryEndTime)
				AddConfigurationError(errors, "Flatten deve ser depois do fim de entrada e antes de 16:45 ET");
			if (MinimumProfitableHoldSeconds < 6)
				AddConfigurationError(errors, "Permanência mínima lucrativa deve ser de pelo menos 6 segundos");

			configurationBlockReason = errors.ToString();
			return errors.Length == 0;
		}

		private void AddConfigurationError(StringBuilder errors, string reason)
		{
			if (errors.Length > 0)
				errors.Append("; ");
			errors.Append(reason);
			Print(Name + ": " + reason + ".");
		}

		private void ResetForSession()
		{
			sessionIterator.GetNextSession(Time[0], true);
			sessionBegin = sessionIterator.ActualSessionBegin;
			sessionInitialized = true;
			dailyLocked = false;
			dailyLockReason = "";
			cooldownUntilBar = -1;
			activeSessionContractLimit = GetConfiguredContractLimit();
			accountSessionTradeCount = 0;
			accountSessionWins = 0;
			accountSessionLosses = 0;
			if (Contracts > activeSessionContractLimit)
			{
				dailyLocked = true;
				dailyLockReason = "Quantidade acima do máximo permitido para esta sessão.";
				Print(Name + ": quantidade acima do limite de contratos configurado (" + activeSessionContractLimit
					+ " contratos). Ordens bloqueadas nesta sessão.");
			}
			if (Position.MarketPosition != MarketPosition.Flat)
			{
				dailyLocked = true;
				dailyLockReason = "Posição aberta na virada da sessão.";
				CancelPendingEntry();
				FlattenPosition("Posição ainda aberta na virada da sessão");
			}
			Print(Name + ": início de sessão " + sessionBegin.ToString("yyyy-MM-dd HH:mm:ss"));
		}

		private void UpdateDailyRisk()
		{
			if (!sessionInitialized)
				return;

			double dailyPnl = GetRealizedSessionPnl();
			if (Position.MarketPosition != MarketPosition.Flat)
				dailyPnl += Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
			currentSessionPnl = dailyPnl;

			if (!dailyLocked && MaxDailyProfit > 0 && dailyPnl >= MaxDailyProfit)
			{
				LockAndFlatten("Meta/teto de lucro diário atingido");
				Print(Name + ": meta diária atingida; estratégia bloqueada até a próxima sessão.");
			}
			else if (!dailyLocked && GetEffectiveDailyLossLimit() <= 0)
			{
				LockAndFlatten("Buffer de segurança sem margem de perda configurada");
			}
			else if (!dailyLocked && dailyPnl <= -GetEffectiveDailyLossLimit())
			{
				LockAndFlatten("Limite diário de perda/DLL/folga de MLL atingido");
				Print(Name + ": limite diário de perda atingido; estratégia bloqueada até a próxima sessão.");
			}

			CheckCompletedTradeLimits();
		}

		private void CheckCompletedTradeLimits()
		{
			if (!sessionInitialized)
				return;

			int trades = 0;
			int consecutiveLosses = 0;
			for (int i = SystemPerformance.AllTrades.Count - 1; i >= 0; i--)
			{
				Trade trade = SystemPerformance.AllTrades[i];
				if (trade.Exit == null)
					continue;
				if (trade.Exit.Time < sessionBegin)
					break;

				trades++;
				if (trade.ProfitCurrency < 0)
					consecutiveLosses++;
				else
					break;
			}

			trades = CountSessionTrades();
			if (MaxTradesPerSession > 0 && trades >= MaxTradesPerSession)
			{
				dailyLocked = true;
				dailyLockReason = "Limite de operações do dia atingido.";
			}
			if (MaxConsecutiveLosses > 0 && consecutiveLosses >= MaxConsecutiveLosses)
			{
				dailyLocked = true;
				dailyLockReason = "Limite de perdas seguidas atingido.";
			}
		}

		private int CountSessionTrades()
		{
			int count = 0;
			for (int i = 0; i < SystemPerformance.AllTrades.Count; i++)
			{
				Trade trade = SystemPerformance.AllTrades[i];
				if (trade.Exit != null && trade.Exit.Time >= sessionBegin)
					count++;
			}
			return count;
		}

		private double GetRealizedSessionPnl()
		{
			double realized = 0;
			sessionTradeCount = 0;
			sessionWins = 0;
			sessionLosses = 0;
			for (int i = 0; i < SystemPerformance.AllTrades.Count; i++)
			{
				Trade trade = SystemPerformance.AllTrades[i];
				if (trade.Exit != null && trade.Exit.Time >= sessionBegin)
				{
					realized += trade.ProfitCurrency;
					sessionTradeCount++;
					if (trade.ProfitCurrency > 0)
						sessionWins++;
					else if (trade.ProfitCurrency < 0)
						sessionLosses++;
				}
			}
			return realized;
		}

		private int GetConfiguredContractLimit()
		{
			if (AccountStageSelected == AccountStage.Evaluation)
				return MaxContractsEvaluation;
			if (AccountStageSelected == AccountStage.Funded)
				return MaxContractsFunded;
			return MaxContractsReal;
		}

		private double GetEffectiveDailyLossLimit()
		{
			double limit = MaxDailyLoss > 0 ? Math.Abs(MaxDailyLoss) : double.PositiveInfinity;
			if (MaximumLossLimitEnabled)
			{
				double mllRoomAfterBuffer = Math.Max(0, MllRemainingRoom - MllSafetyBuffer);
				limit = Math.Min(limit, mllRoomAfterBuffer);
			}

			if (DailyLossLimitEnabled)
				limit = Math.Min(limit, Math.Max(0, DailyLossLimitAmount - DailyLossSafetyBuffer));

			return limit;
		}

		private int ToEasternTime(DateTime timestamp)
		{
			DateTime unspecified = DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified);
			return ToTime(TimeZoneInfo.ConvertTime(unspecified, barTimeZone, easternTimeZone));
		}

		private string GetTimeDiagnostic()
		{
			DateTime localNow = DateTime.Now;
			DateTime easternNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);
			string localTime = localNow.ToString("dd/MM HH:mm") + " " + FormatUtcOffset(
				TimeZoneInfo.Local.GetUtcOffset(localNow));
			string easternTime = easternNow.ToString("dd/MM HH:mm") + " ET";
			string barTime = "N/D";

			if (CurrentBar >= 0 && barTimeZone != null)
			{
				DateTime timestamp = Time[0];
				DateTime easternBarTime = TimeZoneInfo.ConvertTime(
					DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), barTimeZone, easternTimeZone);
				barTime = barTimeZone.Id + " " + timestamp.ToString("dd/MM HH:mm")
					+ " → " + easternBarTime.ToString("dd/MM HH:mm") + " ET";
			}

			return "Hora local (" + TimeZoneInfo.Local.Id + "): " + localTime
				+ " · agora ET: " + easternTime + " · barra: " + barTime;
		}

		private string FormatUtcOffset(TimeSpan offset)
		{
			return "UTC" + (offset < TimeSpan.Zero ? "-" : "+")
				+ offset.Duration().ToString(@"hh\:mm");
		}

		private void PrintTimeDiagnostic()
		{
			Print(Name + ": " + GetTimeDiagnostic());
		}

		private int GetRequiredLookbackBars()
		{
			return Math.Max(6, Math.Max(ZoneLookbackBars,
				Math.Max(NoCrossLookbackBars, Math.Max(BarrierLookbackBars, ScoreBarrierLookbackBars))));
		}

		private bool IsInFlattenWindow(DateTime timestamp)
		{
			int time = ToEasternTime(timestamp);
			return time >= FlattenTime && time < 180000;
		}

		private bool IsWithinEntryWindow(DateTime timestamp)
		{
			int time = ToEasternTime(timestamp);
			return time >= EntryStartTime && time < EntryEndTime;
		}

		private void CancelPendingEntry()
		{
			if (entryOrderPending && pendingEntryOrder != null && !entryCancelRequested)
			{
				entryCancelRequested = true;
				CancelOrder(pendingEntryOrder);
			}
		}

		private void LockAndFlatten(string reason)
		{
			dailyLocked = true;
			dailyLockReason = reason;
			CancelPendingEntry();
			FlattenPosition(reason);
		}

		private void ProcessCompletedTrades()
		{
			int total = SystemPerformance.AllTrades.Count;
			while (lastProcessedTradeCount < total)
			{
				Trade trade = SystemPerformance.AllTrades[lastProcessedTradeCount];
				lastProcessedTradeCount++;
				if (trade.Exit == null || trade.Exit.Time < sessionBegin)
					continue;

				if (trade.ProfitCurrency < 0)
					cooldownUntilBar = CurrentBar + CooldownBarsAfterLoss;

				WriteCsvEvent("TRADE_CLOSED", trade.Exit.Time, trade.Entry.MarketPosition.ToString(),
					trade.Exit.Price, trade.Quantity, trade.ProfitCurrency, currentEntryScore, "Trade concluído");
			}
		}

		private void EvaluateClosedBarSignal(int bar, bool ordersAllowed)
		{
			currentSignalReady = false;
			if (CurrentBar < bar + GetRequiredLookbackBars() + 2)
				return;

			int signalTime = ToEasternTime(Time[bar]);
			bool insideEntryWindow = signalTime >= EntryStartTime && signalTime < EntryEndTime;
			if (!insideEntryWindow && !GenerateSignalsOutsideSession)
			{
				UpdateStatus("Fora do horário configurado para novas entradas.");
				return;
			}

			if (CurrentBar < cooldownUntilBar)
			{
				UpdateStatus("Cooldown após operação perdedora.");
				return;
			}

			int longEventAge;
			int shortEventAge;
			bool longTrigger = IsLongTrigger(bar, out longEventAge);
			bool shortTrigger = IsShortTrigger(bar, out shortEventAge);
			if (!longTrigger && !shortTrigger)
			{
				UpdateStatus(EnableAutomaticTrading ? "AUTOMÁTICO — aguardando sinal" : "SOMENTE SINAIS — aguardando sinal");
				return;
			}

			bool isLong = longTrigger;
			int eventAge = isLong ? longEventAge : shortEventAge;
			string timingBlockReason;
			if (!PassesAntiChasingFilter(isLong, bar, eventAge, out timingBlockReason))
			{
				lastEntryFilterReason = timingBlockReason;
				WriteCsvEvent("SIGNAL_BLOCKED_LATE", Time[bar], isLong ? "LONG" : "SHORT",
					Close[bar], Contracts, 0, 0, timingBlockReason);
				UpdateStatus((isLong ? "Compra" : "Venda") + " bloqueada — " + timingBlockReason);
				return;
			}

			int score = CalculateEntryScore(isLong, bar, true);
			currentEntryScore = score;
			bool scorePassed = !UseQualityScore || score >= MinimumEntryScore;

			if (scorePassed)
			{
				lastEntryFilterReason = "Aprovado: evento MACD há " + eventAge + " tijolo(s).";
				DrawSignal(isLong, bar, score);
			}
			WriteCsvEvent(scorePassed ? "SIGNAL_ELIGIBLE" : "SIGNAL_BLOCKED", Time[bar],
				isLong ? "LONG" : "SHORT", Close[bar], Contracts, 0, score,
				scorePassed ? "Score aprovado; evento MACD há " + eventAge + " tijolo(s)"
					: "Score abaixo do mínimo; evento MACD há " + eventAge + " tijolo(s)");

			if (!scorePassed)
			{
				UpdateStatus((isLong ? "Compra" : "Venda") + " bloqueada — score " + score + "/" + MinimumEntryScore);
				return;
			}

			if (!insideEntryWindow)
			{
				UpdateStatus("SINAL INFORMATIVO fora da janela permitida; nenhuma ordem será enviada.");
				return;
			}

			if (!ordersAllowed || dailyLocked || orderErrorHalt || connectionErrorHalt || !configurationValid)
			{
				UpdateStatus("SINAL informativo; execução bloqueada por limite de risco, estado da conexão ou configuração.");
				return;
			}

			currentSignalReady = true;
			if (!EnableAutomaticTrading)
			{
				UpdateStatus("SOMENTE SINAIS — " + (isLong ? "COMPRA" : "VENDA") + " elegível; nenhuma ordem enviada.");
				return;
			}

			if (Contracts > activeSessionContractLimit)
			{
				UpdateStatus("AUTOMÁTICO BLOQUEADO — quantidade acima do limite de contratos configurado.");
				return;
			}

			if (!ConfirmAutomaticRisk)
			{
				UpdateStatus("AUTOMÁTICO BLOQUEADO — confirme que os limites de risco foram revisados.");
				return;
			}

			if (Account == null || string.IsNullOrEmpty(Account.Name))
			{
				UpdateStatus("AUTOMÁTICO BLOQUEADO — não foi possível identificar a conta selecionada.");
				return;
			}

			if (HasAccountPositionOnInstrument())
			{
				UpdateStatus("AUTOMÁTICO BLOQUEADO — já existe posição na conta para este instrumento.");
				return;
			}

			if (!Account.Name.StartsWith("Sim", StringComparison.OrdinalIgnoreCase)
				&& !(AccountStageSelected == AccountStage.Real ? AllowLiveTrading : AllowPropSimAccount))
			{
				UpdateStatus("AUTOMÁTICO BLOQUEADO — confirme a conta simulada da mesa ou a autorização de conta real.");
				return;
			}

			if (EnableAutomaticTrading && !configurationValid)
				return;

			entryOrderPending = true;
			entryCancelRequested = false;
			protectiveStopConfigured = true;
			breakEvenStopActive = false;
			if (isLong)
			{
				SetStopLoss("LongEntry", CalculationMode.Ticks, EmergencyStopTicks, false);
				EnterLong(Contracts, "LongEntry");
			}
			else
			{
				SetStopLoss("ShortEntry", CalculationMode.Ticks, EmergencyStopTicks, false);
				EnterShort(Contracts, "ShortEntry");
			}

			WriteCsvEvent("ENTRY_SUBMITTED", Time[bar], isLong ? "LONG" : "SHORT",
				Close[bar], Contracts, 0, score, "Entrada enviada pela estratégia");
			UpdateStatus("AUTOMÁTICO — ordem de " + (isLong ? "COMPRA" : "VENDA") + " enviada; aguardando execução.");
		}

		private bool IsLongTrigger(int bar, out int eventAge)
		{
			eventAge = -1;
			return Close[bar] > emaMacro[bar]
				&& emaMacro[bar] >= emaMacro[bar + 1]
				&& IsInRecentZone(true, bar)
				&& macd.MACDLine[bar] < 40
				&& macd.MACDLine[bar] > macd.SignalLine[bar]
				&& HasFreshMacdEvent(true, bar, out eventAge)
				&& Close[bar] > Open[bar]
				&& Close[bar] > emaFast[bar]
				&& PassesMovingAverageFilter(true, bar)
				&& HasBarrierSpace(true, bar);
		}

		private bool IsShortTrigger(int bar, out int eventAge)
		{
			eventAge = -1;
			return Close[bar] < emaMacro[bar]
				&& emaMacro[bar] <= emaMacro[bar + 1]
				&& IsInRecentZone(false, bar)
				&& macd.MACDLine[bar] > -40
				&& macd.MACDLine[bar] < macd.SignalLine[bar]
				&& HasFreshMacdEvent(false, bar, out eventAge)
				&& Close[bar] < Open[bar]
				&& Close[bar] < emaFast[bar]
				&& PassesMovingAverageFilter(false, bar)
				&& HasBarrierSpace(false, bar);
		}

		private bool HasFreshMacdEvent(bool isLong, int bar, out int eventAge)
		{
			eventAge = -1;
			for (int age = 0; age <= MaxBarsAfterMacdEvent; age++)
			{
				int eventBar = bar + age;
				bool crossed = isLong
					? macd.MACDLine[eventBar] > macd.SignalLine[eventBar]
						&& macd.MACDLine[eventBar + 1] <= macd.SignalLine[eventBar + 1]
					: macd.MACDLine[eventBar] < macd.SignalLine[eventBar]
						&& macd.MACDLine[eventBar + 1] >= macd.SignalLine[eventBar + 1];
				if (crossed)
				{
					eventAge = age;
					return true;
				}
			}

			// A rejection is a fresh expansion from the signal line without a new cross.
			double gapNow = (macd.MACDLine[bar] - macd.SignalLine[bar]) * (isLong ? 1.0 : -1.0);
			double gapPrevious = (macd.MACDLine[bar + 1] - macd.SignalLine[bar + 1]) * (isLong ? 1.0 : -1.0);
			if (AllowMacdRejectionTrigger && gapNow > 0 && gapPrevious >= 0
				&& gapPrevious <= MacdRejectionMaxGap && gapNow > gapPrevious)
			{
				eventAge = 0;
				return true;
			}
			return false;
		}

		private bool IsInRecentZone(bool isLong, int signalBar)
		{
			for (int i = 0; i < ZoneLookbackBars; i++)
			{
				int barsAgo = signalBar + i;
				double value = macd.MACDLine[barsAgo];
				if (isLong && value >= EntryZoneBuyLow && value <= EntryZoneBuyHigh)
					return true;
				if (!isLong && value >= EntryZoneSellLow && value <= EntryZoneSellHigh)
					return true;
			}
			return false;
		}

		private bool PassesMovingAverageFilter(bool isLong, int bar)
		{
			if (Math.Abs(emaFast[bar] - smaMedium[bar]) < MinAverageDistanceTicks * TickSize)
				return false;
			if (isLong ? emaFast[bar] <= smaMedium[bar] : emaFast[bar] >= smaMedium[bar])
				return false;
			if (AllowRecentFastMediumCross)
				return true;

			for (int i = 0; i < NoCrossLookbackBars; i++)
			{
				int current = bar + i;
				int previous = current + 1;
				double currentDifference = emaFast[current] - smaMedium[current];
				double previousDifference = emaFast[previous] - smaMedium[previous];
				if (currentDifference == 0 || previousDifference == 0
					|| Math.Sign(currentDifference) != Math.Sign(previousDifference))
					return false;
			}

			return true;
		}

		private bool PassesAntiChasingFilter(bool isLong, int bar, int eventAge, out string reason)
		{
			if (eventAge > MaxBarsAfterMacdEvent)
			{
				reason = "gatilho MACD atrasado (" + eventAge + "/" + MaxBarsAfterMacdEvent + " tijolos)";
				return false;
			}

			int sameDirectionBricks = 0;
			for (int i = bar; i <= bar + MaxSameDirectionBricksBeforeEntry; i++)
			{
				bool directionalBrick = isLong ? Close[i] > Open[i] : Close[i] < Open[i];
				if (!directionalBrick)
					break;
				sameDirectionBricks++;
			}
			if (sameDirectionBricks > MaxSameDirectionBricksBeforeEntry)
			{
				reason = "pernada estendida (" + sameDirectionBricks + " tijolos na direção)";
				return false;
			}

			double distanceFromFast = (Close[bar] - emaFast[bar]) * (isLong ? 1.0 : -1.0);
			double maxDistance = MaxDistanceFromFastEmaBricks * BrickSizeTicks * TickSize;
			if (distanceFromFast > maxDistance)
			{
				reason = "preço estendido da EMA rápida (" + Math.Round(distanceFromFast / (BrickSizeTicks * TickSize), 1)
					+ "/" + MaxDistanceFromFastEmaBricks + " tijolos)";
				return false;
			}

			reason = "Gatilho fresco e sem extensão.";
			return true;
		}

		private bool HasBarrierSpace(bool isLong, int bar)
		{
			double brickSize = BrickSizeTicks * TickSize;
			double barrier = isLong ? double.MinValue : double.MaxValue;
			for (int i = 1; i <= BarrierLookbackBars; i++)
			{
				double value = isLong ? High[bar + i] : Low[bar + i];
				barrier = isLong ? Math.Max(barrier, value) : Math.Min(barrier, value);
			}

			double freeSpace = isLong ? barrier - Close[bar] : Close[bar] - barrier;
			return freeSpace >= brickSize;
		}

		private int CalculateEntryScore(bool isLong, int bar, bool includeFreshTriggerBonus = false)
		{
			int score = 0;
			double direction = isLong ? 1.0 : -1.0;
			double brickSize = BrickSizeTicks * TickSize;
			double macroSlope = (emaMacro[bar] - emaMacro[bar + 5]) * direction;
			double macroDistance = (Close[bar] - emaMacro[bar]) * direction;

			if (macroSlope >= MacroSlopeMinTicks * TickSize)
				score += 15;
			// The trigger has already passed structural filters. Reward its freshness so a clean
			// reversal is not rejected merely because several correlated score components lag.
			if (includeFreshTriggerBonus)
				score += FreshTriggerScoreBonus;
			if (macroDistance >= HealthyDistanceMinBricks * brickSize
				&& macroDistance <= HealthyDistanceMaxBricks * brickSize)
				score += 10;

			double macdValue = macd.MACDLine[bar];
			if (isLong ? (macdValue >= -40 && macdValue <= -20) : (macdValue >= 20 && macdValue <= 40))
				score += 15;
			double acceleration = (macd.MACDLine[bar] - macd.MACDLine[bar + 2]) * direction;
			if (acceleration >= MacdAccelerationThreshold)
				score += 10;
			if (!IsMacdFlatAtExtreme(isLong, bar))
				score += 5;

			if (isLong ? emaFast[bar] > smaMedium[bar] : emaFast[bar] < smaMedium[bar])
				score += 10;
			double fastSlope = (emaFast[bar] - emaFast[bar + 3]) * direction;
			double mediumSlope = (smaMedium[bar] - smaMedium[bar + 3]) * direction;
			if (fastSlope >= ShortAverageSlopeMinTicks * TickSize
				&& mediumSlope >= ShortAverageSlopeMinTicks * TickSize)
				score += 10;

			if (HasFreeSpaceForScore(isLong, bar, brickSize))
				score += 10;
			if (HasCleanPullback(isLong, bar))
				score += 5;

			int time = ToEasternTime(Time[bar]);
			if (IsWithin(time, MainLiquidityStart1, MainLiquidityEnd1)
				|| IsWithin(time, MainLiquidityStart2, MainLiquidityEnd2))
				score += 10;
			else if (IsWithin(time, SecondaryLiquidityStart1, SecondaryLiquidityEnd1)
				|| IsWithin(time, SecondaryLiquidityStart2, SecondaryLiquidityEnd2))
				score += 5;

			return Math.Max(0, Math.Min(100, score));
		}

		private bool IsMacdFlatAtExtreme(bool isLong, int bar)
		{
			double highest = double.MinValue;
			double lowest = double.MaxValue;
			for (int i = 0; i < 5; i++)
			{
				double value = macd.MACDLine[bar + i];
				highest = Math.Max(highest, value);
				lowest = Math.Min(lowest, value);
			}

			bool touchedExtreme = isLong
				? lowest <= -MacdExtremeThreshold
				: highest >= MacdExtremeThreshold;
			return touchedExtreme && highest - lowest <= MacdFlatRangeThreshold;
		}

		private bool HasFreeSpaceForScore(bool isLong, int bar, double brickSize)
		{
			double barrier = isLong ? double.MinValue : double.MaxValue;
			for (int i = 1; i <= ScoreBarrierLookbackBars; i++)
			{
				double value = isLong ? High[bar + i] : Low[bar + i];
				barrier = isLong ? Math.Max(barrier, value) : Math.Min(barrier, value);
			}
			double freeSpace = isLong ? barrier - Close[bar] : Close[bar] - barrier;
			return freeSpace >= 2 * brickSize;
		}

		private bool HasCleanPullback(bool isLong, int bar)
		{
			int counterTrendBars = 0;
			for (int i = bar + 1; i <= bar + 4; i++)
			{
				bool counterTrend = isLong ? Close[i] < Open[i] : Close[i] > Open[i];
				if (!counterTrend)
					break;
				counterTrendBars++;
			}
			return counterTrendBars >= 2 && counterTrendBars <= 4;
		}

		private void ManagePositionAtBarClose(int bar)
		{
			if (CurrentBar < bar + 2)
				return;

			bool isLong = Position.MarketPosition == MarketPosition.Long;
			double unrealized = Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[bar]);
			int weaknessAlerts = 0;

			if (isLong ? macd.MACDLine[bar] < macd.MACDLine[bar + 1]
				: macd.MACDLine[bar] > macd.MACDLine[bar + 1])
				weaknessAlerts++;
			if (isLong ? Close[bar] < emaFast[bar] : Close[bar] > emaFast[bar])
				weaknessAlerts++;
			if (isLong
				? (macd.MACDLine[bar] < macd.SignalLine[bar] && macd.MACDLine[bar + 1] >= macd.SignalLine[bar + 1])
				: (macd.MACDLine[bar] > macd.SignalLine[bar] && macd.MACDLine[bar + 1] <= macd.SignalLine[bar + 1]))
				weaknessAlerts++;

			bool failureExit = isLong
				? ((Close[bar] < Open[bar] && Close[bar] < emaFast[bar] && macd.MACDLine[bar] < macd.SignalLine[bar])
					|| macd.MACDLine[bar] < ExitFailureBuy)
				: ((Close[bar] > Open[bar] && Close[bar] > emaFast[bar] && macd.MACDLine[bar] > macd.SignalLine[bar])
					|| macd.MACDLine[bar] > ExitFailureSell);

			bool profitableExitTooEarly = unrealized > 0
				&& firstEntryFillTime != DateTime.MinValue
				&& (Time[bar] - firstEntryFillTime).TotalSeconds < MinimumProfitableHoldSeconds;

			if ((failureExit || (weaknessAlerts >= 2 && unrealized < 0))
				&& !(failureExit && profitableExitTooEarly))
			{
				if (EnableAutomaticTrading)
					FlattenPosition(failureExit ? "Falha técnica" : "Dois alertas de enfraquecimento no prejuízo");
				else
					DrawExitNotice(bar, "Condição técnica de saída");
				return;
			}

			double breakevenDistance = BreakevenAfterBricks * BrickSizeTicks * TickSize;
			bool minimumHoldElapsed = firstEntryFillTime == DateTime.MinValue
				|| (Time[bar] - firstEntryFillTime).TotalSeconds >= MinimumProfitableHoldSeconds;
			if (EnableAutomaticTrading && minimumHoldElapsed && Position.MarketPosition == MarketPosition.Long
				&& Close[bar] >= Position.AveragePrice + breakevenDistance)
			{
				SetStopLoss("LongEntry", CalculationMode.Price, Position.AveragePrice + TickSize, false);
				breakEvenStopActive = true;
			}
			else if (EnableAutomaticTrading && minimumHoldElapsed && Position.MarketPosition == MarketPosition.Short
				&& Close[bar] <= Position.AveragePrice - breakevenDistance)
			{
				SetStopLoss("ShortEntry", CalculationMode.Price, Position.AveragePrice - TickSize, false);
				breakEvenStopActive = true;
			}

			bool exhaustionExit = isLong
				? (macd.MACDLine[bar] >= 35 && macd.MACDLine[bar] <= macd.MACDLine[bar + 1] && unrealized > 0)
				: (macd.MACDLine[bar] <= -35 && macd.MACDLine[bar] >= macd.MACDLine[bar + 1] && unrealized > 0);
			bool reversalAfterProfit = unrealized >= brickSizeValue()
				&& (isLong
					? (Close[bar] < Open[bar] && macd.MACDLine[bar] < macd.SignalLine[bar]
						&& macd.MACDLine[bar + 1] >= macd.SignalLine[bar + 1])
					: (Close[bar] > Open[bar] && macd.MACDLine[bar] > macd.SignalLine[bar]
						&& macd.MACDLine[bar + 1] <= macd.SignalLine[bar + 1]));

			if ((exhaustionExit || reversalAfterProfit) && !profitableExitTooEarly)
			{
				if (EnableAutomaticTrading)
					FlattenPosition(exhaustionExit ? "Exaustão MACDNG" : "Reversão após lucro");
				else
					DrawExitNotice(bar, exhaustionExit ? "Exaustão MACDNG" : "Reversão após lucro");
			}
		}

		private double brickSizeValue()
		{
			return BrickSizeTicks * TickSize * Contracts * Instrument.MasterInstrument.PointValue;
		}

		private void FlattenPosition(string reason)
		{
			if (Position.MarketPosition == MarketPosition.Flat || exitOrderPending)
				return;

			exitOrderPending = true;
			if (Position.MarketPosition == MarketPosition.Long)
				ExitLong("StrategyExit", "LongEntry");
			else if (Position.MarketPosition == MarketPosition.Short)
				ExitShort("StrategyExit", "ShortEntry");

			Print(Name + ": " + reason);
			UpdateStatus(reason);
		}

		private void DrawSignal(bool isLong, int bar, int score)
		{
			int signalBarNumber = CurrentBar - bar;
			string tag = (isLong ? "BuySignal_" : "SellSignal_") + signalBarNumber;
			Brush brush = isLong ? Brushes.LimeGreen : Brushes.IndianRed;

			if (isLong)
				Draw.ArrowUp(this, tag, false, bar, Low[bar] - 2 * TickSize, brush);
			else
				Draw.ArrowDown(this, tag, false, bar, High[bar] + 2 * TickSize, brush);

			string scoreLabel = score >= HighConfluenceScore ? "ALTA CONFLUÊNCIA " + score : score.ToString();
			Draw.Text(this, tag + "_score", scoreLabel, bar,
				isLong ? Low[bar] - 4 * TickSize : High[bar] + 4 * TickSize, brush);

			if (EnableSoundAlerts && State == State.Realtime)
			{
				string soundPath = Path.Combine(NinjaTrader.Core.Globals.InstallDir, "sounds", "Alert1.wav");
				Alert(tag + "_alert", Priority.Medium, isLong ? "Sinal de compra elegível" : "Sinal de venda elegível",
					soundPath, 0, Brushes.Black, brush);
			}
		}

		private void DrawExitNotice(int bar, string message)
		{
			string tag = "PulseGuard_Exit_" + CurrentBar;
			Draw.Dot(this, tag, false, bar, Close[bar], Brushes.Gold);
			Draw.Text(this, tag + "_text", message, bar, Close[bar], Brushes.Gold);
			if (EnableSoundAlerts && State == State.Realtime)
			{
				string soundPath = Path.Combine(NinjaTrader.Core.Globals.InstallDir, "sounds", "Alert1.wav");
				Alert(tag + "_alert", Priority.High, message, soundPath, 0, Brushes.Black, Brushes.Gold);
			}
		}

		private void UpdateStatus(string message)
		{
			lastStatusMessage = message;
			RenderDashboard();
		}

		private void SubscribeAccountUpdates()
		{
			if (Account == null || object.ReferenceEquals(monitoredAccount, Account))
				return;

			UnsubscribeAccountUpdates();
			monitoredAccount = Account;
			monitoredAccount.AddTrade += OnAccountTradeAdded;
			monitoredAccount.PositionUpdate += OnAccountPositionUpdated;
		}

		private void UnsubscribeAccountUpdates()
		{
			if (monitoredAccount == null)
				return;

			monitoredAccount.AddTrade -= OnAccountTradeAdded;
			monitoredAccount.PositionUpdate -= OnAccountPositionUpdated;
			monitoredAccount = null;
		}

		private void RefreshAccountSnapshot()
		{
			if (Account == null || State != State.Realtime)
				return;

			accountRealizedPnl = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
			accountUnrealizedPnl = Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
			accountOpenPositionCount = 0;
			accountHasPositionOnInstrument = false;
			foreach (Position accountPosition in Account.Positions)
			{
				if (accountPosition.Quantity != 0
					&& accountPosition.MarketPosition != MarketPosition.Flat)
				{
					accountOpenPositionCount++;
					if (Instrument != null && accountPosition.Instrument != null
						&& accountPosition.Instrument.FullName == Instrument.FullName)
						accountHasPositionOnInstrument = true;
				}
			}
		}

		private bool HasAccountPositionOnInstrument()
		{
			return accountHasPositionOnInstrument;
		}

		private void RefreshAccountDashboard(object state)
		{
			if (State != State.Realtime)
				return;

			RefreshAccountSnapshot();
			RenderDashboard();
		}

		private void RenderAccountDashboard(object state)
		{
			if (State == State.Realtime)
				RenderDashboard();
		}

		private void OnAccountPositionUpdated(object sender, PositionEventArgs e)
		{
			if (State == State.Realtime)
				TriggerCustomEvent(RefreshAccountDashboard, null);
		}

		private void OnAccountTradeAdded(object sender, AddTradeEventArgs e)
		{
			if (State == State.Realtime && e != null && e.Trade != null)
				TriggerCustomEvent(RegisterAccountTrade, e.Trade);
		}

		private void RegisterAccountTrade(object state)
		{
			Trade trade = state as Trade;
			if (State != State.Realtime || trade == null || trade.Exit == null
				|| (sessionInitialized && trade.Exit.Time < sessionBegin))
				return;

			accountSessionTradeCount++;
			if (trade.ProfitCurrency > 0)
				accountSessionWins++;
			else if (trade.ProfitCurrency < 0)
				accountSessionLosses++;

			RefreshAccountSnapshot();
			RenderDashboard();
		}

		protected override void OnAccountItemUpdate(Account account, AccountItem accountItem, double value)
		{
			base.OnAccountItemUpdate(account, accountItem, value);
			if (State != State.Realtime || account == null || Account == null
				|| account.Id != Account.Id)
				return;

			if (accountItem == AccountItem.RealizedProfitLoss)
				accountRealizedPnl = value;
			else if (accountItem == AccountItem.UnrealizedProfitLoss)
				accountUnrealizedPnl = value;
			else
				return;

			TriggerCustomEvent(RenderAccountDashboard, null);
		}

		private void RenderDashboard()
		{
			if (ChartControl == null)
				return;
			if (!MostrarPainel)
			{
				QueueDashboardHide();
				return;
			}
			if ((DateTime.UtcNow - lastDashboardUpdateUtc).TotalMilliseconds < 250)
				return;
			lastDashboardUpdateUtc = DateTime.UtcNow;

			bool positioned = Position.MarketPosition != MarketPosition.Flat;
			bool outsideEntryWindow = !IsWithinEntryWindow(Time[0]);
			bool scheduleLock = dailyLocked && !string.IsNullOrEmpty(dailyLockReason)
				&& dailyLockReason.StartsWith("Horário de operação encerrado",
					StringComparison.OrdinalIgnoreCase);
			bool blocked = (dailyLocked && !scheduleLock)
				|| connectionErrorHalt || orderErrorHalt || !configurationValid;
			// Account.Name is selected by the strategy instance; metrics below include the full account.
			string accountName = Account == null || string.IsNullOrEmpty(Account.Name)
				? "N/D" : Account.Name;
			bool simulatedAccount = accountName.StartsWith("Sim", StringComparison.OrdinalIgnoreCase);
			bool accountAllowed = simulatedAccount
				|| (AccountStageSelected == AccountStage.Real ? AllowLiveTrading : AllowPropSimAccount);
			bool accountHasPositionOnInstrument = HasAccountPositionOnInstrument();
			bool canEnter = !blocked && !outsideEntryWindow && EnableAutomaticTrading
				&& ConfirmAutomaticRisk && accountAllowed
				&& Contracts <= activeSessionContractLimit && !accountHasPositionOnInstrument;
			double unrealized = positioned
				? Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]) : 0;

			int dashboardBar = State == State.Historical ? 0 : (CurrentBar > 0 ? 1 : 0);
			if (CurrentBar >= dashboardBar + GetRequiredLookbackBars() + 2
				&& cachedDashboardBarNumber != CurrentBar)
			{
				double slope = emaMacro[dashboardBar] - emaMacro[dashboardBar + 5];
				double priceDelta = Close[dashboardBar] - emaMacro[dashboardBar];
				cachedTrendBullish = priceDelta > 0 && slope > 0;
				cachedTrendBearish = priceDelta < 0 && slope < 0;
				cachedTrendText = cachedTrendBullish ? "ALTA"
					: (cachedTrendBearish ? "BAIXA" : "LATERAL");
				cachedContextScore = CalculateEntryScore(
					cachedTrendBullish || (!cachedTrendBearish && slope >= 0), dashboardBar);
				cachedDashboardBarNumber = CurrentBar;
			}

			double effectiveLossLimit = GetEffectiveDailyLossLimit();
			double riskRemaining = Math.Max(0, effectiveLossLimit + currentSessionPnl);
			bool nearRiskLimit = IsNearLimit(riskRemaining, effectiveLossLimit);
			bool directionalTrend = cachedTrendBullish || cachedTrendBearish;
			bool readyForSignals = CurrentBar >= BarsRequiredToTrade
				&& CurrentBar >= NormalizationPeriod + SlowPeriod + SignalPeriod;
			bool contextReady = readyForSignals && directionalTrend
				&& cachedContextScore >= (UseQualityScore ? MinimumEntryScore : 50);
			bool greenLight = !positioned && !blocked && !outsideEntryWindow
				&& contextReady && currentSignalReady && canEnter && !entryOrderPending;
			bool yellowLight = !blocked && !outsideEntryWindow
				&& (positioned || accountOpenPositionCount > 0
					|| (readyForSignals && (currentSignalReady || nearRiskLimit
					|| (cachedContextScore >= 50 && directionalTrend))));
			string decisionText = greenLight ? "🟢 OPERE AGORA"
				: (yellowLight ? "🟡 ATENÇÃO" : "🔴 NÃO FAZER NADA");
			Brush decisionBrush = greenLight ? Brushes.LimeGreen
				: (yellowLight ? Brushes.Goldenrod : Brushes.OrangeRed);

			string permissionText = blocked ? "BLOQUEADO · " + GetBlockingReason(scheduleLock)
				: (outsideEntryWindow ? "FORA DO HORÁRIO"
					: (canEnter ? "LIBERADO" : "SOMENTE SINAIS"));
			if (!blocked && !outsideEntryWindow && EnableAutomaticTrading && !canEnter)
			{
				permissionText = !ConfirmAutomaticRisk ? "RISCO NÃO CONFIRMADO"
					: (!accountAllowed ? "CONTA NÃO LIBERADA"
						: (accountHasPositionOnInstrument ? "POSIÇÃO NA CONTA"
							: (Contracts > activeSessionContractLimit ? "LIMITE CONTRATOS" : "AGUARDANDO")));
			}

			string accountEnvironment = simulatedAccount ? "SIM" : "SELECIONADA";
			string accountStage = AccountStageSelected == AccountStage.Evaluation ? "CONTA DE AVALIAÇÃO"
				: (AccountStageSelected == AccountStage.Funded ? "CONTA FINANCIADA" : "CONTA REAL");
			string accountRule = "limites configurados pelo usuário";

			string operationText;
			string actionText;
			if (positioned)
			{
				bool isLong = Position.MarketPosition == MarketPosition.Long;
				double signedMove = (Close[0] - Position.AveragePrice) * (isLong ? 1 : -1);
				double ticks = TickSize > 0 ? signedMove / TickSize : 0;
				operationText = (isLong ? "COMPRA " : "VENDA ") + Position.Quantity
					+ " · " + FormatMoney(unrealized) + " · " + ticks.ToString("+0;-0;0") + " ticks";

				int bar = State == State.Historical ? 0 : (CurrentBar > 0 ? 1 : 0);
				bool enoughPositionBars = CurrentBar >= bar + 2;
				bool lostFastAverage = enoughPositionBars
					&& (isLong ? Close[bar] < emaFast[bar] : Close[bar] > emaFast[bar]);
				bool macdAgainst = enoughPositionBars
					&& (isLong
						? macd.MACDLine[bar] < macd.SignalLine[bar]
						: macd.MACDLine[bar] > macd.SignalLine[bar]);
				double breakevenDistance = BreakevenAfterBricks * BrickSizeTicks * TickSize;
				bool breakevenReached = isLong
					? Close[0] >= Position.AveragePrice + breakevenDistance
					: Close[0] <= Position.AveragePrice - breakevenDistance;
				string attention = connectionErrorHalt || orderErrorHalt ? "conexão/ordem"
					: (lostFastAverage ? "média contra"
						: (macdAgainst ? "MACD contra" : "sem alerta técnico"));
				string recommendation = connectionErrorHalt || orderErrorHalt
					? "Confirmar stop/ordens na plataforma"
					: (breakEvenStopActive ? "Stop 0x0 solicitado"
						: (breakevenReached ? "Próximo fechamento: mover p/ 0x0"
							: (lostFastAverage || macdAgainst
								? "Reavaliar saída no fechamento"
								: (protectiveStopConfigured ? "Manter stop; acompanhar"
									: "Confirmar stop na plataforma"))));
				actionText = "Atenção: " + attention + " · Ação: " + recommendation;
			}
			else if (accountOpenPositionCount > 0)
			{
				operationText = "ROBÔ FLAT · CONTA: " + accountOpenPositionCount
					+ " posição(ões) · flutuante " + FormatMoney(accountUnrealizedPnl);
				actionText = accountHasPositionOnInstrument
					? "Manual/ATM no instrumento · conferir stop/saída na plataforma."
					: "Conta tem posição aberta em outro instrumento.";
			}
			else
			{
				operationText = "FLAT (estratégia) · aguardar gatilho + score ≥ "
					+ (UseQualityScore ? MinimumEntryScore : 50) + ".";
				actionText = currentSignalReady
					? (EnableAutomaticTrading ? "Sinal elegível; ordem depende das permissões."
						: "Sinal elegível · modo alertas, sem envio de ordem.")
					: (outsideEntryWindow ? "Próxima janela: " + FormatTime(EntryStartTime) + " ET."
						: "Aguardar confirmação; não antecipar entrada.");
			}

			double targetRemaining = MaxDailyProfit > 0
				? Math.Max(0, MaxDailyProfit - currentSessionPnl) : 0;
			string[] versionRows = BuildVersionRows();
			Brush[] versionBrushes = BuildVersionBrushes();
			string[] rows =
			{
				"PulseGuard · " + BarsPeriod.BarsPeriodType + " " + BarsPeriod.Value
					+ " · " + (EnableAutomaticTrading ? "AUTOMÁTICO" : "ALERTAS"),
				decisionText,
				"CONTA " + accountName + " (" + accountEnvironment + ") · " + accountStage
					+ " (seleção) · " + accountRule + "\n" + GetTimeDiagnostic(),
				"ANTES DE ENTRAR",
				"Permissão: " + permissionText + " · Tendência: " + cachedTrendText,
				"Score: " + cachedContextScore + "/100 · Risco robô restante: " + FormatAmount(riskRemaining)
					+ (nearRiskLimit ? " · PERTO DO LIMITE" : ""),
				"Filtro de entrada: " + lastEntryFilterReason,
				"EM OPERAÇÃO / PRÓXIMA AÇÃO",
				operationText,
				actionText,
				"RESUMO · CONTA + ESTRATÉGIA",
				"Conta NT: Realizado " + FormatMoney(accountRealizedPnl)
					+ " · Aberto " + FormatMoney(accountUnrealizedPnl),
				"Trades conta (sessão · após ativação): " + accountSessionWins + "/" + accountSessionLosses
					+ " · " + accountSessionTradeCount,
				"Estratégia: " + FormatMoney(currentSessionPnl) + " · W/L " + sessionWins + "/" + sessionLosses
					+ " · " + sessionTradeCount + " trades",
				"Meta: " + (MaxDailyProfit > 0
					? FormatAmount(MaxDailyProfit) + " · faltam " + FormatAmount(targetRemaining) : "OFF")
					+ " · Perda robô: " + (MaxDailyLoss > 0 ? FormatAmount(MaxDailyLoss) : "OFF"),
				"DLL: " + (DailyLossLimitEnabled ? FormatAmount(DailyLossLimitAmount) : "OFF")
					+ " · MLL: " + (MaximumLossLimitEnabled
						? FormatAmount(MllRemainingRoom) : "OFF")
					+ " · Máx.: " + activeSessionContractLimit + " contratos",
				versionRows[0],
				versionRows[1],
				versionRows[2],
				versionRows[3],
				versionRows[4],
				versionRows[5]
			};
			Brush[] rowBrushes =
			{
				Brushes.White, decisionBrush, Brushes.LightSteelBlue, Brushes.DeepSkyBlue,
				canEnter ? Brushes.LimeGreen : (blocked || outsideEntryWindow ? Brushes.OrangeRed : Brushes.Gold),
				nearRiskLimit ? Brushes.OrangeRed : Brushes.LightSteelBlue, Brushes.DeepSkyBlue,
				positioned && unrealized < 0 ? Brushes.OrangeRed
					: (positioned ? Brushes.LimeGreen : Brushes.LightGray),
				positioned && (connectionErrorHalt || orderErrorHalt)
					? Brushes.OrangeRed : Brushes.LightSteelBlue,
				Brushes.DeepSkyBlue, accountRealizedPnl >= 0 ? Brushes.LimeGreen : Brushes.OrangeRed,
				Brushes.LightSteelBlue, currentSessionPnl >= 0 ? Brushes.LimeGreen : Brushes.OrangeRed,
				Brushes.LightSteelBlue, Brushes.LightSteelBlue, Brushes.LightSteelBlue,
				versionBrushes[0], versionBrushes[1], versionBrushes[2],
				versionBrushes[3], versionBrushes[4], versionBrushes[5]
			};
			QueueCompactDashboardUpdate(rows, rowBrushes, decisionBrush);
		}

		private string[] BuildVersionRows()
		{
			return new string[]
			{
				"VERSÕES E ATUALIZAÇÕES",
				BuildVersionRow("Robô", PulseGuardVersion.Strategy, strategyVersionResult),
				BuildVersionRow("Indicador", PulseGuardVersion.Indicator, indicatorVersionResult),
				BuildVersionRow("Template", TemplateVersion, templateVersionResult),
				"Build: " + PulseGuardVersion.BuildDate + " · Commit: " + PulseGuardVersion.Commit
					+ " · Verificação local: " + GetLastVersionCheckTime(),
				GetVersionGuidance()
			};
		}

		private Brush[] BuildVersionBrushes()
		{
			return new Brush[]
			{
				Brushes.DeepSkyBlue,
				GetVersionStatusBrush(strategyVersionResult),
				GetVersionStatusBrush(indicatorVersionResult),
				GetVersionStatusBrush(templateVersionResult),
				Brushes.LightSteelBlue,
				GetVersionGuidanceBrush()
			};
		}

		private static string BuildVersionRow(string label, string localVersion,
			PulseGuardUpdateResult result)
		{
			string installed = string.IsNullOrWhiteSpace(localVersion)
				? "informado manualmente" : localVersion;
			string latest = result == null || string.IsNullOrWhiteSpace(result.LatestVersion)
				? "—" : result.LatestVersion;
			return label + ": " + installed + " · GitHub " + latest + " · "
				+ GetVersionStatusText(result);
		}

		private static string GetVersionStatusText(PulseGuardUpdateResult result)
		{
			if (result == null)
				return "Não verificado";

			switch (result.Status)
			{
				case PulseGuardUpdateStatus.Atualizado:
					return "Atualizado";
				case PulseGuardUpdateStatus.AtualizacaoDisponivel:
					return "Atualização disponível";
				case PulseGuardUpdateStatus.LocalMaisNovo:
					return result.Reason == "Versão local de desenvolvimento."
						? "Local mais nova (dev)" : "Local mais nova";
				case PulseGuardUpdateStatus.Incompativel:
					return "Incompatível";
				case PulseGuardUpdateStatus.Desligado:
					return "Desligado";
				default:
					return "Não verificado";
			}
		}

		private static Brush GetVersionStatusBrush(PulseGuardUpdateResult result)
		{
			if (result == null)
				return Brushes.LightGray;

			switch (result.Status)
			{
				case PulseGuardUpdateStatus.Atualizado:
					return Brushes.LimeGreen;
				case PulseGuardUpdateStatus.AtualizacaoDisponivel:
					return Brushes.Goldenrod;
				case PulseGuardUpdateStatus.LocalMaisNovo:
					return Brushes.LightSkyBlue;
				case PulseGuardUpdateStatus.Incompativel:
					return Brushes.OrangeRed;
				case PulseGuardUpdateStatus.Desligado:
				case PulseGuardUpdateStatus.NaoVerificado:
				default:
					return Brushes.LightGray;
			}
		}

		private string GetVersionGuidance()
		{
			PulseGuardUpdateResult[] results =
			{
				strategyVersionResult, indicatorVersionResult, templateVersionResult
			};
			foreach (PulseGuardUpdateResult result in results)
			{
				if (result != null && result.Status == PulseGuardUpdateStatus.Incompativel)
					return "Incompatível: atualize os componentes necessários em "
						+ GetReleaseNotesUrl(result) + ".";
			}
			foreach (PulseGuardUpdateResult result in results)
			{
				if (result != null && result.Status == PulseGuardUpdateStatus.AtualizacaoDisponivel)
					return "Atualização disponível: consulte " + GetReleaseNotesUrl(result) + ".";
			}
			return "";
		}

		private Brush GetVersionGuidanceBrush()
		{
			PulseGuardUpdateResult[] results =
			{
				strategyVersionResult, indicatorVersionResult, templateVersionResult
			};
			foreach (PulseGuardUpdateResult result in results)
			{
				if (result != null && result.Status == PulseGuardUpdateStatus.Incompativel)
					return Brushes.OrangeRed;
			}
			foreach (PulseGuardUpdateResult result in results)
			{
				if (result != null && result.Status == PulseGuardUpdateStatus.AtualizacaoDisponivel)
					return Brushes.Goldenrod;
			}
			return Brushes.LightSteelBlue;
		}

		private static string GetReleaseNotesUrl(PulseGuardUpdateResult result)
		{
			return result == null || string.IsNullOrWhiteSpace(result.ReleaseNotesUrl)
				? "GitHub Releases" : result.ReleaseNotesUrl;
		}

		private string GetLastVersionCheckTime()
		{
			DateTime? lastChecked = null;
			PulseGuardUpdateResult[] results =
			{
				strategyVersionResult, indicatorVersionResult, templateVersionResult
			};
			foreach (PulseGuardUpdateResult result in results)
			{
				if (result != null && result.CheckedAt.HasValue
					&& (!lastChecked.HasValue || result.CheckedAt.Value > lastChecked.Value))
					lastChecked = result.CheckedAt;
			}
			if (!lastChecked.HasValue && !CheckForUpdates)
				return "desligada";
			return lastChecked.HasValue
				? lastChecked.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
				: "aguardando";
		}

		private async void CheckComponentVersions()
		{
			try
			{
				Task<PulseGuardUpdateResult>[] checks =
				{
					PulseGuardUpdateChecker.CheckAsync(PulseGuardComponent.Strategy,
						PulseGuardVersion.Strategy, PulseGuardVersion.Indicator, CheckForUpdates),
					PulseGuardUpdateChecker.CheckAsync(PulseGuardComponent.Indicator,
						PulseGuardVersion.Indicator, PulseGuardVersion.Indicator, CheckForUpdates),
					PulseGuardUpdateChecker.CheckAsync(PulseGuardComponent.Template,
						TemplateVersion, PulseGuardVersion.Indicator, CheckForUpdates)
				};
				await Task.WhenAll(checks).ConfigureAwait(false);
				strategyVersionResult = checks[0].Result;
				indicatorVersionResult = checks[1].Result;
				templateVersionResult = checks[2].Result;
				Print("PulseGuard version check: robô " + GetVersionStatusText(strategyVersionResult)
					+ ", indicador " + GetVersionStatusText(indicatorVersionResult)
					+ ", template " + GetVersionStatusText(templateVersionResult) + ".");
				QueueVersionPanelRefresh();
			}
			catch (Exception exception)
			{
				PulseGuardUpdateResult failed = new PulseGuardUpdateResult(
					PulseGuardUpdateStatus.NaoVerificado, null,
					exception.GetType().Name, null, DateTime.Now);
				strategyVersionResult = failed;
				indicatorVersionResult = failed;
				templateVersionResult = failed;
				Print("PulseGuard version check: Não verificado.");
				QueueVersionPanelRefresh();
			}
		}

		private void QueueVersionPanelRefresh()
		{
			if (ChartControl == null || ChartControl.Dispatcher.HasShutdownStarted
				|| ChartControl.Dispatcher.HasShutdownFinished)
				return;

			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				if (State == State.Terminated)
					return;

				if (dashboardRows != null && dashboardRows.Length >= 22)
				{
					string[] rows = BuildVersionRows();
					Brush[] brushes = BuildVersionBrushes();
					for (int i = 0; i < rows.Length; i++)
					{
						int rowIndex = i + 16;
						dashboardRows[rowIndex].Text = rows[i];
						dashboardRows[rowIndex].Foreground = brushes[i];
						dashboardRows[rowIndex].Visibility = !ShowVersionPanel
							|| (i == 5 && string.IsNullOrWhiteSpace(rows[i]))
							? Visibility.Collapsed : Visibility.Visible;
					}
				}
				ForceRefresh();
			}));
		}

		private void QueueCompactDashboardUpdate(string[] rows, Brush[] rowBrushes, Brush accentBrush)
		{
			if (ChartControl == null || ChartControl.Dispatcher.HasShutdownStarted
				|| ChartControl.Dispatcher.HasShutdownFinished)
				return;

			lock (dashboardUpdateLock)
			{
				pendingDashboardRows = rows;
				pendingDashboardRowBrushes = rowBrushes;
				pendingDashboardAccentBrush = accentBrush;
				if (dashboardUpdateQueued)
					return;
				dashboardUpdateQueued = true;
			}

			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				string[] rowsToApply;
				Brush[] brushesToApply;
				Brush accentToApply;
				lock (dashboardUpdateLock)
				{
					rowsToApply = pendingDashboardRows;
					brushesToApply = pendingDashboardRowBrushes;
					accentToApply = pendingDashboardAccentBrush;
					pendingDashboardRows = null;
					pendingDashboardRowBrushes = null;
					pendingDashboardAccentBrush = null;
					dashboardUpdateQueued = false;
				}

				if (State == State.Terminated || rowsToApply == null || brushesToApply == null)
					return;

				if (dashboardControl == null)
					CreateCompactDashboardControl(rowsToApply.Length);

				dashboardControl.Visibility = Visibility.Visible;
				dashboardCard.BorderBrush = accentToApply;
				dashboardCard.Background = GetPanelBackground();
				dashboardRows[1].Background = accentToApply;
				for (int i = 0; i < dashboardRows.Length; i++)
				{
					dashboardRows[i].Text = rowsToApply[i];
					dashboardRows[i].Foreground = brushesToApply[i];
					dashboardRows[i].FontSize = i == 0 ? TamanhoFontePainel + 1
						: (i == 1 ? TamanhoFontePainel + 2
							: (i == 3 || i == 6 || i == 9 || i == 16
								? TamanhoFontePainel : TamanhoFontePainel - 1));
					bool section = i == 3 || i == 6 || i == 9 || i == 16;
					if (i >= 16)
						dashboardRows[i].Visibility = !ShowVersionPanel
							|| (i == 21 && string.IsNullOrWhiteSpace(rowsToApply[i]))
							? Visibility.Collapsed : Visibility.Visible;
					else
						dashboardRows[i].Visibility = ModoPainel == PulseGuardDashboardMode.Detailed || !section
							? Visibility.Visible : Visibility.Collapsed;
					if (section)
						dashboardRows[i].Foreground = Brushes.DeepSkyBlue;
				}
				dashboardRows[1].Foreground = Brushes.White;
			}));
		}

		private void QueueDashboardHide()
		{
			if (ChartControl == null || ChartControl.Dispatcher.HasShutdownStarted
				|| ChartControl.Dispatcher.HasShutdownFinished)
				return;

			lock (dashboardUpdateLock)
			{
				if (dashboardHideQueued)
					return;
				dashboardHideQueued = true;
			}

			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				try
				{
					if (dashboardControl != null)
						dashboardControl.Visibility = Visibility.Collapsed;
				}
				finally
				{
					lock (dashboardUpdateLock)
						dashboardHideQueued = false;
				}
			}));
		}

		private void CreateCompactDashboardControl(int rowCount)
		{
			StackPanel content = new StackPanel { Orientation = Orientation.Vertical };
			dashboardRows = new TextBlock[rowCount];
			for (int i = 0; i < dashboardRows.Length; i++)
			{
				bool section = i == 3 || i == 6 || i == 9 || i == 16;
				TextBlock row = new TextBlock
				{
					FontFamily = new FontFamily("Segoe UI"),
					FontSize = TamanhoFontePainel,
					Foreground = Brushes.White,
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(0, section ? 7 : 2, 0, 2),
					Padding = new Thickness(0, 1, 0, 1),
					FontWeight = section ? FontWeights.Bold : FontWeights.Normal
				};
				if (i == 1)
				{
					row.FontSize = TamanhoFontePainel + 2;
					row.FontWeight = FontWeights.Bold;
					row.Padding = new Thickness(8, 5, 8, 5);
				}
				dashboardRows[i] = row;
				content.Children.Add(row);
			}

			dashboardCard = new Border
			{
				Width = 440,
				Padding = new Thickness(12, 8, 12, 10),
				Background = GetPanelBackground(),
				BorderBrush = Brushes.DeepSkyBlue,
				BorderThickness = new Thickness(2),
				CornerRadius = new CornerRadius(8),
				Child = content
			};
			dashboardControl = new UserControl
			{
				Content = dashboardCard,
				Background = Brushes.Transparent,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(12),
				IsHitTestVisible = false
			};
			UserControlCollection.Add(dashboardControl);
		}

		private void RenderDashboardLegacy()
		{
			if (ChartControl == null)
				return;
			if (!MostrarPainel)
			{
				QueueDashboardHide();
				return;
			}
			if ((DateTime.UtcNow - lastDashboardUpdateUtc).TotalMilliseconds < 250)
				return;
			lastDashboardUpdateUtc = DateTime.UtcNow;

			bool positioned = Position.MarketPosition != MarketPosition.Flat;
			bool outsideEntryWindow = !IsWithinEntryWindow(Time[0]);
			bool afterFlattenTime = IsInFlattenWindow(Time[0]);
			double unrealized = positioned
				? Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0])
				: 0;
			bool scheduleLock = dailyLocked && !string.IsNullOrEmpty(dailyLockReason)
				&& dailyLockReason.StartsWith("Horário de operação encerrado",
					StringComparison.OrdinalIgnoreCase);
			bool blocked = (dailyLocked && !scheduleLock)
				|| connectionErrorHalt || orderErrorHalt || !configurationValid;
			bool accountAllowed = Account != null && !string.IsNullOrEmpty(Account.Name)
				&& (Account.Name.StartsWith("Sim", StringComparison.OrdinalIgnoreCase)
					|| AllowPropSimAccount || AllowLiveTrading);
			bool canEnter = !blocked && !outsideEntryWindow && EnableAutomaticTrading
				&& ConfirmAutomaticRisk && accountAllowed
				&& Contracts <= activeSessionContractLimit;
			string robotState;
			string reason;
			Brush stateBrush;

			if (positioned)
			{
				robotState = Position.MarketPosition == MarketPosition.Long
					? "POSICIONADO · COMPRA"
					: "POSICIONADO · VENDA";
				reason = connectionErrorHalt || orderErrorHalt
					? "ATENÇÃO: confirme posição, ordens e stop na plataforma."
					: (dailyLocked ? dailyLockReason : "Operação em acompanhamento pelo robô.");
				stateBrush = connectionErrorHalt || orderErrorHalt
					? Brushes.Orange
					: (unrealized < 0 ? Brushes.OrangeRed : Brushes.LimeGreen);
			}
			else if (blocked)
			{
				robotState = "BLOQUEADO";
				reason = connectionErrorHalt
					? "Conexão perdida; novas entradas estão bloqueadas."
					: (orderErrorHalt
						? "Falha em ordem; confira a plataforma antes de reiniciar."
						: (dailyLocked
							? (string.IsNullOrEmpty(dailyLockReason) ? "Limite ou pausa diária atingida." : dailyLockReason)
							: "Gráfico ou parâmetros não correspondem ao perfil configurado."));
				stateBrush = Brushes.OrangeRed;
			}
			else if (outsideEntryWindow)
			{
				robotState = GenerateSignalsOutsideSession
					? "ALERTAS APENAS · FORA DA JANELA"
					: "PAUSADO · FORA DO HORÁRIO";
				reason = afterFlattenTime
					? "Sessão encerrada; nenhuma entrada será enviada."
					: "Próxima janela: " + FormatTime(EntryStartTime) + " ET.";
				stateBrush = Brushes.DeepSkyBlue;
			}
			else
			{
				robotState = canEnter
					? "ATIVO · LIBERADO PARA NOVA ENTRADA"
					: "ATIVO · SOMENTE SINAIS";
				reason = canEnter
					? "Regras de risco e horário permitem uma entrada automática."
					: (!EnableAutomaticTrading
						? "Modo de alertas: nenhuma ordem será enviada automaticamente."
						: (!ConfirmAutomaticRisk
							? "Revise e confirme os limites de risco antes de habilitar ordens."
							: (!accountAllowed
								? "Conta não confirmada para automação."
								: (Contracts > activeSessionContractLimit
									? "Quantidade acima do máximo permitido para esta sessão."
									: GetPlainReason()))));
				stateBrush = canEnter ? Brushes.LimeGreen : Brushes.Gold;
			}

			int dashboardBar = State == State.Historical ? 0 : (CurrentBar > 0 ? 1 : 0);
			bool enoughBars = CurrentBar >= dashboardBar + GetRequiredLookbackBars() + 2;
			if (enoughBars && cachedDashboardBarNumber != CurrentBar)
			{
				double slope = emaMacro[dashboardBar] - emaMacro[dashboardBar + 5];
				double priceDelta = Close[dashboardBar] - emaMacro[dashboardBar];
				bool bullish = priceDelta > 0 && slope > 0;
				bool bearish = priceDelta < 0 && slope < 0;
				cachedTrendBullish = bullish;
				cachedTrendBearish = bearish;
				cachedTrendText = bullish
					? "Alta (mais atenção a compras)"
					: (bearish ? "Baixa (mais atenção a vendas)" : "Lateral / sem confirmação (evitando risco)");

				int contextScore = CalculateEntryScore(bullish || (!bearish && slope >= 0), dashboardBar);
				cachedScoreText = "Leitura do cenário: " + contextScore + "/100 — " + DescribeScore(contextScore)
					+ " (não garante resultado).";
				cachedDashboardBarNumber = CurrentBar;
			}

			string operationText = "SEM POSIÇÃO · aguardar sinal confirmado e score mínimo.";
			string watchText = "Observe tendência, score e horário antes de qualquer entrada.";
			string actionText = canEnter
				? "POSTURA: aguardar o sinal; não antecipar a entrada."
				: "POSTURA: preservar capital; não enviar ordem enquanto houver bloqueio.";
			Brush operationBrush = Brushes.LightGray;
			Brush actionBrush = canEnter ? Brushes.LimeGreen : Brushes.Gold;
			bool adverseCondition = false;
			if (positioned)
			{
				bool isLong = Position.MarketPosition == MarketPosition.Long;
				double signedMove = (Close[0] - Position.AveragePrice) * (isLong ? 1 : -1);
				double ticks = TickSize > 0 ? signedMove / TickSize : 0;
				operationText = (isLong ? "COMPRA" : "VENDA") + " · " + Position.Quantity
					+ " contrato(s) · preço médio " + Position.AveragePrice.ToString("0.00")
					+ " · " + FormatMoney(unrealized) + " · " + ticks.ToString("+0;-0;0") + " ticks";
				operationBrush = unrealized < 0 ? Brushes.OrangeRed : Brushes.LimeGreen;

				int bar = State == State.Historical ? 0 : (CurrentBar > 0 ? 1 : 0);
				bool enoughPositionBars = CurrentBar >= bar + 2;
				bool lostFastAverage = enoughPositionBars
					&& (isLong ? Close[bar] < emaFast[bar] : Close[bar] > emaFast[bar]);
				bool macdAgainst = enoughPositionBars
					&& (isLong
						? macd.MACDLine[bar] < macd.SignalLine[bar]
						: macd.MACDLine[bar] > macd.SignalLine[bar]);
				adverseCondition = lostFastAverage || macdAgainst;
				double breakevenDistance = BreakevenAfterBricks * BrickSizeTicks * TickSize;
				bool breakevenReached = isLong
					? Close[0] >= Position.AveragePrice + breakevenDistance
					: Close[0] <= Position.AveragePrice - breakevenDistance;
				bool nearExhaustion = isLong
					? macd.MACDLine[bar] >= 35
					: macd.MACDLine[bar] <= -35;

				watchText = connectionErrorHalt || orderErrorHalt
					? "ATENÇÃO AGORA: conexão/ordem com problema; confira proteção diretamente na plataforma."
					: (lostFastAverage
						? "ATENÇÃO AGORA: preço fechou do lado contrário da média curta."
						: (macdAgainst
							? "ATENÇÃO AGORA: MACDNG está contra a posição; aguarde o fechamento do tijolo."
							: (nearExhaustion
								? "ATENÇÃO AGORA: cenário pode estar perdendo força; observe o próximo tijolo."
								: "ATENÇÃO AGORA: fechamento do próximo tijolo e direção do MACDNG.")));
				actionText = breakEvenStopActive
					? "AÇÃO DO ROBÔ: ajuste do stop solicitado para entrada +1 tick."
					: (breakevenReached
						? "AÇÃO DO ROBÔ: próximo fechamento válido pode mover o stop para proteger o zero a zero."
						: (protectiveStopConfigured
							? "AÇÃO DO ROBÔ: stop de segurança configurado; " + (lostFastAverage || macdAgainst
								? "saída técnica será reavaliada no fechamento do tijolo."
								: "manter proteção e acompanhar.")
							: "AÇÃO: confirme o stop de segurança na aba de ordens."));
				actionBrush = lostFastAverage || macdAgainst || unrealized < 0
					? Brushes.Gold
					: (breakEvenStopActive ? Brushes.LimeGreen : Brushes.DeepSkyBlue);
			}

			double winRate = sessionTradeCount > 0 ? sessionWins * 100.0 / sessionTradeCount : 0;
			double targetRemaining = MaxDailyProfit > 0 ? Math.Max(0, MaxDailyProfit - currentSessionPnl) : 0;
			string targetProgress = MaxDailyProfit > 0
				? MakeProgressBar(currentSessionPnl, MaxDailyProfit)
				: "desativada";
			double mllMargin = Math.Max(0, MllRemainingRoom + currentSessionPnl);
			double lossLimit = GetEffectiveDailyLossLimit();

			string dllMarginText = DailyLossLimitEnabled
				? FormatMoney(Math.Max(0, DailyLossLimitAmount + currentSessionPnl))
				: "DLL desligado";
			string[] rows =
			{
				"PulseGuard  ·  " + BarsPeriod.BarsPeriodType + " " + BarsPeriod.Value,
				(EnableAutomaticTrading ? "AUTOMAÇÃO" : "MODO ALERTAS")
					+ "  ·  " + (AccountStageSelected == AccountStage.Evaluation
						? "CONTA DE AVALIAÇÃO" : (AccountStageSelected == AccountStage.Funded
							? "CONTA FINANCIADA" : "CONTA REAL")),
				robotState,
				"DECISÃO RÁPIDA  ·  ANTES DE ENTRAR",
				(canEnter ? "● LIBERADO" : "● " + (outsideEntryWindow ? "FORA DA JANELA" : (blocked ? "BLOQUEADO" : "SEM ORDENS")))
					+ "  ·  " + reason,
				"TENDÊNCIA: " + cachedTrendText + "  |  " + cachedScoreText,
				"FILTRO DE ENTRADA: " + lastEntryFilterReason,
				"RISCO: DLL disponível " + dllMarginText
					+ "  ·  margem MLL " + FormatMoney(mllMargin)
					+ " " + MakeLossProgressBar(currentSessionPnl, MllRemainingRoom)
					+ "  ·  trava da estratégia " + FormatMoney(lossLimit),
				"ACOMPANHAMENTO  ·  OPERAÇÃO ATUAL",
				operationText,
				watchText,
				actionText,
				"SESSÃO  ·  MÉTRICAS E LIMITES",
				"HOJE (esta estratégia): " + FormatMoney(currentSessionPnl) + "  ·  "
					+ sessionTradeCount + " operações  ·  " + sessionWins + " ganhos / "
					+ sessionLosses + " perdas  ·  acerto " + winRate.ToString("0") + "%",
				"META DIÁRIA: " + (MaxDailyProfit > 0
					? "faltam " + FormatMoney(targetRemaining) + " " + targetProgress
						+ (IsNearLimit(targetRemaining, MaxDailyProfit) ? "  ·  ATENÇÃO" : "")
					: "desativada"),
				"LIMITE DIÁRIO: " + (DailyLossLimitEnabled
					? FormatMoney(DailyLossLimitAmount) + "  ·  margem " + FormatMoney(Math.Max(0, DailyLossLimitAmount + currentSessionPnl))
						+ " " + MakeLossProgressBar(currentSessionPnl, DailyLossLimitAmount)
						+ (IsNearLimit(DailyLossLimitAmount + currentSessionPnl, DailyLossLimitAmount)
							? "  ·  PERTO DO LIMITE" : "")
					: "desligado"),
				"PERDA INTERNA: " + (MaxDailyLoss > 0
					? "margem " + FormatMoney(Math.Max(0, MaxDailyLoss + currentSessionPnl))
						+ " " + MakeLossProgressBar(currentSessionPnl, MaxDailyLoss)
					: "desativada")
					+ "  ·  MLL restante " + FormatMoney(mllMargin)
						+ " " + MakeLossProgressBar(currentSessionPnl, MllRemainingRoom)
						+ (IsNearLimit(mllMargin, MllRemainingRoom) ? "  ·  MARGEM BAIXA" : ""),
				"CONTA: " + (AccountStageSelected == AccountStage.Evaluation
					? "Conta de Avaliação" : (AccountStageSelected == AccountStage.Funded
						? "Conta Financiada" : "Conta Real"))
					+ "  ·  máximo permitido nesta sessão: " + activeSessionContractLimit + " contratos"
			};
			Brush[] rowBrushes =
			{
				Brushes.White,
				Brushes.LightSteelBlue,
				stateBrush,
				Brushes.DeepSkyBlue,
				canEnter ? Brushes.LimeGreen : (outsideEntryWindow || !blocked ? Brushes.Gold : Brushes.OrangeRed),
				cachedTrendBullish ? Brushes.LimeGreen : (cachedTrendBearish ? Brushes.OrangeRed : Brushes.Gold),
				IsNearLimit(mllMargin, MllRemainingRoom) ? Brushes.OrangeRed : Brushes.DeepSkyBlue,
				Brushes.DeepSkyBlue,
				operationBrush,
				adverseCondition ? Brushes.Gold : Brushes.LightSteelBlue,
				actionBrush,
				Brushes.DeepSkyBlue,
				currentSessionPnl >= 0 ? Brushes.LimeGreen : Brushes.OrangeRed,
				IsNearLimit(targetRemaining, MaxDailyProfit) ? Brushes.Gold : Brushes.White,
				DailyLossLimitEnabled && IsNearLimit(DailyLossLimitAmount + currentSessionPnl, DailyLossLimitAmount)
					? Brushes.OrangeRed : Brushes.White,
				IsNearLimit(mllMargin, MllRemainingRoom) ? Brushes.OrangeRed : Brushes.White,
				Brushes.LightSteelBlue
			};
			QueueDashboardUpdate(rows, rowBrushes, stateBrush);
		}

		private void QueueDashboardUpdate(string[] rows, Brush[] rowBrushes, Brush accentBrush)
		{
			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				if (dashboardControl == null)
					CreateDashboardControl();

				dashboardControl.Visibility = Visibility.Visible;
				dashboardCard.BorderBrush = accentBrush;
				dashboardCard.Background = GetPanelBackground();
				dashboardRows[2].Background = accentBrush;

				for (int i = 0; i < dashboardRows.Length; i++)
				{
					dashboardRows[i].Text = rows[i];
					dashboardRows[i].Foreground = rowBrushes[i];
					dashboardRows[i].FontSize = i == 0 ? TamanhoFontePainel + 4
						: (i == 2 ? TamanhoFontePainel + 1
							: ((i == 3 || i == 7 || i == 11) ? TamanhoFontePainel : TamanhoFontePainel - 1));
					if (i == 3 || i == 7 || i == 11)
						dashboardRows[i].Foreground = Brushes.DeepSkyBlue;
				}
				dashboardRows[2].Foreground = Brushes.White;
			}));
		}

		private void CreateDashboardControl()
		{
			StackPanel content = new StackPanel
			{
				Orientation = Orientation.Vertical
			};
			dashboardRows = new TextBlock[17];
			for (int i = 0; i < dashboardRows.Length; i++)
			{
				TextBlock row = new TextBlock
				{
					FontFamily = new FontFamily("Segoe UI"),
					FontSize = TamanhoFontePainel,
					Foreground = Brushes.White,
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(0, i == 3 || i == 7 || i == 11 ? 9 : 3, 0, 3),
					Padding = new Thickness(0, 1, 0, 1)
				};
				if (i == 3 || i == 7 || i == 11)
				{
					row.FontSize = TamanhoFontePainel;
					row.FontWeight = FontWeights.Bold;
				}
				if (i == 2)
				{
					row.FontSize = TamanhoFontePainel + 1;
					row.FontWeight = FontWeights.Bold;
					row.Padding = new Thickness(8, 5, 8, 5);
				}
				dashboardRows[i] = row;
				content.Children.Add(row);
			}

			dashboardCard = new Border
			{
				Width = 500,
				Padding = new Thickness(14, 10, 14, 12),
				Background = GetPanelBackground(),
				BorderBrush = Brushes.DeepSkyBlue,
				BorderThickness = new Thickness(2),
				CornerRadius = new CornerRadius(8),
				Child = content
			};
			dashboardControl = new UserControl
			{
				Content = dashboardCard,
				Background = Brushes.Transparent,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(12),
				IsHitTestVisible = false
			};
			UserControlCollection.Add(dashboardControl);
		}

		private static void SetBrushValue(ref Brush brush, ref string serializedValue, Brush value)
		{
			if (value == null)
			{
				brush = null;
				serializedValue = string.Empty;
				return;
			}

			if (!value.IsFrozen && !value.Dispatcher.CheckAccess())
				throw new InvalidOperationException("Brushes must be assigned on their owning dispatcher.");

			Brush stableBrush = value;
			if (!value.IsFrozen && value.CanFreeze)
			{
				stableBrush = value.CloneCurrentValue();
				stableBrush.Freeze();
			}

			string text = new BrushConverter().ConvertToString(stableBrush);
			brush = stableBrush;
			serializedValue = text;
		}

		private static Brush ParseBrushValue(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? null
				: (Brush)new BrushConverter().ConvertFromString(value);
		}

		private Brush GetPanelBackground()
		{
			Color color = Color.FromRgb(13, 20, 32);
			SolidColorBrush configuredBrush = CorFundoPainel as SolidColorBrush;
			if (configuredBrush != null)
				color = configuredBrush.Color;
			return new SolidColorBrush(Color.FromArgb((byte)(255 * OpacidadeFundoPainel / 100),
				color.R, color.G, color.B));
		}

		private string GetPlainReason()
		{
			if (lastStatusMessage.IndexOf("score", StringComparison.OrdinalIgnoreCase) >= 0)
				return "O sinal ainda não atingiu a qualidade mínima escolhida.";
			if (lastStatusMessage.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0)
				return "Pausa curta após uma operação com perda.";
			if (lastStatusMessage.IndexOf("aguardando", StringComparison.OrdinalIgnoreCase) >= 0)
				return "Aguardando uma oportunidade que cumpra todas as regras.";
			if (lastStatusMessage.IndexOf("somente sinais", StringComparison.OrdinalIgnoreCase) >= 0)
				return "Modo de alertas: nenhuma ordem será enviada automaticamente.";
			if (lastStatusMessage.IndexOf("ordem", StringComparison.OrdinalIgnoreCase) >= 0)
				return lastStatusMessage;
			return "Aguardando uma oportunidade que cumpra todas as regras.";
		}

		private string GetBlockedStatus()
		{
			if (!configurationValid)
				return "Bloqueado: " + GetBlockingReason(false);
			if (connectionErrorHalt)
				return string.IsNullOrEmpty(connectionErrorReason)
					? "Conexão perdida; confira posição e ordens e reinicie manualmente."
					: connectionErrorReason + "; confira posição e ordens e reinicie manualmente.";
			if (orderErrorHalt)
				return string.IsNullOrEmpty(orderErrorReason)
					? "Falha em ordem; confira a plataforma antes de reiniciar."
					: orderErrorReason + "; confira a plataforma antes de reiniciar.";
			if (dailyLocked)
				return string.IsNullOrEmpty(dailyLockReason)
					? "Limite ou pausa diária atingida."
					: dailyLockReason;
			return "Estratégia bloqueada.";
		}

		private string GetBlockingReason(bool scheduleLock)
		{
			if (!configurationValid)
				return string.IsNullOrEmpty(configurationBlockReason)
					? "GRÁFICO/PARÂMETROS INVÁLIDOS"
					: configurationBlockReason;
			if (orderErrorHalt)
				return string.IsNullOrEmpty(orderErrorReason) ? "ERRO DE ORDEM" : orderErrorReason;
			if (connectionErrorHalt)
				return string.IsNullOrEmpty(connectionErrorReason) ? "CONEXÃO DA CONTA" : connectionErrorReason;
			if (dailyLocked && !scheduleLock)
				return string.IsNullOrEmpty(dailyLockReason) ? "LIMITE/PAUSA DA SESSÃO" : dailyLockReason;
			return "BLOQUEIO ATIVO";
		}

		private string DescribeScore(int score)
		{
			if (score >= HighConfluenceScore)
				return "muito boa";
			if (score >= MinimumEntryScore)
				return "boa";
			if (score >= 50)
				return "moderada";
			return "baixa";
		}

		private string FormatMoney(double value)
		{
			CultureInfo culture = CultureInfo.GetCultureInfo("pt-BR");
			return (value < 0 ? "-$" : "+$") + Math.Abs(value).ToString("N2", culture);
		}

		private string FormatAmount(double value)
		{
			if (double.IsInfinity(value))
				return "OFF";
			return "$" + Math.Abs(value).ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
		}

		private string FormatTime(int time)
		{
			return (time / 10000).ToString("00") + ":" + ((time / 100) % 100).ToString("00");
		}

		private string MakeProgressBar(double value, double maximum)
		{
			if (maximum <= 0)
				return "[----------]";
			int filled = Math.Max(0, Math.Min(10, (int)Math.Round(value / maximum * 10)));
			return "[" + new string('#', filled) + new string('-', 10 - filled) + "]";
		}

		private string MakeLossProgressBar(double dailyPnl, double lossLimit)
		{
			double lossUsed = Math.Max(0, -dailyPnl);
			return MakeProgressBar(lossUsed, lossLimit);
		}

		private bool IsNearLimit(double remaining, double total)
		{
			return total > 0 && !double.IsInfinity(total)
				&& remaining <= Math.Max(total * 0.2, 50);
		}

		private bool IsWithin(int time, int start, int end)
		{
			return time >= start && time < end;
		}

		private void WriteCsvEvent(string eventName, DateTime time, string direction, double price,
			int quantity, double pnl, int score, string reason)
		{
			if (!EnableCsvLog || string.IsNullOrEmpty(logFilePath))
				return;

			StringBuilder row = new StringBuilder();
			row.Append(time.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(',')
				.Append(Csv(eventName)).Append(',')
				.Append(Csv(Instrument.FullName)).Append(',')
				.Append(Csv(direction)).Append(',')
				.Append(price.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
				.Append(quantity).Append(',')
				.Append(pnl.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
				.Append(score).Append(',')
				.Append(Csv(reason)).AppendLine();

			lock (csvQueueLock)
			{
				csvWriteTask = csvWriteTask.ContinueWith(task => AppendCsvRow(row.ToString()),
					TaskScheduler.Default);
			}
		}

		private void AppendCsvRow(string row)
		{
			try
			{
				lock (fileLock)
				{
					string directory = Path.GetDirectoryName(logFilePath);
					if (!Directory.Exists(directory))
						Directory.CreateDirectory(directory);

					bool writeHeader = !File.Exists(logFilePath);
					string content = writeHeader
						? "Timestamp,Event,Instrument,Direction,Price,Quantity,PnL,Score,Reason"
							+ Environment.NewLine + row
						: row;
					File.AppendAllText(logFilePath, content, Encoding.UTF8);
				}
			}
			catch (Exception exception)
			{
				Print(Name + ": falha ao gravar CSV (" + logFilePath + "): " + exception.Message);
			}
		}

		private string Csv(string value)
		{
			if (value == null)
				return "\"\"";
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}

		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity,
			int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
		{
			base.OnOrderUpdate(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, comment);
			if (order == null)
				return;

			if (order.Name == "LongEntry" || order.Name == "ShortEntry")
			{
				bool entryTerminal = orderState == OrderState.Filled
					|| orderState == OrderState.Cancelled
					|| orderState == OrderState.Rejected;
				pendingEntryOrder = entryTerminal ? null : order;
				if (entryTerminal)
					entryCancelRequested = false;
				entryOrderPending = orderState != OrderState.Filled
					&& orderState != OrderState.Cancelled
					&& orderState != OrderState.Rejected;
				if (entryOrderPending && (dailyLocked || IsInFlattenWindow(time)))
					CancelPendingEntry();
			}
			else if (order.Name == "StrategyExit"
				&& (orderState == OrderState.Filled || orderState == OrderState.Cancelled || orderState == OrderState.Rejected))
			{
				exitOrderPending = false;
			}

			if (orderState == OrderState.Rejected || error != ErrorCode.NoError)
			{
				orderErrorHalt = true;
				orderErrorReason = "Ordem " + order.Name + " " + orderState + " (" + error + ")";
				if (!string.IsNullOrEmpty(comment))
					orderErrorReason += ": " + comment;
				Print(Name + ": ordem " + order.Name + " em estado " + orderState
					+ "; erro " + error + "; " + comment + ". Novas entradas bloqueadas.");
				WriteCsvEvent("ORDER_ERROR", time, order.Name, averageFillPrice, filled, 0,
					currentEntryScore, comment);
			}
		}

		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity,
			MarketPosition marketPosition, string orderId, DateTime time)
		{
			base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);
			if (execution == null || execution.Order == null)
				return;

			if ((execution.Order.Name == "LongEntry" || execution.Order.Name == "ShortEntry")
				&& firstEntryFillTime == DateTime.MinValue)
				firstEntryFillTime = time;

			if (marketPosition == MarketPosition.Flat)
			{
				exitOrderPending = false;
				firstEntryFillTime = DateTime.MinValue;
				protectiveStopConfigured = false;
				breakEvenStopActive = false;
			}

			WriteCsvEvent("EXECUTION", time, execution.Order.Name, price, quantity, 0,
				currentEntryScore, execution.Order.OrderState.ToString());
		}

		protected override void OnConnectionStatusUpdate(ConnectionStatusEventArgs connectionStatusUpdate)
		{
			base.OnConnectionStatusUpdate(connectionStatusUpdate);
			if (State == State.Realtime && connectionStatusUpdate != null
				&& Account != null && Account.Connection != null
				&& object.ReferenceEquals(connectionStatusUpdate.Connection, Account.Connection)
				&& (connectionStatusUpdate.Status == ConnectionStatus.Disconnected
					|| connectionStatusUpdate.Status == ConnectionStatus.ConnectionLost))
			{
				connectionErrorHalt = true;
				string connectionName = Account.Connection.Options == null
					? "selecionada" : Account.Connection.Options.Name;
				connectionErrorReason = "Conexão da conta "
					+ connectionName + " " + connectionStatusUpdate.Status;
				lastStatusMessage = "Conexão perdida. Novas entradas bloqueadas.";
				Print(Name + ": " + connectionErrorReason
					+ ". Novas entradas bloqueadas; verifique posição e ordens.");
				WriteCsvEvent("CONNECTION_LOST", Time[0], "", 0, 0, 0, currentEntryScore,
					connectionStatusUpdate.Status.ToString());
			}
		}

		#region Parameters
		[NinjaScriptProperty]
		[Display(Name = "Enable automatic trading", GroupName = "1. Mode", Order = 1)]
		public bool EnableAutomaticTrading { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Confirm risk settings reviewed", GroupName = "1. Mode", Order = 2)]
		public bool ConfirmAutomaticRisk { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Allow live account (after validation)", GroupName = "1. Mode", Order = 3)]
		public bool AllowLiveTrading { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Confirm selected prop account is simulated", GroupName = "1. Mode", Order = 4)]
		public bool AllowPropSimAccount { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Generate signals outside session", GroupName = "1. Mode", Order = 5)]
		public bool GenerateSignalsOutsideSession { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Tipo da conta",
			Description = "Selecione o estágio informado pela corretora ou mesa; o NinjaTrader não o detecta.",
			GroupName = "5. Regras de Conta e Risco", Order = 1)]
		public AccountStage AccountStageSelected { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Máx. contratos — avaliação", GroupName = "5. Regras de Conta e Risco", Order = 2)]
		public int MaxContractsEvaluation { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Máx. contratos — financiada", GroupName = "5. Regras de Conta e Risco", Order = 3)]
		public int MaxContractsFunded { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Máx. contratos — real", GroupName = "5. Regras de Conta e Risco", Order = 4)]
		public int MaxContractsReal { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Limite diário de perda ativo", GroupName = "5. Regras de Conta e Risco", Order = 5)]
		public bool DailyLossLimitEnabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Limite diário de perda", GroupName = "5. Regras de Conta e Risco", Order = 6)]
		public double DailyLossLimitAmount { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Buffer do limite diário de perda", GroupName = "5. Regras de Conta e Risco", Order = 7)]
		public double DailyLossSafetyBuffer { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Limite máximo de perda ativo", GroupName = "5. Regras de Conta e Risco", Order = 8)]
		public bool MaximumLossLimitEnabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Margem restante até perda máxima (informada)", GroupName = "5. Regras de Conta e Risco", Order = 9)]
		public double MllRemainingRoom { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Buffer de perda máxima", GroupName = "5. Regras de Conta e Risco", Order = 10)]
		public double MllSafetyBuffer { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Require Renko chart", GroupName = "1. Chart validation", Order = 1)]
		public bool RequireRenkoChart { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Expected Renko value", GroupName = "1. Chart validation", Order = 2)]
		public int ExpectedRenkoValue { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Require instrument name", GroupName = "1. Chart validation", Order = 3)]
		public bool RequireInstrument { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Required instrument name", GroupName = "1. Chart validation", Order = 4)]
		public string RequiredInstrumentName { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "MACD Fast", GroupName = "2. MACDNG", Order = 1)]
		public int FastPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Name = "MACD Slow", GroupName = "2. MACDNG", Order = 2)]
		public int SlowPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Signal period", GroupName = "2. MACDNG", Order = 3)]
		public int SignalPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(2, int.MaxValue)]
		[Display(Name = "Normalization period", GroupName = "2. MACDNG", Order = 4)]
		public int NormalizationPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Zone lookback bars", GroupName = "2. MACDNG", Order = 5)]
		public int ZoneLookbackBars { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Fast EMA period", GroupName = "3. Moving averages", Order = 1)]
		public int FastEmaPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Medium SMA period", GroupName = "3. Moving averages", Order = 2)]
		public int MediumSmaPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Macro EMA period", GroupName = "3. Moving averages", Order = 3)]
		public int MacroEmaPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Minimum EMA/SMA distance (ticks)", GroupName = "3. Moving averages", Order = 4)]
		public int MinAverageDistanceTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "No-cross lookback bars", GroupName = "3. Moving averages", Order = 5)]
		public int NoCrossLookbackBars { get; set; }

		[XmlIgnore]
		[Display(Name = "EMA 9 color", GroupName = "3. Moving averages", Order = 6)]
		public Brush FastEmaBrush
		{
			get { return fastEmaBrush; }
			set { SetBrushValue(ref fastEmaBrush, ref fastEmaBrushSerializable, value); }
		}

		[Browsable(false)]
		public string FastEmaBrushSerializable
		{
			get { return fastEmaBrushSerializable; }
			set { SetBrushValue(ref fastEmaBrush, ref fastEmaBrushSerializable, ParseBrushValue(value)); }
		}

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Name = "EMA 9 width", GroupName = "3. Moving averages", Order = 7)]
		public int FastEmaWidth { get; set; }

		[XmlIgnore]
		[Display(Name = "SMA 21 color", GroupName = "3. Moving averages", Order = 8)]
		public Brush MediumSmaBrush
		{
			get { return mediumSmaBrush; }
			set { SetBrushValue(ref mediumSmaBrush, ref mediumSmaBrushSerializable, value); }
		}

		[Browsable(false)]
		public string MediumSmaBrushSerializable
		{
			get { return mediumSmaBrushSerializable; }
			set { SetBrushValue(ref mediumSmaBrush, ref mediumSmaBrushSerializable, ParseBrushValue(value)); }
		}

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Name = "SMA 21 width", GroupName = "3. Moving averages", Order = 9)]
		public int MediumSmaWidth { get; set; }

		[XmlIgnore]
		[Display(Name = "EMA 200 color", GroupName = "3. Moving averages", Order = 10)]
		public Brush MacroEmaBrush
		{
			get { return macroEmaBrush; }
			set { SetBrushValue(ref macroEmaBrush, ref macroEmaBrushSerializable, value); }
		}

		[Browsable(false)]
		public string MacroEmaBrushSerializable
		{
			get { return macroEmaBrushSerializable; }
			set { SetBrushValue(ref macroEmaBrush, ref macroEmaBrushSerializable, ParseBrushValue(value)); }
		}

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Name = "EMA 200 width", GroupName = "3. Moving averages", Order = 11)]
		public int MacroEmaWidth { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Brick size (ticks)", GroupName = "4. Filters and score", Order = 1)]
		public int BrickSizeTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Barrier lookback bars", GroupName = "4. Filters and score", Order = 2)]
		public int BarrierLookbackBars { get; set; }

		[NinjaScriptProperty]
		[Range(2, 100)]
		[Display(Name = "Score barrier lookback bars", GroupName = "4. Filters and score", Order = 3)]
		public int ScoreBarrierLookbackBars { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Use quality score", GroupName = "4. Filters and score", Order = 4)]
		public bool UseQualityScore { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Minimum entry score", GroupName = "4. Filters and score", Order = 5)]
		public int MinimumEntryScore { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "High confluence score", GroupName = "4. Filters and score", Order = 6)]
		public int HighConfluenceScore { get; set; }

		[NinjaScriptProperty]
		[Range(0, 5)]
		[Display(Name = "Máx. tijolos após evento MACD", Description = "Mantém válido apenas o cruzamento MACDNG recente. Zero exige o tijolo do evento.", GroupName = "4. Filters and score", Order = 7)]
		public int MaxBarsAfterMacdEvent { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Permitir rejeição na linha de sinal MACD", Description = "Aceita retomada fresca a partir da linha de sinal, ainda alinhada à estrutura.", GroupName = "4. Filters and score", Order = 8)]
		public bool AllowMacdRejectionTrigger { get; set; }

		[NinjaScriptProperty]
		[Range(0, 50)]
		[Display(Name = "Distância máx. para rejeição MACD", Description = "Afastamento normalizado máximo da linha de sinal no tijolo anterior.", GroupName = "4. Filters and score", Order = 9)]
		public double MacdRejectionMaxGap { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "Máx. tijolos na pernada antes da entrada", Description = "Bloqueia a entrada quando a sequência de tijolos já estiver estendida.", GroupName = "4. Filters and score", Order = 10)]
		public int MaxSameDirectionBricksBeforeEntry { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "Distância máx. da EMA rápida (tijolos)", Description = "Bloqueia preço excessivamente afastado da EMA rápida no sentido da entrada.", GroupName = "4. Filters and score", Order = 11)]
		public int MaxDistanceFromFastEmaBricks { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Permitir cruzamento recente EMA/SMA", Description = "Não descarta uma virada limpa apenas porque EMA 9 e SMA 21 cruzaram recentemente.", GroupName = "4. Filters and score", Order = 12)]
		public bool AllowRecentFastMediumCross { get; set; }

		[NinjaScriptProperty]
		[Range(0, 40)]
		[Display(Name = "Bônus de score para gatilho fresco", Description = "Evita que componentes de score redundantes rejeitem uma virada estrutural já validada.", GroupName = "4. Filters and score", Order = 13)]
		public int FreshTriggerScoreBonus { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name = "Macro slope minimum (ticks)", GroupName = "4. Filters and score", Order = 14)]
		public int MacroSlopeMinTicks { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Healthy distance minimum (bricks)", GroupName = "4. Filters and score", Order = 8)]
		public int HealthyDistanceMinBricks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Healthy distance maximum (bricks)", GroupName = "4. Filters and score", Order = 9)]
		public int HealthyDistanceMaxBricks { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "MACD acceleration threshold", GroupName = "4. Filters and score", Order = 10)]
		public int MacdAccelerationThreshold { get; set; }

		[NinjaScriptProperty]
		[Range(40, 60)]
		[Display(Name = "MACD extreme threshold", GroupName = "4. Filters and score", Order = 11)]
		public int MacdExtremeThreshold { get; set; }

		[NinjaScriptProperty]
		[Range(0, 20)]
		[Display(Name = "MACD flat range threshold", GroupName = "4. Filters and score", Order = 12)]
		public double MacdFlatRangeThreshold { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Short average slope minimum (ticks)", GroupName = "4. Filters and score", Order = 13)]
		public int ShortAverageSlopeMinTicks { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Main liquidity start 1 (HHmmss)", GroupName = "4. Filters and score", Order = 14)]
		public int MainLiquidityStart1 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Main liquidity end 1 (HHmmss)", GroupName = "4. Filters and score", Order = 15)]
		public int MainLiquidityEnd1 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Main liquidity start 2 (HHmmss)", GroupName = "4. Filters and score", Order = 16)]
		public int MainLiquidityStart2 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Main liquidity end 2 (HHmmss)", GroupName = "4. Filters and score", Order = 17)]
		public int MainLiquidityEnd2 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Secondary liquidity start 1", GroupName = "4. Filters and score", Order = 18)]
		public int SecondaryLiquidityStart1 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Secondary liquidity end 1", GroupName = "4. Filters and score", Order = 19)]
		public int SecondaryLiquidityEnd1 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Secondary liquidity start 2", GroupName = "4. Filters and score", Order = 20)]
		public int SecondaryLiquidityStart2 { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Secondary liquidity end 2", GroupName = "4. Filters and score", Order = 21)]
		public int SecondaryLiquidityEnd2 { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Contracts", GroupName = "5. Regras de Conta e Risco", Order = 11)]
		public int Contracts { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Stop de emergência (ticks)", GroupName = "5. Regras de Conta e Risco", Order = 12)]
		public int EmergencyStopTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "Breakeven após tijolos", GroupName = "5. Regras de Conta e Risco", Order = 13)]
		public int BreakevenAfterBricks { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Limite diário de lucro da estratégia", GroupName = "5. Regras de Conta e Risco", Order = 14)]
		public double MaxDailyProfit { get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Limite diário de perda da estratégia", GroupName = "5. Regras de Conta e Risco", Order = 15)]
		public double MaxDailyLoss { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Máximo de perdas consecutivas", GroupName = "5. Regras de Conta e Risco", Order = 16)]
		public int MaxConsecutiveLosses { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000)]
		[Display(Name = "Máximo de operações por sessão", GroupName = "5. Regras de Conta e Risco", Order = 17)]
		public int MaxTradesPerSession { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Barras de pausa após perda", GroupName = "5. Regras de Conta e Risco", Order = 18)]
		public int CooldownBarsAfterLoss { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Entry start (HHmmss)", GroupName = "6. Session times", Order = 1)]
		public int EntryStartTime { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Entry end (HHmmss)", GroupName = "6. Session times", Order = 2)]
		public int EntryEndTime { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Flatten time (HHmmss)", GroupName = "6. Session times", Order = 3)]
		public int FlattenTime { get; set; }

		[NinjaScriptProperty]
		[Range(6, 600)]
		[Display(Name = "Segundos mínimos antes de saída técnica lucrativa", GroupName = "5. Regras de Conta e Risco", Order = 19)]
		public int MinimumProfitableHoldSeconds { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Sound alerts", GroupName = "7. Logging and alerts", Order = 1)]
		public bool EnableSoundAlerts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Mostrar painel informativo", GroupName = "8. Painel informativo", Order = 1)]
		public bool MostrarPainel { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Check for updates", Description = "Desliga as consultas do robô, do indicador MACDNG incluído e do template.", GroupName = "9. Version information", Order = 1)]
		public bool CheckForUpdates { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show version panel", GroupName = "9. Version information", Order = 2)]
		public bool ShowVersionPanel { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Template version (manual)", Description = "Preencha a versão do template aplicado; o NinjaTrader não a detecta automaticamente.", GroupName = "9. Version information", Order = 3)]
		public string TemplateVersion { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Modo do painel", GroupName = "8. Painel informativo", Order = 2)]
		public PulseGuardDashboardMode ModoPainel { get; set; }

		[NinjaScriptProperty]
		[Range(10, 28)]
		[Display(Name = "Tamanho da fonte do painel", GroupName = "8. Painel informativo", Order = 3)]
		public int TamanhoFontePainel { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Opacidade do fundo do painel", GroupName = "8. Painel informativo", Order = 4)]
		public int OpacidadeFundoPainel { get; set; }

		[XmlIgnore]
		[Display(Name = "Cor do fundo do painel", GroupName = "8. Painel informativo", Order = 5)]
		public Brush CorFundoPainel
		{
			get { return panelBackgroundBrush; }
			set { SetBrushValue(ref panelBackgroundBrush, ref panelBackgroundBrushSerializable, value); }
		}

		[Browsable(false)]
		public string CorFundoPainelSerializable
		{
			get { return panelBackgroundBrushSerializable; }
			set { SetBrushValue(ref panelBackgroundBrush, ref panelBackgroundBrushSerializable, ParseBrushValue(value)); }
		}

		[NinjaScriptProperty]
		[Display(Name = "CSV event log", GroupName = "7. Logging and alerts", Order = 2)]
		public bool EnableCsvLog { get; set; }
		#endregion
	}
}
