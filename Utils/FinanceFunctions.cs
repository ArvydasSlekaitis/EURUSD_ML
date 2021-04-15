using System;

public static class FinanceFunctions
{
	
//**************************************************************************************	

	/// <summary>
	/// Discounts future profits using given cost of capital.
	/// </summary>
	/// <param name="iPrice">Array containing prices.</param>
	/// <param name="iCOC">Cost of capital.</param>
	/// <param name="iStartTime">Starting time index.</param>
	/// <param name="iMaxProfitTime">Number of future prices to discount.</param>
	/// <returns>Discounted future profit.</returns>
	public static float DiscountFutureProfits(float[] iPrice, float iCOC, int iStartTime, int iMaxProfitTime)
	{
		if (iPrice is null)
			throw new ArgumentException("Array should not be NULL", "iPrice");

		if (iCOC < 0)
			throw new ArgumentException("Cost of capital cannot be less than 0", "iCOC");

		if (iStartTime >= iPrice.Length-1)
			throw new ArgumentException("Start time cannot be higher than iPrice length", "iStartTime >= iPrice.Length-1");

		if (iStartTime > iPrice.Length - 1 - iMaxProfitTime)
			throw new ArgumentException("Array not large enough to discount all prices up to iMaxProfitTime", "iStartTime >= iPrice.Length - 1 - iMaxProfitTime");

		float kCOCMultiplier = 1 + iCOC;
		float profit = 0;
		float culmulativeCOC = kCOCMultiplier;

		for (int i = iStartTime+1; i < iStartTime + 1 + iMaxProfitTime; i++)
		{
			profit += (iPrice[i] - iPrice[i - 1]) / culmulativeCOC;
			culmulativeCOC *= kCOCMultiplier;
		}

		return profit;
	}
	
	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing future profits.
	/// </summary>
	public static float[] CalculateFutureProfits(float[] iPrice, int iMaxProfitTime)
	{
		if (iPrice is null)
			throw new ArgumentNullException(nameof(iPrice));
				
		// Calulcate profits		
		float[] profits = new float[iPrice.Length];
		for (int i = 0; i < iPrice.Length - iMaxProfitTime; i++)
			profits[i] = (float)Math.Log(iPrice[i + iMaxProfitTime] / iPrice[i]); 

		return profits;
	}

	//**************************************************************************************

	/// <summary>
	/// Calcualtes and returns cost of capital.
	/// </summary>
	public static float CalculateCostOfCapital(float[] iPrice, float iMarginRequirement, float iMaxCapitalPerTrade)
	{
		// Calcluate close price change standard deviation -> Will be used to calculate discount rate
		float priceChangeStdDev = Statistics.StandardDeviation(Statistics.CalculateChange(iPrice, false));

		// Calculate cost of capital
		// If in period with 68 percent it might move stddev
		// And we have 2400 capital with 3.5 percent margin this gives us ~60000 trading capital
		// If we usualy trade only portion of this capital, say 12 percent
		// Then available trading capital is equal to  60000 * 0,12 = 7200 
		// Then this price move will correspond to 7200 * std dev
		// So final change is:
		// ((capital / margin) * capitalPerTrade * stddev) / capital -> capitalPerTrade * stddev / margin
		return iMaxCapitalPerTrade * priceChangeStdDev / iMarginRequirement;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates last cash flow which makes current ant target NPV equal.
	/// </summary>
	public static float CalculateLastCashflow(float iCurrentNPV, float iTargetNPV, float iCostOfCapital, int iLastKnownCashflowTime)
	{
		var diff = iTargetNPV - iCurrentNPV;
		var discount = (float)Math.Pow(1 + iCostOfCapital, iLastKnownCashflowTime + 1);
		return diff * discount;
	}

	//**************************************************************************************
}
