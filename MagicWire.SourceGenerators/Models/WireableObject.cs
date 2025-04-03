using System.Collections.Generic;

namespace MagicWire.SourceGenerators.Models;

/// <summary>
/// The wireable object defines a class that can be used in the wireable object.
/// </summary>
public sealed class WireableObject(string @namespace, string className, string instanceName, bool isStandalone = false)
{
    /// <summary>
    /// The namespace of the C# class.
    /// </summary>
    public string Namespace { get; } = @namespace;
    
    /// <summary>
    /// The name of the wireable object instance.
    /// </summary>
    public string InstanceName { get; } = instanceName;
    
    /// <summary>
    /// The name of the wireable object class. Equivalent to the class name in the C# code.
    /// </summary>
    public string ClassName { get; } = className;

    /// <summary>
    /// Whether its a standalone wireable object or not.
    /// </summary>
    public bool IsStandalone { get; } = isStandalone;
    
    /// <summary>
    /// A list of wireable fields that are used in the wireable object.
    /// </summary>
    public List<WireableField> Fields { get; } = [];
    
    /// <summary>
    /// A list of wireable operations that are used in the wireable object.
    /// </summary>
    public List<WireableOperation> Operations { get; } = [];
    
    /// <summary>
    /// A list of wireable events that are used in the wireable object.
    /// </summary>
    public List<WireableEvent> Events { get; } = [];
}