using System.Net;
using System.Text.RegularExpressions;

namespace ArakCoin.Networking;

/**
 * Class to represent a correctly formatted identifier for a network host, containing its ipv4 address and port.
 * Performs format checking but does not validate the host actually exists or is legal.
 * Will throw an exception if instantiated with invalid arguments. This class can be extended with
 * additional fields in the future should they be needed.
 */
public class Host
{
    public string ip;
    public int port;
    
    public Host(string ip, int port)
    {
        //ip format check
        if (!isIpFormatValid(ip))
            throw new ArgumentException("ip is invalid", nameof(ip));
        
        //port format check
        if (port < 0 || port > 65535)
            throw new ArgumentException("port is invalid", nameof(port));
        
        this.ip = ip;
        this.port = port;
    }

    //returns whether this host has valid formatting or not
    public bool validateHostFormatting()
    {
        //ip format check
        if (!isIpFormatValid(ip))
            return false;
        
        //port format check
        if (port < 0 || port > 65535)
            return false;

        return true;
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
    
    
    #region equality override
    public override bool Equals(object? o)
    {
        if (o is null || o.GetType() != typeof(Host))
            return false;
        Host other = (Host)o;

        if (this.ip == other.ip && this.port == other.port)
            return true;

        return false;
    }

    public static bool operator == (Host t1, Host t2)
    {
        return t1.Equals(t2);
    }

    public static bool operator != (Host t1, Host t2)
    {
        return !(t1 == t2);
    }

    #endregion equality override
    
}