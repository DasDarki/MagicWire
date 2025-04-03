namespace MagicWire;

/// <summary>
/// Marks a wireable method as a to-client method. This means that the method can be called on the server-side which
/// results in an event being sent to the client-side. The client-side can then handle the event and process it.
/// The event cannot return any value. The method must be partial and can only be void.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ToClientAttribute : Attribute;