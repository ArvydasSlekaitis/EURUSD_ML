using System;
using System.Collections.Generic;
using System.Windows.Forms;

/// <summary>
/// Contains realtime trading functions.
/// </summary>
static class Realtime
{
	//**************************************************************************************

	/// <summary>
	/// Loads and asserts that realtime candlesticks is retrieved.
	/// </summary>
	public static CandlestickCollection LoadRealTimeCandlesticks(CandlestickCollection iHistoricalCandlesticks)
	{
		CandlestickCollection realtimeCandlesticks = new CandlestickCollection(CandlestickCollection.Type.Realtime);

		if (realtimeCandlesticks[Candlestick.Period.m30] == null)
			throw new Exception("Could not retrieve real time 30 minutes candlesticks.");

		CandlesticksConsistencyCheck(realtimeCandlesticks, iHistoricalCandlesticks);
		Console.WriteLine("Realtime candlesticks retrieved.");
	
		return realtimeCandlesticks;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads and asserts that realtime price is retrieved.
	/// </summary>
	public static float LoadRealTimePrice()
	{
		try
		{
			float price = AlphaVantage.RetrieveCurrentPrice();
			Console.WriteLine("Realtime price retrieved.");
			return price;
		}
		catch(System.Exception)
		{
			Console.WriteLine("Could not retrieve realtime price.");
			Console.WriteLine("Enter realtime price manually");
			return float.Parse(Console.ReadLine());
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Checks if retrieved candlesticks is similar to historical data.
	/// </summary>
	private static void CandlesticksConsistencyCheck(CandlestickCollection iRealtimeCandlesticks, CandlestickCollection iHistoricalCandlesticks)
	{
		List<float> diff = new List<float>();

		for (int i = 0; i < 5 * 365; i++)
		{
			DateTime referenceDate = new DateTime(2012, 1, 1, 23, 59, 59);
			referenceDate = referenceDate.AddDays(i);
			Candlestick directRealtime = iRealtimeCandlesticks[Candlestick.Period.D1][Candlestick.FindIndex(iRealtimeCandlesticks[Candlestick.Period.D1], Utils.DateTimeToUnix(referenceDate))];
			Candlestick directHistorical = iHistoricalCandlesticks[Candlestick.Period.H1][Candlestick.FindIndex(iHistoricalCandlesticks[Candlestick.Period.H1], Utils.DateTimeToUnix(referenceDate))];
			diff.Add(Math.Abs(directHistorical.ClosePrice - directRealtime.ClosePrice));
		}

		var currentDiff = Statistics.ArithmeticMean(diff.ToArray());

		if (currentDiff > 0.000403)// Should be 400!!
		{
			Console.WriteLine("Realtime candlesticks incosistent with historical candlesticks");
			Console.WriteLine("Continue (c); Quit (q)?");
			string answer = Console.ReadLine();

			if (answer == "q")
				Application.Exit();
			else if(answer!="c")
				throw new Exception("Realtime candlesticks incosistent with historical candlesticks: " + currentDiff);
		}

		// Check for same time zone
		var consolidatedH = Candlestick.Consolidate(iRealtimeCandlesticks[Candlestick.Period.H1], 1);
		var trueDaily = iRealtimeCandlesticks[Candlestick.Period.D1];

		if (consolidatedH[consolidatedH.Count - 1].EndTime != trueDaily[trueDaily.Count - 1].EndTime)
			Console.WriteLine("WARNING: Realtime candlesticks timezone incosistent");

		if (consolidatedH[consolidatedH.Count - 1].OpenPrice != trueDaily[trueDaily.Count - 1].OpenPrice && consolidatedH[consolidatedH.Count - 1].ClosePrice != trueDaily[trueDaily.Count - 1].ClosePrice)
			Console.WriteLine("WARNING: Realtime candlesticks timezone incosistent");
	}

	//**************************************************************************************
}
