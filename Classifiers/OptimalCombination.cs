using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

class OptimalCombination
{
	//**************************************************************************************

	/// <summary>
	/// Starts optimal classifiers combination search.
	/// </summary>
	public static void Search(CandlestickCollection iHistoricalCandlesticks, List<WekaClassifier> iRootClassifiers)
	{
		var startTime = new DateTime(2002, 1, 1);
		var endTime = new DateTime(2019, 12, 23);

		while (true)
		{
			// Load enabled classifiers
			var enabledClassifiers = WekaClassifier.Find(iRootClassifiers, WekaClassifier.LoadEnabledClassifiers());
			enabledClassifiers = enabledClassifiers.OrderBy(x => x.GetPrecision()).ToList();

			// Performe historical simulation
			ImpliedSimulation.SimulateFullHistory(iHistoricalCandlesticks, enabledClassifiers, startTime, endTime, out var bestCorrelation, out var bestStdDev);
			var bestPrecision = CalculateAveragePrecision(enabledClassifiers) / 100.0f;
			var found = false;
			OutputNewBest(bestCorrelation, bestStdDev, bestPrecision);

			// Perform historical simulations with one classifier removed
			for (int i = 0; i < enabledClassifiers.Count; i++)
			{
				var currentClassifiers = WekaClassifier.Remove(enabledClassifiers, enabledClassifiers[i].ID);
				ImpliedSimulation.SimulateFullHistory(iHistoricalCandlesticks, currentClassifiers, startTime, endTime, out var currentCorrelation, out var currentStdDev, "WithoutClassifier" + enabledClassifiers[i].ID.ToString());
				var currentPrecision = CalculateAveragePrecision(currentClassifiers) / 100.0f;

				if (IsBetterCombination(bestCorrelation, bestStdDev, bestPrecision, currentCorrelation, currentStdDev, currentPrecision))
				{
					Console.WriteLine("Weak classifier has been found. Classifier ID: " + enabledClassifiers[i].ID);
					OutputNewBest(currentCorrelation, currentStdDev, currentPrecision);
					WekaClassifier.DisableClassifier(enabledClassifiers[i].ID);
					ClearOutputDirectory();
					found = true;
					break;
				}
			}

			if (found)
				continue;

			// Create optional classifiers list
			var optionalClassifiers = new List<WekaClassifier>();

			foreach (WekaClassifier cl in iRootClassifiers)
				if (!enabledClassifiers.Contains(cl))
					optionalClassifiers.Add(cl);

			optionalClassifiers = optionalClassifiers.OrderBy(x => x.GetPrecision()).Reverse().ToList();
			enabledClassifiers = enabledClassifiers.OrderBy(x => x.GetPrecision()).ToList();

			// Perform historical simulations by upgrading existing classifiers
			for (int i = 0; i < enabledClassifiers.Count; i++)						
			{
				for (int k = 0; k < optionalClassifiers.Count; k++)
					if (optionalClassifiers[k].ProfitTime == enabledClassifiers[i].ProfitTime)
					{
						if(optionalClassifiers[k].GetPrecision() <= enabledClassifiers[i].GetPrecision())
						{
							if(i<3)
								Console.WriteLine("Low precision classifier detected. Classifier ID: " + optionalClassifiers[k].ID);

							continue;
						}

						var currentClassifiers = WekaClassifier.Remove(enabledClassifiers, enabledClassifiers[i].ID);
						currentClassifiers.Add(optionalClassifiers[k]);

						ImpliedSimulation.SimulateFullHistory(iHistoricalCandlesticks, currentClassifiers, startTime, endTime, out var currentCorrelation, out var currentStdDev, "ClassifierUpgrade_" + enabledClassifiers[i].ID.ToString() + "_To_" + optionalClassifiers[k].ID.ToString());
						var currentPrecision = CalculateAveragePrecision(currentClassifiers) / 100.0f;

						if (IsBetterCombination(bestCorrelation, bestStdDev, bestPrecision, currentCorrelation, currentStdDev, currentPrecision))
						{
							Console.WriteLine("Classifier upgrade has been found. Old classifier ID: " + enabledClassifiers[i].ID + ". New classifier ID:" + optionalClassifiers[k].ID);
							OutputNewBest(currentCorrelation, currentStdDev, currentPrecision);
							WekaClassifier.DisableClassifier(enabledClassifiers[i].ID);
							WekaClassifier.EnableClassifier(optionalClassifiers[k].ID);
							ClearOutputDirectory();
							found = true;
							break;
						}
						else 
							optionalClassifiers[k].UnloadModel();
					}
				if (found)
					break;
			}

			if (found)
				continue;

			// Perform historical simulations with one classifier added
			for (int i = 0; i < optionalClassifiers.Count; i++)
			{
				var currentClassifiers = new List<WekaClassifier>(enabledClassifiers);
				currentClassifiers.Add(optionalClassifiers[i]);
				ImpliedSimulation.SimulateFullHistory(iHistoricalCandlesticks, currentClassifiers, startTime, endTime, out var currentCorrelation, out var currentStdDev, "WithClassifier" + optionalClassifiers[i].ID.ToString());
				var currentPrecision = CalculateAveragePrecision(currentClassifiers) / 100.0f;

				if (IsBetterCombination(bestCorrelation, bestStdDev, bestPrecision, currentCorrelation, currentStdDev, currentPrecision))
				{
					Console.WriteLine("Strong classifier has been found. Classifier ID: " + optionalClassifiers[i].ID);
					OutputNewBest(currentCorrelation, currentStdDev, currentPrecision);
					WekaClassifier.EnableClassifier(optionalClassifiers[i].ID);
					ClearOutputDirectory();
					found = true;
					break;
				}
				else 
					optionalClassifiers[i].UnloadModel();

			}

			if (found)
				continue;
			else
				break;
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates average precision as seen by implied simulation.
	/// </summary>
	private static float CalculateAveragePrecision(List<WekaClassifier> iClassifiers)
	{
		var weight = new List<float>(iClassifiers.Count);
		var precisions = new List<float>();

		var classfiers = WekaClassifier.SplitByProfitTime(iClassifiers);

		foreach (KeyValuePair<int, List<WekaClassifier>> pair in classfiers)
		{
			var p = WekaClassifier.GetAveragePrecision(pair.Value);
			precisions.Add(p);
			weight.Add((float)(Math.Pow(p, 6) / Math.Pow(pair.Value[0].GetProfitsStdDev(), 6)));
		}

		return Statistics.WeightedArithmeticMean(precisions.ToArray(), Statistics.Normalize(weight.ToArray()));
	}

	//**************************************************************************************

	/// <summary>
	/// Returns true if current results are better than best.
	/// </summary>
	static bool IsBetterCombination(float iBestCorrelation, float iBestStdDev, float iBestPrecision, float iCurrentCorrelation, float iCurrentStdDev, float iCurrentPrecision) =>
		iCurrentStdDev / iCurrentPrecision < iBestStdDev / iBestPrecision && iCurrentCorrelation * iCurrentPrecision > iBestCorrelation * iBestPrecision;

	//**************************************************************************************

	/// <summary>
	/// Clears output directory.
	/// </summary>
	static void ClearOutputDirectory()
	{
		DirectoryInfo di = new DirectoryInfo("Output/");

		foreach (FileInfo file in di.GetFiles())
			file.Delete();
	}

	//**************************************************************************************
	
	/// <summary>
	/// Outputs new best result to console.
	/// </summary>
	static void OutputNewBest(float iCorrelation, float iStdDev, float iPrecision) =>
		Console.WriteLine("Correlation: " + (iCorrelation * iPrecision) + "(" + iCorrelation + ") Std Dev: " + (iStdDev / iPrecision) + "(" + iStdDev + ")");

	//**************************************************************************************
}
