namespace ArakCoin.Transactions;

public class TxOut
{
	public string address;
	public long amount;

	public TxOut(string address, long amount)
	{
		this.address = address;
		this.amount = amount;
	}
}