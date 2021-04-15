using System;
using System.Collections.Generic;
using System.Data.SqlClient;

class WekaJ48Info
{
	public readonly int ID;
	public float? ProfitAverage;
	public float? ProfitStdDev;
	public float? Precision;
	public int? ParentID;
	public int?[] ChildrenID;
	public bool? ReproductionComplete;
	public bool? IsSingular;
	public float?[] PredictionProfits;

	//**************************************************************************************

	public WekaJ48Info(int iID)
	{
		ID = iID;
		var predictionCount = Enum.GetNames(typeof(WekaJ48.Prediction)).Length;

		ChildrenID = new int?[predictionCount];
		PredictionProfits = new float?[predictionCount];
	}

	//**************************************************************************************

	/// <summary>
	/// Loads prediction profits from DB.
	/// </summary>
	public static List<WekaJ48Info> LoadFromDB()
	{
		var results = new List<WekaJ48Info>();

		var sql = "Select * from J48Info";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();

			var command = new SqlCommand(sql, connection);
			using (var reader = command.ExecuteReader())
				while (reader.Read())
				{					
					var info = new WekaJ48Info(reader.GetInt32(reader.GetOrdinal("Id")));

					if (!reader.IsDBNull(reader.GetOrdinal("ProfitAverage")))
						info.ProfitAverage = (float)reader.GetDouble(reader.GetOrdinal("ProfitAverage"));

					if (!reader.IsDBNull(reader.GetOrdinal("ProfitStdDev")))
						info.ProfitStdDev = (float)reader.GetDouble(reader.GetOrdinal("ProfitStdDev"));

					if (!reader.IsDBNull(reader.GetOrdinal("Precision")))
						info.Precision = (float)reader.GetDouble(reader.GetOrdinal("Precision"));

					if (!reader.IsDBNull(reader.GetOrdinal("ParentID")))
						info.ParentID = reader.GetInt32(reader.GetOrdinal("ParentID"));

					foreach (WekaJ48.Prediction p in (WekaJ48.Prediction[])Enum.GetValues(typeof(WekaJ48.Prediction)))
						if (!reader.IsDBNull(reader.GetOrdinal("Child" + p.ToString() + "ID")))
							info.ChildrenID[(int)p] = reader.GetInt32(reader.GetOrdinal("Child" + p.ToString() + "ID"));

					if (!reader.IsDBNull(reader.GetOrdinal("ReproductionComplete")))
						info.ReproductionComplete = reader.GetBoolean(reader.GetOrdinal("ReproductionComplete"));

					if (!reader.IsDBNull(reader.GetOrdinal("IsSingular")))
						info.IsSingular = reader.GetBoolean(reader.GetOrdinal("IsSingular"));

					foreach (WekaJ48.Prediction p in (WekaJ48.Prediction[])Enum.GetValues(typeof(WekaJ48.Prediction)))
						if (!reader.IsDBNull(reader.GetOrdinal("PP" + p.ToString())))
							info.PredictionProfits[(int)p] = (float)reader.GetDouble(reader.GetOrdinal("PP" + p.ToString()));

					results.Add(info);
				}
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Inserts prediction profits info to DB
	/// </summary>
	public static void UpdateDB(WekaJ48Info iInfo)
	{
		var sql = "IF EXISTS(SELECT * FROM J48Info WHERE Id = @ID) " +
			"Update J48Info SET " +
			"ProfitAverage = @ProfitAverage, ProfitStdDev = @ProfitStdDev, Precision = @Precision, ParentID=@ParentID, ChildWeakBuyID=@ChildWeakBuyID, ChildBuyID=@ChildBuyID, ChildStrongBuyID=@ChildStrongBuyID, ChildWeakSellID=@ChildWeakSellID, ChildSellID=@ChildSellID, ChildStrongSellID=@ChildStrongSellID, ReproductionComplete=@ReproductionComplete, IsSingular=@IsSingular, PPWeakBuy=@PPWeakBuy, PPBuy=@PPBuy, PPStrongBuy=@PPStrongBuy, PPWeakSell=@PPWeakSell, PPSell=@PPSell, PPStrongSell=@PPStrongSell " +
			" Where ID = @ID " +
			"ELSE" +
			" INSERT INTO J48Info (Id, ProfitAverage, ProfitStdDev, Precision, ParentID, ChildWeakBuyID, ChildBuyID, ChildStrongBuyID, ChildWeakSellID, ChildSellID, ChildStrongSellID, ReproductionComplete, IsSingular, PPWeakBuy, PPBuy, PPStrongBuy, PPWeakSell, PPSell, PPStrongSell) VALUES(@ID, @ProfitAverage, @ProfitStdDev, @Precision, @ParentID, @ChildWeakBuyID, @ChildBuyID, @ChildStrongBuyID, @ChildWeakSellID, @ChildSellID, @ChildStrongSellID, @ReproductionComplete, @IsSingular, @PPWeakBuy, @PPBuy, @PPStrongBuy, @PPWeakSell, @PPSell, @PPStrongSell)";


		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();

			var cmd = new SqlCommand(sql, connection);
			cmd.Parameters.AddWithValue("@Id", iInfo.ID);
			cmd.Parameters.AddWithValue("@ProfitAverage", (object)iInfo.ProfitAverage ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@ProfitStdDev", (object)iInfo.ProfitStdDev ?? DBNull.Value);

			cmd.Parameters.AddWithValue("@Precision", (object)iInfo.Precision ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@ParentID", (object)iInfo.ParentID ?? DBNull.Value);

			foreach (WekaJ48.Prediction p in (WekaJ48.Prediction[])Enum.GetValues(typeof(WekaJ48.Prediction)))
				cmd.Parameters.AddWithValue("@Child" + p.ToString() + "ID", (object)iInfo.ChildrenID[(int)p] ?? DBNull.Value);

			cmd.Parameters.AddWithValue("@ReproductionComplete", (object)iInfo.ReproductionComplete ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@IsSingular", (object)iInfo.IsSingular ?? DBNull.Value);

			foreach (WekaJ48.Prediction p in (WekaJ48.Prediction[])Enum.GetValues(typeof(WekaJ48.Prediction)))
				cmd.Parameters.AddWithValue("@PP" + p.ToString(), (object)iInfo.PredictionProfits[(int)p] ?? DBNull.Value);

			var dataAdapter = new SqlDataAdapter
			{
				InsertCommand = cmd
			};
			dataAdapter.InsertCommand.ExecuteNonQuery();
			dataAdapter.InsertCommand.Dispose();
		}
	}


	//**************************************************************************************

	/// <summary>
	/// Removes prediction profits info from DB.
	/// </summary>
	public static void RemoveFromDB(int iClassifierID)
	{
		var sql = "DELETE FROM J48Info WHERE Id= '" + iClassifierID + "'";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			using (SqlCommand command = new SqlCommand(sql, connection))
				command.ExecuteNonQuery();
		}
	}

	//**************************************************************************************

	/// <summary>
	///  Resets classifier parameters.
	/// </summary>
	public void OnClassifierRebuild()
	{
		ProfitAverage = null;
		ProfitStdDev = null;
		Precision = null;
		for (int i = 0; i < ChildrenID.Length; i++)
			ChildrenID[i] = null;
		ReproductionComplete = null;
		IsSingular = null;
		for (int i = 0; i < PredictionProfits.Length; i++)
			PredictionProfits[i] = null;
	}

	//**************************************************************************************
}
