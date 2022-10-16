namespace ArakCoin.Transactions;

public class TxIn
{
	public string txOutId;
	public int txOutIndex;
	public string? signature;

	public TxIn(string txOutId, int txOutIndex, string? signature)
	{
		this.txOutId = txOutId;
		this.txOutIndex = txOutIndex;
		this.signature = signature;
	}
	
	#region equality override
	public override bool Equals(object? o)
	{
		if (o is null || o.GetType() != typeof(TxIn))
			return false;
		TxIn other = (TxIn)o;

		if (this.txOutId == other.txOutId && this.txOutIndex == other.txOutIndex && this.signature == other.signature)
			return true;

		return false;
	}

	public static bool operator == (TxIn t1, TxIn t2)
	{
		return t1.Equals(t2);
	}

	public static bool operator != (TxIn t1, TxIn t2)
	{
		return !(t1 == t2);
	}
	
	#endregion equality override
}