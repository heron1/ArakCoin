namespace ArakCoin;

/**
 * Wrapper class for I/O on this node's local computer disk
 */
public static class Storage
{
    //returns false if failure
    public static bool writeJsonToDisk(string data, string filename)
    {
        try
        {
            File.WriteAllText(filename, data);
        }
        catch
        {
            return false;
        }

        return true;
    }

    //return null if failure
    public static string? readJsonFromDisk(string filename)
    {
        string text;
        try
        {
            text = File.ReadAllText(filename);
        }
        catch
        {
            return null;
        }

        return text;
    }
}