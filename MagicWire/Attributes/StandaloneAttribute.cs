namespace MagicWire;

/// <summary>
/// A class marked as standalone won't be included into the "wire" namespace on the frontend TypeScript code.
/// This is a more organizational attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class StandaloneAttribute : Attribute;