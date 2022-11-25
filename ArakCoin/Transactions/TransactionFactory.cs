using static ArakCoin.Transactions.Transaction;

namespace ArakCoin.Transactions;

public static class TransactionFactory
{
    //add an additional txOut to a txOut array for a miner fee. Not signed
    public static TxOut[] addMinerFeeToTxOutArray(TxOut[] txOuts, long minerFee)
    {
        TxOut feeTx = new TxOut(Protocol.FEE_ADDRESS, minerFee);
        return new TxOut[] { feeTx }.Concat(txOuts).ToArray();
    }
    
    /**
     * This function is the recommended way to create new transactions for a blockchain
     * 
     * Attempts to create a new transaction with the given txOuts, signing them with the given privateKey and
     * then adding the transaction to the given blockchain's mempool. Returns the tx on succes, otherwise null
     */
    public static Transaction? createNewTransactionForBlockchain(TxOut[] txOuts, string privateKey,
        Blockchain bchain, long minerFee = 0, bool addToMemPool = true)
    {
        lock (bchain.blockChainLock)
        {
            Transaction? tx = createTransaction(txOuts, privateKey, bchain.uTxOuts, 
                bchain.mempool, minerFee, addToMemPool);
            return tx;
        }
    }
    
    /**
     * WARNING: Use the *createNewTransactionForBlockchain* for creating new transactions for a blockchain - this will
     * ensure a lock exists to protect against mutating containers from other threads. This method does not do that.
     * This method here should only be used directly in a synchronous environment if not acquiring a blockchain lock.
     * 
     * Attempt to create transaction given some txouts. If fails, returns null. Note: This will mutate the
     * input mempool to include the new transaction if successful by default, however
     * only if the transaction can be legally added to the mempool, otherwise null is returned.
     * Set addToMemPool to false to simply create a transaction only and return it without any mutation
     */
    public static Transaction? createTransaction(TxOut[] txOuts, string privateKey,
        UTxOut[] uTxOuts, List<Transaction> mempool, 
        long minerFee = 0, bool addToMemPool = true)
    {
        //add the miner fee to this transaction if it's not 0
        if (minerFee != 0)
        {
            txOuts = addMinerFeeToTxOutArray(txOuts, minerFee);
        }
        
        long totalTxOutAmount = getTotalAmountFromTxOuts(txOuts); //sum of all coins in the TxOuts
        if (totalTxOutAmount == 0)
            return null; //a transaction containing no coins is considered invalid
        
        string? publicKey = Cryptography.getPublicKeyFromPrivateKey(privateKey);
        if (publicKey is null)
            return null;
        
        var myUtxOuts = new List<UTxOut>();
        foreach (var uTxOut in uTxOuts)
        {
            if (uTxOut.address == publicKey)
            {
                myUtxOuts.Add(uTxOut);
            }
        }
        
        //create a new container of spendable uTxOuts, filtered from spent Utxouts currently in the mempool
        var spendableUTxOuts = new List<UTxOut>();
        var spentUtxOuts = new List<UTxOut>();

        var txpIns = Transaction.getTxInsFromTransactionContainer(mempool);
        
        foreach (var utxo in myUtxOuts)
        {
            foreach (var txIn in txpIns)
            {
                if (txIn.txOutIndex == utxo.txOutIndex && txIn.txOutId == utxo.txOutId)
                    spentUtxOuts.Add(utxo);
            }
        }
        
        foreach (var utxo in myUtxOuts)
        {
            if (!spentUtxOuts.Contains(utxo))
            {
                spendableUTxOuts.Add(utxo);
            }
        }
        
        //from the spendable utxouts, we only retrieve the ones needed for our desired spend amount. We also keep
        //track of the excess amount from the remaining utxout to send back to this tx creator's public key
        long foundAmount = 0;
        long excessAmount = 0;
        var requiredSpendableUTxOuts = new List<UTxOut>();
        foreach (var uTxOut in spendableUTxOuts)
        {
            requiredSpendableUTxOuts.Add(uTxOut);
            foundAmount += uTxOut.amount;
            //we break early once we acquire the txouts needed for the transaction amount
            if (foundAmount >= totalTxOutAmount)
            {
                excessAmount = foundAmount - totalTxOutAmount; //calculate excess
                break;
            }
        }
        
        //if we've consumed all the spendable utxouts and still don't have a balance to match our desired spend amount,
        //the transaction cannot be created
        if (foundAmount < totalTxOutAmount) //insufficient balance
            return null;

        //we now create a new unsigned TxIn for every corresponding UTxOut for our spend amount. These are
        //the txins that will be used when creating our transaction
        var txIns = new List<TxIn>();
        foreach (var utxo in requiredSpendableUTxOuts)
        {
            txIns.Add(new TxIn(utxo.txOutId, utxo.txOutIndex, null));
        }

        //add the excess amount as a new txOut back to ourselves
        if (excessAmount != 0)
        {
            TxOut excessTx = new TxOut(publicKey, excessAmount);
            txOuts = txOuts.Concat(new TxOut[] { excessTx }).ToArray();
        }
        
        //create our transaction
        Transaction tx = new Transaction(null, txIns.ToArray(),
            txOuts);
        tx.id = Transaction.getTxId(tx);
        
        //we now sign all of our TxIns with this transaction id
        foreach (var txIn in tx.txIns)
        {
            //first get corresponding uTxout for this txIn, assert it exists
            UTxOut? uTxOut = getUTxOut(txIn.txOutId, txIn.txOutIndex, requiredSpendableUTxOuts.ToArray()); 
            if (uTxOut is null || uTxOut.address != Cryptography.getPublicKeyFromPrivateKey(privateKey))
            {
                return null;
            }
		
            string? signature = Cryptography.signData(tx.id, privateKey);
            if (signature is null)
                return null;

            txIn.signature = signature;
        }

        if (addToMemPool)
        {
            //assert transaction can be legally added to mempool, and if so, do it, otherwise return null
            if (!Blockchain.addTransactionToMempoolGivenNodeRequirements(tx, mempool, uTxOuts))
                return null;
        }

        return tx;
    }

    //Creates and returns the coinbase transaction, given a list of block transactions. This should be the
    //first transaction that appears in a block. The rewardPublicKey is the address that should receive both the
    //coinbase miner reward, and also any fees from the listed transactions. Note this function does not perform any
    //validation - it is up to each blockchain node to validate the coinbase transaction as part of its
    //block validation process - the miner fees and coinbase reward must match exactly what's expected
    public static Transaction? createCoinbaseTransaction(string rewardPublicKey, int blockId, Transaction[]? transactions)
    {
        //the total reward begins with the coinbase miner reward as per the protocol
        long totalReward = Protocol.BLOCK_REWARD;

        if (transactions is not null)
        {
            foreach (var tx in transactions)
            {
                totalReward += Transaction.getMinerFeeFromTransaction(tx);
            }
        }

        Transaction coinbaseTx = new Transaction(null, new[] 
                { new TxIn($"cb{blockId}", 0, null) },
            new TxOut[] { new TxOut(rewardPublicKey, totalReward) }, true);
        coinbaseTx.id = Transaction.getTxId(coinbaseTx);

        return coinbaseTx;
    }
}