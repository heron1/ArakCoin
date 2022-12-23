using ArakCoin.Transactions;

namespace ArakCoin.Data;

/**
 * Contains various functions related to merkle trees
 */
public static class MerkleFunctions
{
	/**
	 * Build the merkle tree for the given input array of transactions (assumes each tx has a valid & verified id)
	 */
    public static string[] buildMerkleTree(Transaction[] blockTxes)
    {
	    //check for the special case that there is only a single transaction. This transaction id is always the
	    //merkle root
	    if (blockTxes.Length == 1)
		    return new string[] { blockTxes[0].id };
	    
	    //check there is at least a single transaction, otherwise we return an empty string list for the tree
	    if (blockTxes.Length == 0)
		    return new string[] {};
	    
	    List<List<string?>> merkleLevels = new();
	    List<string?> merkleLevel = new();
	    List<string?> nextMerkleLevel = new();
	    foreach (var tx in blockTxes)
	    {
		    if (tx is null || tx.id is null)
			    return new string[] {}; //return an empty tree if any tx is lacking an id
		    merkleLevel.Add(tx.id);
	    }
	    //we must populate null values into the base merkleLevel until its length is a number that is a power of 2.
	    //This is to allow the creation of a complete binary tree that our algorithm depends on
	    int intendedLength = (int)Math.Pow(2, (int)Math.Ceiling(Math.Log2(merkleLevel.Count)));
	    while (merkleLevel.Count < intendedLength)
		    merkleLevel.Add(null);
		
	    merkleLevels.Add(merkleLevel.ToList());

	    while (true)
	    {
		    for (int i = 0; i < merkleLevel.Count; i += 2)
		    {
			    if (i + 1 < merkleLevel.Count && merkleLevel[i+1] is not null)
			    {
				    nextMerkleLevel.Add(Utilities.calculateSHA256Hash(merkleLevel[i] + merkleLevel[i + 1]));
			    }
			    else
			    {
				    //if no sibling, we pass up the current hash
				    nextMerkleLevel.Add(merkleLevel[i]);
			    }
		    }
                                     
		    if (nextMerkleLevel.Count == 1) //root node reached
		    {
			    merkleLevels.Add(nextMerkleLevel.ToList());
			    break;
		    }
			
		    if (nextMerkleLevel.Count % 2 != 0)
			    nextMerkleLevel.Add(null); //make leaf count even

		    merkleLevels.Add(nextMerkleLevel.ToList());
		    merkleLevel = nextMerkleLevel;
		    nextMerkleLevel = new();
	    }

	    List<string?> merkleTreeList = new();
	    merkleLevels.Reverse();
	    foreach (var level in merkleLevels)
	    {
		    foreach (var node in level)
		    {
			    merkleTreeList.Add(node);
		    }
	    }

	    return merkleTreeList.ToArray();
    }

	/**
	 * Calculate the merkle root from the given merkle tree. This is an O(1) operation
	 */
    public static string getMerkleRoot(string[] merkleTree)
	{
		if (merkleTree.Length == 0)
			return ""; //return an empty string if no elements in the merkle tree
		
	    return merkleTree[0]; //the first element of the merkle tree is the merkle root
    }

	/**
	 * Calculate the merkle root from the given transaction array
	 */
    public static string getMerkleRoot(Transaction[] blockTxes)
    {
	    var merkleTree = buildMerkleTree(blockTxes);
	    return getMerkleRoot(merkleTree);
    }

	/**
	 * Given the input transaction, find its block within the global blockchain and calculate the minimal subset of
	 * hashes within the block's transactions (represented as a merkle tree) required to calculate the merkle root
	 * from scratch. This should have a space complexity of O(lg n) of the merkle tree size "n". This is useful to send
	 * to clients desiring Simple Payment Verification without them requiring a complete copy of the block's
	 * transactions, but being able to prove to themselves that the block does indeed contain the input
	 * transaction (or not) by calculating the merkle root themselves, but with only a partial subset of the merkle
	 * tree hashes.
	 *
	 * Returns null if the transaction cannot be found in the blockchain, or calculating the minimal subset of the
	 * merkle tree hashes fails for whatever reason. If the tx is the only one within the block's transactions,
	 * then the hash return array is simply empty (indicating that the input transaction id should in fact be equal
	 * to the merkle root)
	 */
	public static SPVMerkleHash[]? calculateMinimalVerificationHashesFromTx(Transaction tx)
	{
		if (tx.id is null)
			return null;

		Block? foundBlock;
		int? blockIndex;
		lock (Globals.masterChain.blockChainLock)
		{
			blockIndex = Globals.masterChain.getBlockIdFromTxId(tx.id);
			if (blockIndex is null)
				return null;

			foundBlock = Globals.masterChain.getBlockByIndex((int)blockIndex);
			if (foundBlock is null)
				return null;
		}

		List<SPVMerkleHash> merkleHashes = new(); //the minimal merkle hashes to be sent to the client we will build

		//we build the tree, and populate the merkleHashes
		string[] merkleTree = buildMerkleTree(foundBlock.transactions);
		
		string lastNode = tx.id; //we initialize the beginning node as the input tx id hash
		
		//now begin the recursive operation to populate the hashes from siblings whilst moving up the tree 
		while (true)
		{
			var parent = BinaryTreeArrayHelpers.getParent(lastNode, merkleTree);
			if (parent is null) //we have possibly arrived at the root merkle node
			{
				//verify we are indeed at the root node, if not return null
				if (foundBlock.merkleRoot != lastNode)
					return null;

				return merkleHashes.ToArray();
			}

			//retrieve the sibling
			var sibling = BinaryTreeArrayHelpers.getSibling(lastNode, merkleTree); 
			if (sibling is null) 
				return null; //a sibling should never be null
			
			//add the sibling to the minimal required hashes to calculate the merkle root
			merkleHashes.Add(new SPVMerkleHash(sibling.Value.node, sibling.Value.position, (int)blockIndex));
			
			lastNode = parent; //we move up the tree
		}
	}

	/**
	 * Given our desired transaction to validate, and an ordered container of merkle hashes
	 * (within the SPVMerkleHash object), recursively calculate the hashes to derive the merkle root of the
	 * full merkle tree, even though it's only partially known from the spvMerkleHashes
	 */
	public static string calculateMerkleRootFromMinimalVerificationHashes(
		Transaction tx, SPVMerkleHash[] spvMerkleHashes)
	{
		if (tx.id is null)
			return "";
		
		string hashBuilder = tx.id;

		for (int i = 0; i < spvMerkleHashes.Length; i++)
		{
			//determine side for the calculation, and concatenate the two hashes
			string concatenatedHash = spvMerkleHashes[i].siblingSide == 0 ? 
				$"{spvMerkleHashes[i].hash}{hashBuilder}" : 
				$"{hashBuilder}{spvMerkleHashes[i].hash}";

			hashBuilder = Utilities.calculateSHA256Hash(concatenatedHash);
		}

		return hashBuilder; //our purported merkle root
	}
}