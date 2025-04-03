using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace MagicWire;

/// <summary>
/// The session class is used to manage the sessions of the clients. It is used to identify the client in the server code.
/// </summary>
internal sealed class Session : IFrontend
{
    /// <inheritdoc cref="IFrontend.Id"/>
    public string Id { get; }

    /// <inheritdoc cref="IFrontend.Authorities"/>
    public IReadOnlyList<object> Authorities => _authorities.AsReadOnly();

    /// <inheritdoc cref="IFrontend.DataKeys"/>
    public IEnumerable<string> DataKeys => _data.Keys;

    /// <summary>
    /// The owning container of the session.
    /// </summary>
    internal WireContainer Container { get; }

    /// <summary>
    /// The currently open HTTP context used to send SSE to the client.
    /// </summary>
    internal HttpContext? SseContext { get; set; }
    
    /// <summary>
    /// A token source used to debounce reconnects.
    /// </summary>
    internal CancellationTokenSource? ReconnectTokenSource { get; set; }
    
    private readonly ConcurrentDictionary<string, object> _data = new();
    private readonly List<object> _authorities = [];

    internal Session(string id, WireContainer container)
    {
        Id = id;
        Container = container;
    }
    
    /// <inheritdoc cref="IFrontend.Own"/>
    public void Own(object obj)
    {
        Container.SetObjectAuthority(obj, this);
        
        if (!_authorities.Contains(obj))
        {
            _authorities.Add(obj);
        }
    }
    
    /// <inheritdoc cref="IFrontend.Disown"/>
    public void Disown(object obj)
    {
        Container.RemoveObjectAuthority(obj, this);

        _authorities.Remove(obj);
    }
    
    /// <inheritdoc cref="IFrontend.DisownAll"/>
    public void DisownAll()
    {
        foreach (var obj in _authorities)
        {
            Container.RemoveObjectAuthority(obj, this);
        }
        
        _authorities.Clear();
    }

    /// <inheritdoc cref="IFrontend.Owns"/>
    public bool Owns(object obj)
    {
        return _authorities.Contains(obj);
    }

    /// <inheritdoc cref="IFrontend.HasData"/>
    public bool HasData(string key)
    {
        return _data.ContainsKey(key);
    }

    /// <inheritdoc cref="IFrontend.SetData"/>
    public void SetData(string key, object value)
    {
        _data[key] = value;
    }

    /// <inheritdoc cref="IFrontend.DeleteData"/>
    public void DeleteData(string key)
    {
        _data.TryRemove(key, out _);
    }

    /// <inheritdoc cref="IFrontend.ClearData"/>
    public void ClearData()
    {
        _data.Clear();
    }

    /// <inheritdoc cref="IFrontend.GetData"/>
    public object? GetData(string key)
    {
        return _data.GetValueOrDefault(key);
    }

    internal void EmitObjectPropertyChange(string objName, string propName, string? value)
    {
        if (SseContext != null)
        {
            var data = $"PC|{objName}|{propName}|{value ?? "null"}";

            Task.Run(async () =>
            {
                await SseContext.Response.WriteAsync("data: " + data + "\n\n");
                await SseContext.Response.Body.FlushAsync();
            });
        }
    }

    internal void EmitEvent(string objName, string eventName, string? value)
    {
        if (SseContext != null)
        {
            var data = $"EV|{objName}|{eventName}|{value ?? "null"}";

            Task.Run(async () =>
            {
                await SseContext.Response.WriteAsync("data: " + data + "\n\n");
                await SseContext.Response.Body.FlushAsync();
            });
        }
    }
}