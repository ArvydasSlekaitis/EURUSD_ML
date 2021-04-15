using System;
using System.Collections.Generic;
using System.IO;

class WekaLinearRegression : WekaClassifier
{
	public weka.classifiers.functions.LinearRegression Model { get; private set; }

	public override bool IsModelLoaded() => Model != null;
	public override void UnloadModel() => Model = null;

	public override float GetProfitsStdDev() => (float)LRInfo.ProfitStdDev;
	public override float? GetPrecision() => (float)LRInfo.CoefOfDetermination * 100.0f;

	public readonly WekaLinearRegressionInfo LRInfo;

	double[] coefficients = null;

	//**************************************************************************************

	public WekaLinearRegression(WekaInfo iInfo, List<ClassifierParameter> iParameters, WekaLinearRegressionInfo iLRInfo)
		: base(iInfo, iParameters)
	{
		LRInfo = iLRInfo ?? throw new ArgumentNullException("iLRInfo");
	}

	//**************************************************************************************

	/// <summary>
	/// Iteratevily predicts DFP.
	/// </summary>
	public override float PredictDFP(float[] iParameters)
	{
		if (iParameters is null)
			throw new ArgumentNullException(nameof(iParameters));

		WaitTillModelReady();

		var sum = 0.0;
		for (int i = 0; i < iParameters.Length; i++)
			sum += coefficients[i] * iParameters[i];
		sum += coefficients[coefficients.Length - 1];

		return (float)sum;
	}

	//**************************************************************************************

	protected override weka.core.Attribute GetDependentAttribute() => new weka.core.Attribute("PL");

	//**************************************************************************************

	/// <summary>
	///	Build cllasifier model and save it to a file.
	/// </summary>
	public override void Build(CandlestickCollection iCandlestick)
	{
		List<int> trainingPoints = null;

		// Build model
		if (!File.Exists(ModelFilename))
		{
			OutputMessage("Building model");
			trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

			Model = new weka.classifiers.functions.LinearRegression();
			Model.setEliminateColinearAttributes(LRInfo.EliminateColinearAttributes);
			Model.setMinimal(LRInfo.Minimal);
			Model.buildClassifier(CreateInstances(iCandlestick, trainingPoints, Attributes, Parameters, Period, ProfitTime));
			weka.core.SerializationHelper.write(ModelFilename, Model);
			coefficients = Model.coefficients();
		}

		// Calculate coeficient of determination and profits std dev
		if(LRInfo.CoefOfDetermination == null || LRInfo.ProfitStdDev == null)
		{
			WaitTillModelReady();
			OutputMessage("Calculating coeficient of determination and profits std dev");

			if (trainingPoints is null)
				trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

			var par = CalculateParameters(Parameters, iCandlestick, trainingPoints, Period);
			var futureProfits = FullToTraining(new List<float>(CalculateFutureProfits(iCandlestick[kTrainingPeriod], ProfitTime)), trainingPoints).ToArray();
			var pred = new List<float>();

			for (int j = 0; j < par.Count; j++)
			{
				var fp = PredictDFP(par[j]);

				var targetPriceChange = (float)Math.Exp(fp);
				pred.Add(targetPriceChange - 1.0f);
			}

			LRInfo.CoefOfDetermination = (float)Math.Pow(Statistics.CalculateCorrelation(futureProfits, pred.ToArray()), 2.0f);
			LRInfo.ProfitStdDev = Statistics.StandardDeviation(futureProfits, pred.ToArray());
			WekaLinearRegressionInfo.UpdateDB(LRInfo);
		}
	}

	//**************************************************************************************

	protected override void LoadModel()
	{
		lock (this)
		{
			if (Model is null)
			{
				Model = (weka.classifiers.functions.LinearRegression)weka.core.SerializationHelper.read(ModelFilename);
				coefficients = Model.coefficients();
			}
		}		
	}

	//**************************************************************************************

	public override void RemoveClassifier()
	{
		if (!Exist(ID))
			return;

		Console.WriteLine("Removing classifier: " + ID);
			
		// Remove model
		DeleteModel(ID);

		// Remove training data
		DeleteTrainingPoints(ID);

		WekaInfo.RemoveFromDB(ID);
		WekaLinearRegressionInfo.RemoveFromDB(ID);

		// Remove target classifier from static list		
		classifiers.Remove(ID);
	}

	//**************************************************************************************

	public override void RebuildClassifier()
	{
		OutputMessage("Rebuilding classifier");

		// Remove model
		DeleteModel(ID);

		// Remove training data
		DeleteTrainingPoints(ID);

		// Update info
		LRInfo.OnClassifierRebuild();
		WekaLinearRegressionInfo.UpdateDB(LRInfo);
	}

	//**************************************************************************************

	/// <summary>
	/// Creates and fills in weka instances object.
	/// </summary>
	protected static weka.core.Instances CreateInstances(CandlestickCollection iCandlesticks, List<int> iTrainingPoints, java.util.ArrayList iAttributes, List<ClassifierParameter> iParameters, Candlestick.Period iPeriod, int iMaxProfitTime)
	{
		int dataSize = iTrainingPoints.Count;

		// Create instance object
		var instances = new weka.core.Instances("Data", iAttributes, dataSize);

		// Set class index
		instances.setClassIndex(instances.numAttributes() - 1);

		// Fill instances
		FillInstances(instances, CalculateDecisions(iCandlesticks[kTrainingPeriod], iTrainingPoints, iMaxProfitTime), CalculateParameters(iParameters, iCandlesticks, iTrainingPoints, iPeriod));

		return instances;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing profits
	/// </summary>
	private static float[] CalculateDecisions(List<Candlestick> iCandlestickData, List<int> iTrainingPoints, int iProfitTime)
	=> FullToTraining(new List<float>(CalculateFutureProfits(iCandlestickData, iProfitTime)), iTrainingPoints).ToArray();

	//**************************************************************************************

	public static WekaLinearRegression CreateNew(int iParamtersID, List<ClassifierParameter> iParameters, Candlestick.Period iPeriod, int iProfitTime, List<int> iTrainingPoints, bool iEliminateColinearAttributes, bool iMinimal)
	{
		// Create WekaInfo and retrive ID
		var info = new WekaInfo(null, typeof(WekaJ48), iParamtersID, iPeriod, iProfitTime);
		WekaInfo.InsertToDB(info);

		if (info.ID is null)
			throw new Exception("Could not deduct ID");

		// Create LinearRegressionInfo
		var lrInfo = new WekaLinearRegressionInfo((int)info.ID, iEliminateColinearAttributes, iMinimal);
		WekaLinearRegressionInfo.UpdateDB(lrInfo);

		// Save training points
		if (iTrainingPoints != null)
			SaveTrainingPoints(iTrainingPoints, "Data/TrainingPoints/Classifier_" + info.ID.ToString() + ".dat");

		// Create classifier
		return new WekaLinearRegression(info, iParameters, lrInfo);
	}

	//**************************************************************************************


}

