using System;
using System.Collections.Generic;
using System.Data.SqlClient;

class WekaInfo
{
	public int? ID { get; private set; }
	public readonly int ParametersID;
	public readonly Candlestick.Period Period;
	public readonly int ProfitTime;
	public readonly Type Type;

	//**************************************************************************************

	public WekaInfo(int? iID, Type iType, int iParametersID, Candlestick.Period iPeriod, int iProfitTime)
	{
		ID = iID;
		Type = iType;
		ParametersID = iParametersID;
		Period = iPeriod;
		ProfitTime = iProfitTime;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads info from DB.
	/// </summary>
	public static List<WekaInfo> LoadFromDB()
	{
		var results = new List<WekaInfo>();

		var sql = "Select * from Classifiers";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();

			var command = new SqlCommand(sql, connection);
			using (var reader = command.ExecuteReader())
				while (reader.Read())
				{
					results.Add(new WekaInfo(
						reader.GetInt32(reader.GetOrdinal("Id")),
						Type.GetType(reader.GetString(reader.GetOrdinal("Type"))),
						reader.GetInt32(reader.GetOrdinal("ParametersID")),
						(Candlestick.Period)Enum.Parse(typeof(Candlestick.Period), reader.GetString(reader.GetOrdinal("Period"))),
						reader.GetInt32(reader.GetOrdinal("ProfitTime"))
						));
				}
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Inserts info to DB
	/// </summary>
	public static void InsertToDB(WekaInfo iInfo)
	{
		var sql = "INSERT INTO Classifiers (Type, ParametersID, Period, ProfitTime) output INSERTED.ID VALUES(@Type, @ParametersID, @Period, @ProfitTime)";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			{
				var cmd = new SqlCommand(sql, connection);
				cmd.Parameters.AddWithValue("@Type", iInfo.Type.Name);
				cmd.Parameters.AddWithValue("@ParametersID", iInfo.ParametersID);
				cmd.Parameters.AddWithValue("@Period", iInfo.Period.ToString());
				cmd.Parameters.AddWithValue("@ProfitTime", iInfo.ProfitTime);

				var dataAdapter = new SqlDataAdapter
				{
					InsertCommand = cmd
				};

				int id = (int)dataAdapter.InsertCommand.ExecuteScalar();
				iInfo.ID = id;

				dataAdapter.InsertCommand.Dispose();
			}
		}
	}


	//**************************************************************************************

	/// <summary>
	/// Removes info from DB.
	/// </summary>
	public static void RemoveFromDB(int iClassifierID)
	{
		var sql = "DELETE FROM Classifiers WHERE Id= '" + iClassifierID + "'";

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			using (SqlCommand command = new SqlCommand(sql, connection))
				command.ExecuteNonQuery();
		}
	}

	//**************************************************************************************
}
