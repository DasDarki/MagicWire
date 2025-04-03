using System;
using System.Collections.Generic;

namespace MagicWire.SourceGenerators.Models;

/// <summary>
/// A wireable event defines an event that can be sent to the frontend.
/// </summary>
public sealed class WireableEvent(string methodName, string eventName)
{
    /// <summary>
    /// The name of the method. This is also the exact name of the method in the C# code.
    /// </summary>
    public string MethodName { get; } = methodName;
    
    /// <summary>
    /// The name of the event that is used in the frontend.
    /// </summary>
    public string EventName { get; } = eventName;

    /// <summary>
    /// A list of parameters that are used in the event. Its a list of tuples where the first item is the name of the
    /// parameter and the second item is the type of the parameter.
    /// </summary>
    public List<Tuple<string, string>> Parameters { get; set; } = [];
}