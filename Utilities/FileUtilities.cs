using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
namespace ProceduralSFXCompanion.Utilities;

public static class FileUtilities
{
    public static Task<string> ReadFirstSentenceAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) 
                return Task.FromResult(string.Empty);

            StringBuilder sb = new StringBuilder();
            using (var reader = new StreamReader(filePath))
            {
                int charCode;
                while ((charCode = reader.Read()) != -1)
                {
                    char c = (char)charCode;
                    sb.Append(c);
                    if (c == '\n' || c == '\r')
                        break;
                }
            }
            
            return Task.FromResult(sb.ToString().Trim());
        }
        catch 
        {
            // ignored
        }

        return Task.FromResult(string.Empty);
    }
    
    public static List<FileInfo> GetFilesByCreationDate(string directoryPath, string extension = Constants.DescriptionExtension)
    {
        if (!Directory.Exists(directoryPath))
            return [];
        
        DirectoryInfo directory = new DirectoryInfo(directoryPath);
        List<FileInfo> sortedFiles = directory.GetFiles($"*{extension}", SearchOption.TopDirectoryOnly)
                                     .OrderBy(f => f.CreationTime)
                                     .ToList();

        return sortedFiles;
    }
    
    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr apidl, int dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);
    
    public static void ShowInFolder(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) 
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr pidl = ILCreateFromPath(filePath);
                if (pidl != IntPtr.Zero)
                {
                    try
                    {
                        // Use shell command for scrolling accuracy, also it is actually faster than using process
                        // cidl = 0 and apidl = null tells it to select the file pointed to by pidlFolder
                        // and automatically scroll it into view.
                        SHOpenFolderAndSelectItems(pidl, 0, IntPtr.Zero, 0);
                    }
                    finally
                    {
                        ILFree(pidl);
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: 'open -R' reveals the file in Finder
                Process.Start("open", $"-R \"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: Most distros use dbus to highlight files, 
                // but opening the parent folder is the most reliable fallback.
                var directory = Path.GetDirectoryName(filePath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{directory}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}