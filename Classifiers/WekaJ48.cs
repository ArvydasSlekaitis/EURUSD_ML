using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class WekaJ48 : WekaClassifier
{
	public enum Prediction { StrongSell, Sell, WeakSell, WeakBuy, Buy, StrongBuy };
	public weka.classifiers.Classifier Model { get; private set; }

	public readonly WekaJ48Info J48Info;
	public readonly WekaJ48[] childs = new WekaJ48[6];

	public override float GetProfitsStdDev() => (float)J48Info.ProfitStdDev;
	public override float? GetPrecision() => J48Info.Precision;

	public override bool IsModelLoaded() => Model != null;
	public override void UnloadModel() => Model = null;

	//**************************************************************************************

	public WekaJ48(WekaInfo iInfo, List<ClassifierParameter> iParameters, WekaJ48Info iJ48Info)
		: base(iInfo, iParameters)
	{
		J48Info = iJ48Info ?? throw new ArgumentNullException("iJ48Info");
	}

	//**************************************************************************************

	/// <summary>
	/// Returns classifier prediction based on provided parameters.
	/// </summary>
	/// <param name="iParameters"></param>
	public Prediction Predict(float[] iParameters)
	{
		if (iParameters is null)
			throw new ArgumentNullException(nameof(iParameters));

		WaitTillModelReady();

		// Create instances object
		var instances = new weka.core.Instances("Test", Attributes, 1);
		instances.setClassIndex(instances.numAttributes() - 1);

		// Create single instance
		var instance = new weka.core.DenseInstance(instances.numAttributes() - 1);
		instance.setDataset(instances);

		// Fill instance with data
		for (int i = 0; i < iParameters.Length; i++)
			instance.setValue(i, iParameters[i]);

		// Get prediction
		double prediction = Model.classifyInstance(instance);

		// Convert prediction to decision
		return (Prediction)Enum.Parse(typeof(Prediction), instances.classAttribute().value((int)prediction));
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns prediction using provided candlesticks and their last values as current time.
	/// </summary>
	/// <param name="iCurrentPrice">Last known price.</param>
	public Prediction Predict(CandlestickCollection iCandlesticks, float iCurrentPrice, CandlestickIndexCollection iCandlestickIndex = null)
	{
		WaitTillModelReady();

		return Predict(ClassifierParameter.CalculateParameters(Parameters, iCandlesticks, Period, iCurrentPrice, iCandlestickIndex));
	}

	//**************************************************************************************

	/// <summary>
	///	Build cllasifier model and save it to a file.
	/// </summary>
	public override void Build(CandlestickCollection iCandlestick)
	{
		List<int> trainingPoints = null; 

		// Calculate average profit and std dev
		if(J48Info.ProfitAverage is null || J48Info.ProfitStdDev is null)
		{
			trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);
			float[] profits = FullToTraining(new List<float>(CalculateFutureProfits(iCandlestick[kTrainingPeriod], ProfitTime)), trainingPoints).ToArray();
			J48Info.ProfitStdDev = Statistics.StandardDeviation(profits);
			J48Info.ProfitAverage = J48Info.ParentID is null ? 0.0f : Statistics.ArithmeticMean(profits);
			WekaJ48Info.UpdateDB(J48Info);
		}

		// Build model
		if (!File.Exists(ModelFilename))
		{
			OutputMessage("Building model");

			if(trainingPoints is null)
				trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

			Model = new weka.classifiers.trees.J48();
			Model.buildClassifier(CreateInstances(iCandlestick, trainingPoints, Attributes, Parameters, Period, ProfitTime));
			weka.core.SerializationHelper.write(ModelFilename, Model);
		}

		// Perfrom crossfold test
		if(J48Info.Precision is null)
		{
			if (Model is null)
				LoadModel();

			OutputMessage("Perfroming crossfold");

			if (trainingPoints is null)
				trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

			var instances = CreateInstances(iCandlestick, trainingPoints, Attributes, Parameters, Period, ProfitTime);
			var evaluation = new weka.classifiers.Evaluation(instances);
			evaluation.crossValidateModel(Model, instances, 10, new java.util.Random(0));
			
			J48Info.Precision = (float)evaluation.pctCorrect();

			WekaJ48Info.UpdateDB(J48Info);
		}

		// Perfrom singular test
		if(J48Info.IsSingular == null)
		{
			if (Model is null)
				LoadModel();

			OutputMessage("Perfroming singular test");

			var results = new SortedList<Prediction, List<int>>();
			foreach (Prediction p in (Prediction[])Enum.GetValues(typeof(Prediction)))
				results.Add(p, new List<int>());

			if (trainingPoints is null)
				trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

			var parameters = CalculateParameters(Parameters, iCandlestick, trainingPoints, Period);

			for (int k = 0; k < parameters.Count; k++)
			{
				var pred = Predict(parameters[k]);
				results[pred].Add(trainingPoints[k]);
			}

			J48Info.IsSingular = results.Count(x => x.Value.Count > 0) <= 1;

			WekaJ48Info.UpdateDB(J48Info);
		}
		
		// Calculating prediction profits
		if (J48Info.PredictionProfits.Count(x => x != null) == 0)
		{
			if (Model is null)
				LoadModel();

			OutputMessage("Calculating prediction profits");

			if (trainingPoints is null)
				trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

			var predictionPoints = GetHistoricalPredictionPoints(iCandlestick, trainingPoints);

			foreach (Prediction p in (Prediction[])Enum.GetValues(typeof(Prediction)))
			{
				float[] profits = FullToTraining(new List<float>(CalculateFutureProfits(iCandlestick[kTrainingPeriod], ProfitTime)), predictionPoints[p]).ToArray();

				if (profits.Length < 10)
					J48Info.PredictionProfits[(int)p] = DecisionToFutureProfit(p, (float)J48Info.ProfitStdDev, (float)J48Info.ProfitAverage);
				else
					J48Info.PredictionProfits[(int)p] = Statistics.ArithmeticMean(profits);
			}

			WekaJ48Info.UpdateDB(J48Info);
		}
		
		// Create children
		if(!J48Info.ReproductionComplete.GetValueOrDefault(false))
		{
			lock (this)
			{
				if (J48Info.Precision > 50.0f && !J48Info.IsSingular.GetValueOrDefault(false))
				{
					OutputMessage("Creating children");

					if (trainingPoints is null)
						trainingPoints = LoadTrainingPoints(iCandlestick, ID, ProfitTime);

					var predictionPoints = GetHistoricalPredictionPoints(iCandlestick, trainingPoints);

					foreach (Prediction p in (Prediction[])Enum.GetValues(typeof(Prediction)))
						if (predictionPoints[p] != null && predictionPoints[p].Count >= 1000 && J48Info.ChildrenID[(int)p] == null)
						{
							var child = CreateNew(ParametersID, Parameters, Period, ProfitTime, predictionPoints[p]);

							// Set parent
							child.J48Info.ParentID = ID;
							WekaJ48Info.UpdateDB(child.J48Info);

							// Update parent info
							J48Info.ChildrenID[(int)p] = (int)child.ID;
							WekaJ48Info.UpdateDB(J48Info);										
							childs[(int)p] = child;
						}
				}

				J48Info.ReproductionComplete = true;
				WekaJ48Info.UpdateDB(J48Info);
			}
		}
	}

	//**************************************************************************************

	protected override void LoadModel()
	{
		lock (this)
		{
			if(Model is null)
				Model = (weka.classifiers.Classifier)weka.core.SerializationHelper.read(ModelFilename);
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Retrieves data points for each prediction.
	/// </summary>
	private SortedList<Prediction, List<int>> GetHistoricalPredictionPoints(CandlestickCollection iCandlestick, List<int> iTrainingPoints)
	{
		var results = new SortedList<Prediction, List<int>>();

		foreach (var p in (Prediction[])Enum.GetValues(typeof(Prediction)))
			results.Add(p, new List<int>());

		WaitTillModelReady();

		// Calculate parameters
		var parameters = CalculateParameters(Parameters, iCandlestick, iTrainingPoints, Period);

		// Predictions
		for (int k = 0; k < parameters.Count; k++)
		{
			if (parameters[k] is null)
				throw new SystemException("Parameter cannot be empty");

			var pred = Predict(parameters[k]);
			results[pred].Add(iTrainingPoints[k]);
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Iteratevily predicts DFP.
	/// </summary>
	public override float PredictDFP(float[] iParameters)
	{
		WaitTillModelReady();

		var prediction = Predict(iParameters);

		if (J48Info.ChildrenID[(int)prediction] != null && childs[(int)prediction] == null)
			childs[(int)prediction] = Find((int)J48Info.ChildrenID[(int)prediction]) as WekaJ48;

		if (childs[(int)prediction] !=null && childs[(int)prediction].J48Info.Precision > 50.0f)
			return childs[(int)prediction].PredictDFP(iParameters);
		else
		{
			if (J48Info.PredictionProfits[(int)prediction] is null)
				return DecisionToFutureProfit(prediction, (float)J48Info.ProfitStdDev, (float)J48Info.ProfitAverage);
			else
				return (float)J48Info.PredictionProfits[(int)prediction];
		}
	}

	//**************************************************************************************

	protected override weka.core.Attribute GetDependentAttribute()
	{
		java.util.List PL_values = new java.util.ArrayList();
		PL_values.add("WeakSell");
		PL_values.add("WeakBuy");
		PL_values.add("Buy");
		PL_values.add("Sell");
		PL_values.add("StrongBuy");
		PL_values.add("StrongSell");
		return new weka.core.Attribute("PL", PL_values);
	}

	//**************************************************************************************

	/// <summary>
	/// Returns decision based on given profit/loss and overall profits standard deviation. (WeakBuy, Buy, StrongBuy, WeakSell, Sell, StrongSell).
	/// </summary>
	public static string FutureProfitToDecision(float iProfitLoss, float iProfitStdDev, float iProfitAverage)
	{
		float kBuySellTreshold = iProfitStdDev;
		float kStronBuySellTreshold = iProfitStdDev * 2.0f;

		if (iProfitLoss <= iProfitAverage - kStronBuySellTreshold)
			return "StrongSell";

		if (iProfitLoss >= iProfitAverage + kStronBuySellTreshold)
			return "StrongBuy";

		if (iProfitLoss <= iProfitAverage - kBuySellTreshold)
			return "Sell";

		if (iProfitLoss >= iProfitAverage + kBuySellTreshold)
			return "Buy";

		if (iProfitLoss >= iProfitAverage)
			return "WeakBuy";

		if (iProfitLoss < iProfitAverage)
			return "WeakSell";

		throw new ArgumentException("Could not select decision.");
	}

	//**************************************************************************************

	/// <summary>
	/// Returns average future profit based on a given decision.
	/// </summary>
	public static float DecisionToFutureProfit(Prediction iPrediction, float iProfitStdDev, float iProfitAverage)
	{
		switch (iPrediction)
		{
			// Average of range
			case Prediction.WeakBuy:
				return iProfitAverage + iProfitStdDev * 0.5f;

			case Prediction.WeakSell:
				return iProfitAverage - iProfitStdDev * 0.5f;

			case Prediction.Buy:
				return iProfitAverage + iProfitStdDev * 1.5f;

			case Prediction.Sell:
				return iProfitAverage - iProfitStdDev * 1.5f;

			case Prediction.StrongBuy:
				return iProfitAverage + iProfitStdDev * 2.5f;

			case Prediction.StrongSell:
				return iProfitAverage - iProfitStdDev * 2.5f;
		}

		throw new ArgumentException("Could not select decision.");
	}

	//**************************************************************************************

	public override void RemoveClassifier()
	{
		if (!Exist(ID))
			return;

		Console.WriteLine("Removing classifier: " + ID);

		// Start removal from children
		foreach (var i in J48Info.ChildrenID)
			if (i != null)
			{
				var c = Find((int)i);
				if(c!=null)
					c.RemoveClassifier();
			}
		// Remove model
		DeleteModel(ID);

		// Remove training data
		DeleteTrainingPoints(ID);

		// Remove info from parents
		if (J48Info.ParentID != null)
		{
			if(Find((int)J48Info.ParentID) is WekaJ48 parent)
				for (int i = 0; i < parent.J48Info.ChildrenID.Length; i++)
					if (parent.J48Info.ChildrenID[i] == ID)
					{
						parent.J48Info.ChildrenID[i] = null;
						WekaJ48Info.UpdateDB(parent.J48Info);
					}
		}
		
		// Remove info
		WekaJ48Info.RemoveFromDB(ID);
		WekaInfo.RemoveFromDB(ID);

		// Remove target classifier from static list		
		classifiers.Remove(ID);
	}

	//**************************************************************************************

	public override void RebuildClassifier()
	{
		Console.WriteLine("Rebuilding classifier: " + ID);

		// Start removal from children
		foreach (var i in J48Info.ChildrenID)
			if (i != null)
			{
				var c = Find((int)i);
				if (c != null)
					c.RemoveClassifier();
			}

		// Update info
		J48Info.OnClassifierRebuild();
		WekaJ48Info.UpdateDB(J48Info);

		// Remove model
		DeleteModel(ID);
				
		// Remove training data
		DeleteTrainingPoints(ID);
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing buy/sell decisions (WeakBuy, Buy, StrongBuy, WeakSell, Sell, StrongSell).
	/// </summary>
	private static string[] CalculateDecisions(List<Candlestick> iCandlestickData, List<int> iTrainingPoints, int iProfitTime)
	{
		var decisions = new string[iTrainingPoints.Count];

		float[] profits = FullToTraining(new List<float>(CalculateFutureProfits(iCandlestickData, iProfitTime)), iTrainingPoints).ToArray();
		float profitsStdDev = Statistics.StandardDeviation(profits);
		float profitsAverage = Statistics.ArithmeticMean(profits);

		for (int i = 0; i < iTrainingPoints.Count; i++)
			decisions[i] = FutureProfitToDecision(profits[i], profitsStdDev, profitsAverage);

		return decisions;
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

	public static WekaJ48 CreateNew(int iParamtersID, List<ClassifierParameter> iParameters, Candlestick.Period iPeriod, int iProfitTime, List<int> iTrainingPoints)
	{
		// Create WekaInfo and retrive ID
		var info = new WekaInfo(null, typeof(WekaJ48), iParamtersID, iPeriod, iProfitTime);
		WekaInfo.InsertToDB(info);

		if (info.ID is null)
			throw new Exception("Could not deduct ID");

		// Create J48Info
		var j48Info = new WekaJ48Info((int)info.ID);
		WekaJ48Info.UpdateDB(j48Info);

		// Save training points
		if(iTrainingPoints != null)
			SaveTrainingPoints(iTrainingPoints, "Data/TrainingPoints/Classifier_" + info.ID.ToString() + ".dat");

		// Create classifier
		return new WekaJ48(info, iParameters, j48Info);
	}

	//**************************************************************************************

	public void OutputRSQ(CandlestickCollection iCandlesticks, string oFilename)
	{
		WaitTillModelReady();

		var trainingPoints = LoadTrainingPoints(iCandlesticks, -1, ProfitTime);

		var par = CalculateParameters(Parameters, iCandlesticks, trainingPoints, Period);
		var futureProfits = FullToTraining(new List<float>(CalculateFutureProfits(iCandlesticks[kTrainingPeriod], ProfitTime)), trainingPoints).ToArray();
		var pred = new List<float>();

		for (int j = 0; j < par.Count; j++)
		{
			var fp = PredictDFP(par[j]);

			var targetPriceChange = (float)Math.Exp(fp);
			pred.Add(targetPriceChange - 1.0f);
		}

		var CoefOfDetermination = (float)Math.Pow(Statistics.CalculateCorrelation(futureProfits, pred.ToArray()), 2.0f);
		var ProfitStdDev = Statistics.StandardDeviation(futureProfits, pred.ToArray());
		Console.WriteLine("Classifier " + ID);
		Console.WriteLine(CoefOfDetermination);
		Console.WriteLine(ProfitStdDev);

		using (StreamWriter writer = new StreamWriter(File.OpenWrite(oFilename)))
		{
			writer.WriteLine("DateTime;RSQ;");

			for (int j = 0; j < trainingPoints.Count; j++)
			{
				var time = Utils.UnixToDateTime(iCandlesticks[kTrainingPeriod][trainingPoints[j]].EndTime).ToString();
				writer.WriteLine(time + ";" + Math.Pow(futureProfits[j] - pred[j], 2.0) +";");
			}
		}		
	}

	//**************************************************************************************

}

