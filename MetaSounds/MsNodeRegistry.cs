using System;
using System.Collections.Generic;
using System.IO;
using ProceduralSFXCompanion.Utilities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProceduralSFXCompanion.MetaSounds;

public static class MsNodeRegistry
{
    public static readonly Regex ObjectRegex = new(
        @"^Begin Object[\s\S]*?^\s*End Object", 
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline
    );
    
    public static readonly Regex HeaderRegex = new(@"Class=([^ ]+)(?:\s+Name=""([^""]+)"")?(?:\s+ExportPath=""([^""]+)"")?", RegexOptions.Compiled);
    public static readonly Regex PinContentRegex = new(@"\((.*)\)", RegexOptions.Compiled);
    public static readonly Regex PinKeyValueRegex = new(@"([\w\.]+)=((?:\([^)]*\)|[^,])+)", RegexOptions.Compiled);
    
    private static readonly Regex ClassShortNameRegex = new("Name=\"([^\"]+)\"", RegexOptions.Compiled);

    public const int UnitGridSize = 16;
    public const int MajorGridSize = 8 * UnitGridSize;
    public const int PinGridSize = 3 *  UnitGridSize;
    
    public const string AllId2Class = "IdToClass";
    public const string AllClassDef = "ClassDefs";
    public const string AllNodes = "AllNodes";
    public const string AllConnections = "Connections";

    public const string ModalObjPath = "/ImpactSFXSynth/Audio/ImpactData/ModalObj/";
    public const string ImpModalCreatorClass = "ImpactModalCreator";
    public const string InputClassHeader = "/Script/MetasoundEditor.MetasoundEditorGraphInputNode";
    public const string NoConnectionPrefix = "n";
    public const string Coordinate = "Coordinate";
    
    public const string BreadcrumbPrefix = "Breadcrumb=";
    public const string ImpModalCreatorBreadcrumb =
        "Breadcrumb=(ClassName=(Namespace=\"LBS\",Name=\"Impact Modal Creator\"),NodeConfiguration=/Script/ImpactSFXSynth.ImpactModalCreatorNodeConfiguration(NumInputs=1))";

    public const double ImpModalCreatorMajorGridSizeIncrement = 0.9;
    
    public const string CommentBlockBreadcrumb = "";
    
    public static readonly string[] SpecialClasses = [ "ImpactSFXMono", 
                                                        "ImpactSFXStereo", 
                                                        "Chirp", 
                                                        "ChirpFast", 
                                                        "VehicleEngineSynth", 
                                                        "ImpactExternalForceMono", 
                                                        "ImpactExternalForceStereo", 
                                                        "ImpactForceMono", 
                                                        "ImpactForceStereo", 
                                                        "ImpactModalCreator"];
    public static readonly KeyValuePair<string, string> ClassHeaderMap = new ("class", "cls");
    public static readonly KeyValuePair<string, string> OnPlayBreadcrumbMap = new("UE.Source.OnPlay",
        "Breadcrumb=(AccessType=Reference,MemberName=\"UE.Source.OnPlay\",DataType=\"Trigger\",VertexMetadata=(bSerializeText=False),ClassName=(Namespace=\"Input\",Name=\"Trigger\"))");
    
    public static readonly KeyValuePair<string, string> ClassNameToken = new ("ClassName", "class");
    public static readonly KeyValuePair<string, string> PinIdToken = new ("PinId", "pid");
    public static readonly KeyValuePair<string, string> PinNameToken = new ("PinName", "pn");
    public static readonly KeyValuePair<string, string> PinDirectionToken = new ("Direction", "direction");
    public static readonly KeyValuePair<string, string> PinCategoryToken = new ("PinType.PinCategory", "category");
    public static readonly KeyValuePair<string, string> PinValueToken = new ("DefaultValue", "value");
    public static readonly KeyValuePair<string, string> PinLinkToken = new ("LinkedTo", "link");
    
    public static readonly KeyValuePair<string, string> PinFloatCategoryToken = new ("\"Float\"", "Float");
    public static readonly KeyValuePair<string, string> PinIntCategoryToken = new ("\"Int32\"", "Int32");
    public static readonly KeyValuePair<string, string> PinBoolCategoryToken = new ("\"Bool\"", "Bool");
    public static readonly KeyValuePair<string, string> PinTriggerCategoryToken = new ("\"Trigger\"", "Trigger");
    public static readonly KeyValuePair<string, string> PinTimeCategoryToken = new ("\"Time\"", "Time");
    public static readonly KeyValuePair<string, string> PinAudioCategoryToken = new ("\"Audio\"", "Audio");
    public static readonly KeyValuePair<string, string> PinObjectCategoryToken = new ("\"object\"", "Object");
    
    public static readonly KeyValuePair<string, string> PinDirectionOutMap = new ("\"EGPD_Output\"", "OUT");
    public static readonly Dictionary<string, string> PinCategoriesMap = new Dictionary<string, string>
    {
        {PinFloatCategoryToken.Key, PinFloatCategoryToken.Value},
        {PinIntCategoryToken.Key, PinIntCategoryToken.Value},
        {PinBoolCategoryToken.Key, PinBoolCategoryToken.Value},
        {PinTriggerCategoryToken.Key, PinTriggerCategoryToken.Value},
        {PinTimeCategoryToken.Key, PinTimeCategoryToken.Value},
        {PinAudioCategoryToken.Key, PinAudioCategoryToken.Value},
        {PinObjectCategoryToken.Key, PinObjectCategoryToken.Value}
    };

    public static readonly Dictionary<string, string> PinBloatProps = new Dictionary<string, string>()
    {
        {"PinType.PinSubCategory", "\"\""},
        {"PinType.PinSubCategoryMemberReference", "()"},
        {"PinType.PinValueType", "()"},
        {"PinType.ContainerType", "None"},
        {"PinType.bIsReference", "False"},
        {"PinType.bIsConst", "False"},
        {"PinType.bIsWeakPointer", "False"},
        {"PinType.bIsUObjectWrapper", "False"},
        {"PinType.bSerializeAsSinglePrecisionFloat", "False"},
        {"PersistentGuid", "00000000000000000000000000000000"},
        {"bHidden", "False"},
        {"bNotConnectable", "False"},
        {"bDefaultValueIsReadOnly", "False"},
        {"bDefaultValueIsIgnored", "False"},
        {"bAdvancedView", "False"},
        {"bOrphanedPin", "False"}
    };
    
    public static readonly Dictionary<string, string> SpecialPinNameMap = new Dictionary<string, string>
    {
        {"\"UE.OutputFormat.Mono.Audio:0\"", "OutMono"},
        {"\"UE.Source.OnPlay\"", "Play"},
        {"\"UE.Source.OneShot.OnFinished\"", "OnFinished"}
    };
    
    private static readonly Dictionary<string, JsonElement>? NodeCompactDefs;
    private static readonly Dictionary<string, JsonElement>? NodeFullDefs;
    private static readonly Dictionary<string, MsNodeMap>? NodeBreadcrumb2NameMap;
    private static readonly Dictionary<string, MsNodeMap>? NodeShortName2BreadcrumbMap;
    private static readonly Dictionary<string, MsNodeMap>? NodeFullName2BreadcrumbMap;
    
    static MsNodeRegistry()
    {
        var folderPath = Path.Combine(AppContext.BaseDirectory, Constants.MetaSoundsNodeDefsFolder);
        byte[] jsonBytes = File.ReadAllBytes(Path.Combine(folderPath, "MetaClassCompression.json"));
        NodeCompactDefs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
        
        jsonBytes = File.ReadAllBytes(Path.Combine(folderPath, "MetaClassFullDefs.json"));
        NodeFullDefs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
        
        jsonBytes = File.ReadAllBytes(Path.Combine(folderPath, "MetaClassNameShort.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        NodeBreadcrumb2NameMap = JsonSerializer.Deserialize<Dictionary<string, MsNodeMap>>(jsonBytes, options);
        if (NodeBreadcrumb2NameMap is not null)
        {
            NodeShortName2BreadcrumbMap = new Dictionary<string, MsNodeMap>();
            NodeFullName2BreadcrumbMap = new Dictionary<string, MsNodeMap>();
            foreach (var (k, v) in NodeBreadcrumb2NameMap)
            {
                v.Breadcrumb = k;
                NodeShortName2BreadcrumbMap.Add(v.ShortName, v);
                // Ignore nodes with the same name likes reroute node which has different variant
                NodeFullName2BreadcrumbMap[v.FullName] = v;
            }
        }
    }

    /// <summary>
    /// Breadcrumb changed based on the version of nodes in MetaSound
    /// so this function should only be used for mapping between data in def files
    /// DO NOT use this for mapping when parsing T3D from MetaSounds
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static MsNodeMap? GetNodeNameMapByBreadcrumb(string key)
    {
        return NodeBreadcrumb2NameMap!.GetValueOrDefault(key);
    }
    
    public static MsNodeMap? GetNodeNameMapByClassName(string className)
    {
        return NodeFullName2BreadcrumbMap!.GetValueOrDefault(className);
    }
    
    public static MsNodeMap? GetNodeNameMapByShortName(string key)
    {
        return NodeShortName2BreadcrumbMap!.GetValueOrDefault(key);
    }
    
    public static JsonElement? GetNodeFullDefByKey(string key)
    {
        return NodeFullDefs!.GetValueOrDefault(key);
    }
    
    public static JsonElement? GetNodeCompactDefByKey(string key)
    {
        return NodeCompactDefs!.GetValueOrDefault(key);
    }

    public static string? GetShortNameFromClassName(string className)
    {
        Match match = ClassShortNameRegex.Match(className);
        if(!match.Success)
            return null;

        return match.Groups[1].Value.Replace(" ", "");
    }
    
    public static string GetPinKeyInFullDef(string pinName, string category, bool isOutput)
    {
        string direction = isOutput ? "OUT" : "IN";
        pinName = pinName.Trim('\"').Replace(" ", "");;
        return $"{direction}_{pinName}_{PinCategoriesMap[category]}";
    }
    
    public static string GetNewGuid()
    {
        Guid guid = Guid.NewGuid();
        return guid.ToString("N").ToUpper();
    }
}