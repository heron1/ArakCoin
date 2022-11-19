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
            string specialAppFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appdataFolder = Path.Combine(specialAppFolder, "ArakCoin");
            
            if (!Directory.Exists(specialAppFolder))
                Directory.CreateDirectory(specialAppFolder);
            
            if (!Directory.Exists(appdataFolder))
                Directory.CreateDirectory(appdataFolder);
            
            File.WriteAllText(Path.Combine(appdataFolder, filename), data);
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
            string appdataFolder = 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArakCoin");
            
            if (!Directory.Exists(appdataFolder))
                Directory.CreateDirectory(appdataFolder);
            
            text = File.ReadAllText(Path.Combine(appdataFolder, filename));
        }
        catch
        {
            return null;
        }

        return text;
    }
}