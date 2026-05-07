using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.Models;

public class WebTutorial
{
    [JsonPropertyName("title_fld")]
    public string? Title { get; set; }
    public string? Description { get; set; }
    
    [JsonPropertyName("YoutubeUrl")]
    public string? ContentUrl { get; set; }
    public int Order { get; set; }
    public List<string>? Group { get; set; }
}