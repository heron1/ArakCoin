﻿ArakCoin is intended to be a Peer-To-Peer distributed UTXO 
based blockchain written from scratch in C#. 

This is a solo project to serve as an educational tool for
myself in making a blockchain. I make no guarantee as to its suitability in a real
world environment. It's currently undergoing development
and does not work, however unit tests can be run to 
test various isolated pieces of functionality

 # Compilation Instructions
Currently .NET 6 SDK will need to be installed on either
Windows or Linux. Then enter the following commands:  
1) git clone https://github.com/heron1/ArakCoin.git  
2) dotnet build ArakCoin.sln (from within cloned directory)  
3) Execution of Tests:  
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


