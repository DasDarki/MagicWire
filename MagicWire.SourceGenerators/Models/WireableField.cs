namespace MagicWire.SourceGenerators.Models;

/// <summary>
/// The wireable field defines a field that can be used in the wireable object.
/// </summary>
public sealed class WireableField(string fieldName, string propertyName, string frontendName, string type)
{
    /// <summary>
    /// The name of the field. This is also the exact name of the field in the C# code.
    /// </summary>
    public string FieldName { get; } = fieldName;
    
    /// <summary>
    /// The name of the property which gets generated.
    /// </summary>
    public string PropertyName { get; } = propertyName;
    
    /// <summary>
    /// The name of the field in the frontend object.
    /// </summary>
    public string FrontendName { get; } = frontendName;
    
    /// <summary>
    /// The data type of the field.
    /// </summary>
    public string Type { get; } = type;
}