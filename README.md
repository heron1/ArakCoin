ArakCoin is intended to be a Proof of Work Peer-To-Peer distributed UTXO 
based blockchain written from scratch in C#. 

This is a solo project to serve as an educational tool for
myself in making a blockchain. I make no guarantee as to its suitability in a real
world environment. It's currently undergoing development.

The current revision has a working command line interface that can be used
on both Windows & Linux by hosts wishing to be either clients or nodes.

A GUI also exists for Windows 10+. It's recommended to use the pre-built binary for installation.

# Binary Installation instructions (recommended)
Currently a command line UI exists for both Windows & Linux, and a GUI for Windows 10+.

Download corresponding pre-built binary package from: https://github.com/heron1/ArakCoin/releases/tag/release-prototype

**Windows x64 CLI**

1) Download *ArakCoin-CLI_Windows_x64.zip*
2) unzip, run *ArakCoinCLI.exe*

**Windows 10 GUI (desktop)**
1) Download *ArakCoin-GUI_Windows10.zip*
2) unzip, right click *Install.ps1* and select "Run With Powershell"
3) Follow the prompt to install the key
4) Launch the ArakCoin.msix package

**Linux x64 CLI**
1) Download *ArakCoin-CLI_Linux_x64.tar* 
2) extract via the command: *tar -xf ArakCoin-CLI_Linux_x64.tar*
3) Enter the created directory and execute the program: *./ArakCoinCLI*

**Linux arm (eg: raspberry pi) CLI**
1) Download *ArakCoin-CLI_Linux_arm.tar*
2) extract via the command: *tar -xf ArakCoin-CLI_Linux_arm.tar*
3) Enter the created directory and execute the program: *./ArakCoinCLI*


# Manual Compilation Instructions (from source)
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

To manually compile the Windows 10 GUI, load the solution in Visual Studio, right 
click the ArakCoin-GUI project, select "Deploy". After that right click it again and select "Publish".
Generate a self-signed key and add it to the Trusted Persons key list. Run the generated .msix package

 # Getting Started
Ensure there is at least 1 starting node on the network that your host can communicate with. This can be added from the CLI
via selecting option "5", or within the initial startup prompt. For the GUI, select "Manage Nodes" from the left
side menu, and enter either a Hardcoded Node or Network Node as the starting node.

If you wish to be a node, ensure you enter a publicly accessible ipv4 address and port for sockets connections. Inbound TCP connections
must be possible for the details entered.

If wishing to be a miner, you must also be a node. All settings are adjustable, including the 
minimum miner fee to accept transactions, as well as the max mempool size. Note that incoming transactions will always be
prioritized in the mempool based upon their miner fee.

If wishing to be a client (not performing any node or miner operations), then simply
select to not be a node or a miner (for the CLI, this is set at first time application startup. Within the GUI it's
set within Settings on the left side menu).

 # Storage
The program will be installed within the cloned directory,
however user files are stored in the application user
directory that is OS specific:  

Windows 10/11: %appdata%/ArakCoin  
Linux: ~/.config/ArakCoin


