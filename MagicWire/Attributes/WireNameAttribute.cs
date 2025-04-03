namespace MagicWire;

/// <summary>
/// By default, the TypeScript generator generates wireable class instance and property names using the name of the class
/// but in camel case. In order to change this behavior, the <see cref="WireNameAttribute"/> can be used to specify a
/// specific name to be used.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
public sealed class WireNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}