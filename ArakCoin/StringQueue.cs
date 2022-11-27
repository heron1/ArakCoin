namespace ArakCoin;

/**
 * Custom helper class to write string output to a circular queue for easy printing when desired
 */
public static class StringQueue
{
    private static int maxSize = 200;
    private static int currIndex = 0;
    private static bool overlap = false; //has the queue already been fully written to?
    private static string[] strQueue = new string[maxSize];
    private static readonly object queueLock = new object(); //lock for this object

    public static void addToQueue(string s)
    {
        lock (queueLock)
        {
            strQueue[currIndex++] = s;
            if (currIndex == maxSize)
            {
                overlap = true;
                currIndex = 0;
            }  
        }
    }

    //returns a list of strings which are ordered from the first to last elements written to the queue
    public static List<string> retrieveOrderedQueue()
    {
        lock (queueLock)
        {
            var returnedQueue = new List<string>();
        
            //first write from currIndex + 1 to maxSize if queue has already overlapped
            if (overlap) 
            {
                for (int i = currIndex + 1; i < maxSize; i++)
                    returnedQueue.Add(strQueue[i]);
            }
        
            //now write from 0th to current index
            for (int i = 0; i <= currIndex; i++)
            {
                returnedQueue.Add(strQueue[i]);
            }

            return returnedQueue;
        }
    }
}