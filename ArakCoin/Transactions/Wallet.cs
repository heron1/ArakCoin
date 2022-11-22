namespace ArakCoin.Transactions;

public class Wallet
{
    public static long getAddressBalance(string address, UTxOut[] uTxOuts)
    {
        long sum = 0;

        foreach (var utxout in uTxOuts)
        {
            if (utxout.address == address)
                sum += utxout.amount;
        }

        return sum;
    }

    public static long getCurrentCirculatingCoinSupply(Blockchain blockchain)
    {
        long totalCirculatingSupply = 0;
        foreach (var uTxOut in blockchain.uTxOuts)
        {
            if (uTxOut.address != Protocol.FEE_ADDRESS)
                totalCirculatingSupply += uTxOut.amount;
        }
        
        return totalCirculatingSupply;
    }
}