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
}