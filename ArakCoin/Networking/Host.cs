using System.Net;
using System.Text.RegularExpressions;

namespace ArakCoin.Networking;

/**
 * Class to represent a correctly formatted identifier for a network host, containing its ipv4 address and port.
 * Does not validate the host actually exists or is legal. Will throw an exception if instantiated with
 * invalid arguments. This class can be extended with additional fields in the future should they be needed.
 */
public class Host
{
    public string ip;
    public int port;
    
    public Host(string ip, int port)
    {
        //sanitize ip
        if (!isIpFormatValid(ip))
            throw new ArgumentException("ip is invalid", nameof(ip));
        
        //sanitize port
        if (port < 0 || port > 65535)
            throw new ArgumentException("port is invalid", nameof(port));
        
        this.ip = ip;
        this.port = port;
    }

    public static bool isIpFormatValid(string ip)
    {
        //this is my own regex as online solutions I found didn't seem to properly validate the IP. Also 
        //using the native IPAddress.Parse(ip) method results in invalid ips being parsed. This will require
        //testing to see if it works in all scenarios. Alternatively a better online solution may be found
        string pattern = @"^((?!0)[\d]{1,3}\.){3}(?!0)[\d]{1,3}$";
        //timeout match within 10ms to prevent a DOS string from being parsed
        var match = Regex.Matches(ip, pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(10));
        if (match.Count != 1) //assert a single match
            return false;
        //we can now safely divide the string into four octets, and assert each is <= 255.
        //Ensuring non-zero start value in each octet was already done in the regex
        string[] ipOctets = ip.Split(".");
        foreach (var octet in ipOctets)
        {
            //assert the octet is <= 255
            int octetNum;
            if (!Int32.TryParse(octet, out octetNum) || octetNum > 255)
                return false;
        }

        return true;
    }

    public override string ToString()
    {
        return $"{this.ip}:{this.port}";
    }
    
}