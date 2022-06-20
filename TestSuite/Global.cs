global using static TestSuite.Global;

namespace TestSuite;

public static class Global
{
	public static void LogTestMsg(string msg)
	{
		TestContext.WriteLine(msg);
		Console.WriteLine(msg);
	}
}