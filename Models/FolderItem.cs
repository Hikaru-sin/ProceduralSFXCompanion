using System.Text.Json.Serialization;

namespace ProceduralSFXCompanion.Models;

public class FolderItem
{
    [JsonIgnore]
    public string? Path
    {
        get;
        set
        {
            field = value?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            FolderName = System.IO.Path.GetFileName(field);
        }
    }

    public bool IsSelected { get; set; } = true;

    [JsonIgnore] public bool IsRemovable { get; set; } = true;
    
    [JsonIgnore] 
    public string? FolderName { get; private set; }
}