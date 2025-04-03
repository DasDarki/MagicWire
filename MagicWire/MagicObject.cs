using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace MagicWire;

/// <summary>
/// The magic object is a mapped wireable object used for direct access internally.
/// </summary>
public sealed class MagicObject(string name)
{
    internal string Name { get; } = name;
    
    internal ConcurrentDictionary<string, Session> Authorities { get; } = [];
    
    public delegate Task<object?> WireableMethodDelegate(IFrontend frontend, JsonArray args);
    public delegate JsonObject WireableObjectStateDelegate();
    
    private readonly Dictionary<string, WireableMethodDelegate> _methods = new();
    private WireableObjectStateDelegate? _getState;
    
    /// <summary>
    /// Internal use only. Do not use manually.
    /// </summary>
    public void SetStateDelegate(WireableObjectStateDelegate getState)
    {
        _getState = getState;
    }
    
    /// <summary>
    /// Internal use only. Do not use manually.
    /// </summary>
    public void SetMethod(string name, WireableMethodDelegate method)
    {
        _methods[name] = method;
    }

    internal JsonObject GetCurrentState()
    {
        return _getState?.Invoke() ?? new JsonObject();
    }
    
    internal Task<object?> CallMethod(IFrontend frontend, string name, JsonArray args)
    {
        if (_methods.TryGetValue(name, out var method))
        {
            return method.Invoke(frontend, args);
        }
            
        return Task.FromResult<object?>(null);
    }
}