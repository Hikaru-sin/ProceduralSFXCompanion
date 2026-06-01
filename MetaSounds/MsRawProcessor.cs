using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls.Mixins;

namespace ProceduralSFXCompanion.MetaSounds;

public static class MsRawProcessor
{
    /// <summary>
    /// Parse the raw text from MetaSounds T3D format (copy-paste from Unreal) into the intermediate raw structure
    /// which is easier to be processed or converted to other formats.
    /// </summary>
    /// <param name="text">The T3D text</param>
    /// <returns>A dictionary with each key is the node name (unique ID in Unreal)</returns>
    public static Dictionary<string, MsNodeRaw> ParseMetaSoundsText(string text)
    {
        var matches = MsNodeRegistry.ObjectRegex.Matches(text);
        Dictionary<string, MsNodeRaw> rawNodes = new Dictionary<string, MsNodeRaw>(matches.Count);
        foreach (Match match in matches)
        {
            MsNodeRaw newNode = new(match.Value);
            rawNodes[newNode.Name] = newNode;
        }
        return rawNodes;
    }

    /// <summary>
    /// Merge all the OnPlay nodes of the graph into one node with reroute 
    /// </summary>
    /// <param name="rawNodes">The original nodes</param>
    /// <param name="localTriggerName">The name of the mapping local trigger variable</param>
    /// <returns>The new modified nodes with only one OnPlay node</returns>
    public static Dictionary<string, MsNodeRaw> MergeOnPlayNodesToLocalTrigger(Dictionary<string, MsNodeRaw> rawNodes, string localTriggerName)
    {
        Dictionary<string, MsNodeRaw> newNodes = new();
        Dictionary<string, MsNodeRaw> onPlayNodes = new();
        var leftTopOnPlayNode = SplittingOnPlayNodes(rawNodes, onPlayNodes, newNodes);
        if (leftTopOnPlayNode is null)
            return newNodes;

        // Create deep copy for modifying
        leftTopOnPlayNode = new MsNodeRaw(leftTopOnPlayNode);
        leftTopOnPlayNode.RemoveOtherProperty("Input"); // Remove reference to avoid warning in Unreal
        var triggerSetterNode = CreateTriggerSetter(leftTopOnPlayNode, localTriggerName);
        
        MapOnPlayTriggerToNewTriggerVariable(onPlayNodes, newNodes, localTriggerName);

        var (onPlayPid, onPlayPin) = leftTopOnPlayNode.Pins.First();
        onPlayPin.Links.Clear();
        var triggerSetterPin = triggerSetterNode.Pins.First().Value;
        onPlayPin.Links.Add($"{triggerSetterNode.Name} {triggerSetterPin.Id}", (triggerSetterNode.Name, triggerSetterPin.Id));
        triggerSetterPin.Links.Add($"{leftTopOnPlayNode.Name} {onPlayPid}", (leftTopOnPlayNode.Name, onPlayPid));
        newNodes.Add(leftTopOnPlayNode.Name, leftTopOnPlayNode);
        newNodes.Add(triggerSetterNode.Name, triggerSetterNode);
        return newNodes;
    }

    public static void AddEncloseComment(Dictionary<string, MsNodeRaw> rawNodes, string comment, 
                                         int topLeftPadding = MsNodeRegistry.UnitGridSize * 6, 
                                         int bottomRightPadding = MsNodeRegistry.UnitGridSize * 10)
    {
        GetTotalGraphLayoutArea(rawNodes, out Coordinate topLeft, out Coordinate bottomRight);
        int posX = topLeft.Y - topLeftPadding;
        int posY = topLeft.X - topLeftPadding;
        int width = bottomRight.Y - topLeft.Y + bottomRightPadding;
        int height = bottomRight.X - topLeft.X + bottomRightPadding;
        var commentNode = MsNodeRaw.CreateCommentNode(comment, posX, posY, width, height);
        rawNodes.Add(commentNode.Name, commentNode);
    }

    public static void GetTotalGraphLayoutArea(Dictionary<string, MsNodeRaw> rawNodes, out Coordinate topLeft, out Coordinate bottomRight)
    {
        if (rawNodes.Count == 0)
        {
            topLeft = new Coordinate(0, 0);
            bottomRight = new Coordinate(0, 0);
            return;
        }
        
        var (nk, firstNode) = rawNodes.First();
        int top = firstNode.PosY;
        int bottom = firstNode.PosY;
        int left = firstNode.PosX;
        int right = firstNode.PosX;
        foreach (var (key, node) in rawNodes)
        {
            MsNodeMap? nodeName = MsNodeRegistry.GetNodeNameMapByClassName(node.ClassName);
            int nodeBottom = node.PosY;
            int nodeRight = node.PosX;
            if (nodeName is not null)
            {
                nodeRight += (int)Math.Ceiling(nodeName.Size[0] * MsNodeRegistry.MajorGridSize);
                nodeBottom += (int)Math.Ceiling(nodeName.Size[1] * MsNodeRegistry.MajorGridSize);
            }
            
            if(node.PosY < top)
                top = node.PosY;
            if(node.PosX < left)
                left = node.PosX;
            
            if(nodeBottom > bottom)
                bottom = nodeBottom;
            if(nodeRight > right) 
                right = nodeRight;
        }
        topLeft = new Coordinate(top, left);
        bottomRight = new Coordinate(bottom, right);
    }
    
    private static void MapOnPlayTriggerToNewTriggerVariable(Dictionary<string, MsNodeRaw> onPlayNodes, 
                                                             Dictionary<string, MsNodeRaw> newNodes, 
                                                             string localTriggerName)
    {
        foreach (var (id, node) in onPlayNodes)
        {
            var (pid, pin) = node.Pins.First();
            // OnPlay node -> to other nodes
            foreach (var (linkId, (otherId, otherPid)) in pin.Links)
            {
                // Get the connected node based on its ID in the link
                if (newNodes.TryGetValue(otherId, out var otherNode))
                {
                    int posX = otherNode.PosX - MsNodeRegistry.UnitGridSize * 5;
                    int posY = otherNode.PosY + MsNodeRegistry.UnitGridSize;
                    var localTriggerNode = MsNodeRaw.TryCreateLocalVariableGetter(pin, localTriggerName, posX, posY);
                    if(localTriggerNode is null)
                        throw new Exception("Failed to create Local trigger variable!");
                    
                    var localTriggerNodePin = localTriggerNode.Pins.First().Value;
                    localTriggerNodePin.Links.Add($"{otherNode.Name} {otherPid}", (otherNode.Name, otherPid));
                    newNodes.Add(localTriggerNode.Name, localTriggerNode);
                    
                    // Get the pin of the connected node based on the pinId in the link of OnPlay node
                    if (otherNode.Pins.TryGetValue(otherPid, out var otherPin))
                    {
                        // Now we can modify the link to point to the new reroute node
                        if (otherPin.Links.Remove($"{node.Name} {pid}"))
                            otherPin.Links.TryAdd($"{localTriggerNode.Name} {localTriggerNodePin.Id}", (localTriggerNode.Name, localTriggerNodePin.Id));
                    }
                }
            }
        }
    }

    private static MsNodeRaw CreateTriggerSetter(MsNodeRaw leftTopOnPlayNode, string name)
    {
        var triggerOutPin = leftTopOnPlayNode.Pins.First().Value;
        var triggerSetterNode = MsNodeRaw.TryCreateLocalVariableSetter(triggerOutPin, name, leftTopOnPlayNode.PosX, leftTopOnPlayNode.PosY);
        if(triggerSetterNode is null)
            throw new Exception("CreateLocalTriggers: failed to create trigger setter!");
        
        // moving the nodes a bit to align them
        leftTopOnPlayNode.PosX -= 175;
        triggerSetterNode.PosY += 40;
        
        return triggerSetterNode;
    }

    private static MsNodeRaw? SplittingOnPlayNodes(Dictionary<string, MsNodeRaw> rawNodes, Dictionary<string, MsNodeRaw> onPlayNodes, Dictionary<string, MsNodeRaw> nonOnPlayNodes)
    {
        MsNodeRaw? leftTopOnPlayNode = null;
        foreach (var (id, node) in rawNodes)
        {
            if (node.IsOnPlayNode())
            {
                // The OnPlay node of Unreal only has 1 output pin
                if (node.Pins.Count == 1)
                {
                    onPlayNodes.Add(id, node);
                    if (leftTopOnPlayNode is null)
                        leftTopOnPlayNode = node;
                    else
                    {
                        int diffX = leftTopOnPlayNode.PosX - node.PosX;
                        int diffY = leftTopOnPlayNode.PosY - node.PosY;
                        if (diffX > 100)
                            leftTopOnPlayNode = node;
                        else if (diffX > -100)
                        {
                            if (diffY > 0)
                                leftTopOnPlayNode = node;
                        }
                    }
                }

                continue;
            }

            MsNodeRaw newNode = new MsNodeRaw(node);
            nonOnPlayNodes.Add(id, newNode);
        }

        return leftTopOnPlayNode;
    }

    public static void MergeSameFloatsToInputs(Dictionary<string, MsNodeRaw> allNodes, string uniqueId)
    {
        Dictionary<string, List<(MsNodeRaw, string)>> allSameValues = GetAllSameInputPinsWithSameValue(allNodes, typeof(float), new HashSet<string>());
        AddInputPinsForSameParamsCategoryByName(allNodes, allSameValues, $"{uniqueId}F");
    }
    
    public static void MergeSameTimesToInputs(Dictionary<string, MsNodeRaw> allNodes, string uniqueId)
    {
        HashSet<string> ignoreNodes = ["TriggerDelay"];
        Dictionary<string, List<(MsNodeRaw, string)>> allTimeValues = GetAllSameInputPinsWithSameValue(allNodes, typeof(DateTime), ignoreNodes);
        AddInputPinsForSameParamsCategoryByName(allNodes, allTimeValues, $"{uniqueId}T");
    }
    
    private static Dictionary<string, List<(MsNodeRaw, string)>> GetAllSameInputPinsWithSameValue(Dictionary<string, MsNodeRaw> allNodes, 
        Type dataType, HashSet<string> ignoreNodes)
    {
        Dictionary<string, List<(MsNodeRaw, string)>> allValues = new();
        foreach (var (nk, node) in allNodes)
        {
            MsNodeMap? nodeMap = MsNodeRegistry.GetNodeNameMapByClassName(node.ClassName);
            if(nodeMap is null)
                continue; // Only process nodes stored in our database
            
            JsonElement? nodeFullDefs = MsNodeRegistry.GetNodeFullDefByKey(nodeMap.Breadcrumb);
            if (nodeFullDefs is null)
                continue; // Only process nodes stored we know its pin default value
            
            if(ignoreNodes.Contains(nodeMap.ShortName))
                continue;
            
            if(!nodeFullDefs.Value.TryGetProperty("pins", out var pinsJson))
                continue;
            
            foreach (var (pk, pin) in node.Pins)
            {
                if(pin.IsOutput())
                    continue;
                if(pin.Links.Count > 0)
                    continue;
                
                if (pin.CategoryType == dataType)
                {
                    if(!pin.TryGetFloatValue(out var pinValue))
                        continue;

                    string jK = MsNodeRegistry.GetPinKeyInFullDef(pin.Name, pin.Category, false);
                    if (pinsJson.TryGetProperty(jK, out var pinJson))
                    {
                        if (pinJson.TryGetProperty(MsNodeRegistry.PinValueToken.Key, out var defaultValueJson))
                        {
                            if (float.TryParse(defaultValueJson.GetString().Trim("\""), out var defaultValue))
                            {
                                if(Math.Abs(pinValue - defaultValue) < 1e-3f)
                                    continue;

                                string key = $"{pinValue}_{pin.Name}";
                                if (allValues.ContainsKey(key))
                                    allValues[key].Add((node, pk));
                                else
                                    allValues.Add(key, [(node, pk)]);
                            }
                        }
                    }
                }
            }
        }

        return allValues;
    }
    
    private static void AddInputPinsForSameParamsCategoryByName(Dictionary<string, MsNodeRaw> allNodes, 
                                                                Dictionary<string, List<(MsNodeRaw, string)>> allSameValues, 
                                                                string uniqueId)
    {
        int counter = 0;
        foreach (var (value, nodes) in allSameValues)
        {
            if(nodes.Count < 2)
                continue;
            
            MsNodeRaw firstNode = nodes[0].Item1;
            MsNodePinRaw firstPin = firstNode.Pins[nodes[0].Item2];
            string name = $"\"{firstPin.Name.Trim('\"')}_{counter}_{uniqueId}\"";
            counter++;
            
            foreach (var (node, pk) in nodes)
            {
                int posX = node.PosX - MsNodeRegistry.MajorGridSize;
                int posY = node.PosY + node.Pins[pk].Index * MsNodeRegistry.PinGridSize;
                MsNodeRaw? newInput = MsNodeRaw.TryCreateConstructorInputPin(firstPin, name, posX, posY);
                if(newInput is null)
                    continue;

                MsNodePinRaw newInputPin = newInput.Pins.First().Value;
                newInputPin.Links.Clear();
                node.Pins[pk].Links.Add($"{newInput.Name} {newInputPin.Id}", (newInput.Name, newInputPin.Id));
                newInputPin.Links.Add($"{node.Name} {pk}", (node.Name, pk));
                allNodes.Add(newInput.Name, newInput);
            }
        }
    }
    
    private static MsNodeRaw CreateRerouteFromNodeList(List<(MsNodeRaw, string)> allNodes, string shortName)
    {
        MsNodeRaw anchorNode = allNodes[0].Item1;
        string pinKey = allNodes[0].Item2;
        int posX = anchorNode.PosX;
        int posY = anchorNode.PosY;
        for (int i = 1; i < allNodes.Count; i++)
        {
            var (node, pk) = allNodes[i];
            if (posX > node.PosX)
            {
                anchorNode = node;
                pinKey = pk;
                posX = node.PosX;
                posY = node.PosY;
            }
        }

        posX -= MsNodeRegistry.UnitGridSize * 2;
        posY += anchorNode.Pins[pinKey].Index * MsNodeRegistry.PinGridSize;
        var rerouteNode = MsNodeRaw.CreateExternalNodeFromMetaSoundJson(shortName, posX, posY);
        return rerouteNode;
    }

    public static string SerializeToMetaSounds(Dictionary<string, MsNodeRaw> rawNodes)
    {
        StringBuilder outGraph = new StringBuilder();

        foreach (var (id, node) in rawNodes)
        {
            if (!String.IsNullOrWhiteSpace(node.ExportPath))
                outGraph.AppendLine($"Begin Object Class={node.ClassPath} Name={node.Name} ExportPath={node.ExportPath}");
            else
                outGraph.AppendLine($"Begin Object Class={node.ClassPath} Name={node.Name}");
         
            if(!String.IsNullOrWhiteSpace(node.Breadcrumb))
                outGraph.AppendLine($"   Breadcrumb={node.Breadcrumb}");
            
            if(!String.IsNullOrWhiteSpace(node.ClassName))
                outGraph.AppendLine($"   ClassName={node.ClassName}");
            
            if(!String.IsNullOrWhiteSpace(node.NodeId))
                outGraph.AppendLine($"   NodeID={node.NodeId}");
            
            outGraph.AppendLine($"   NodePosX={node.PosX}");
            outGraph.AppendLine($"   NodePosY={node.PosY}");
            outGraph.AppendLine($"   ErrorType={node.ErrorType}");
            
            if(!String.IsNullOrWhiteSpace(node.NodeGuid))
                outGraph.AppendLine($"   NodeGuid={node.NodeGuid}");

            foreach (var props in node.OtherProperties)
                outGraph.AppendLine($"   {props.Key}={props.Value}");

            foreach (var (pid, pin) in node.Pins)
            {
                outGraph.Append("   CustomProperties Pin (");
                outGraph.Append($"{MsNodeRegistry.PinIdToken.Key}={pin.Id},");
                outGraph.Append($"{MsNodeRegistry.PinNameToken.Key}={pin.Name},");
                outGraph.Append($"{MsNodeRegistry.PinCategoryToken.Key}={pin.Category},");
                if(!String.IsNullOrWhiteSpace(pin.Direction))
                    outGraph.Append($"{MsNodeRegistry.PinDirectionToken.Key}={pin.Direction},");
                if(!String.IsNullOrWhiteSpace(pin.Value))
                    outGraph.Append($"{MsNodeRegistry.PinValueToken.Key}={pin.Value},");
                if (pin.Links.Count > 0)
                {
                    outGraph.Append($"{MsNodeRegistry.PinLinkToken.Key}=(");
                    foreach (var link in pin.Links)
                        outGraph.Append($"{link.Key},");
                    outGraph.Append("),");
                }
                
                foreach (var (pk, pv) in pin.Others)
                    outGraph.Append($"{pk}={pv},");
                outGraph.Remove(outGraph.Length - 1, 1);
                outGraph.AppendLine(")");
            }
            outGraph.AppendLine("End Object");
        }
        
        return  outGraph.ToString();
    }
}