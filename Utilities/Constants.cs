using System;

namespace ProceduralSFXCompanion.Utilities;

public static class Constants
{
    public const string DictionariesFolder = "Resources\\Dictionaries";
    public const string DefaultGraphFolder = "Resources\\Default Graphs";
    public const string DescriptionExtension = ".txt";
    public const string GraphExtension = ".msresp";
    public const string AudioExtension = ".wav";

    public const string LatestAppVerUrl = "https://api.github.com/repos/Hikaru-sin/ProceduralSFXCompanion/releases";
    public const string WebTutorialsUrl = "https://www.proceduralsfx.com/_functions/latestTutorials";
    
    public const string LatestDefaultAsset = "https://www.proceduralsfx.com/_functions/latestMetaSoundsDefaultAsset";
    //IMPORTANT: This must be the same as the version of the default bundled asset when releasing the app.
    public const int DefaultAssetVersion = 0;  
    
    public const string UngroupTutorial = "Uncategorized";
    
    public static readonly Version? AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
}