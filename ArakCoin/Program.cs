global using System;
global using System.Security.Cryptography;

namespace ArakCoin
{
	internal class Program
	{
		static void Main(string[] args)
		{
			// Console.WriteLine($"{}");
			Block b = Utilities.createGenesisBlock();
			Console.WriteLine(b.calculateBlockHash());
			Console.WriteLine(b.timestamp);

			

		}

		
	}
	
	

}