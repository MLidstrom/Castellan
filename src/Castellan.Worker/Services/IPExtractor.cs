using System.Net;
using System.Text.RegularExpressions;

namespace Castellan.Worker.Services;

public static class IPExtractor
{
    // Regex pattern to match IPv4 addresses
    private static readonly Regex IPv4Pattern = new(
        @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        RegexOptions.Compiled);

    // Common field names that contain IP addresses in Windows Event Logs
    private static readonly string[] IPFieldNames = 
    {
        "SourceAddress", "Source Network Address", "Client Address", "Client IP",
        "TargetAddress", "Target Network Address", "Server IP", "Server Address",
        "RemoteAddress", "Remote IP", "Source IP", "Target IP",
        "WorkstationName", "Workstation", "Computer", "Client Name"
    };

    /// <summary>
    /// Extracts all unique valid IP addresses from log event message
    /// </summary>
    public static List<string> ExtractIPAddresses(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new List<string>();

        var matches = IPv4Pattern.Matches(message);
        var ipAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var ip = match.Value;
            
            // Validate that it's a real IP address
            if (IPAddress.TryParse(ip, out var ipAddr) && ipAddr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                // Exclude obviously invalid addresses
                if (!IsInvalidIP(ip))
                {
                    ipAddresses.Add(ip);
                }
            }
        }

        return ipAddresses.ToList();
    }

    /// <summary>
    /// Extracts IP addresses specifically from Windows Event Log authentication events
    /// </summary>
    public static List<string> ExtractAuthenticationIPs(string message, int eventId)
    {
        var allIPs = ExtractIPAddresses(message);
        
        // For authentication events, focus on source/client addresses
        if (eventId == 4624 || eventId == 4625) // Logon events
        {
            // Try to find IPs near source/client indicators
            var sourceIPs = new List<string>();
            var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Look for lines containing source address indicators
                if (ContainsSourceIndicator(trimmedLine))
                {
                    var ips = ExtractIPAddresses(trimmedLine);
                    sourceIPs.AddRange(ips);
                }
            }
            
            // If we found source-specific IPs, prefer those
            if (sourceIPs.Any())
            {
                return sourceIPs.Distinct().ToList();
            }
        }
        
        return allIPs;
    }

    /// <summary>
    /// Gets the most relevant IP address from a list for security analysis
    /// </summary>
    public static string? GetPrimaryIP(List<string> ipAddresses)
    {
        if (!ipAddresses.Any())
            return null;

        // Prefer public IPs over private IPs for threat analysis
        var publicIPs = ipAddresses.Where(ip => !IsPrivateIP(ip)).ToList();
        if (publicIPs.Any())
        {
            return publicIPs.First();
        }

        // If only private IPs, return the first non-localhost
        var nonLocalhost = ipAddresses.Where(ip => ip != "127.0.0.1" && !ip.StartsWith("127.")).ToList();
        if (nonLocalhost.Any())
        {
            return nonLocalhost.First();
        }

        // Fallback to first IP
        return ipAddresses.First();
    }

    /// <summary>
    /// Checks if an IP address is in private ranges (RFC 1918)
    /// </summary>
    private static bool IsPrivateIP(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        
        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;
            
        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;
            
        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
            
        // 127.0.0.0/8 (loopback)
        if (bytes[0] == 127)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if the line contains source address indicators
    /// </summary>
    private static bool ContainsSourceIndicator(string line)
    {
        var lowerLine = line.ToLowerInvariant();
        
        return lowerLine.Contains("source network address") ||
               lowerLine.Contains("source address") ||
               lowerLine.Contains("client address") ||
               lowerLine.Contains("client ip") ||
               lowerLine.Contains("source ip") ||
               lowerLine.Contains("workstation name") ||
               lowerLine.Contains("client name");
    }

    /// <summary>
    /// Checks for obviously invalid IP addresses
    /// </summary>
    private static bool IsInvalidIP(string ip)
    {
        // Exclude common invalid patterns
        return ip == "0.0.0.0" ||
               ip == "255.255.255.255" ||
               ip.StartsWith("0.") ||
               ip.EndsWith(".0") ||
               ip.EndsWith(".255");
    }
}
