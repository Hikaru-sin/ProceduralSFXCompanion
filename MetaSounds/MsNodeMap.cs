using System.Collections.Generic;

namespace ProceduralSFXCompanion.MetaSounds;

public class MsNodeMap
{
    public string Breadcrumb { get; set; } = string.Empty;
    public required string ShortName { get; set; }
    public required string FullName { get; set; }
    public required string Comment { get; set; }
    public required double[] Size { get; set; }
    public required List<string> ConstructorPins { get; set; }
}