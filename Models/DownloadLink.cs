using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProceduralSFXCompanion.Models;

public class DownloadLink
{
    [JsonPropertyName("title_fld")]
    public string? Title { get; set; }
    public string? Url { get; set; }
    public int Version { get; set; }
    public int LastCheckTime { get; set; }
}