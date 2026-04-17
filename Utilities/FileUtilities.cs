using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    if (c == '.' || c == '\n' || c == '\r')
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
    
    public static List<FileInfo> GetFilesByCreationDate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return [];
        
        DirectoryInfo directory = new DirectoryInfo(directoryPath);
        List<FileInfo> sortedFiles = directory.GetFiles("*.txt", SearchOption.TopDirectoryOnly)
                                     .OrderBy(f => f.CreationTime)
                                     .ToList();

        return sortedFiles;
    }
}