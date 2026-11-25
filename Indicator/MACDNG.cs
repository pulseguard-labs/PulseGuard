#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
	public class MACDNG : Indicator
	{
		private EMA				emaFast;
		private EMA				emaSlow;
		private EMA				emaSignalRaw;		// Signal do MACD bruto (quando Normalize = false)
		private EMA				emaSignalNorm;		// Signal do MACD já normalizado (quando Normalize = true)
		private Series<double>	macdSeries;
		private Series<double>	normMacdSeries;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= @"MACD Min-Max com compressão suave nas extremidades e linha de sinal de alta precisão.";
				Name						= "MACDNG";
				Calculate					= Calculate.OnBarClose;
				IsOverlay					= false;
				DisplayInDataBox			= true;
				DrawOnPricePanel			= false;
				DrawHorizontalGridLines		= true;
				DrawVerticalGridLines		= true;
				PaintPriceMarkers			= true;
				ScaleJustification			= ScaleJustification.Right;
				IsAutoScale					= true;
				IsSuspendedWhileInactive	= true;

				// MACD Parameters
				Fast						= 12;
				Slow						= 26;
				Smooth						= 9;

				// Normalization & Precision
				Normalize					= true;
				NormPeriod					= 50;
				CompressionThreshold		= 40.0;
				DisplayScaleLimit			= 60.0;
				SignalCloseness				= 0.25;

				// Plots (linhas principais)
				AddPlot(new Stroke(Brushes.ForestGreen, 2), PlotStyle.Line, "MACDLine");
				AddPlot(new Stroke(Brushes.Crimson, 2), PlotStyle.Line, "SignalLine");

				// Horizontal lines - cores e valores mais próximos da imagem oficial
				AddLine(new Stroke(Brushes.LimeGreen, DashStyleHelper.Dash, 1), 40, "OverboughtExtreme");
				AddLine(new Stroke(Brushes.Red, DashStyleHelper.Dash, 1), 20, "Overbought");
				AddLine(new Stroke(Brushes.Black, DashStyleHelper.Dash, 1), 0, "ZeroLine");
				AddLine(new Stroke(Brushes.Red, DashStyleHelper.Dash, 1), -20, "Oversold");
				AddLine(new Stroke(Brushes.LimeGreen, DashStyleHelper.Dash, 1), -40, "OversoldExtreme");
			}
			else if (State == State.DataLoaded)
			{
				emaFast			= EMA(Close, Fast);
				emaSlow			= EMA(Close, Slow);
				macdSeries		= new Series<double>(this);
				normMacdSeries	= new Series<double>(this);

				emaSignalRaw	= EMA(macdSeries, Smooth);		// usado quando Normalize = false
				emaSignalNorm	= EMA(normMacdSeries, Smooth);	// usado quando Normalize = true
			}
		}

		public override void OnCalculateMinMax()
		{
			base.OnCalculateMinMax();

			if (Normalize)
			{
				double limit = Math.Max(10.0, DisplayScaleLimit);
				MinValue = -limit;
				MaxValue = limit;
			}
		}

		private double CompressNormalizedValue(double value)
		{
			double limit = Math.Max(10.0, DisplayScaleLimit);
			double threshold = Math.Max(0.0, Math.Min(CompressionThreshold, limit - 1.0));
			double absoluteValue = Math.Abs(value);

			if (absoluteValue <= threshold)
				return value;

			double headroom = Math.Max(1.0, limit - threshold);
			double excess = absoluteValue - threshold;
			double compressedExcess = headroom * Math.Tanh(excess / (headroom * 1.25));
			return Math.Sign(value) * (threshold + compressedExcess);
		}

		private double RefineSignalLine(double macdValue, double signalValue)
		{
			double closeness = Math.Max(0.0, Math.Min(0.8, SignalCloseness));
			return signalValue + (macdValue - signalValue) * closeness;
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < Slow)
			{
				Values[0][0] = 0;
				Values[1][0] = 0;
				return;
			}

			// MACD clássico (bruto)
			double macdValue = emaFast[0] - emaSlow[0];
			macdSeries[0] = macdValue;

			if (Normalize)
			{
				if (CurrentBar < NormPeriod)
				{
					Values[0][0] = 0;
					Values[1][0] = 0;
					return;
				}

				double highest = double.MinValue;
				double lowest  = double.MaxValue;

				for (int i = 0; i < NormPeriod; i++)
				{
					double v = macdSeries[i];
					if (v > highest) highest = v;
					if (v < lowest)  lowest  = v;
				}

				double range = highest - lowest;
				double rawNormMacd = 0;

				if (range.ApproxCompare(0) != 0)
					rawNormMacd = ((macdValue - lowest) / range) * 200.0 - 100.0;

				double normMacd = CompressNormalizedValue(rawNormMacd);

				// A linha de sinal usa a mesma escala comprimida exibida no painel.
				normMacdSeries[0] = normMacd;

				// Plot 0 = MACD normalizado
				Values[0][0] = normMacd;

				// Plot 1 = Linha de sinal ajustada para maior proximidade e precisão
				Values[1][0] = RefineSignalLine(normMacd, emaSignalNorm[0]);
			}
			else
			{
				// Modo clássico (sem normalização)
				Values[0][0] = macdValue;
				Values[1][0] = RefineSignalLine(macdValue, emaSignalRaw[0]);
			}
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Fast", Description = "Período da EMA rápida", Order = 1, GroupName = "1. MACD Parameters")]
		public int Fast { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Slow", Description = "Período da EMA lenta", Order = 2, GroupName = "1. MACD Parameters")]
		public int Slow { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Smooth", Description = "Período da Linha de Sinal", Order = 3, GroupName = "1. MACD Parameters")]
		public int Smooth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Normalize", Description = "Aplica Min-Max com compressão suave para evitar extremos esticados", Order = 1, GroupName = "2. Normalization & Precision")]
		public bool Normalize { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "NormPeriod", Description = "Período de lookback para normalização Min-Max", Order = 2, GroupName = "2. Normalization & Precision")]
		public int NormPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 200.0)]
		[Display(Name = "Compression Threshold", Description = "Nível a partir do qual inicia a compressão suave nas extremidades", Order = 3, GroupName = "2. Normalization & Precision")]
		public double CompressionThreshold { get; set; }

		[NinjaScriptProperty]
		[Range(10.0, 200.0)]
		[Display(Name = "Display Scale Limit", Description = "Limite de escala do painel quando normalizado", Order = 4, GroupName = "2. Normalization & Precision")]
		public double DisplayScaleLimit { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 0.8)]
		[Display(Name = "Signal Closeness", Description = "Aproxima a linha de sinal (0 = EMA pura, maior = mais próxima do MACD)", Order = 5, GroupName = "2. Normalization & Precision")]
		public double SignalCloseness { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Overbought Extreme", Description = "Nível de Sobrecompra Extrema", Order = 1, GroupName = "3. Horizontal Levels")]
		public double OverboughtExtreme
		{
			get { return Lines[0].Value; }
			set { Lines[0].Value = value; }
		}

		[NinjaScriptProperty]
		[Display(Name = "Overbought", Description = "Nível de Sobrecompra", Order = 2, GroupName = "3. Horizontal Levels")]
		public double Overbought
		{
			get { return Lines[1].Value; }
			set { Lines[1].Value = value; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public double ZeroLine
		{
			get { return Lines[2].Value; }
			set { Lines[2].Value = value; }
		}

		[NinjaScriptProperty]
		[Display(Name = "Oversold", Description = "Nível de Sobrevenda", Order = 3, GroupName = "3. Horizontal Levels")]
		public double Oversold
		{
			get { return Lines[3].Value; }
			set { Lines[3].Value = value; }
		}

		[NinjaScriptProperty]
		[Display(Name = "Oversold Extreme", Description = "Nível de Sobrevenda Extrema", Order = 4, GroupName = "3. Horizontal Levels")]
		public double OversoldExtreme
		{
			get { return Lines[4].Value; }
			set { Lines[4].Value = value; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> MACDLine
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> SignalLine
		{
			get { return Values[1]; }
		}
		#endregion
	}
}