using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;

public class Candlestick 
{
	public enum Period { D1, H12, H6, H3, H2, H1, m30, m15, m5, m1 };

	public readonly ulong StartTime;
	public readonly ulong EndTime;
	public readonly float OpenPrice;
	public readonly float ClosePrice;
	public readonly float HighPrice;
	public readonly float LowPrice;
	public readonly float MedianPrice;

//**************************************************************************************

	public Candlestick(ulong iStartTime, ulong iEndTime, float iOpenPrice, float iClosePrice, float iHighPrice, float iLowPrice, float iMedianPrice) 
	{
		StartTime = iStartTime;
		EndTime = iEndTime;
		OpenPrice = iOpenPrice;
		ClosePrice = iClosePrice;
		HighPrice = iHighPrice;
		LowPrice = iLowPrice;
		MedianPrice = iMedianPrice;

		if (OpenPrice < 0 || ClosePrice < 0 || LowPrice < 0 || HighPrice < 0)
			throw new ArgumentException("Negative data", "openPrice < 0 || closePrice < 0 || lowPrice < 0 || highPrice < 0");

		if(StartTime > EndTime)
			throw new ArgumentException("End time should not be earlier than start time", "StartTime > EndTime");
	}

//**************************************************************************************

	/// <summary>
	/// Construct candlestick from a list of candlesticks (consolidate).
	/// </summary>
	public Candlestick(List<Candlestick> iSourceCandlesticks)
		: this(iSourceCandlesticks[0].StartTime, iSourceCandlesticks[iSourceCandlesticks.Count-1].EndTime, iSourceCandlesticks[0].OpenPrice, iSourceCandlesticks[iSourceCandlesticks.Count - 1].ClosePrice, iSourceCandlesticks.Max(x => x.HighPrice), iSourceCandlesticks.Min(x => x.LowPrice), iSourceCandlesticks.Median(x => x.ClosePrice))
	{
	}

	//**************************************************************************************

	/// <summary>
	/// Load candlesticks from a CSV file.
	/// </summary>
	public static List<Candlestick> LoadCSV(StreamReader iReader, Period iCandlestickPeriod, int iHoursOffest)
	{
		List<Candlestick> results = new List<Candlestick>();

		while (!iReader.EndOfStream)
		{
			string line = iReader.ReadLine();
			if (line == "timestamp,open,high,low,close")
				continue;

			string[] contents;

			if (line.Contains(","))
				contents = line.Split(',');
			else
				contents = line.Split(';');

			DateTime date;

			if (contents[0].Contains(":") || contents[0].Contains("-"))
				date = DateTime.Parse(contents[0]);
			else
				date = DateTime.ParseExact(contents[0], "yyyyMMdd HHmmss", CultureInfo.InvariantCulture);

			date = date.Add(new TimeSpan(iHoursOffest, 0, 0));
			
			DateTime dateBegin, dateEnd;
			
			switch(iCandlestickPeriod)
			{
				case Period.m1:
					dateBegin = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, 59);
					break;

				case Period.m5:
					int current = (int)(Math.Floor((float)date.Minute / 5) * 5);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, date.Hour, current, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, date.Hour, current + 4, 59);
					break;

				case Period.m15:
					current = (int)(Math.Floor((float)date.Minute / 15) * 15);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, date.Hour, current, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, date.Hour, current + 14, 59);
					break;

				case Period.m30:
					current = (int)(Math.Floor((float)date.Minute / 30) * 30);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, date.Hour, current, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, date.Hour, current + 29, 59);
					break;

				case Period.H1:
					dateBegin = new DateTime(date.Year, date.Month, date.Day, date.Hour, 0, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, date.Hour, 59, 59);
					break;

				case Period.H2:
					current = (int)(Math.Floor((float)date.Hour / 2) * 2);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, current, 0, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, current+1, 59, 59);
					break;

				case Period.H3:
					current = (int)(Math.Floor((float)date.Hour / 3) * 3);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, current, 0, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, current + 2, 59, 59);
					break;

				case Period.H6:
					current = (int)(Math.Floor((float)date.Hour / 6) * 6);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, current, 0, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, current + 5, 59, 59);
					break;

				case Period.H12:
					current = (int)(Math.Floor((float)date.Hour / 12) * 12);
					dateBegin = new DateTime(date.Year, date.Month, date.Day, current, 0, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, current + 11, 59, 59);
					break;

				case Period.D1:
					dateBegin = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
					dateEnd = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);
					break;

				default: throw new ArgumentException("Undefined candlestick period.");
			}		

			Candlestick entry;
			try
			{
				float openPrice = float.Parse(contents[1], CultureInfo.InvariantCulture);
				float closePrice = float.Parse(contents[4], CultureInfo.InvariantCulture);
				float highPrice = float.Parse(contents[2], CultureInfo.InvariantCulture);
				float lowPrice = float.Parse(contents[3], CultureInfo.InvariantCulture);
				entry = new Candlestick(Utils.DateTimeToUnix(dateBegin), Utils.DateTimeToUnix(dateEnd), openPrice, closePrice, highPrice, lowPrice, new float[] { openPrice, closePrice, lowPrice, highPrice }.Median());
			}
			catch
			{
				Console.WriteLine("Invalid data detected while reading candlesticks from csv:" + line);
				entry = null;
			}

			if (entry != null)
				results.Add(entry);
		}

		return results;
	}

//**************************************************************************************

	/// <summary>
	/// Save candlesticks to binary file.
	/// </summary>
	public static void Save(string iFilename, List<Candlestick> iCandlestickData)
	{
		using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(iFilename)))
		{
			writer.Write(iCandlestickData.Count);

			for (int i = 0; i < iCandlestickData.Count; i++)
			{
				writer.Write(iCandlestickData[i].StartTime);
				writer.Write(iCandlestickData[i].EndTime);
				writer.Write(iCandlestickData[i].OpenPrice);
				writer.Write(iCandlestickData[i].ClosePrice);
				writer.Write(iCandlestickData[i].HighPrice);
				writer.Write(iCandlestickData[i].LowPrice);
				writer.Write(iCandlestickData[i].MedianPrice);
			}
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Save candlesticks to DB.
	/// </summary>
	public static void SaveToDB(string iTableName, List<Candlestick> iCandlestickData)
	{
		var table = new DataTable();
		table.Columns.Add("StartTime", typeof(DateTime));
		table.Columns.Add("EndTime", typeof(DateTime));
		table.Columns.Add("OpenPrice", typeof(float));
		table.Columns.Add("ClosePrice", typeof(float));
		table.Columns.Add("HighPrice", typeof(float));
		table.Columns.Add("LowPrice", typeof(float));
		table.Columns.Add("MedianPrice", typeof(float));
		
		for (var i = 0; i < iCandlestickData.Count; i++)
		{
			var row = table.NewRow();

			row["StartTime"] = Utils.UnixToDateTime(iCandlestickData[i].StartTime);
			row["EndTime"] = Utils.UnixToDateTime(iCandlestickData[i].EndTime);
			row["OpenPrice"] = iCandlestickData[i].OpenPrice;
			row["ClosePrice"] = iCandlestickData[i].ClosePrice;
			row["HighPrice"] = iCandlestickData[i].HighPrice;
			row["LowPrice"] = iCandlestickData[i].LowPrice;
			row["MedianPrice"] = iCandlestickData[i].MedianPrice;
			table.Rows.Add(row);
		}

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();
			using (var bulk = new SqlBulkCopy(connection))
			{
				bulk.DestinationTableName = iTableName;
				bulk.WriteToServer(table);
			}
		}
	}

	//**************************************************************************************

	/// <summary>
	/// Load candlesticks from DB.
	/// </summary>
	public static List<Candlestick> LoadFromDB(string iTableName)
	{
		var results = new List<Candlestick>();

		using (var connection = new SqlConnection(Program.SQLConnectionName))
		{
			connection.Open();

			var sql = "Select * from " + iTableName;

			var command = new SqlCommand(sql, connection);
			using (var reader = command.ExecuteReader())
			{
				while (reader.Read())
				{
					var startTime = Utils.DateTimeToUnix(reader.GetDateTime(0));
					var endTime = Utils.DateTimeToUnix(reader.GetDateTime(1));
					var openPrice = (float)reader.GetDouble(2);
					var closePrice = (float)reader.GetDouble(3);
					var highPrice = (float)reader.GetDouble(4);
					var lowPrice = (float)reader.GetDouble(5);
					var medianPrice = (float)reader.GetDouble(6);

					var c = new Candlestick(startTime, endTime, openPrice, closePrice, highPrice, lowPrice, medianPrice);
					results.Add(c);
				}
			}
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Load candlesticks from binary file.
	/// </summary>
	public static List<Candlestick> Load(string iFilename)
	{
		List<Candlestick> candlesticks;

		using (BinaryReader reader = new BinaryReader(File.OpenRead(iFilename)))
		{
			int count = reader.ReadInt32();
			candlesticks = new List<Candlestick>(count);

			for (int i = 0; i < count; i++)
				candlesticks.Add(new Candlestick(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
		}

		return candlesticks;
	}

	//**************************************************************************************

	/// <summary>
	/// Consolidate candlesticks from higher period to lower period.
	/// </summary>
	public static List<Candlestick> Consolidate(List<Candlestick> iCandlesticks, int iPeriodsPerDay)
	{
		if (iCandlesticks is null)
			throw new NullReferenceException("iSourceCandlestick");

		if (iCandlesticks.Count <= 0)
			throw new ArgumentException("Array should not be empty", "iSourceCandlestick");

		if (iPeriodsPerDay <= 0)
			throw new ArgumentException("Value should be greater than zero", "iPeriodsPerDay");

		List<Candlestick> results = new List<Candlestick>();

		DateTime initialTime = Utils.UnixToDateTime(iCandlesticks[0].StartTime);
		initialTime = new DateTime(initialTime.Year, initialTime.Month, initialTime.Day, 0, 0, 0);

		ulong periodDuration = 86400000 / (ulong)iPeriodsPerDay;
		ulong startTime = Utils.DateTimeToUnix(initialTime);
		ulong endTime = startTime + periodDuration;

		List<Candlestick> periodCandlesticks = new List<Candlestick>();

		for (int i = 0; i < iCandlesticks.Count; i++)
		{
			if (iCandlesticks[i].EndTime > endTime)
			{
				if (periodCandlesticks.Count > 0)
				{
					results.Add(new Candlestick(periodCandlesticks));
					periodCandlesticks.Clear();
				}
				startTime += periodDuration;
				endTime += periodDuration;
				i--;
				continue;
			}
			else
				periodCandlesticks.Add(iCandlesticks[i]);
		}

		return results;
	}
		
	//**************************************************************************************

	/// <summary>
	/// Converts candlestick period to daily periods.
	/// </summary>
	public static int PeriodToDaily(Period iPeriod)
	{
		switch (iPeriod)
		{
			case Period.D1: return 1;
			case Period.H12: return 2;
			case Period.H6: return 4;
			case Period.H3: return 8;
			case Period.H2: return 12;
			case Period.H1: return 24;
			case Period.m30: return 48;
			case Period.m15: return 96;
			case Period.m5: return 288;
			case Period.m1: return 1440;
		}

		throw new ArgumentException("iPeriod");
	}

	//**************************************************************************************

	/// <summary>
	/// Converst daily periods to candlestick period.
	/// </summary>
	/// <param name="iDailyPeriods">Possible values: 1, 2, 4, 8, 12, 24, 48, 96, 288, 1440.</param>
	public static Period DailyToPeriod(int iDailyPeriods)
	{
		switch (iDailyPeriods)
		{
			case 1: return Period.D1;
			case 2: return Period.H12;
			case 4: return Period.H6;
			case 8: return Period.H3;
			case 12: return Period.H2;
			case 24: return Period.H1;
			case 48: return Period.m30;
			case 96: return Period.m15;
			case 288: return Period.m5;
			case 1440: return Period.m1;
		}

		throw new ArgumentException("iDailyPeriods");
	}

	//**************************************************************************************

	/// <summary>
	/// Converts period name to candlestick period.
	/// </summary>
	/// <param name="iName">Possible values: 1D, 12H, 6H, 3H, 2H, 1H, 30m, 15m, 5m, 1m.</param>
	public static Period NameToPeriod(string iName)
	{
		switch (iName)
		{
			case "1D": return Period.D1;
			case "12H": return Period.H12;
			case "6H": return Period.H6;
			case "3H": return Period.H3;
			case "2H": return Period.H2;
			case "1H": return Period.H1;
			case "30m": return Period.m30;
			case "15m": return Period.m15;
			case "5m": return Period.m5;
			case "1m": return Period.m1;
		}

		throw new ArgumentException("iName");
	}

	//**************************************************************************************

	/// <summary>
	/// Converts candlestick period to period name.
	/// </summary>
	public static string PeriodToName(Period iPeriod)
	{
		switch (iPeriod)
		{
			case Period.D1: return "1D";
			case Period.H12: return "12H";
			case Period.H6: return "6H";
			case Period.H3: return "3H";
			case Period.H2: return "2H";
			case Period.H1: return "1H";
			case Period.m30: return "30m";
			case Period.m15: return "15m";
			case Period.m5: return "5m";
			case Period.m1: return "1m";
		}

		throw new ArgumentException("iPeriod");
	}
	
	//**************************************************************************************

	/// <summary>
	/// Given a presorted candlestick list (based on StartTime) returns candlestick that starts with a given time.
	/// </summary>
	public static int FindIndex(List<Candlestick> iCandlesticks, ulong iTime)
	{
		if (iCandlesticks is null)
			throw new ArgumentNullException(nameof(iCandlesticks));

		int lowerBound = 0;
		int upperBound = iCandlesticks.Count;

		while (lowerBound <= upperBound)
		{
			int midPoint = (int)Math.Floor((double)(lowerBound + upperBound) / 2);

			if (iCandlesticks[midPoint].StartTime < iTime)
			{
				lowerBound = midPoint;
				if (upperBound - lowerBound <= 1)
					return midPoint;
			}
			else if (iCandlesticks[midPoint].StartTime > iTime)
			{
				upperBound = midPoint;
				if (upperBound - lowerBound <= 1)
					return midPoint -1;
			
			}
			else return midPoint;
		}
			/*
			int lowerBound = 0;
			int upperBound = iCandlesticks.Count;
			while (lowerBound <= upperBound)
			{
				if (upperBound - lowerBound <= 1)
				{
					if (upperBound >= iCandlesticks.Count)
						return lowerBound;
					if (Math.Abs((double)iCandlesticks[lowerBound].StartTime - (double)iTime) < Math.Abs((double)iCandlesticks[upperBound].StartTime - (double)iTime))
						return lowerBound;
					else
						return upperBound;				}
				int midPoint = (int)Math.Floor((double)(lowerBound + upperBound) / 2);
				if (iCandlesticks[midPoint].StartTime < iTime)
					lowerBound = midPoint;
				else if (iCandlesticks[midPoint].StartTime > iTime)
					upperBound = midPoint;
				else return midPoint;
		   }
			*/

			throw new ArgumentException("Could not find candlestick index.");
	}

	//**************************************************************************************

	/// <summary>
	/// Given a presorted candlestick list (based on StartTime) returns candlestick that starts with a given time.
	/// </summary>
	public static int FindIndex(List<Candlestick> iCandlesticks, ulong iTime, int iLastKnownIndex)
	{
		if (iCandlesticks is null)
			throw new ArgumentNullException(nameof(iCandlesticks));

		if (iTime >= iCandlesticks[iLastKnownIndex].StartTime && iTime <= iCandlesticks[iLastKnownIndex].EndTime)
			return iLastKnownIndex;

		if (iTime >= iCandlesticks[iLastKnownIndex+1].StartTime && iTime <= iCandlesticks[iLastKnownIndex+1].EndTime)
			return iLastKnownIndex+1;

		return FindIndex(iCandlesticks, iTime);
	}

	//**************************************************************************************

	/// <summary>
	/// Creates and returns a new candlestick list that is the copy of provided list with range [iStartIndex; iEndIndex].
	/// </summary>
	public static List<Candlestick> CreateCopy(List<Candlestick> iCandlesticks, int iStartIndex, int iEndIndex)
	{
		if (iCandlesticks is null)
			throw new ArgumentNullException(nameof(iCandlesticks));

		Candlestick[] results = new Candlestick[iEndIndex - iStartIndex + 1];
		iCandlesticks.CopyTo(iStartIndex, results, 0, results.Length);
		return new List<Candlestick>(results);
	}
	
	//**************************************************************************************
}
