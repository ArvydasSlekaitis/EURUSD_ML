using System.Collections.Generic;
using System;
using System.Linq;

public static class Statistics 
{

//**************************************************************************************

	/// <summary>
	/// Calculates and returns arithmetic mean.
	/// </summary>
	public static float ArithmeticMean(float[] iNumbers)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		return Sum(iNumbers) / (float)iNumbers.Length;
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns weighted arithmetic mean.
	/// </summary>
	/// <param name="iWeights"> Constrain: sum of all weights must be equal to 1.</param>
	public static float WeightedArithmeticMean(float[] iNumbers, float[] iWeights)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		if(iWeights is null || iNumbers.Length != iWeights.Length)
			throw new ArgumentException("Weights array must contain same amount of values as numbers array.", "iWeights");

		float sum = 0.0f;

		for(int i=0; i<iNumbers.Length; i++)
			sum += iNumbers[i]*iWeights[i];

		return sum;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns median value using Linq.
	/// </summary>
	public static float Median(this IEnumerable<float> source)
	{
		if (!(source?.Any() ?? false))
		{
			throw new InvalidOperationException("Cannot compute median for a null or empty set.");
		}

		var sortedList = (from number in source
						  orderby number
						  select number).ToList();

		int itemIndex = sortedList.Count / 2;

		if (sortedList.Count % 2 == 0)
		{
			// Even number of items.
			return (sortedList[itemIndex] + sortedList[itemIndex - 1]) / 2;
		}
		else
		{
			// Odd number of items.
			return sortedList[itemIndex];
		}
	}

	// Generic Median overload.
	public static float Median<T>(this IEnumerable<T> numbers,
						   Func<T, float> selector) =>
		(from num in numbers select selector(num)).Median();

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns percentile.
	/// </summary>
	public static float Percentile(float[] iNumbers, float iPercentile)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		if (iPercentile < 0 || iPercentile > 1.0f)
			throw new ArgumentException("Percentile must be between [0; 1]", "iPercentile");
		
		if (iNumbers.Length == 1)
			return iNumbers[0];

		List<float> sortedList = new List<float>(iNumbers);
		sortedList.Sort();

		var index = (int)Math.Floor(iPercentile * sortedList.Count);
		return sortedList[index];
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns geometric mean.
	/// </summary>
	public static float GeometricMean(float[] iNumbers)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		float product = iNumbers [0];

		for(int i=1; i<iNumbers.Length; i++)
			product *= iNumbers[i];

		return (float)Math.Pow(product, 1.0f / (float)iNumbers.Length);
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns harmonic mean.
	/// </summary>
	public static float HarmonicMean(float[] iNumbers)
	{
		if (iNumbers == null || iNumbers.Length < 1)
			throw new System.ArgumentException("Array must contain at least one value.", "iNumbers");

		float sum = 0.0f;

		foreach (float v in iNumbers)
			sum += 1.0f/v;

		return (float)iNumbers.Length / sum;
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns exponential moving average.
	/// </summary>
	/// <param name="iNumbers">iNumbers[0] - oldest entry</param>
	public static float[] EMA(float[] iNumbers, int iNumberOfPeriods)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		float k = 2.0f/(iNumberOfPeriods+1.0f);
		float[] ema = new float[iNumbers.Length];
		ema[0] = iNumbers[0];

		for(int i=1; i<iNumbers.Length; i++)
			ema[i] = iNumbers[i] * k + ema[i - 1] * (1 - k);

		return ema;
	}

//**************************************************************************************

	/// <summary>
	/// Finds and returns maximum value.
	/// </summary>
	public static float Max(float[] iNumbers)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		float maxValue = iNumbers[0];

		for (int i = 1; i < iNumbers.Length; i++)
			if (iNumbers[i] > maxValue)
				maxValue = iNumbers[i];

		return maxValue;
	}

	//**************************************************************************************

	/// <summary>
	/// Finds and returns minimum value.
	/// </summary>
	public static float Min(float[] iNumbers)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		float minValue = iNumbers[0];

		for (int i = 1; i < iNumbers.Length; i++)
			if (iNumbers[i] < minValue)
				minValue = iNumbers[i];

		return minValue;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns sum of array elements.
	/// </summary>
	public static float Sum(float[] iNumbers)
	{
		if (iNumbers is null)
			throw new NullReferenceException("iNumbers");

		if (iNumbers.Length < 1)
			return 0;

		float sum = iNumbers [0];

		for (int i = 1; i < iNumbers.Length; i++)
			sum += iNumbers [i];

		return sum;
	}


	//**************************************************************************************

	/// <summary>
	/// Calculates and returns standard deviation of given number array.
	/// </summary>
	public static float StandardDeviation(float[] iNumbers) => StandardDeviation(iNumbers, ArithmeticMean(iNumbers));

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns standard deviation of given number array.
	/// </summary>
	public static float StandardDeviation(float[] iNumbers, float iMean)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		float sum = 0.0f;

		for(int i=0; i<iNumbers.Length; i++)
			sum += (float)Math.Pow(iNumbers[i] - iMean, 2.0f);

		return (float)Math.Sqrt(sum / ((double)iNumbers.Length - 1.0));
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns standard deviation of given number array using second number array as reference (median) values.
	/// </summary>
	public static float StandardDeviation(float[] iNumbers1, float[] iNumbers2)
	{
		if (iNumbers1 is null || iNumbers1.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers1");

		if (iNumbers2 is null || iNumbers2.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers2");

		if (iNumbers1.Length != iNumbers2.Length)
			throw new ArgumentException("Both arrays size must match.", "iNumbers1 and iNumbers2");

		float sum = 0.0f;

		for(int i=0; i<iNumbers1.Length; i++)
			sum += (float)Math.Pow(iNumbers1[i] - iNumbers2[i], 2.0);

		return (float)Math.Sqrt(sum / ((float)iNumbers1.Length - 1.0));
	}

//**************************************************************************************

	/// <summary>
	/// Calculates and returns weighted standard deviation of given number array.
	/// </summary>
	public static float WeightedStandardDeviation(float[] iNumbers, float[] iWeights, float iMean)
	{
		if (iNumbers is null || iNumbers.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iNumbers");

		if (iWeights is null || iWeights.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iWeights");

		if (iNumbers.Length != iWeights.Length)
			throw new ArgumentException("Both arrays size must match.", "iNumbers and iWeights");

		float sum = 0.0f;

		for(int i=0; i<iNumbers.Length; i++)
			sum += iWeights[i] * (float)Math.Pow(iNumbers[i] - iMean, 2.0);

		return (float)Math.Sqrt(sum / ((double)iNumbers.Length - 1.0));
	}	
	
//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing value changes in original array.
	/// </summary>
	/// <param name="iArray">Each value must be greater than zero.</param>
	/// <param name="iKeepFirstValue">If set to true returns array with size of the original, and first value set to zero. If set to false returns one entry smaller array containing only changes.</param>
	public static float[] CalculateChange(float[] iArray, bool iKeepFirstValue = true)
	{
		if (iArray is null || iArray.Length < 2)
			throw new ArgumentException("Array must contain at least one value.", "iPrice");

		float[] change = new float[iKeepFirstValue ? iArray.Length : iArray.Length-1];

		for(int i=1; i<iArray.Length; i++)
			change[iKeepFirstValue ? i : i-1] = (iArray[i] / iArray[i - 1]) - 1;

		return change;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing value changes in original array.
	/// </summary>
	/// <param name="iArray">Each value must be greater than zero.</param>
	/// <param name="iKeepFirstValue">If set to true returns array with size of the original, and first value set to zero. If set to false returns one entry smaller array containing only changes.</param>
	public static float[] CalculateChangeLN(float[] iArray, bool iKeepFirstValue = true)
	{
		if (iArray is null || iArray.Length < 2)
			throw new ArgumentException("Array must contain at least one value.", "iPrice");

		float[] change = new float[iKeepFirstValue ? iArray.Length : iArray.Length - 1];

		for (int i = 1; i < iArray.Length; i++)
			change[iKeepFirstValue ? i : i - 1] = (float)Math.Log(iArray[i] / iArray[i - 1]);

		return change;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array that contains differences in values. 
	/// </summary>
	/// <param name="iArray">First entry - oldest.</param>
	public static float[] CalculateDifferences(float[] iArray)
	{
		if (iArray is null || iArray.Length < 3)
			throw new ArgumentException("Array must contain at least two values.", "iPrice");

		float[] diff = new float[iArray.Length - 1];

		for (int i = 1; i < iArray.Length; i++)
			diff[i-1] = iArray[i] - iArray[i - 1];

		return diff;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates and returns correlation between two number arrays.
	/// </summary>
	public static float CalculateCorrelation(float[] iArray1, float[] iArray2)
	{
		if (iArray1 is null || iArray1.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iArray1");

		if (iArray2 is null || iArray2.Length < 1)
			throw new ArgumentException("Array must contain at least one value.", "iArray2");

		if (iArray1.Length != iArray2.Length)
			throw new ArgumentException("Both arrays size must match.", "iArray1 and iArray2");

		float sum_x = 0;
		float sum_y = 0;
		float sum_xy = 0;
		float sum_xpow2 = 0;
		float sum_ypow2 = 0;

		for (int i = 0; i < iArray1.Length; i++)
		{
			sum_x += iArray1[i];
			sum_y += iArray2[i];
			sum_xy += iArray1[i] * iArray2[i];
			sum_xpow2 += (float)Math.Pow(iArray1[i], 2.0);
			sum_ypow2 += (float)Math.Pow(iArray2[i], 2.0);
		}
		
		double Ex2 = Math.Pow(sum_x, 2.00);
		double Ey2 = Math.Pow(sum_y, 2.00);

		return (iArray1.Length * sum_xy - sum_x * sum_y) /
			   (float)Math.Sqrt((iArray1.Length * sum_xpow2 - Ex2) * (iArray1.Length * sum_ypow2 - Ey2));
	}

	//**************************************************************************************

	/// <summary>
	/// Returns true if both arrays values are equal.
	/// </summary>
	public static bool Equals(float[] iArray1, float[] iArray2)
	{
		if (iArray1.Length != iArray2.Length)
			return false;

		for (int i = 0; i < iArray1.Length; i++)
			if (iArray1[i] != iArray2[i])
				return false;

		return true;
	}

	//**************************************************************************************

	/// <summary>
	/// Returns normalized array.
	/// </summary>
	public static float[] Normalize(float[] iValues)
	{
		if (iValues is null)
			throw new ArgumentNullException("iValues");

		if (iValues.Length < 1)
			throw new ArgumentException("Cannot normalize empty array");

		var results = new float[iValues.Length];
		var sum = Sum(iValues);

		for (int i = 0; i < iValues.Length; i++)
			results[i] = iValues[i] / sum;

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Clamps value in the given range.
	/// </summary>
	public static float Clamp(float iValue, float iMin, float iMax) => Math.Max(Math.Min(iValue, iMax), iMin);

	//**************************************************************************************

	/// <summary>
	/// Returns linearly interpolated value.
	/// </summary>
	public static float Lerp(float iBegin, float iEnd, float iAmount) => iBegin * (1 - iAmount) + iEnd * iAmount;

	//**************************************************************************************
}
