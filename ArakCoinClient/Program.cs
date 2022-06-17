using ArakCoin;

namespace ArakCoinClient
{
	internal class Program
	{
		static void Main(string[] args)
		{
			var p = new ArakCoin.g();
			Console.WriteLine($"Hello From ArakCoinClient! Communicating with: {p.a}");
			
		}
	}
}