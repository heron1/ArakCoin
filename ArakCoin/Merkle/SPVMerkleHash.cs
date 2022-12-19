namespace ArakCoin.Data;

/**
 * For client Simple Payment Verification, nodes need only send a small subset of hashes (log n) to clients of a block
 * containing "n" hashes representing nodes in the merkle transaction tree. This class represents one of these hashes,
 * and provided the client has an ordered container of them, they can re-calaculate the merkle root to verify their
 * queried transaction is indeed part of the merkle tree, without obtaining the full merkle tree or a full list of
 * transactions from the node.
 */
public class SPVMerkleHash
{
    public string hash; //hash of this node
    public byte siblingSide; //the side this node's sibling is on. 0 is left, 1 is right
    public int blockId; //the block index this SPV belongs to
    
    public SPVMerkleHash(string hash, byte siblingSide, int blockId)
    {
        if (hash is null)
            throw new ArgumentException("hash value cannot be null");
        if (siblingSide != 0 && siblingSide != 1)
            throw new ArgumentException("siblingSide must be 0 or 1");
        if (blockId <= 0)
            throw new ArgumentException("blockId cannot be <= 0");
        
        this.hash = hash;
        this.siblingSide = siblingSide;
        this.blockId = blockId;
    }
}