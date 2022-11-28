global using static TestSuite.Globals;
using System;

namespace TestSuite;

public static class Globals
{
	public static string testPublicKey = "e4d4f90ebdb65b2601a994445b6191c734edeaa5003a2b96747bbfe475dbf790";
	public static string testPrivateKey = "42457c05923b617205e07e211289217cb28b494aee922ff968f226ed17e35d20";

	public static string testPublicKey2 = "cf5c9b7fb7270f0edc2492c01666ab2dd70c078fbceab1c74109f756f81a0d57";
	public static string testPrivateKey2 = "f2898d8b1db6a7c4a2661ec78dd4883a2b126e7bf24af6e6ec941c767bf2f83b";

	public static string testPublicKey3 = "de331ff8ad72b556d2b5aa7dc2043635482b50e4a3588c5c3b9ab85e0a2dca26";
	public static string testPrivateKey3 = "8a17b9aa9e3db4b46265840cd888de2805dda1d0023b5c441745b54058d85675";

	//display unit test messages to console
	public static void LogTestMsg(string msg)
	{
		Console.Error.WriteLine(msg);
	}
}