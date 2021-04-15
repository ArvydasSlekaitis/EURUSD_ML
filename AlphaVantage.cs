using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;

class AlphaVantage
{
	private const string kAlphaVantageAPIKey = "IT0Y1CLTZVUT7UY0";

	//**************************************************************************************

	/// <summary>
	/// Retrieves candlesticks from a server.
	/// </summary>
	public static List<Candlestick> RetrieveCandlesticks(Candlestick.Period iCandlestickPeriod)
	{
		HttpWebRequest request;

		switch (iCandlestickPeriod)
		{
			case Candlestick.Period.D1:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_DAILY&from_symbol=EUR&to_symbol=USD&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Candlestick.Period.m1:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=1min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Candlestick.Period.m5:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=5min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Candlestick.Period.m15:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=15min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Candlestick.Period.m30:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=30min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			default:
				throw new ArgumentException("Unsuported candlestick period", "iCandlestickPeriod");
		}

		HttpWebResponse response = (HttpWebResponse)request.GetResponse();

		if (response.StatusCode != HttpStatusCode.OK)
			throw new Exception("Could not load candlesticks.");

		using (Stream stream = response.GetResponseStream())
			using (StreamReader reader = new StreamReader(stream))
			{
				List<Candlestick> results = Candlestick.LoadCSV(reader, iCandlestickPeriod, 0);
				results.Reverse();

				// Remove not full periods.
				if (Utils.UnixToDateTime(results[results.Count - 1].EndTime) > DateTime.UtcNow)
					results.RemoveAt(results.Count - 1);

				return results;
			}
	}

	//**************************************************************************************

	/// <summary>
	/// Retrieves current price from a server.
	/// </summary>
	public static float RetrieveCurrentPrice()
	{
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency=EUR&to_currency=USD&apikey=" + kAlphaVantageAPIKey);
		HttpWebResponse response = (HttpWebResponse)request.GetResponse();
		using (Stream stream = response.GetResponseStream())
		using (StreamReader reader = new StreamReader(stream))
		{
			JObject answer = JObject.Parse(reader.ReadToEnd());
			return float.Parse((string)answer["Realtime Currency Exchange Rate"]["5. Exchange Rate"], CultureInfo.InvariantCulture);
		}		
	}

	//**************************************************************************************
}

