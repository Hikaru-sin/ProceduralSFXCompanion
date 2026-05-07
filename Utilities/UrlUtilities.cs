using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ProceduralSFXCompanion.Utilities;

public static class UrlUtilities
{
    public static void OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url.Replace("&", "^&")) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
    }
    
    public static string GetYouTubeId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";
        
        // This regex looks for the 11-character ID after v= or after the slash in youtu.be
        var regex = new Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})");
        var match = regex.Match(url);
    
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}