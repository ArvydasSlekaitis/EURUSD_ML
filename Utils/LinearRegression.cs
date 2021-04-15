using System;

/// <summary>
/// Calculates and stores linear regression data.
/// </summary>
public class LinearRegression 
{
	public readonly float RSq;
	public readonly float Intercept;
	public readonly float Slope;

//**************************************************************************************

	public LinearRegression(float[] yVals)
	{
		if (yVals == null || yVals.Length < 1)
			throw new ArgumentException("Input array length cannot be less than one");

		float sumOfX = 0;
		float sumOfY = 0;
		float sumOfXSq = 0;
		float sumOfYSq = 0;
		float ssX = 0;
		float sumCodeviates = 0;
		float sCo = 0;
		float count = yVals.Length;

		for (int ctr = 0; ctr < yVals.Length; ctr++)
		{
			float x = ctr;
			float y = yVals[ctr];
			sumCodeviates += x * y;
			sumOfX += x;
			sumOfY += y;
			sumOfXSq += x * x;
			sumOfYSq += y * y;
		}
		ssX = sumOfXSq - ((sumOfX * sumOfX) / count);
		float RNumerator = (count * sumCodeviates) - (sumOfX * sumOfY);
		float RDenom = (count * sumOfXSq - (sumOfX * sumOfX)) * (count * sumOfYSq - (sumOfY * sumOfY));
		sCo = sumCodeviates - ((sumOfX * sumOfY) / count);

		float meanX = sumOfX / count;
		float meanY = sumOfY / count;
		float dblR = RNumerator / (float) Math.Sqrt(RDenom);
		RSq = dblR * dblR;
		Intercept = meanY - ((sCo / ssX) * meanX);
		Slope = sCo / ssX;
	}

//**************************************************************************************
}
