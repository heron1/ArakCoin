namespace ArakCoin.Data;

/**
 * A helper class for accessing elements in a binary tree that is represented as a standard array. Will return null
 * if the input node cannot be found within the Binary Tree container, or the desired return node doesn't exist
 */
public static class BinaryTreeArrayHelpers
{
    private static int? getIndexOfNode<T>(T? node, T[]? nodeList) where T : class
    {
        if (node is null || nodeList is null)
            return null;
        
        for (int i = 0; i < nodeList.Length; i++)
        {
            if (node.Equals(nodeList[i]))
                return i;
        }

        return null;
    }

    public static T? getParent<T>(T node, T[] nodeList) where T : class
    {
        int? nodeIndex = getIndexOfNode<T>(node, nodeList);
        if (nodeIndex is null)
            return null;

        if (nodeIndex == 0)
            return null; //index 0 is already the parent, so this node has no parent

        return nodeList[((int)nodeIndex - 1) / 2];
    }
    
    public static T? getLeftChild<T>(T node, T[] nodeList) where T : class
    {
        int? nodeIndex = getIndexOfNode<T>(node, nodeList);
        if (nodeIndex is null)
            return null;

        if (2 * nodeIndex + 1 >= nodeList.Length) //left child is out of range
            return null;
        
        return nodeList[2 * (int)nodeIndex + 1];
    }
    
    public static T? getRightChild<T>(T node, T[] nodeList) where T : class
    {
        int? nodeIndex = getIndexOfNode<T>(node, nodeList);
        if (nodeIndex is null)
            return null;

        if (2 * nodeIndex + 2 >= nodeList.Length) //right child is out of range
            return null;

        return nodeList[2 * (int)nodeIndex + 2];
    }
    
    public static T? getLeftSibling<T>(T node, T[] nodeList) where T : class
    {
        int? nodeIndex = getIndexOfNode<T>(node, nodeList);
        if (nodeIndex is null)
            return null;

        if (nodeIndex % 2 == 1 || nodeIndex == 0) //left sibling is out of range
            return null;

        return nodeList[(int)nodeIndex - 1];
    }
    
    public static T? getRightSibling<T>(T node, T[] nodeList) where T : class
    {
        int? nodeIndex = getIndexOfNode<T>(node, nodeList);
        if (nodeIndex is null)
            return null;

        if (nodeIndex % 2 == 0 || nodeIndex + 1 >= nodeList.Length) //right sibling is out of range
            return null;

        return nodeList[(int)nodeIndex + 1];
    }

    //note we return the sibling along with a byte object. A "0" value in the byte object means the sibling is located
    //to the left of the input node, and a "1" value indicates it's located to the right of the input node
    public static (T node, byte position)? getSibling<T>(T node, T[] nodeList) where T : class
    {
        int? nodeIndex = getIndexOfNode<T>(node, nodeList);
        if (nodeIndex is null)
            return null;

        T? sibling = getLeftSibling(node, nodeList);
        if (sibling is not null)
            return (sibling, 0); //returns the left sibling

        sibling = getRightSibling(node, nodeList);
        if (sibling is not null)
            return (sibling, 1); //returns the right sibling
        
        return null; //no sibling exists
    }
}