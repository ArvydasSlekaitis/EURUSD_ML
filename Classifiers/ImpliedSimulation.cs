using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class ImpliedSimulation
{
	//**************************************************************************************

	/// <summary>
	/// Starts simulation starting from the end of historical candlesticksm and up to prediction duration.
	/// </summary>
	private static void Start(CandlestickCollection iHistoricalCandlesticks, CandlestickCollection iCandlesticks, List<WekaClassifier> iClassifiers, int iPredictionDuration)
	{	
		var index = new CandlestickIndexCollection();
		var candlesticks = iCandlesticks;

		var classfiers = WekaClassifier.SplitByProfitTime(iClassifiers);
		var initialCount = candlesticks[WekaClassifier.kTrainingPeriod].Count;
		var startIndex = Math.Max(initialCount - classfiers.Keys.Max() - 1, 1);
		var endIndex = initialCount + iPredictionDuration - 1;
		var priceDiffStdDev = Statistics.StandardDeviation(Statistics.CalculateDifferences(iHistoricalCandlesticks[WekaClassifier.kTrainingPeriod].Select(x => x.MedianPrice).ToArray()));

		var wfp = new SortedDictionary<int, List<float>>();
		var weight = new List<float>(classfiers.Count);

		foreach (KeyValuePair<int, List<WekaClassifier>> pair in classfiers)
		{
			wfp.Add(pair.Key, new List<float>());
			var precision = WekaClassifier.GetAveragePrecision(pair.Value);
			weight.Add((float)(Math.Pow(precision, 6) / Math.Pow(pair.Value[0].GetProfitsStdDev(), 6.0f)));
		}

		weight = Statistics.Normalize(weight.ToArray()).ToList();

		for (int i = startIndex; i <= endIndex; i++)
		{
			// Update indexes
			for (int k = (int)WekaClassifier.kTrainingPeriod; k >= 0; k--)
				index[k] = Math.Max(0, Math.Min(candlesticks[k].Count - 1, Candlestick.FindIndex(candlesticks[k], candlesticks[WekaClassifier.kTrainingPeriod][i].StartTime, index[k]) - 1));
						
			// Extract current price
			var p = candlesticks[WekaClassifier.kTrainingPeriod][i].MedianPrice;

			// Calculate WFP
			foreach (KeyValuePair<int, List<WekaClassifier>> pair in classfiers)
				if(i >= initialCount - pair.Key - 1)
				{
					var predictionsNow = WekaClassifier.PredictFP(pair.Value, p, candlesticks, index);
					var wfpNow = WekaClassifier.FPToWFP(pair.Value, predictionsNow);
					wfp[pair.Key].Add(wfpNow);
				}

			// Future
			if (i + 1 >= initialCount)
			{
				var lastCandle = candlesticks[WekaClassifier.kTrainingPeriod][candlesticks[WekaClassifier.kTrainingPeriod].Count - 1];

				var estimatedPrices = new List<float>(classfiers.Count);
				foreach(KeyValuePair<int, List<WekaClassifier>> pair in classfiers)
					estimatedPrices.Add(EstimatePrice(pair.Key, wfp[pair.Key], candlesticks[WekaClassifier.kTrainingPeriod]));
				
				var targetPrice = Statistics.WeightedArithmeticMean(estimatedPrices.ToArray(), weight.ToArray());
				
				targetPrice = Statistics.Clamp(targetPrice, lastCandle.MedianPrice * (1.0f - 3.0f*priceDiffStdDev), lastCandle.MedianPrice * (1.0f + 3.0f*priceDiffStdDev));

				var candle = new Candlestick(lastCandle.EndTime, lastCandle.EndTime + 86400000 / (ulong)Candlestick.PeriodToDaily(Candlestick.Period.H1), lastCandle.ClosePrice, targetPrice, Math.Max(lastCandle.ClosePrice, targetPrice), Math.Min(lastCandle.ClosePrice, targetPrice), targetPrice);

				candlesticks.Add(candle, WekaClassifier.kTrainingPeriod);							   				 
			}
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Estimates price based on historical WFP
	/// </summary>
	private static float EstimatePrice(int iMaxProfitTime, List<float> iWFP, List<Candlestick> iCandlesticks)
	{
		var startIndex = Math.Max(0, iWFP.Count - iMaxProfitTime);

		if (iMaxProfitTime >= 48)
		{
			var priceBeforeID = iCandlesticks.Count - 1 - iWFP.Count + startIndex;
			var priceBefore = iCandlesticks[priceBeforeID].MedianPrice;
			var targetPriceChange = (float)Math.Exp(iWFP[startIndex]);
			return priceBefore * targetPriceChange;
		}

		var estimatedPrices = new List<float>(24);	
		var endIndex = Math.Min(startIndex + Math.Min(24, iMaxProfitTime), iWFP.Count);

		for (int k = startIndex; k < endIndex; k++)
		{
			var priceBeforeID = iCandlesticks.Count - 1 - iWFP.Count + k;
			var priceBefore = iCandlesticks[priceBeforeID].MedianPrice;
			var targetPriceChange = (float)Math.Exp(iWFP[k]);
			estimatedPrices.Add(priceBefore * targetPriceChange);
		}

		return Utils.Last(Statistics.EMA(estimatedPrices.ToArray(), estimatedPrices.Count));
	}

	//**************************************************************************************

	/// <summary>
	/// Performs simulation and outputs results to a DataTable
	/// </summary>
	public static DataTable PerformSimulation(CandlestickCollection iCandlesticks, List<WekaClassifier> iClassifiers, DateTime iStartTime, int iNumberOfHours, Candlestick.Period iOutputPeriod = Candlestick.Period.H1)
	{
		// Create new data table.
		var table = new DataTable(iCandlesticks.type.ToString() + "Simulation_" + iStartTime.Year + "_" + iStartTime.Month + "_" + iStartTime.Day + "_" + iNumberOfHours);
		table.Columns.Add("EndTime", typeof(DateTime));

		if (iCandlesticks.type != CandlestickCollection.Type.Realtime) 
			table.Columns.Add("RealPrice", typeof(float));

		table.Columns.Add("SimulationPrice", typeof(float));

		try
		{
			CandlestickCollection.Split(iCandlesticks, iStartTime, Candlestick.Period.H1, out CandlestickCollection results, out CandlestickCollection oPart2);
			var startIndex = results[iOutputPeriod].Count;

			Start(iCandlesticks, results, iClassifiers, iNumberOfHours);

			var endIndex = iCandlesticks.type == CandlestickCollection.Type.Realtime ? results[iOutputPeriod].Count : Math.Min(results[iOutputPeriod].Count, iCandlesticks[iOutputPeriod].Count);

			for (int i = startIndex; i < endIndex; i++)
			{
				var row = table.NewRow();
				row["EndTime"] = Utils.UnixToDateTime(results[iOutputPeriod][i].EndTime);

				if (iCandlesticks.type != CandlestickCollection.Type.Realtime)
					row["RealPrice"] = iCandlesticks[iOutputPeriod][i].MedianPrice;

				row["SimulationPrice"] = results[iOutputPeriod][i].MedianPrice;
				table.Rows.Add(row);
			}

			if (iCandlesticks.type == CandlestickCollection.Type.Realtime)
			{
				var medians = new List<float>(24);
				var sindex = Candlestick.FindIndex(results[Candlestick.Period.H1], Utils.DateTimeToUnix(iStartTime));
				for (int i = sindex; i < Math.Min(sindex + 24, results[Candlestick.Period.H1].Count); i++)
					medians.Add(results[Candlestick.Period.H1][i].MedianPrice);

				var low = float.MaxValue;
				var high = float.MinValue;

				for (int i = sindex; i < results[Candlestick.Period.H1].Count; i++)
				{
					var p = results[Candlestick.Period.H1][i].MedianPrice;
					if (p < low)
						low = p;
					if (p > high)
						high = p;			
				}

				Console.WriteLine("Next 24 hours estimated median price: " + medians.Median());
				Console.WriteLine("Next 7 days High/Low: " + high.ToString() + "/" + low.ToString());
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Exception was thrown while performing simulation: " + table.TableName + " Exp.:" + ex.ToString());
		}

		return table;
	}

	//**************************************************************************************

	/// <summary>
	/// Simulates full history broked down into periods and returns DataTable containing results.
	/// </summary>
	private static DataTable SimulateFullHistory(CandlestickCollection iHistoricalCandlesticks, List<WekaClassifier> iClassifiers, DateTime iStartTime, DateTime iEndTime, string iName = "")
	{
		if (iHistoricalCandlesticks.type == CandlestickCollection.Type.Realtime)
			throw new ArgumentException("Historical simulation can not be performed with realtime candlesticks.");

		// Create summary data table.
		var results = new DataTable();
		results.Columns.Add("EndTime", typeof(DateTime));
		results.Columns.Add("RealPrice", typeof(float));
		results.Columns.Add("SimulationPrice", typeof(float));
		results.Columns.Add("PDifference", typeof(float));
		results.Columns.Add("PRealPriceChange", typeof(float));
		results.Columns.Add("PSimulationPriceChange", typeof(float));

		// Construct file name
		var fileName = "Output/" + iHistoricalCandlesticks.type.ToString() + "Simulation_" + iName + ".csv";

		Console.WriteLine("Starting simulation: " + iName);

		if (File.Exists(fileName))
		{
			// Open file
			using (StreamReader reader = new StreamReader(fileName))
			{
				// Read header
				reader.ReadLine();

				// Read file contents
				while (!reader.EndOfStream)
				{
					var line = reader.ReadLine();
					var contents = line.Split(';');

					var row = results.NewRow();
					row["EndTime"] = DateTime.Parse(contents[0]);
					row["RealPrice"] = float.Parse(contents[1]);
					row["SimulationPrice"] = float.Parse(contents[2]);
					row["PDifference"] = float.Parse(contents[3]);
					row["PRealPriceChange"] = float.Parse(contents[4]);
					row["PSimulationPriceChange"] = float.Parse(contents[5]);
					results.Rows.Add(row);
				}
			}
			return results;
		}

		// Use only high precision classifiers
		var classifiers = iClassifiers;

		int it = 0;
		var tasks = new List<Task<DataTable>>();

		// Iterate through history
		while (true)
		{
			DateTime d = iStartTime;
			d = d.AddDays(it * 7);

			if(d > iEndTime)
				break;

			it++;

			// Create simulation tasks
			var t = Task.Run(() => PerformSimulation(iHistoricalCandlesticks, classifiers, d, 7 * 24, Candlestick.Period.D1));
			tasks.Add(t);
		}

		// Wait for tasks to complete
		foreach (Task t in tasks)
			t.Wait();

		foreach (Task<DataTable> t in tasks)
		{
			var simRes = t.Result;
			if (simRes == null || simRes.Rows.Count < 2)
				continue;

			for (int i = 1; i < simRes.Rows.Count; i++)
			{
				var row = results.NewRow();
				row["EndTime"] = simRes.Rows[i]["EndTime"];
				row["RealPrice"] = simRes.Rows[i]["RealPrice"];
				row["SimulationPrice"] = simRes.Rows[i]["SimulationPrice"];
				row["PDifference"] = (float)simRes.Rows[i]["SimulationPrice"] / (float)simRes.Rows[i]["RealPrice"] - 1.0f;
				row["PRealPriceChange"] = Math.Log((float)simRes.Rows[i]["RealPrice"] / (float)simRes.Rows[i - 1]["RealPrice"]);
				row["PSimulationPriceChange"] = Math.Log((float)simRes.Rows[i]["SimulationPrice"] / (float)simRes.Rows[i - 1]["SimulationPrice"]);
				results.Rows.Add(row);
			}
		}

		Utils.SaveDataTableToCSV(results, fileName);
		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Simulates full history broked down into periods and std. dev. and correlation.
	/// </summary>
	public static void SimulateFullHistory(CandlestickCollection iHistoricalCandlesticks, List<WekaClassifier> iClassifiers, DateTime iStartTime, DateTime iEndTime, out float oCorrelation, out float oStdDev, string iName = "")
	{
		var currentSim = SimulateFullHistory(iHistoricalCandlesticks, iClassifiers, iStartTime, iEndTime, iName);
		oStdDev = Statistics.StandardDeviation(currentSim.AsEnumerable().Select(r => r.Field<float>("PDifference")).ToArray());
		oCorrelation = (float)Math.Pow(Statistics.CalculateCorrelation(currentSim.AsEnumerable().Select(r => r.Field<float>("PRealPriceChange")).ToArray(), currentSim.AsEnumerable().Select(r => r.Field<float>("PSimulationPriceChange")).ToArray()), 2.0f);
	}

	//**************************************************************************************

}

