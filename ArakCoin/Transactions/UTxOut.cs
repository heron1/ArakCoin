namespace ArakCoin.Transactions;

/**
 * Unspent Transaction Output. Refers to spendable coins located at the given address for the given amount.
 * txOutId refers to the transaction the coins came from, and txOutIndex the TxOut index within that transaction
 */
public class UTxOut
{
	public string txOutId;
	public int txOutIndex;
	public string address;
	public long amount;

	public UTxOut(string txOutId, int txOutIndex, string address, long amount)
	{
		this.txOutId = txOutId;
		this.txOutIndex = txOutIndex;
		this.address = address;
		this.amount = amount;
	}
	
	#region equality override
	public override bool Equals(object? o)
	{
		if (o is null || o.GetType() != typeof(UTxOut))
			return false;
		UTxOut other = (UTxOut)o;

		if (this.txOutId == other.txOutId && this.txOutIndex == other.txOutIndex && this.address == other.address &&
		    this.amount == other.amount)
			return true;

		return false;
	}

	public static bool operator == (UTxOut b1, UTxOut b2)
	{
		return b1.Equals(b2);
	}

	public static bool operator != (UTxOut b1, UTxOut b2)
	{
		return !(b1 == b2);
	}
	
	#endregion equality override
}