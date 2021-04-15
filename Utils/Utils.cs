using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

class Utils
{
//**************************************************************************************

	/// <summary>
	/// Converts given unix time to DateTime.
	/// </summary>
	public static DateTime UnixToDateTime(double iUnixTime)
	{
		DateTime dtDateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
		dtDateTime = dtDateTime.AddMilliseconds(iUnixTime);
		return dtDateTime;
	}

	//**************************************************************************************	

	/// <summary>
	/// Converts given date time to unix time.
	/// </summary>
	public static ulong DateTimeToUnix(DateTime iDateTime) 	=> (ulong)iDateTime.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

	//**************************************************************************************

	/// <summary>
	/// Calculates angle between two vectors.
	/// </summary>
	public static float AngleBetween(Vector2 iA, Vector2 iB) => RadianToDegree((float)Math.Atan2(iB.Y - iA.Y, iB.X - iA.X));

	//**************************************************************************************

	/// <summary>
	/// Converts given radian angle to degrees.
	/// </summary>
	public static float RadianToDegree(float iAngle) => iAngle * (180.0f / (float)Math.PI);

	//**************************************************************************************

	/// <summary>
	/// Returns last element of a given array.
	/// </summary>
	public static float Last(float[] iArray) => iArray[iArray.Length - 1];

	//**************************************************************************************

	/// <summary>
	/// Saves given data table to a csv.
	/// </summary>
	public static void SaveDataTableToCSV(DataTable iDataTable, string iFilename)
	{
		StringBuilder sb = new StringBuilder();

		IEnumerable<string> columnNames = iDataTable.Columns.Cast<DataColumn>().
										  Select(column => column.ColumnName);
		sb.AppendLine(string.Join(";", columnNames));

		foreach (DataRow row in iDataTable.Rows)
		{
			IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
			sb.AppendLine(string.Join(";", fields));
		}

		File.WriteAllText(iFilename, sb.ToString());
	}

	//**************************************************************************************
}

