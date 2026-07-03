using System.Collections.Generic;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.Models;

public class AppSettings
{
    public Dictionary<string, FolderItem> FolderPaths { get; set; } = new();
    public Dictionary<string, FolderItem> EditingFolderPaths { get; set; } = new();
    public string? AudioFolderToCopyPath { get; set; }
    
    public WebTutorialsFetch? TutorialsFetch { get; set; }
    public DownloadLink DefaultAssetLink { get; set; } = new DownloadLink() { Version = Constants.DefaultAssetVersion };
    
    public bool SearchMergeOnPlayTriggers { get; set; } = true;
    public bool SearchEncloseWithComment { get; set; } = true;
    public bool SearchMergeSameTimes { get; set; } = false;
    public bool SearchMergeSameFloats{ get; set; } = false;
}