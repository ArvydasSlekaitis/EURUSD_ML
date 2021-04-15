/// <summary>
/// Stores candlestick indexes.
/// </summary>
public class CandlestickIndexCollection
{
	private readonly int[] indexes = new int[10];

	public int Count => indexes.Length;

	//**************************************************************************************

	public CandlestickIndexCollection()
	{
		for (int i = 0; i < indexes.Length; i++)
			indexes[i] = 0;
	}

	//**************************************************************************************

	public int this[Candlestick.Period candlestickPeriod]
	{
		get => this[(int)candlestickPeriod];
		set => this[(int)candlestickPeriod] = value;
	}

	//**************************************************************************************

	public int this[int index]
	{
		get => indexes[index];
		set => indexes[index] = value;
	}

	//**************************************************************************************
}
