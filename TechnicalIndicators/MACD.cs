
using System;
/// <summary>
/// Technical analysis MACD indicator.
/// </summary>
public class MACD
{
	public readonly float Macd;
	public readonly float Signal;
	public readonly float Hist;

	//**************************************************************************************

	public MACD(float[] iNumbers, int iSlowEMA = 26, int iFastEMA = 12, int iSignalEMA = 9)
	{
		if (iFastEMA > iSlowEMA)
			throw new ArgumentException("iFastEMA should be smaller than iSlowEMA");

		if (iSlowEMA <= 0 || iFastEMA <= 0 || iSignalEMA <= 0)
			throw new ArgumentException("All ema periods should be greater than zero", "iSlowEMA <= 0 || iFastEMA <= 0 || iSignalEMA <= 0");

		float[] slowEMA = Statistics.EMA(iNumbers, iSlowEMA);
		float[] fastEMA = Statistics.EMA(iNumbers, iFastEMA);

		float[] signalEMAData = new float[iNumbers.Length];

		for (int i = 0; i < signalEMAData.Length; i++)
			signalEMAData[i] = fastEMA[i] - slowEMA[i];

		float[] signalEMA = Statistics.EMA(signalEMAData, iSignalEMA);

		Macd = fastEMA[fastEMA.Length - 1] - slowEMA[slowEMA.Length - 1];
		Signal = signalEMA[signalEMA.Length - 1];
		Hist = Macd - Signal;
	}

	//**************************************************************************************

}
