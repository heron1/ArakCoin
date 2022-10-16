namespace ArakCoin.Transactions;

/**
 * This type doesn't serve as any part of the UTXO model implementation in this project,
 * but is used as an easy way to keep track of transactions from a sender to a receiver, for informational purposes
 */
public class TxRecord
{
    public string sender;
    public string receiver;
    public long amount;
    public string transactionId;

    public TxRecord(string sender, string receiver, long amount, string transactionId)
    {
        this.sender = sender;
        this.receiver = receiver;
        this.amount = amount;
        this.transactionId = transactionId;
    }
}