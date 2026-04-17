using System.IO;

namespace ProceduralSFXCompanion.Models;

public class GraphDescription
{
    public string FileName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long LastModified { get; set; }

    public bool CanPreview { get; set; } = false;
}