using System;
using System.Collections.Generic;
using System.Data.SqlClient;

class WekaLinearRegressionInfo
{
	public readonly int ID;
	public readonly bool EliminateColinearAttributes;
	public readonly bool Minimal;
	public float? CoefOfDetermination;
	public float? ProfitStdDev;

	//**************************************************************************************

	public WekaLinearRegressionInfo(int iID, bool iEliminateColinearAttributes, bool iMinimal)
	{
		ID = iID;
		EliminateColinearAttributes = iEliminateColinearAttributes;
		Minimal = iMinimal;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads prediction profits from DB.
	/// </summary>
	public static List<WekaLinearRegressionInfo> LoadFromDB()
	{
		var results = new List<WekaLinearRegressionInfo>();

		var sql = "Select * from LinearRegressionInfo";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();

			var command = new SqlCommand(sql, connection);
			using (var reader = command.ExecuteReader())
				while (reader.Read())
				{
					var info = new WekaLinearRegressionInfo(reader.GetInt32(reader.GetOrdinal("Id")), reader.GetBoolean(reader.GetOrdinal("EliminateColinearAttributes")), reader.GetBoolean(reader.GetOrdinal("Minimal")));

					if (!reader.IsDBNull(reader.GetOrdinal("CoefOfDetermination")))
						info.CoefOfDetermination = (float)reader.GetDouble(reader.GetOrdinal("CoefOfDetermination"));

					if (!reader.IsDBNull(reader.GetOrdinal("ProfitStdDev")))
						info.ProfitStdDev = (float)reader.GetDouble(reader.GetOrdinal("ProfitStdDev"));

					if (!reader.IsDBNull(reader.GetOrdinal("ProfitStdDev")))
						info.ProfitStdDev = (float)reader.GetDouble(reader.GetOrdinal("ProfitStdDev"));

					results.Add(info);
				}
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Inserts prediction profits info to DB
	/// </summary>
	public static void UpdateDB(WekaLinearRegressionInfo iInfo)
	{
		var sql = "IF EXISTS(SELECT * FROM LinearRegressionInfo WHERE Id = @ID) " +
			"Update LinearRegressionInfo SET " +
			"CoefOfDetermination = @CoefOfDetermination, ProfitStdDev = @ProfitStdDev " +
			" Where ID = @ID " +
			"ELSE" +
			" INSERT INTO LinearRegressionInfo (Id, EliminateColinearAttributes, Minimal, CoefOfDetermination, ProfitStdDev) VALUES(@ID, @EliminateColinearAttributes, @Minimal, @CoefOfDetermination, @ProfitStdDev)";


		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			{
				var cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@Id", iInfo.ID);
				cmd.Parameters.AddWithValue("@EliminateColinearAttributes", iInfo.EliminateColinearAttributes);
				cmd.Parameters.AddWithValue("@Minimal", iInfo.Minimal);

				cmd.Parameters.AddWithValue("@CoefOfDetermination", (object)iInfo.CoefOfDetermination ?? DBNull.Value);
				cmd.Parameters.AddWithValue("@ProfitStdDev", (object)iInfo.ProfitStdDev ?? DBNull.Value);

				var dataAdapter = new SqlDataAdapter
				{
					InsertCommand = cmd
				};
				dataAdapter.InsertCommand.ExecuteNonQuery();
				dataAdapter.InsertCommand.Dispose();
			}
		}
	}


	//**************************************************************************************

	/// <summary>
	/// Removes prediction profits info from DB.
	/// </summary>
	public static void RemoveFromDB(int iClassifierID)
	{
		var sql = "DELETE FROM LinearRegressionInfo WHERE Id= '" + iClassifierID + "'";

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
		CoefOfDetermination = null;
		ProfitStdDev = null;
	}

	//**************************************************************************************
}

