using System.Collections.Generic;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Stores and manipulates generic classifier parameters.
/// </summary>
class ClassifierParameter
{
	public enum ParameterType { RSI, RSIWithCurrentPrice, RSIInt, RSIIntWithCurrentPrice, LastCriticalRSI, LastCriticalRSIWithCurrentPrice, MeanToStd, MeanToStdInt, LinearRegressionSlope, LinearRegressionSlopePN, MarginSlope, MarginSlopePN, MACDSign, MACDSignWithCurrentPrice, MACDHistWithCurrentPrice, MACDHist, MACDHistChange, MACDHistChangeWithCurrentPrice, MACDHistSlope, MACDHistPN, MACDHistCrossed, MACDHistDifference, MACD, SlopesEMA, ABAverage, PercentMargin, Classifier, ClassifierTargetChangeOldest };

	public readonly ParameterType Type;
	public readonly int Periods;
	public readonly List<float> Attributes = new List<float>();

	//**************************************************************************************

	public ClassifierParameter(ParameterType iType, int iPeriods, List<float> iAttributes)
	{
		Type = iType;
		Periods = iPeriods;
		Attributes = iAttributes;
	}

	//**************************************************************************************

	public ClassifierParameter(string iDefinition)
	{
		string[] values = iDefinition.Split(';');
		Type = (ParameterType)Enum.Parse(typeof(ParameterType), values[0]);
		Periods = int.Parse(values[1]);

		for (int i = 2; i < values.Length; i++)
			if(values[i].Length > 0)
				Attributes.Add(float.Parse(values[i], CultureInfo.InvariantCulture));
	}

	//**************************************************************************************

	/// <summary>
	/// Returns calculated parameters.
	/// </summary>
	/// <param name="iCurrentPrice">Last known price.</param>
	/// <param name="iCandlestickIndex">Provides index (current time) for each candlestick list. If this value is set to null, the most recent entry will be used.</param>
	/// <param name="iCandlesticks">Maximum amount of daily candlesticks: 270; 12H: 25; 6H: 50; 3H: 100; 2H: 150; 1H: 300; 30m: 600; 15m: 0; 5m: 0; 1m:0</param>
	public static float[] CalculateParameters(List<ClassifierParameter> iParameters, CandlestickCollection iCandlesticks, Candlestick.Period iPeriod, float iCurrentPrice, CandlestickIndexCollection iCandlestickIndex)
	{
		var candlesticks = iCandlesticks[iPeriod];
		int candlestickIndex = iCandlestickIndex == null ? candlesticks.Count - 1 : iCandlestickIndex[iPeriod];

		if (candlestickIndex >= iCandlesticks[iPeriod].Count)
			throw new Exception("Candlestick index is higher than total number of candlesticks");

		// Make sure we have enough periods
		for (int i = 0; i < iParameters.Count; i++)
			if (candlesticks.Count < iParameters[i].Periods + 10)
				return null;

		float[] results = new float[iParameters.Count];

		var macd = new SortedDictionary<int, MACD>();
		var macdCurrentPrice = new SortedDictionary<int, MACD>();

		for (int i = 0; i < iParameters.Count; i++)
		{
			//int candlestickIndex = Candlestick.GetIndex(iParameters[i].Candlesticks, iCandlesticks, iCandlestickIndex);
			int periods = iParameters[i].Periods;

			switch (iParameters[i].Type)
			{
				case ParameterType.RSI:
					results[i] = TechnicalIndicators.CalculateRSI(candlesticks, periods, candlestickIndex);
					break;

				case ParameterType.RSIWithCurrentPrice:
					results[i] = TechnicalIndicators.CalculateRSI(candlesticks, periods, candlestickIndex, iCurrentPrice);
					break;

				case ParameterType.RSIInt:
					results[i] = TechnicalIndicators.RSIToInt(TechnicalIndicators.CalculateRSI(candlesticks, periods, candlestickIndex));
					break;

				case ParameterType.RSIIntWithCurrentPrice:
					results[i] = TechnicalIndicators.RSIToInt(TechnicalIndicators.CalculateRSI(candlesticks, periods, candlestickIndex, iCurrentPrice));
					break;

				case ParameterType.LastCriticalRSI:
					int startIndex = Math.Max(candlestickIndex - 270 + iParameters[i].Periods, iParameters[i].Periods);
					int lastCriticalRSI = 0;

					for (int k = startIndex; k >= candlestickIndex; k++)
						lastCriticalRSI = TechnicalIndicators.CalculateLastCriticalRSI(TechnicalIndicators.RSIToInt(TechnicalIndicators.CalculateRSI(candlesticks, periods, k)), lastCriticalRSI);

					results[i] = lastCriticalRSI;
					break;

				case ParameterType.LastCriticalRSIWithCurrentPrice:
					startIndex = Math.Max(candlestickIndex - 270 + iParameters[i].Periods, iParameters[i].Periods);
					lastCriticalRSI = 0;

					for (int k = startIndex; k >= candlestickIndex; k++)
						lastCriticalRSI = TechnicalIndicators.CalculateLastCriticalRSI(TechnicalIndicators.RSIToInt(TechnicalIndicators.CalculateRSI(candlesticks, periods, k, iCurrentPrice)), lastCriticalRSI);

					results[i] = lastCriticalRSI;
					break;

				case ParameterType.MeanToStd:
					results[i] = TechnicalIndicators.CalculateMeanToStdDev(candlesticks, periods, candlestickIndex, iCurrentPrice);
					break;

				case ParameterType.MeanToStdInt:
					results[i] = (float)Math.Floor(TechnicalIndicators.CalculateMeanToStdDev(candlesticks, periods, candlestickIndex, iCurrentPrice));
					break;

				case ParameterType.LinearRegressionSlope:
					results[i] = (float)TechnicalIndicators.CalculateLinearRegressionSlope(candlesticks, periods, candlestickIndex);
					break;

				case ParameterType.LinearRegressionSlopePN:
					results[i] = TechnicalIndicators.CalculateLinearRegressionSlope(candlesticks, periods, candlestickIndex) >= 0 ? 1 : -1;
					break;

				case ParameterType.MarginSlope:
					results[i] = TechnicalIndicators.CalculateMarginSlope(candlesticks, periods, candlestickIndex, iCurrentPrice, iParameters[i].Attributes[0]);
					break;

				case ParameterType.MarginSlopePN:
					results[i] = TechnicalIndicators.CalculateMarginSlopePN(candlesticks, periods, candlestickIndex, iCurrentPrice, iParameters[i].Attributes[0]);
					break;

				case ParameterType.MACDSign:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));
					
						results[i] = macd[periods].Signal;
					break;

				case ParameterType.MACDSignWithCurrentPrice:
					if (!macdCurrentPrice.ContainsKey(periods))
						macdCurrentPrice.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex, iCurrentPrice));

					results[i] = macdCurrentPrice[periods].Signal;
					break;

				case ParameterType.MACDHist:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));

					results[i] = macd[periods].Hist;
					break;

				case ParameterType.MACDHistWithCurrentPrice:
					if (!macdCurrentPrice.ContainsKey(periods))
						macdCurrentPrice.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex, iCurrentPrice));

					results[i] = macdCurrentPrice[periods].Hist;
					break;

				case ParameterType.MACDHistChange:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));

					MACD macd90 = macd[periods];
					MACD previousMACD90 = TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex - 1);

					results[i] = previousMACD90.Hist == 0 ? 0 : (macd90.Hist / previousMACD90.Hist - 1);
					break;

				case ParameterType.MACDHistChangeWithCurrentPrice:
					if (!macdCurrentPrice.ContainsKey(periods))
						macdCurrentPrice.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex, iCurrentPrice));

					macd90 = macdCurrentPrice[periods];
					previousMACD90 = TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex - 1);

					results[i] = previousMACD90.Hist == 0 ? 0 : (macd90.Hist / previousMACD90.Hist - 1);
					break;

				case ParameterType.MACDHistSlope:
					float[] hist = new float[(int)iParameters[i].Attributes[0]];
					for (int k = hist.Length - 1; k >= 0; k--)
						hist[hist.Length - 1 - k] = TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex - k).Hist;

					results[i] = new LinearRegression(hist).Slope;
					break;

				case ParameterType.MACDHistPN:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));

					results[i] = macd[periods].Hist >= 0 ? 1 : -1;
					break;

				case ParameterType.MACDHistCrossed:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));

					macd90 = macd[periods];
					previousMACD90 = TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex - 1);

					if (macd90.Hist >= 0)
						results[i] = previousMACD90.Hist >= 0 ? 0 : 1;
					else
						results[i] = previousMACD90.Hist < 0 ? 0 : -1;
					break;

				case ParameterType.MACDHistDifference:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));

					macd90 = macd[periods];
					previousMACD90 = TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex - 1);

					results[i] = macd90.Hist - previousMACD90.Hist;
					break;

				case ParameterType.MACD:
					if (!macd.ContainsKey(periods))
						macd.Add(periods, TechnicalIndicators.CalculateMACD(candlesticks, periods, candlestickIndex));

					results[i] = macd[periods].Macd;
					break;

				case ParameterType.SlopesEMA:
					List<float> slopes = new List<float>();

					for (int k = 0; k < iParameters[i].Attributes.Count; k++)
					{
						int periodLength = (int)iParameters[i].Attributes[k];
						slopes.Add(new LinearRegression(TechnicalIndicators.CreatePriceArray(candlesticks, periodLength, candlestickIndex)).Slope);
					}

					results[i] = Utils.Last(Statistics.EMA(slopes.ToArray(), slopes.Count));
					break;

				case ParameterType.ABAverage:
					results[i] = (TechnicalIndicators.CalculatePriceABaverage(candlesticks, periods, candlestickIndex, iCurrentPrice) ? 1.0f : 0.0f);
					break;

				case ParameterType.PercentMargin:
					results[i] = (TechnicalIndicators.CalculateOnePercentMargin(candlesticks, periods, candlestickIndex, iCurrentPrice, iParameters[i].Attributes[0]) ? 1.0f : 0.0f);
					break;

				case ParameterType.Classifier:
					results[i] = WekaClassifier.Find((int)iParameters[i].Attributes[0]).PredictDFP(iCandlesticks, iCurrentPrice, iCandlestickIndex);
					break;

				case ParameterType.ClassifierTargetChangeOldest:
					var classifier = WekaClassifier.Find((int)iParameters[i].Attributes[0]);
					var targetTime = iCandlesticks[WekaClassifier.kTrainingPeriod][iCandlestickIndex[WekaClassifier.kTrainingPeriod] - classifier.ProfitTime].StartTime;

					var pastIndex = new CandlestickIndexCollection();
					for (int k = (int)WekaClassifier.kTrainingPeriod; k >= 0; k--)
						pastIndex[k] = Math.Max(0, Math.Min(iCandlesticks[k].Count - 1, Candlestick.FindIndex(iCandlesticks[k], targetTime)));

					var priceBefore = iCandlesticks[WekaClassifier.kTrainingPeriod][pastIndex[WekaClassifier.kTrainingPeriod]].MedianPrice;

					var wfp = classifier.PredictDFP(iCandlesticks, priceBefore, pastIndex);
					var targetPriceChange = (float)Math.Exp(wfp);

					results[i] = priceBefore * targetPriceChange / iCurrentPrice - 1.0f;
					break;

					
			}
		}

		return results;
	}

	//**************************************************************************************

	public bool Equals(ClassifierParameter iOther)
	{
		return Type == iOther.Type && Periods == iOther.Periods &&
			Attributes.Count == iOther.Attributes.Count &&
			Attributes.SequenceEqual(iOther.Attributes);
	}

	//**************************************************************************************

	/// <summary>
	/// Returns true if given parameters' lists matches.
	/// </summary>
	public static bool Equals(List<ClassifierParameter> iParameters1, List<ClassifierParameter> iParameters2)
	{
		if (iParameters1.Count != iParameters2.Count)
			return false;

		for (int i = 0; i < iParameters1.Count; i++)
			if (!Exist(iParameters2, iParameters1[i]))
				return false;

		return true;
	}

	//**************************************************************************************

	/// <summary>
	/// Returns true if given parameter exist in provided list.
	/// </summary>
	public static bool Exist(List<ClassifierParameter> iParameters, ClassifierParameter iParameter)
		=> !(iParameters.FirstOrDefault(x => x.Equals(iParameter)) is null);

	//**************************************************************************************

	/// <summary>
	/// Returns string that uniquely represents parameter.
	/// </summary>
	public string ToUniqueString()
	{
		string results = Type.ToString() + "_" + Periods.ToString() + "_";

		for (int i = 0; i < Attributes.Count; i++)
			results += Attributes[i].ToString() + "_";

		return results;
	}

	//**************************************************************************************
	
	/// <summary>
	/// Converts given definition to a paramters list.
	/// </summary>
	public static List<ClassifierParameter> CreateParameters(List<string> iDefinitions)
	{
		var results = new List<ClassifierParameter>(iDefinitions.Count);

		for (int i = 0; i < iDefinitions.Count; i++)
			results.Add(new ClassifierParameter(iDefinitions[i]));

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads parameters from a txt file.
	/// </summary>
	public static List<ClassifierParameter> Load(string iFilename)
	{
		var results = new List<ClassifierParameter>();
		if (!File.Exists(iFilename))
			return results;

		using (StreamReader reader = new StreamReader(iFilename))
			while (!reader.EndOfStream)
				results.Add(new ClassifierParameter(reader.ReadLine()));
		
		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Save parameters to a txt file.
	/// </summary>
	public static void Save(string iFilename, List<ClassifierParameter> iParameters)
	{
		using (StreamWriter writer = new StreamWriter(iFilename))
			foreach (var p in iParameters)
			{
				var line = p.Type.ToString() + ";" + p.Periods.ToString() + ";";
				line += string.Join(";", p.Attributes.Select(x => x.ToString()).ToArray());
				writer.WriteLine(line);
			}					
	}

	//**************************************************************************************
}

