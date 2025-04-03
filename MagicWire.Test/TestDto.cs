using System.Text.Json.Serialization;

namespace MagicWire.Test;

public class TestDto
{
    public string Name { get; }
    
    [JsonPropertyName("description")]
    public string Description { get; }
}