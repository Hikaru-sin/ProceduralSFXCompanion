using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ProceduralSFXCompanion.Services;

public class VideoService
{
    public void PlayVideo(string filePath)
    {
        if (File.Exists(filePath))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", filePath);
            }
        }
    }
}