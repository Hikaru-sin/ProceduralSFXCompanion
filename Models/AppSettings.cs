using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.Models;

public class AppSettings
{
    public Dictionary<string, FolderItem> FolderPaths { get; set; } = new();
    public Dictionary<string, FolderItem> EditingFolderPaths { get; set; } = new();
    public string? AudioFolderToCopyPath { get; set; }
    
    public WebTutorialsFetch? TutorialsFetch { get; set; }
    public DownloadLink DefaultAssetLink { get; set; } = new DownloadLink() { Version = Constants.DefaultAssetVersion };
}