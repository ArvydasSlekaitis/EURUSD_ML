using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// On demand retrieval and store of candlestick lists.
/// </summary>
public class CandlestickCollection
{
	public enum Type { Custom, Historical, Realtime };

	private readonly List<Candlestick>[] candlesticks = new List<Candlestick>[10];
	public readonly Type type;
	public int Count => candlesticks.Length;

	//**************************************************************************************

	public CandlestickCollection(Type iType)
	{
		type = iType;
	}

	//**************************************************************************************

	public List<Candlestick> this[Candlestick.Period candlestickPeriod]
	{
		get => this[(int)candlestickPeriod];
		set => this[(int)candlestickPeriod] = value;
	}

	//**************************************************************************************

	public List<Candlestick> this[int index]
	{
		get
		{
			if(candlesticks[index] != null)
				return candlesticks[index];

			lock (candlesticks)
			{
				if (candlesticks[index] == null && type != Type.Custom)
					Load((Candlestick.Period)index);
			}

			return candlesticks[index];
		}
		set => candlesticks[index] = value;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads given period candlesticks.
	/// </summary>
	private void Load(Candlestick.Period iPeriod)
	{
		switch(type)
		{
			case Type.Realtime:
				switch (iPeriod)
				{
					case Candlestick.Period.D1:
					case Candlestick.Period.m15:
					case Candlestick.Period.m5:
					case Candlestick.Period.m1:
						candlesticks[(int)iPeriod] = AlphaVantage.RetrieveCandlesticks(iPeriod);
						break;

					case Candlestick.Period.m30:
						var realtimeCandles = AlphaVantage.RetrieveCandlesticks(iPeriod);
						Candlestick.SaveToDB("RealtimeCandlesticks30M", realtimeCandles);
						candlesticks[(int)iPeriod] = Candlestick.LoadFromDB("RealtimeCandlesticks30M");
						break;

					case Candlestick.Period.H12:
					case Candlestick.Period.H6:
					case Candlestick.Period.H3:
					case Candlestick.Period.H2:
					case Candlestick.Period.H1:
						candlesticks[(int)iPeriod] = Candlestick.Consolidate(this[Candlestick.Period.m30], Candlestick.PeriodToDaily(iPeriod));
						break;
				}
				break;

			case Type.Historical:
				string filename = "Data/EURUSD/Candlestick/" + Candlestick.PeriodToName(iPeriod) + ".dat";

				if (File.Exists(filename))
					candlesticks[(int)iPeriod] = Candlestick.Load(filename);
				else
				{
					if (iPeriod == Candlestick.Period.m1)
						candlesticks[(int)iPeriod] = LoadRawHistorical1M();
					else
						candlesticks[(int)iPeriod] = Candlestick.Consolidate(this[Candlestick.Period.m1], Candlestick.PeriodToDaily(iPeriod));
	
					Candlestick.Save(filename, candlesticks[(int)iPeriod]);
				}				
				break;
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Adds new candlestick to the end of the list.
	/// </summary>
	public void Add(Candlestick iCandlestick, Candlestick.Period iPeriod)
	{
		this[iPeriod].Add(iCandlestick);
		
		int startIndex = Math.Max(0, this[iPeriod].Count - Candlestick.PeriodToDaily(iPeriod));
			   
		List<Candlestick> copy = Candlestick.CreateCopy(this[iPeriod], startIndex, this[iPeriod].Count-1);

		for (int k = (int)iPeriod - 1; k >= 0; k--)
		{
			List<Candlestick> cons =  Candlestick.Consolidate(copy, Candlestick.PeriodToDaily((Candlestick.Period)k));

			if (cons.Count < 1)
				continue;

			if (cons[cons.Count - 1].EndTime != this[k][this[k].Count - 1].EndTime)
				this[k].Add(cons[cons.Count - 1]);
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Splits candlestick collection into two, each containing copy of candlesticks.
	/// </summary>
	public static void Split(CandlestickCollection iCandlesticks, DateTime iSplitTime, Candlestick.Period iHighestFrequency, out CandlestickCollection oPart1, out CandlestickCollection oPart2)
	{
		if (iCandlesticks is null)
			throw new ArgumentNullException(nameof(iCandlesticks));

		oPart1 = new CandlestickCollection(Type.Custom);
		oPart2 = new CandlestickCollection(Type.Custom);

		for (int k = (int)iHighestFrequency; k >= 0; k--)
		{
			int splitIndex = Candlestick.FindIndex(iCandlesticks[k], Utils.DateTimeToUnix(iSplitTime));

			oPart1[k] = Candlestick.CreateCopy(iCandlesticks[k], 0, splitIndex-1);
			oPart2[k] = Candlestick.CreateCopy(iCandlesticks[k], splitIndex, iCandlesticks[k].Count - 1);
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Loads and returns raw candlestick data from csv files.
	/// </summary>
	public static List<Candlestick> LoadRawHistorical1M()
	{
		if (!Directory.Exists("Data/EURUSD/Candlestick"))
			Directory.CreateDirectory("Data/EURUSD/Candlestick");

		var candlesticks = new List<Candlestick>();

		for (int i = 2000; i <= 2020; i++)
		{
			var filename = "Data/EURUSD/Raw/" + i.ToString() + ".csv";
			if (File.Exists(filename))
				using (StreamReader reader = new StreamReader(filename))
					candlesticks.AddRange(Candlestick.LoadCSV(reader, Candlestick.Period.m1, 6));
		}

		return candlesticks;
	}

	//**************************************************************************************
}
