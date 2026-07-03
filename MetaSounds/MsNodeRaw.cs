using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ProceduralSFXCompanion.MetaSounds;

public readonly record struct Coordinate(int X, int Y);

public class MsNodeRaw
{
    /// The unique ID used in node connections
    public string Name { get; private set; } = string.Empty;

    /// Boilerplate class path
    public string ClassPath { get; private set; } = string.Empty;

    /// The most important line to retrieve the correct node class and its properties 
    public string Breadcrumb { get; private set; } = string.Empty;

    /// Boilerplate assess path
    public string ExportPath { get; private set; } = string.Empty;

    /// The name of the node to be used
    public string ClassName { get; private set; } = string.Empty;

    /// Not doing anything in Unreal when copy-paste
    public string NodeId { get; private set; } = string.Empty;

    /// Not doing anything in Unreal when copy-paste
    public string NodeGuid { get; private set; } = string.Empty;

    /// X position in the graph
    public int PosX { get; set; }

    /// Y position in the graph
    public int PosY { get; set; }

    /// Usually 4 unless it's a hanging or to be updated nodes
    public int ErrorType { get; private set; } = 4;

    /// Boilerplate properties for some special nodes definitions
    public Dictionary<string, string> OtherProperties { get; set; } = new();

    /// All pins with key = Pin ID
    public Dictionary<string, MsNodePinRaw> Pins = new();

    public MsNodeRaw()
    {
    }

    public MsNodeRaw(string nodeRawText)
    {
        Match header = MsNodeRegistry.HeaderRegex.Match(nodeRawText);
        ClassPath = header.Groups[1].Value;
        Name = header.Groups[2].Success ? header.Groups[2].Value : string.Empty;
        ExportPath = header.Groups[3].Success ? header.Groups[3].Value : string.Empty;
        int pinIndex = 0;
        foreach (string line in nodeRawText.Split("\n",
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("CustomProperties Pin"))
            {
                Match pinMatch = MsNodeRegistry.PinContentRegex.Match(line);
                if (pinMatch.Success)
                {
                    MsNodePinRaw newNodePin = new(pinMatch.Groups[1].Value, pinIndex);
                    pinIndex++;
                    Pins.Add(newNodePin.Id, newNodePin);
                }
            }
            else if (line.Contains("=") && !line.StartsWith("Begin Object"))
            {
                var keyValue = line.Split("=", 2,
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length == 2)
                {
                    string key = keyValue[0];
                    string value = keyValue[1];
                    if (String.Equals(key, "Breadcrumb", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Breadcrumb = value;
                    }
                    else if (String.Equals(key, "NodePosX", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (float.TryParse(value, out var outValue))
                            PosX = (int)Math.Round(outValue);
                    }
                    else if (String.Equals(key, "NodePosY", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (float.TryParse(value, out var outValue))
                            PosY = (int)Math.Round(outValue);
                    }
                    else if (String.Equals(key, MsNodeRegistry.ClassNameToken.Key,
                                 StringComparison.InvariantCultureIgnoreCase))
                    {
                        ClassName = value;
                    }
                    else if (String.Equals(key, "NodeId", StringComparison.InvariantCultureIgnoreCase))
                    {
                        NodeId = value;
                    }
                    else if (String.Equals(key, "ErrorType", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (int.TryParse(value, out var outValue))
                            ErrorType = outValue;
                    }
                    else if (String.Equals(key, "NodeGuid", StringComparison.InvariantCultureIgnoreCase))
                    {
                        NodeGuid = value;
                    }
                    else
                    {
                        OtherProperties[key] = value;
                    }
                }
            }
        }
    }

    public MsNodeRaw(MsNodeRaw other)
    {
        Name = other.Name;
        ClassPath = other.ClassPath;
        Breadcrumb = other.Breadcrumb;
        ExportPath = other.ExportPath;
        ClassName = other.ClassName;
        NodeId = other.NodeId;
        NodeGuid = other.NodeGuid;
        PosX = other.PosX;
        PosY = other.PosY;
        ErrorType = other.ErrorType;
        foreach (var (k, v) in other.OtherProperties)
            OtherProperties.Add(k, v);

        foreach (var (id, pin) in other.Pins)
        {
            var newPin = new MsNodePinRaw(pin);
            Pins.Add(id, newPin);
        }
    }

    public static MsNodeRaw CreateExternalNodeFromMetaSoundJson(string nodeShortName, int posX, int posY)
    {
        MsNodeRaw newNode = new MsNodeRaw
        {
            PosX = posX,
            PosY = posY
        };

        var nodeNameMap = MsNodeRegistry.GetNodeNameMapByShortName(nodeShortName);
        if (nodeNameMap is null || nodeNameMap.Breadcrumb is null)
            throw new Exception($"Cannot find {nodeShortName} node Breadcrumb!");

        newNode.Breadcrumb = nodeNameMap.Breadcrumb;
        if (nodeNameMap.Breadcrumb.StartsWith(MsNodeRegistry.BreadcrumbPrefix))
            newNode.Breadcrumb = nodeNameMap.Breadcrumb[MsNodeRegistry.BreadcrumbPrefix.Length..];

        JsonElement? nodeJson = MsNodeRegistry.GetNodeFullDefByKey(nodeNameMap.Breadcrumb);
        if (nodeJson == null)
            throw new Exception($"Could not find node MetaSounds JSON definition for {nodeShortName}!");

        foreach (var property in nodeJson.Value.EnumerateObject())
        {
            if (property.NameEquals(MsNodeRegistry.ClassHeaderMap.Key))
                newNode.ClassPath = property.Value.GetString() ?? string.Empty;
            else if (property.NameEquals(MsNodeRegistry.ClassNameToken.Key))
                newNode.ClassName = property.Value.GetString() ?? string.Empty;
            else if (property.NameEquals("ErrorType"))
            {
                if (int.TryParse(property.Value.GetString(), out var result))
                    newNode.ErrorType = result;
            }
            else if (property.NameEquals("pins"))
            {
                int index = 0;
                foreach (var pin in property.Value.EnumerateObject())
                {
                    MsNodePinRaw pinRaw = new MsNodePinRaw(pin, index);
                    index++;
                    newNode.Pins.Add(pinRaw.Id, pinRaw);
                }
            }
            else
            {
                var text = property.Value.GetString();
                if (!String.IsNullOrWhiteSpace(text))
                    newNode.OtherProperties.Add(property.Name, text);
            }
        }

        // put this at the end to make sure we create a new id even if the id exists in the defs
        newNode.Name = MsNodeRegistry.GetNewGuid();
        return newNode;
    }

    public static MsNodeRaw CreateCommentNode(string comment, int posX, int posY, int width, int height)
    {
        MsNodeRaw newNode = new MsNodeRaw
        {
            PosX = posX,
            PosY = posY,
            ClassPath = "/Script/MetasoundEditor.MetasoundEditorGraphCommentNode",
            Name = MsNodeRegistry.GetNewGuid(),
            NodeGuid = MsNodeRegistry.GetNewGuid()
        };

        newNode.OtherProperties.Add("CommentID", MsNodeRegistry.GetNewGuid());
        newNode.OtherProperties.Add("CommentColor", "(R=0.15,G=0.150000,B=0.15,A=0.5)");
        newNode.OtherProperties.Add("bCommentBubbleVisible_InDetailsPanel", "False");
        newNode.OtherProperties.Add("NodeWidth", width.ToString());
        newNode.OtherProperties.Add("NodeHeight", height.ToString());
        newNode.OtherProperties.Add("bCommentBubblePinned", "False");
        newNode.OtherProperties.Add("bCommentBubbleVisible", "False");
        newNode.OtherProperties.Add("NodeComment", $"\"{comment}\"");

        return newNode;
    }

    /// <summary>
    /// Create a constructor input pin from an input pin of a node.
    /// WARNING: Only Float, Time, and Int32 category types are supported!
    /// </summary>
    /// <param name="fromPin">the pin to create input pin from</param>
    /// <param name="name">name of the created pin</param>
    /// <param name="posX">X position</param>
    /// <param name="posY">Y position</param>
    /// <returns>A constructor pin based on the input pin</returns>
    public static MsNodeRaw? TryCreateConstructorInputPin(MsNodePinRaw fromPin, string name, int posX, int posY)
    {
        if (!GetPinValueAndTypeString(fromPin, out var valueType, out var pinValue)) 
            return null;
        
        MsNodeRaw newNode = new MsNodeRaw
        {
            PosX = posX,
            PosY = posY,
            ClassPath = "/Script/MetasoundEditor.MetasoundEditorGraphInputNode",
            Name = MsNodeRegistry.GetNewGuid(),
            NodeGuid = MsNodeRegistry.GetNewGuid(),
            NodeId = MsNodeRegistry.GetNewGuid(),
            Breadcrumb = $"(AccessType=Value,MemberName={name},DataType={fromPin.Category},DefaultLiterals=((00000000000000000000000000000000, (Type={valueType},As{valueType}=({pinValue.Trim('\"')})))),ClassName=(Namespace=\"Input\",Name={fromPin.Category},Variant=\"Constructor\"))"
        };
        
        MsNodePinRaw newPin = new MsNodePinRaw(fromPin, "\"Value\"", MsNodeRegistry.GetNewGuid(), true, 0);
        newNode.Pins.Add(newPin.Id, newPin);
        return newNode;
    }
    
    /// <summary>
    /// Create a setter for a local variable. The links of the newly created node are empty.
    /// </summary>
    /// <param name="fromPin">The pin to copy properties from</param>
    /// <param name="name">Name of the new local pin</param>
    /// <param name="posX">X position</param>
    /// <param name="posY">Y position</param>
    /// <returns>New MsNodeRaw pin if success otherwise null</returns>
    public static MsNodeRaw? TryCreateLocalVariableSetter(MsNodePinRaw fromPin, string name, int posX, int posY)
    {
        if (!GetPinValueAndTypeString(fromPin, out var pinValueType, out var pinValue)) 
            return null;

        MsNodeRaw newNode = new MsNodeRaw
        {
            PosX = posX,
            PosY = posY,
            ClassPath = "/Script/MetasoundEditor.MetasoundEditorGraphVariableNode",
            ClassName = $"(Namespace=\"VariableMutator\",Name={fromPin.Category})",
            Name = MsNodeRegistry.GetNewGuid(),
            NodeId = MsNodeRegistry.GetNewGuid(),
            NodeGuid = MsNodeRegistry.GetNewGuid(),
        };
        
        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (String.IsNullOrWhiteSpace(pinValueType)) // Empty means audio type which is a special case
            newNode.Breadcrumb = $"(MemberName=\"{name}\",DataType={fromPin.Category},DefaultLiterals=((00000000000000000000000000000000, (AsNumDefault=1))), ClassName=(Namespace=\"VariableMutator\",Name={fromPin.Category}))";
        else
            newNode.Breadcrumb = $"(MemberName=\"{name}\",DataType={fromPin.Category},DefaultLiterals=((00000000000000000000000000000000, (Type={pinValueType},As{pinValueType}=({pinValue})))), ClassName=(Namespace=\"VariableMutator\",Name={fromPin.Category}))";
        
        newNode.OtherProperties.Add("ClassType", "VariableMutator");
        MsNodePinRaw newPin = new MsNodePinRaw(fromPin, "\"Value\"", MsNodeRegistry.GetNewGuid(), false, 0);
        newPin.Links.Clear();
        newNode.Pins.Add(newPin.Id, newPin);
        return newNode;
    }
    
    /// <summary>
    /// Create a getter for a local variable. The links of the newly created node are empty.
    /// </summary>
    /// <param name="fromPin">The pin to copy properties from</param>
    /// <param name="name">Name of the new local pin</param>
    /// <param name="posX">X position</param>
    /// <param name="posY">Y position</param>
    /// <returns>New MsNodeRaw pin if success otherwise null</returns>
    public static MsNodeRaw? TryCreateLocalVariableGetter(MsNodePinRaw fromPin, string name, int posX, int posY)
    {
        if (!GetPinValueAndTypeString(fromPin, out var pinValueType, out var pinValue)) 
            return null;

        MsNodeRaw newNode = new MsNodeRaw
        {
            PosX = posX,
            PosY = posY,
            ClassPath = "/Script/MetasoundEditor.MetasoundEditorGraphVariableNode",
            ClassName = $"(Namespace=\"VariableAccessor\",Name={fromPin.Category})",
            Name = MsNodeRegistry.GetNewGuid(),
            NodeId = MsNodeRegistry.GetNewGuid(),
            NodeGuid = MsNodeRegistry.GetNewGuid(),
        };
        
        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (String.IsNullOrWhiteSpace(pinValueType)) // Empty means audio type which is a special case
            newNode.Breadcrumb = $"(MemberName=\"{name}\",DataType={fromPin.Category},DefaultLiterals=((00000000000000000000000000000000, (AsNumDefault=1))), ClassName=(Namespace=\"VariableAccessor\",Name={fromPin.Category}))";
        else
            newNode.Breadcrumb = $"(MemberName=\"{name}\",DataType={fromPin.Category},DefaultLiterals=((00000000000000000000000000000000, (Type={pinValueType},As{pinValueType}=({pinValue})))),ClassName=(Namespace=\"VariableAccessor\",Name={fromPin.Category}))";
        
        newNode.OtherProperties.Add("ClassType", "VariableAccessor");
        MsNodePinRaw newPin = new MsNodePinRaw(fromPin, "\"Value\"", MsNodeRegistry.GetNewGuid(), true, 0);
        newPin.Links.Clear();
        newNode.Pins.Add(newPin.Id, newPin);
        return newNode;
    }

    private static bool GetPinValueAndTypeString(MsNodePinRaw fromPin, out string pinValueType, out string pinValue)
    {
        pinValueType = fromPin.Category;
        pinValue = fromPin.Value ?? string.Empty;
        if(String.Equals(pinValueType, MsNodeRegistry.PinTimeCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase) 
           || String.Equals(pinValueType, MsNodeRegistry.PinFloatCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase))
            pinValueType = MsNodeRegistry.PinFloatCategoryToken.Value;
        else if (String.Equals(pinValueType, MsNodeRegistry.PinTimeCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase))
            pinValueType = "Integer";
        else if (String.Equals(pinValueType, MsNodeRegistry.PinTriggerCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase))
        {
            pinValueType = "Boolean";
            pinValue = "False";
        }
        else if (String.Equals(pinValueType, MsNodeRegistry.PinBoolCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase))
            pinValueType = "Boolean";
        else if (String.Equals(pinValueType, MsNodeRegistry.PinAudioCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase))
        {
            pinValueType = "";
            pinValue = "";
        }
        else 
            return false;

        return true;
    }

    public bool IsOnPlayNode()
    {
        if (String.Equals(ClassPath, MsNodeRegistry.InputClassHeader, StringComparison.InvariantCultureIgnoreCase))
        {
            if (Breadcrumb.Contains(MsNodeRegistry.OnPlayBreadcrumbMap.Key))
            {
                if (Pins.Count == 1)
                {
                    var (pid, pin) = Pins.First();
                    if(pin.IsOutputTrigger())
                        return true;
                }
            }
        }
        return false;
    }

    public bool RemoveOtherProperty(string propertyName)
    {
        return OtherProperties.Remove(propertyName);
    }
}

public class MsNodePinRaw
{
    public string Name { get; } = string.Empty;
    public string Id { get; } = string.Empty;
    public string Category 
    { 
        get;
        private init
        {
            field = value;
            if (String.Equals(MsNodeRegistry.PinFloatCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(float);
            else if(String.Equals(MsNodeRegistry.PinTimeCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(DateTime);
            else if (String.Equals(MsNodeRegistry.PinIntCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(Int32);
            else if (String.Equals(MsNodeRegistry.PinBoolCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(bool);
            else if (String.Equals(MsNodeRegistry.PinAudioCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(Array);
            else if (String.Equals(MsNodeRegistry.PinTriggerCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(Delegate);
            else if (String.Equals(MsNodeRegistry.PinObjectCategoryToken.Key, value, StringComparison.InvariantCultureIgnoreCase))
                CategoryType = typeof(object);
        } 
    } = string.Empty;
    
    public Type CategoryType { get; private set; } = typeof(object);
    public string? Value { get; set; }
    public string? Direction { get; }
    public Dictionary<string, (string nodeId, string pinId)> Links { get; } = new();
    public Dictionary<string, string> Others = new ();

    public int Index { get; } = 0;
    
    public MsNodePinRaw(JsonProperty pin, int index)
    {
        Index = index;
        foreach (var prop in pin.Value.EnumerateObject())
        {
            if(prop.NameEquals(MsNodeRegistry.PinNameToken.Key))
                Name = prop.Value.GetString()  ?? string.Empty;
            else if (prop.NameEquals(MsNodeRegistry.PinCategoryToken.Key))
                Category = prop.Value.GetString() ?? string.Empty;
            else if (prop.NameEquals(MsNodeRegistry.PinValueToken.Key))
                Value = prop.Value.GetString();
            else if (prop.NameEquals(MsNodeRegistry.PinDirectionToken.Key))
                Direction = prop.Value.GetString();
            else
            {
                var text = prop.Value.GetString();
                if(text is not null)
                    Others.Add(prop.Name, text);
            }
        }
        Id = MsNodeRegistry.GetNewGuid();
    }
    
    public MsNodePinRaw(string pinRawText, int index)
    {
        Index = index;
        foreach (Match match in MsNodeRegistry.PinKeyValueRegex.Matches(pinRawText))
        {
            string key = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();
            if (String.Equals(key, MsNodeRegistry.PinNameToken.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                Name = value;
            }
            else if (String.Equals(key, MsNodeRegistry.PinIdToken.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                Id =  value;
            }
            else if (String.Equals(key, MsNodeRegistry.PinLinkToken.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                var linkText = value.Substring(1, value.Length - 2);
                foreach (var link in linkText.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var nodePin = link.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (nodePin.Length == 2)
                        Links[link] = (nodePin[0], nodePin[1]);
                }
            }
            else if (String.Equals(key, MsNodeRegistry.PinCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                Category = value;
            }
            else if (String.Equals(key, MsNodeRegistry.PinValueToken.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                Value = value;
            }
            else if (String.Equals(key, MsNodeRegistry.PinDirectionToken.Key, StringComparison.InvariantCultureIgnoreCase))
            {
                Direction = value;
            }
            else
            {
                Others[key] = value;
            }
        }
    }

    public MsNodePinRaw(MsNodePinRaw other, string? newName = null, string? newId = null, bool? isOutPin = null, int? index = null)
    {
        Name = newName ?? other.Name;
        Id = newId ?? other.Id;
        Category = other.Category;
        Value = other.Value;
        Index = index ?? other.Index;
        
        if(isOutPin is not null)
            Direction = isOutPin.Value ? MsNodeRegistry.PinDirectionOutMap.Key : null;
        else
            Direction = other.Direction;
        
        foreach (var (k, (v1, v2)) in other.Links)
            Links.Add(k, (v1, v2));
        
        foreach (var (k, v) in other.Others)
            Others.Add(k, v);
    }
    
    public bool IsOutput()
    {
        if(String.IsNullOrWhiteSpace(Direction))
            return false;
        
        return String.Equals(Direction, MsNodeRegistry.PinDirectionOutMap.Key, StringComparison.InvariantCultureIgnoreCase);
    }

    public bool IsTrigger()
    {
        if(String.IsNullOrWhiteSpace(Category))
            return false;
        
        return String.Equals(Category, MsNodeRegistry.PinTriggerCategoryToken.Key, StringComparison.InvariantCultureIgnoreCase);
    }
    
    public bool IsInputTrigger()
    {
        return !IsOutput() && IsTrigger();
    }
    
    public bool IsOutputTrigger()
    {
        return IsOutput() && IsTrigger();
    }

    public bool TryGetFloatValue(out float result)
    {
        return float.TryParse(Value.Trim("\""), out result);
    }
}