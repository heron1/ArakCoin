namespace ArakCoin.Transactions;

public class Wallet
{
    /**
     * Retrieve the balance for the given public key address from the given utxouts
     */
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

    /**
     * Overloaded version of getAddressBalance that uses the global masterchain as the source of utxouts
     */
    public static long getAddressBalance(string address)
    {
        return getAddressBalance(address, Globals.masterChain.uTxOuts);
    }
    
    /**
     * Retrieves the total circulating supply of all unspent coins within the given blockchain object
     */
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