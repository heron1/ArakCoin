using ArakCoin;

namespace ArakCoinNode
{
	internal class Program
	{
		static void Main(string[] args)
		{
			var p = new ArakCoin.g();
			Console.WriteLine($"Hello From ArakCoinNode! Communicating with: {p.a}");
			
		}
	}
}