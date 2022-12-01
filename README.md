ArakCoin is intended to be a Proof of Work Peer-To-Peer distributed UTXO 
based blockchain written from scratch in C#. 

This is a solo project to serve as an educational tool for
myself in making a blockchain. I make no guarantee as to its suitability in a real
world environment. It's currently undergoing development.

The current revision has a working command line interface that can be used
on both Windows & Linux by hosts wishing to be either clients or nodes.


 # Compilation Instructions
Currently .NET 6 SDK will need to be installed on either
Windows or Linux. Then enter the following commands:  
1) git clone https://github.com/heron1/ArakCoin.git  
2) dotnet build ArakCoin.sln (from within cloned directory)  
3) Run in terminal: ./ArakCoinCLI/bin/Debug/net6.0/ArakCoinCLI

Execution of Tests:  
   dotnet test --filter TestCategory="UnitTests  
   dotnet test --filter TestCategory="IntegrationTests"  
   dotnet test --filter TestCategory="FunctionalTests"
  
Specific tests can be run as follows:  
dotnet test --filter "*TestName*"

 # Storage
The program will be installed within the cloned directory,
however user files are stored in the application user
directory that is OS specific:  

Windows 10/11: %appdata%/ArakCoin  
Linux: ~/.config/ArakCoin


