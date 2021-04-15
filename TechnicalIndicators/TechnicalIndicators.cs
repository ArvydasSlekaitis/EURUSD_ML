using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class TechnicalIndicators
{
//**************************************************************************************

	/// <summary>
	/// Calculates and returns mean to standard deviation indicator.
	/// </summary>
	public static float MeanToStdDev(float iMeanPrice, float iMeanPriceStandardDeviation, float iCurrentPrice)
	{
		if (iMeanPrice < 0)
			throw new ArgumentException("Mean price cannot be less than 0", "iMeanPrice");

		if (iMeanPriceStandardDeviation < 0)
			throw new ArgumentException("Mean price standard deviation cannot be less than 0", "iMeanPriceStandardDeviation");

		if (iCurrentPrice <= 0)
			throw new ArgumentException("Current price cannot be less or equal to 0", "iCurrentPrice");

		return (iCurrentPrice - iMeanPrice) / iMeanPriceStandardDeviation;
	}

//**************************************************************************************

	/// <summary>
	/// Returns signal array, which contains signals that are generated once one average passes the other.
	/// </summary>
	public int[] GenerateCrossovers(float[] iPrices, int iTrendLine1Periods, int iTrendLine2Periods)
	{
		if (iPrices == null || iPrices.Length < 1)
			throw new ArgumentException("List must contain at least one value.", "iCandlestickEntries");

		if (iTrendLine1Periods < 1 || iTrendLine2Periods < 1)
			throw new ArgumentException("Trend line periods must be greater than one.", "iTrendLine1Periods < 1 || iTrendLine2Periods < 1");

		bool[] trendLine1ABTrendLine2 = new bool[iPrices.Length];
		int[] signals = new int[iPrices.Length];

		for (int i = Math.Max(iTrendLine1Periods, iTrendLine2Periods); i < iPrices.Length; i++)
		{
			float[] rawPrice1 = new float[iTrendLine1Periods];
			for (int ii = i - iTrendLine1Periods; ii < i; ii++)
				rawPrice1[ii - i + iTrendLine1Periods] = iPrices[i];

			float[] rawPrice2 = new float[iTrendLine2Periods];
			for (int ii = i - iTrendLine2Periods; ii < i; ii++)
				rawPrice2[ii - i + iTrendLine2Periods] = iPrices[i];

			double averagePrice1 = Statistics.ArithmeticMean(rawPrice1);
			double averagePrice2 = Statistics.ArithmeticMean(rawPrice2);

			trendLine1ABTrendLine2[i] = averagePrice1 >= averagePrice2 ? true : false;
			signals[i] = trendLine1ABTrendLine2[i] == trendLine1ABTrendLine2[i - 1] ? 0 : (trendLine1ABTrendLine2[i] && !trendLine1ABTrendLine2[i - 1]) ? 1 : -1;
		}

		return signals;
	}

//**************************************************************************************

	/// <summary>
	/// LHA - Angle between low and high. Returns angle between low values slope and high values slope [-1 = strong buy; 1 = strong sell]
	/// </summary>
	public float LHA(float[] iLows, float[] iHigh)
	{
		if (iLows == null || iHigh == null || iLows.Length < 2 || iLows.Length != iHigh.Length)
			throw new ArgumentException("Invalid array");

		LinearRegression rLow = new LinearRegression(iLows);
		LinearRegression rHigh = new LinearRegression(iHigh);

		Vector2 vLow = new Vector2(1, (float)rLow.Slope);
		Vector2 vHigh = new Vector2(1, (float)rHigh.Slope);

		float angle = (float)Utils.AngleBetween(Vector2.Normalize(vLow), Vector2.Normalize(vHigh));

		if (rHigh.Slope < rLow.Slope)
			angle = -angle;

		return Math.Max(Math.Min(-angle / 180.0f, 1.0f), -1.0f);
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns RSI indicator value.
	/// </summary>
	public static float RSI(float[] iPrice, int iNumberOfPeriods = 14)
	{
		if (iPrice == null || iPrice.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iPrice");

		if (iNumberOfPeriods < 1)
			throw new ArgumentException("Number of periods must be greater than zero.", "iNumberOfPeriods");

		iNumberOfPeriods = Math.Min(iPrice.Length - 1, iNumberOfPeriods);

		float gain = 0.0f;
		float loss = 0.0f;

		// first RSI value
		for (int i = iPrice.Length-1; i > iPrice.Length-iNumberOfPeriods; i--)
		{
			var diff = iPrice[i] - iPrice[i - 1];
			if (diff >= 0)
				gain += diff;
			else
				loss -= diff;
		}

		float rs = gain / loss;
		return 100 - (100 / (1 + rs));
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns last critical RSI.
	/// </summary>
	public static int CalculateLastCriticalRSI(int iCurrentRSI, int iPreviousCriticalRSI)
	{
		if (iCurrentRSI != 0)
		{
			if (iCurrentRSI >= 1)
				return Math.Max(iPreviousCriticalRSI, iCurrentRSI);
			else
				return Math.Min(iPreviousCriticalRSI, iCurrentRSI);
		}

		return iPreviousCriticalRSI;
	}

	//**************************************************************************************

	/// <summary>
	/// Converts RSI indicator value to integer representation.
	/// </summary>
	/// <param name="iRSI">[0, 100]</param>
	/// <returns>-1, -2, 1, 2</returns>
	public static int RSIToInt(float iRSI)
	{
		if (iRSI >= 80)
			return 2;

		if (iRSI >= 70)
			return 1;

		if (iRSI <= 20)
			return -2;

		if (iRSI <= 30)
			return -1;

		return 0;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates RSI indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	public static float CalculateRSI(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice) 
		=> RSI(CreateAugmentedPriceArray(iCandlestickData, iPeriods, iIndex, iCurrentPrice));

	//**************************************************************************************

	/// <summary>
	/// Calculates RSI indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	public static float CalculateRSI(List<Candlestick> iCandlestickData, int iPeriods, int iIndex) 
		=> RSI(CreatePriceArray(iCandlestickData, iPeriods, iIndex));

	//**************************************************************************************

	/// <summary>
	/// Calculates mean to standard deviation indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	public static float CalculateMeanToStdDev(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice)
	{
		var mean = ArithmeticMean(iCandlestickData, iPeriods, iIndex);
		var stdev = StandardDeviation(iCandlestickData, iPeriods, iIndex, mean);
		return MeanToStdDev(mean, stdev, iCurrentPrice);
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates above below average indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	public static bool CalculatePriceABaverage(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice) 
		=> iCurrentPrice >= ArithmeticMean(iCandlestickData, iPeriods, iIndex);

	//**************************************************************************************

	/// <summary>
	/// Calculates above below exponential moving average indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	public static bool CalculatePriceABEMA(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice) 
		=> iCurrentPrice >= Utils.Last(Statistics.EMA(CreatePriceArray(iCandlestickData, iPeriods, iIndex), iPeriods)) ? true : false;


	//**************************************************************************************

	/// <summary>
	/// Calculates linear regression slope.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	public static float CalculateLinearRegressionSlope(List<Candlestick> iCandlestickData, int iPeriods, int iIndex) 
		=> new LinearRegression(CreatePriceArray(iCandlestickData, iPeriods, iIndex)).Slope;

	//**************************************************************************************

	/// <summary>
	/// Calculates one percent margin indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	public static bool CalculateOnePercentMargin(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice, float iMargin) 
		=> Math.Abs((ArithmeticMean(iCandlestickData, iPeriods, iIndex) / iCurrentPrice) - 1.0f) <= iMargin ? true : false;

	//**************************************************************************************

	/// <summary>
	/// Calculates margin slope indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	/// <param name="iMargin">Within this margin indicator returns 0.</param>
	public static float CalculateMarginSlope(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice, float iMargin) 
		=> CalculateOnePercentMargin(iCandlestickData, iPeriods, iIndex, iCurrentPrice, iMargin) ? 0.0f : CalculateLinearRegressionSlope(iCandlestickData, iPeriods, iIndex);

	//**************************************************************************************

	/// <summary>
	/// Calculates margin slope positive / negative indicator value.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	/// <param name="iMargin">Within this margin indicator returns 0.</param>
	public static float CalculateMarginSlopePN(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice, float iMargin) 
		=> CalculateOnePercentMargin(iCandlestickData, iPeriods, iIndex, iCurrentPrice, iMargin) ? 0.0f : (CalculateLinearRegressionSlope(iCandlestickData, iPeriods, iIndex) > 0 ? 1.0f : -1.0f);

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns MACD indicator.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	public static MACD CalculateMACD(List<Candlestick> iCandlestickData, int iPeriods, int iIndex) 
		=> new MACD(CreatePriceArray(iCandlestickData, iPeriods, iIndex));

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns MACD indicator.
	/// </summary>
	/// <param name="iPeriods">Number of values to be used for indicator. E.g. for 1D candlesticks giving periods value of 30, function use 30 days data for indicator calculation.</param>
	/// <param name="iIndex">At which index should we calculate indicator values (backwords)?</param>
	/// <param name="iCurrentPrice">Most recent price.</param>
	public static MACD CalculateMACD(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice) 
		=> new MACD(CreateAugmentedPriceArray(iCandlestickData, iPeriods, iIndex, iCurrentPrice));
	
	//**************************************************************************************

	/// <summary>
	/// Creates and returns array containing average prices starting with iIndex-iPeriods up to iIndex and adding additional (last) element of current price.
	/// </summary>
	public static float[] CreateAugmentedPriceArray(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iCurrentPrice)
	{
		float[] array = new float[iPeriods + 1];

		for (int i = iIndex - iPeriods + 1; i <= iIndex; i++)
			array[i - iIndex + iPeriods - 1] = iCandlestickData[i].ClosePrice;

		array[array.Length - 1] = iCurrentPrice;

		return array;
	}

	//**************************************************************************************

	/// <summary>
	/// Creates and returns array containing average prices starting with iIndex-iPeriods up to iIndex.
	/// </summary>
	public static float[] CreatePriceArray(List<Candlestick> iCandlestickData, int iPeriods, int iIndex)
	{
		Candlestick[] rawData = new Candlestick[iPeriods];
		iCandlestickData.CopyTo(iIndex - iPeriods + 1, rawData, 0, iPeriods);

		if (rawData[rawData.Length - 1] != iCandlestickData[iIndex])
			throw new SystemException("Last element mismatch.");

		return rawData.Select(x => x.ClosePrice).ToArray();
	}

	//**************************************************************************************

	static float ArithmeticMean(List<Candlestick> iCandlestickData, int iPeriods, int iIndex)
	{
		var sum = 0.0f;

		for (int i = iIndex - iPeriods + 1; i <= iIndex; i++)
			sum += iCandlestickData[i].ClosePrice;

		return sum / iPeriods;
	}

	//**************************************************************************************

	static float StandardDeviation(List<Candlestick> iCandlestickData, int iPeriods, int iIndex, float iArithmeticMean)
	{
		var sum = 0.0f;

		for (int i = iIndex - iPeriods + 1; i <= iIndex; i++)
			sum += (float)Math.Pow(iCandlestickData[i].ClosePrice - iArithmeticMean, 2.0f);

		return (float)Math.Sqrt(sum / (iPeriods - 1.0));
	}	

	//**************************************************************************************

}

