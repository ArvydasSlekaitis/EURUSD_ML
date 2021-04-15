using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

/// <summary>
/// Generic weka classificator.
/// </summary>
abstract class WekaClassifier
{
	public int ID => (int)info.ID;
	public int ParametersID => info.ParametersID;
	public Candlestick.Period Period => info.Period;
	public int ProfitTime => info.ProfitTime;
	public bool IsLinked => !(Parameters.FirstOrDefault(x=> (x.Type == ClassifierParameter.ParameterType.Classifier || x.Type == ClassifierParameter.ParameterType.ClassifierTargetChangeOldest)) is null);
	public string ModelFilename => "Data/Classifier_" + ID + ".model";

	public readonly List<ClassifierParameter> Parameters;

	public const Candlestick.Period kTrainingPeriod = Candlestick.Period.H1;

	public abstract float? GetPrecision();
	public abstract float GetProfitsStdDev();
	public abstract bool IsModelLoaded();
	public abstract void UnloadModel();
	public abstract void Build(CandlestickCollection iCandlestick);

	protected abstract void LoadModel();
	public abstract void RemoveClassifier();
	public abstract void RebuildClassifier();
	protected void OutputMessage(string iMsg) => Console.WriteLine(GetType().Name + "(" + ID + "): " + iMsg);

	private readonly WekaInfo info;

	protected static readonly SortedList<int, WekaClassifier> classifiers = new SortedList<int, WekaClassifier>();
	public static WekaClassifier Find(int iID) => classifiers[iID];
	public static bool Exist(int iID) => classifiers.ContainsKey(iID);

	protected abstract weka.core.Attribute GetDependentAttribute();
	protected java.util.ArrayList Attributes { get { if (attributes is null) attributes = CreateAttributes(); return attributes; } }
	private java.util.ArrayList attributes = null;

	//**************************************************************************************

	/// <summary>
	/// Constructor.
	/// </summary>
	public WekaClassifier(WekaInfo iInfo, List<ClassifierParameter> iParameters)
	{
		info = iInfo ??  throw new ArgumentNullException("iConnection");
		Parameters = iParameters;

		classifiers.Add(ID, this);
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing future profits.
	/// </summary>
	public static float[] CalculateFutureProfits(List<Candlestick> iCandlestickData, int iProfitTime)
	{
		var medianPrice = iCandlestickData.Select(x => x.MedianPrice).ToArray();
		return FinanceFunctions.CalculateFutureProfits(medianPrice, iProfitTime);
	}

	//**************************************************************************************

	/// <summary>
	/// Creates and returns array containing calculated paramters for a given period.
	/// </summary>
	public static List<float[]> CalculateParameters(List<ClassifierParameter> iDefinition, CandlestickCollection iCandlesticks, List<int> iTrainingPoints, Candlestick.Period iPeriod)
	{
		var results = new List<float[]>();
		var candlestickData = iCandlesticks[kTrainingPeriod];

		var index = new CandlestickIndexCollection();

		for (int j = 0; j < iTrainingPoints.Count; j++)
		{
			var i = iTrainingPoints[j];

			for (int k = (int)kTrainingPeriod; k >= 0; k--)
				index[k] = Math.Max(0, Math.Min(iCandlesticks[k].Count - 1, Candlestick.FindIndex(iCandlesticks[k], iCandlesticks[kTrainingPeriod][i].StartTime, index[k]) - 1));

			// Calculate parameters
			results.Add(ClassifierParameter.CalculateParameters(iDefinition, iCandlesticks, iPeriod, candlestickData[i].MedianPrice, index));
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Returns predictions for each classifier and price combination.
	/// </summary>
	public static SortedDictionary<int, float> PredictFP(List<WekaClassifier> iClassfiers, float iPrice, CandlestickCollection iCandlesticks, CandlestickIndexCollection iCandlestickIndex = null)
	{
		if (iClassfiers is null)
			throw new ArgumentNullException("iClassfiers");

		if (iCandlesticks is null)
			throw new ArgumentNullException("iCandlesticks");

		if (iPrice <= 0.0f)
			throw new ArgumentException("Price must be higher than zero.");

		var results = new SortedDictionary<int, float>();

		foreach (WekaClassifier c in iClassfiers)
		{
			var pred = c.PredictDFP(iCandlesticks, iPrice, iCandlestickIndex);
			results.Add(c.ID, pred);
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns weighted future profits.
	/// </summary>
	public static float FPToWFP(List<WekaClassifier> iClassifiers, SortedDictionary<int, float> iDFP)
	{
		// Compute normalized weights
		var weights = new float[iClassifiers.Count];

		for (int i = 0; i < iClassifiers.Count; i++)
			weights[i] = Math.Max(0, ((float)iClassifiers[i].GetPrecision() - 0.5f) * 2.0f);
		weights = Statistics.Normalize(weights);

		// Calculate weighted future profits
		float weightedFutureProfits = 0.0f;

		for (int k = 0; k < iClassifiers.Count; k++)
			weightedFutureProfits += weights[k] * iDFP[iClassifiers[k].ID];

		return weightedFutureProfits;
	}

	//**************************************************************************************

	/// <summary>
	/// Creates and returns a new list without given classifier.
	/// </summary>
	public static List<WekaClassifier> Remove(List<WekaClassifier> iClassifiers, int iClassifierID)
		=> iClassifiers.Where(x => x.ID != iClassifierID).ToList();

	//**************************************************************************************

	/// <summary>
	/// Returns average precision. 
	/// </summary>
	public static float GetAveragePrecision(List<WekaClassifier> iClassifiers) =>
		iClassifiers.Average(x => (float)x.GetPrecision());

	//**************************************************************************************

	/// <summary>
	/// Sort classifiers based on their precision with first element having lowest precision.
	/// </summary>
	public static List<WekaClassifier> Sort(List<WekaClassifier> iClassifiers, SortedDictionary<int, float> iPrecisions)
		=> iClassifiers.OrderBy(x => x.GetPrecision()).ToList();

	//**************************************************************************************

	/// <summary>
	/// Breaks down classifiers into groups.
	/// </summary>
	public static SortedDictionary<int, List<WekaClassifier>> SplitByProfitTime(List<WekaClassifier> iList)
		=> new SortedDictionary<int, List<WekaClassifier>>(iList.GroupBy(x => x.ProfitTime).ToDictionary(x => x.Key, x=> x.ToList()));

	//**************************************************************************************

	/// <summary>
	/// Loads from DB a list of enabled classifiers.
	/// </summary>
	public static List<int> LoadEnabledClassifiers()
	{
		var results = new List<int>();
		var sql = "Select Id from EnabledClassifiers";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			var command = new SqlCommand(sql, connection);

			using (var reader = command.ExecuteReader())
				while (reader.Read())
					results.Add(reader.GetInt32(0));
		}
		return results;
	}

	//**************************************************************************************


	/// <summary>
	/// Disables given classifier.
	/// </summary>
	public static void DisableClassifier(int iID)
	{
		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			using (SqlCommand command = new SqlCommand("DELETE FROM EnabledClassifiers WHERE ID = " + iID, connection))
				command.ExecuteNonQuery();
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Enables given classifier.
	/// </summary>
	public static void EnableClassifier(int iID)
	{
		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			using (SqlCommand command = new SqlCommand("Insert into EnabledClassifiers (Id) values (" + iID + ")", connection))
				command.ExecuteNonQuery();
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Finds all classifiers.
	/// </summary>
	public static List<WekaClassifier> Find(List<WekaClassifier> iClassifiers, List<int> iClassifierID)
		=> iClassifiers.Where(x => iClassifierID.Contains(x.ID)).ToList();

	//**************************************************************************************

	public static List<float> FullToTraining(List<float> iFullData, List<int> iTrainingPoints)
	{
		var results = new List<float>(iTrainingPoints.Count);
		for (int i = 0; i < iTrainingPoints.Count; i++)
			results.Add(iFullData[iTrainingPoints[i]]);

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads classifier parameters from a file.
	/// </summary>
	public static SortedList<int, List<ClassifierParameter>> LoadParameters(string iFilename)
	{
		var results = new SortedList<int, List<ClassifierParameter>>();

		using (StreamReader reader = new StreamReader(iFilename))
			while (!reader.EndOfStream)
			{
				string line = reader.ReadLine();
				{
					string[] sline = line.Split(';');
					int id = int.Parse(sline[0]);

					if (reader.ReadLine() != "{")
						throw new Exception("Could not parse provided file: " + iFilename + " Line:" + line);

					var classifierParameters = new List<string>();

					while (true)
					{
						string parameter = reader.ReadLine();
						if (parameter == "}")
						{
							results.Add(id, ClassifierParameter.CreateParameters(classifierParameters));
							break;
						}
						else
							classifierParameters.Add(parameter);
					}
				}
			}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads training points from a file, or creates from a full set.
	/// </summary>
	public static List<int> LoadTrainingPoints(CandlestickCollection iCandlesticks, int iClassifierID, int iProfitTime)
	{
		var filename = "Data/TrainingPoints/Classifier_" + iClassifierID.ToString() + ".dat";
		if (File.Exists(filename))
			using (BinaryReader reader = new BinaryReader(File.OpenRead(filename)))
			{
				int count = reader.ReadInt32();
				var trainingPoints = new List<int>(count);

				for (int i = 0; i < count; i++)
					trainingPoints.Add(reader.ReadInt32());

				return trainingPoints;
			}
		else
		{
			var trainingPoints = new List<int>();
			for (int i = 300 * Candlestick.PeriodToDaily(kTrainingPeriod); i < iCandlesticks[kTrainingPeriod].Count - iProfitTime; i++)
				trainingPoints.Add(i);
			return trainingPoints;
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Saves training points to a file.
	/// </summary>
	public static void SaveTrainingPoints(List<int> iTrainingPoints, string iFilename)
	{
		// Save training points to a file
		if (!Directory.Exists("Data/TrainingPoints"))
			Directory.CreateDirectory("Data/TrainingPoints");

		using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(iFilename)))
		{
			writer.Write(iTrainingPoints.Count);
			for (int i = 0; i < iTrainingPoints.Count; i++)
				writer.Write(iTrainingPoints[i]);
		}
	}
	
	//**************************************************************************************

	/// <summary>
	/// Iteratevily predicts DFP.
	/// </summary>
	public float PredictDFP(CandlestickCollection iCandlesticks, float iCurrentPrice, CandlestickIndexCollection iCandlestickIndex = null) =>
		PredictDFP(ClassifierParameter.CalculateParameters(Parameters, iCandlesticks, Period, iCurrentPrice, iCandlestickIndex));

	//**************************************************************************************

	/// <summary>
	/// Iteratevily predicts DFP.
	/// </summary>
	public abstract float PredictDFP(float[] iParameters);	
	
	//**************************************************************************************

	/// <summary>
	/// Deletes model.
	/// </summary>
	/// <param name="iClassifierID"></param>
	protected static void DeleteModel(int iClassifierID)
	{
		string filename = "Data/Classifier_" + iClassifierID + ".model";

		if (File.Exists(filename))
			File.Delete(filename);
	}

	//**************************************************************************************

	/// <summary>
	/// Deletes training points.
	/// </summary>
	protected static void DeleteTrainingPoints(int iClassifierID)
	{
		string filename = "Data/TrainingPoints/Classifier_" + iClassifierID.ToString() + ".dat";

		if (File.Exists(filename))
			File.Delete(filename);
	}

	//**************************************************************************************

	/// <summary>
	/// Fills in instances object with instance data.
	/// </summary>
	protected static void FillInstances(weka.core.Instances iInstances, string[] iDecisions, List<float[]> iParameters)
	{
		for (int i = 0; i < iParameters.Count; i++)
		{
			// Skip null parameters
			if (iParameters[i] is null)
				continue;

			// Create instance object
			weka.core.Instance instance = new weka.core.DenseInstance(iInstances.numAttributes());
			instance.setDataset(iInstances);

			// Fill in instance object with parameters
			for (int k = 0; k < iParameters[i].Length; k++)
				instance.setValue(k, iParameters[i][k]);

			// Set decision value
			instance.setValue(iParameters[i].Length, iDecisions[i]);

			// Add instance to the list
			iInstances.add(instance);
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Fills in instances object with instance data.
	/// </summary>
	protected static void FillInstances(weka.core.Instances iInstances, float[] iDecisions, List<float[]> iParameters)
	{
		for (int i = 0; i < iParameters.Count; i++)
		{
			// Skip null parameters
			if (iParameters[i] is null)
				continue;

			// Create instance object
			weka.core.Instance instance = new weka.core.DenseInstance(iInstances.numAttributes());
			instance.setDataset(iInstances);

			// Fill in instance object with parameters
			for (int k = 0; k < iParameters[i].Length; k++)
				instance.setValue(k, iParameters[i][k]);

			// Set decision value
			instance.setValue(iParameters[i].Length, iDecisions[i]);

			// Add instance to the list
			iInstances.add(instance);
		}
	}
		
	//**************************************************************************************

	public void WaitTillModelReady()
	{
		lock(this)
		{
			if(!IsModelLoaded())
				LoadModel();
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Creates and returns weka attibutes list based on given parameters.
	/// </summary>
	private java.util.ArrayList CreateAttributes()
	{
		java.util.ArrayList attributes = new java.util.ArrayList();

		// Create parameter attributes
		for (int i = 0; i < Parameters.Count; i++)
			attributes.add(new weka.core.Attribute("Parameter" + i.ToString()));

		attributes.add(GetDependentAttribute());
	
		return attributes;
	}

	//**************************************************************************************

	public static weka.classifiers.functions.LinearRegression CreateLinearRegressionModel(CandlestickCollection iCandlesticks, int iProfitTime, List<int> iTrainingPoints, List<ClassifierParameter> iParameters, Candlestick.Period iPeriod, bool iEliminateCollinearAttributes, bool iMinimal, out float oCoefOfDetermination, out float oStdDev)
	{
		var futureProfits = FullToTraining(new List<float>(CalculateFutureProfits(iCandlesticks[kTrainingPeriod], iProfitTime)), iTrainingPoints).ToArray();

		java.util.ArrayList attr = new java.util.ArrayList();

		for (int i = 0; i < iParameters.Count; i++)
			attr.add(new weka.core.Attribute("Parameter" + i.ToString()));

		attr.add(new weka.core.Attribute("Profit"));

		int dataSize = iTrainingPoints.Count;

		// Create instance object
		var instances = new weka.core.Instances("Data", attr, dataSize);

		// Set class index
		instances.setClassIndex(instances.numAttributes() - 1);

		var par = CalculateParameters(iParameters, iCandlesticks, iTrainingPoints, iPeriod);
		// Fill instances
		FillInstances(instances, futureProfits, par);

		var model = new weka.classifiers.functions.LinearRegression();
		model.setEliminateColinearAttributes(iEliminateCollinearAttributes);
		model.setMinimal(iMinimal);
		model.buildClassifier(instances);

		var pred = new List<float>();

		for (int j = 0; j < instances.numInstances(); j++)
			pred.Add((float)model.classifyInstance(instances.instance(j)));

		var correl = Statistics.CalculateCorrelation(futureProfits, pred.ToArray());

		oCoefOfDetermination = (float)Math.Pow(correl, 2.0f);
		oStdDev = Statistics.StandardDeviation(futureProfits, pred.ToArray());

		return model;
	}

	//**************************************************************************************

	public List<WekaClassifier> GetLinkedClassifiers()
	{
		var ids = Parameters.Where(x => x.Type == ClassifierParameter.ParameterType.Classifier || x.Type == ClassifierParameter.ParameterType.ClassifierTargetChangeOldest)
		.Select(y => y.Attributes[0]).ToList();

		return ids.Select(x => Find((int)x)).ToList();
	}

	//**************************************************************************************
}