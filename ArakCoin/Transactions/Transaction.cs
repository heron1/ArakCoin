using System.Text;

namespace ArakCoin.Transactions;

public class Transaction
{
	public string? id;
	public TxIn[] txIns;
	public TxOut[] txOuts;
	public bool isCoinbaseTx;

	public Transaction(string? id, TxIn[] txIns, TxOut[] txOuts, bool isCoinbaseTx = false)
	{
		this.id = id;
		this.txIns = txIns;
		this.txOuts = txOuts;
		this.isCoinbaseTx = isCoinbaseTx;
	}

	public static string getTxId(Transaction transaction)
	{
		StringBuilder idAccumString = new StringBuilder();

		// add tx ins to id
		foreach (TxIn txIn in transaction.txIns)
		{
			idAccumString.Append($"{txIn.txOutId}{txIn.txOutIndex.ToString()}");
		}
		
		// add tx outs to id
		foreach (TxOut txout in transaction.txOuts)
		{
			idAccumString.Append($"{txout.address}{txout.amount.ToString()}");
		}

		return Utilities.calculateSHA256Hash(idAccumString.ToString());
	}

	/**
	 * Given an input container of uTxOuts, return the matching uTxOut with the given transaction id & txOut index.
	 * If it doesn't exist, return null
	 */
	public static UTxOut? getUTxOut(string txId, int index, UTxOut[] uTxOuts)
	{
		foreach (var uTxOut in uTxOuts)
		{
			if (uTxOut.txOutId == txId && uTxOut.txOutIndex == index)
				return uTxOut;
		}

		return null;
	}
	
	/**
	 * Given any container of transactions that implements the IEnumerable interface, return all of its TxIns
	 */
	public static List<TxIn> getTxInsFromTransactionContainer(IEnumerable<Transaction> txContainer)
	{
		var txIns = new List<TxIn>();
		
		foreach (var tx in txContainer)
		{
			foreach (var txin in tx.txIns)
			{
				txIns.Add(txin);
			}
		}

		return txIns;
	}

	/**
	 * Checks all the transactions in the given array of transactions for a duplicate txIn
	 */
	public static bool doesTxArrayContainDuplicateTxIn(Transaction[] transactions)
	{
		var txins = Transaction.getTxInsFromTransactionContainer(transactions);
		for (int i = 0; i < txins.Count; i++)
		{
			for (int j = i + 1; j < txins.Count; j++)
			{
				if (txins[i].txOutId == txins[j].txOutId && txins[i].txOutIndex == txins[j].txOutIndex)
					return true;
			}
		}

		return false;
	}

	/**
	 * Checks whether there is a duplicate unspent transaction output in the input utxOut array
	 */
	public static bool doesUTxOutArrayContainDuplicateUTxOut(UTxOut[] utxOuts)
	{
		for (int i = 0; i < utxOuts.Length; i++)
		{
			for (int j = i + 1; j < utxOuts.Length; j++)
			{
				if (utxOuts[i].txOutId == utxOuts[j].txOutId && utxOuts[i].txOutIndex == utxOuts[j].txOutIndex)
					return true;
			}
		}

		return false;
	}
	
	//check whether the input transaction is the coinbase transaction
	//for the given block and chain, and meets protocol requirements. Returns true if valid, otherwise false
	public static bool isValidCoinbaseTransaction(Transaction coinbaseTx, Block block, Blockchain blockchain)
	{
		//the block must contain at least one transaction
		if (block.transactions.Length == 0)
			return false;
	
		//verify the proposed coinbase tx is the same as the first tx in the block, and is coinbase
		if (block.transactions[0] != coinbaseTx || !coinbaseTx.isCoinbaseTx)
			return false;
        
        //verify formatting is correct
        if (coinbaseTx.txIns.Length != 1 || coinbaseTx.txOuts.Length != 1)
	        return false;
        
        TxIn coinbaseTxIn = coinbaseTx.txIns[0];
        if (coinbaseTx.id != Transaction.getTxId(coinbaseTx) || coinbaseTxIn.txOutIndex != 0 ||
            coinbaseTxIn.signature is not null)
	        return false;

        TxOut coinbaseTxOut = coinbaseTx.txOuts[0];
        long correctReward = blockchain.currentBlockReward; //initialize correctReward as the block reward
        foreach (var tx in block.transactions.Skip(1)) //loop through normal (non-coinbase) transactions
        {
	        //then add the miner fees from each normal transaction in the block
	        correctReward += Transaction.getMinerFeeFromTransaction(tx);
	        
	        //also assert there can only be one coinbase tx in the block
	        if (tx.isCoinbaseTx)
		        return false;
        }
        
        //now assert the proposed coinbase transaction has the correct reward, both from tx fees
        //in the specific block, and the protocol mining reward
        if (coinbaseTxOut.amount != correctReward)
	        return false;

        return true; //all checks passed
	}
	
	/**
	 * Checks whether *normal* transaction is valid within the context of the given uTxOuts
	 */
	public static bool isValidTransaction(Transaction tx, UTxOut[] uTxOuts)
	{
		if (tx.id is null || tx.txIns is null || tx.txOuts is null)
			return false;
		
		//ensure txOuts are valid
		validateTxOuts(tx.txOuts);

		//ensure transaction hasn't been tampered with
		if (getTxId(tx) != tx.id)
			return false;
		
		//ensure there is both at least 1 txIn and 1 txOut
		if (tx.txIns.Length == 0 || tx.txOuts.Length == 0)
			return false;
		
		//validate txIns and txOuts
		long totalTxInAmounts = 0;
		foreach (var txIn in tx.txIns)
		{
			//we reject unsigned txIns
			if (txIn.signature is null)
				return false;
			
			//retrieve the corresponding uTxOut for the given txIn (from the input UtxOuts)
			UTxOut? uTxOutForTxIn = getUTxOut(txIn.txOutId, txIn.txOutIndex, uTxOuts);
			if (uTxOutForTxIn is null)
				return false;
			
			//verify the signature for the txIn is signed by the corresponding address
			if (!Cryptography.verifySignedData(txIn.signature, tx.id, uTxOutForTxIn.address))
				return false;
			
			//increase total amount of coins found for the txIns
			totalTxInAmounts += uTxOutForTxIn.amount;
		}

		long totalTxOutValues = 0;
		foreach (var txOut in tx.txOuts)
		{
			if (txOut.amount <= 0)
				return false; //every txOut amount must be positive
			totalTxOutValues += txOut.amount; //increase total amount of coins found for the txOuts
		}

		if (totalTxInAmounts != totalTxOutValues) //ensure total TxIn and TxOut amounts match
			return false;

		if (totalTxOutValues == 0)
			return false; //we don't accept transactions without any coins
	
		return true;
	}

	//verify whether the transaction is valid within the input transaction pool, and available uTxOuts
    public static bool isValidTransactionWithinPool(Transaction tx, List<Transaction> txPool, UTxOut[] uTxOuts)
    {
	    //we must first assert the transaction is even valid
	    if (!isValidTransaction(tx, uTxOuts))
		    return false;
	    
        //retrieve all TxIns from the transaction pool
        var txPoolIns = getTxInsFromTransactionContainer(txPool);
        
        //compare pool TxIns to the transaction's TxIns. If any match, the transaction isn't valid within the pool
        foreach (var txIn in tx.txIns)
        {
            foreach (var txPoolIn in txPoolIns)
            {
                if (TxIn.nonSignatureEqualityCompare(txIn, txPoolIn))
                    return false;
            }
        }

        return true;
    }

    /**
     * Returns whether the transaction meets requirements to be added to the mempool for this node. Note that
     * unlike the method *isValidTransactionWithinPool*, this method additionally asserts that local settings
     * (such as minimum miner fee) are satisfied. There's an optional parameter to ignore the pool size check
     * as specified in the node's settings file (default value of this is set to false)
     */
    public static bool doesTransactionMeetMemPoolAddRequirements(Transaction tx, 
	    List<Transaction> txPool, UTxOut[] uTxOuts, bool ignorePoolSize = false)
    {
	    //first make sure the mempool isn't full as specified in the node's settings (local node requirement)
	    if (!ignorePoolSize && txPool.Count >= Settings.maxMempoolSize)
		    return false;
	    
	    //next assert tx is valid within the context of the pool (protocol requirement)
	    if (!isValidTransactionWithinPool(tx, txPool, uTxOuts))
			return false;

	    //lastly assert the miner fee meets threshold requirements for this node (local node requirement)
	    return getMinerFeeFromTransaction(tx) >= Settings.minMinerFee;
    }

    public static long getTotalAmountFromTxOuts(TxOut[] TxOuts)
    {
        long total = 0;
        foreach (var TxOut in TxOuts)
            total += TxOut.amount;

        return total;
    }

    /**
     * Given a container of transactions and related UtxOuts, return an updated container of UtxOuts which removes any
     * spent ones from the input UTxouts, and adds new ones from the transactions TxOuts
     */
    public static UTxOut[] getUpdatedUTxOuts(Transaction[] txes, UTxOut[] currentUtxOuts)
    {
        var spentUtxOuts = new List<UTxOut>();
        var updatedUTxOuts = new List<UTxOut>();

        foreach (var tx in txes)
        {
            for (int i = 0; i < tx.txOuts.Length; i++)
            {
	            //don't include burnt coins used as miner fees in a new UTxOut (these will be received in coinbase tx)
	            if (tx.txOuts[i].address == Protocol.FEE_ADDRESS)
		            continue;
	            
                TxOut txout = tx.txOuts[i];
                updatedUTxOuts.Add(new UTxOut(tx.id, i, txout.address, txout.amount));
            }
            
            foreach (var txIn in tx.txIns)
            {
	            spentUtxOuts.Add(new UTxOut(txIn.txOutId, txIn.txOutIndex, null, 0));
            }
        }

        foreach (var uTxOut in currentUtxOuts)
        {
	        //if the uTxOut from the input uTxouts is consumed, we don't add it to the newUtxOuts. Otherwise, we do
            if (Transaction.getUTxOut(uTxOut.txOutId, uTxOut.txOutIndex, spentUtxOuts.ToArray()) is null)
                updatedUTxOuts.Add(uTxOut);
        }
        
        return updatedUTxOuts.ToArray();
    }
    
    /**
    * Examines the TxOuts in the Transaction where the receiving address is the protocol
    * fee address. The miner may claim these coins for themselves as the transaction reward, as part
    * of the coinbase transaction. Note: It's assumed the input transaction has been validated before
    * the return value from here is used. This function does not perform any validation
    */
    public static long getMinerFeeFromTransaction(Transaction transaction)
    {
	    long minerCoins = 0;
	    
	    foreach (var txOut in transaction.txOuts)
	    {
		    if (txOut.address == Protocol.FEE_ADDRESS)
		    {
			    minerCoins += txOut.amount;
		    }
	    }

	    return minerCoins;
    }

    /**
     * Attempts to retrieve the public key from the input mined block. The caller is assumed to provide a correctly
     * mined block that contains a valid public key for the miner otherwise an exception may be thrown.
     */
    public static string getMinerPublicKeyFromBlock(Block block)
    {
	    return block.transactions[0].txOuts[0].address;
    }

    /**
     * Checks whether the input txOuts are valid or not
     */
    public static bool validateTxOuts(TxOut[] txOuts)
    {
	    foreach (var txOut in txOuts)
	    {
		    if (txOut.amount <= 0)
			    return false;

		    if (!validateAddressFormat(txOut.address))
			    return false;
	    }

	    return true;
    }

    public static bool validateAddressFormat(string? address)
    {
	    //addresses must be at least 1 character in length
	    if (address is null || address.Length < 1)
		    return false;

	    return true;
    }
    
    #region equality override
    public override bool Equals(object? o)
    {
	    if (o is null || o.GetType() != typeof(Transaction))
		    return false;
	    Transaction other = (Transaction)o;

	    if (getTxId(this) == getTxId(other) && this.id == other.id && this.isCoinbaseTx == other.isCoinbaseTx)
		    return true;

	    return false;
    }

    public static bool operator == (Transaction t1, Transaction t2)
    {
	    return t1.Equals(t2);
    }

    public static bool operator != (Transaction t1, Transaction t2)
    {
	    return !(t1 == t2);
    }
    
    public override int GetHashCode()
    {
	    return getTxId(this).GetHashCode(); //hash code for the dictionary storage of tx keys in the blockchain
    }
	
    #endregion equality override
}