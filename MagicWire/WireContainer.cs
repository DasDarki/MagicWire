using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MagicWire;

/// <summary>
/// The wire container is the main component for the MagicWire library. It is used to manage the server, the sessions
/// and all wireable objects. Under the hood MagicWire uses a webserver to communicate with the client.
/// </summary>
public sealed class WireContainer
{
    /// <summary>
    /// The singleton instance of the wire container. This is used to access the wire container from anywhere.
    /// </summary>
    public static WireContainer Instance { get; } = new();
    
    /// <summary>
    /// Gets called when an exception arises in the wire container. This can be used to log the exception or
    /// to handle it in general. MagicWire won't through exceptions in the process of wire operations. Instead,
    /// they will be passed to this event. This event is not called when the server is stopped or disposed.
    /// </summary>
    public event Action<Exception> Error = null!;
    
    /// <summary>
    /// Gets called when a new session is created. This can be used to initialize the session or to
    /// declare authorities over objects by calling <see cref="IFrontend.Own"/>.
    /// </summary>
    public event Action<IFrontend> SessionCreated = null!;
    
    /// <summary>
    /// Gets called when a session is disconnected. This happens when the SSE stream is closed.
    /// </summary>
    public event Action<IFrontend> SessionDisconnected = null!;

    /// <summary>
    /// Gets called when a session has been disconnected and connects within 1 seconds (reconnect intervall).
    /// </summary>
    public event Action<IFrontend> SessionReconnected = null!; 
    
    /// <summary>
    /// Gets called when a session is destroyed. This happens when the session is no longer needed and
    /// didn't reconnect within 1 seconds. This can be used to clean up the session.
    /// </summary>
    public event Action<IFrontend> SessionDestroyed = null!;

    private WebApplication _host = null!;
    private readonly ConcurrentDictionary<string, Session> _sessions;
    private readonly ConcurrentDictionary<object, MagicObject> _wireableObjects;
    private readonly ConcurrentDictionary<string, MagicObject> _wireableObjectsByName;

    private WireContainer()
    {
        _sessions = new ConcurrentDictionary<string, Session>();
        _wireableObjects = new ConcurrentDictionary<object, MagicObject>();
        _wireableObjectsByName = new ConcurrentDictionary<string, MagicObject>();
    }

    /// <summary>
    /// Starts the current instance of the wire container. This will create a new instance of the webserver and start it.
    /// It also starts the session management and begins the wireable object management.
    /// </summary>
    /// <param name="port">The port to bind the server to.</param>
    /// <param name="configure">A function to configure the web application builder. This can be used to add middleware or services to the server.</param>
    /// <remarks>
    /// This will not block the current thread. The server will run in the background.
    /// <br/><br/>
    /// Before it starts it will check for --gen-ts in arguments and start the TypeScript generation. If finished,
    /// the program gets terminated with exit code 0.
    /// </remarks>
    public void Start(int port = 25319, Action<WebApplicationBuilder>? configure = null)
    {
        TypeScriptGenerator.CheckAndGenerate();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(b =>
            {
                b.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
        
        configure?.Invoke(builder);

        var version = "0.0.0.0";
        var assembly = Assembly.GetAssembly(typeof(WireContainer));
        if (assembly != null)
        {
            var assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion != null)
            {
                version = assemblyVersion.ToString();
            }
        }
        
        var app = builder.Build();

        app.UseCors();
        app.MapGet("/", () => version);
        app.MapPost("/.well-known/wire/init", HandleInit);
        app.MapGet("/.well-known/wire/stream", HandleStream);
        app.MapPost("/.well-known/wire/objects/{objName}/invoke/{operation}", HandleObjectInvoke);

        _ = (_host = app).StartAsync();
    }

    /// <summary>
    /// Stops the current instance of the underlying webserver.
    /// </summary>
    public Task StopAsync() => _host.StopAsync();

    /// <summary>
    /// Emits an object property change to the client.
    /// </summary>
    public void EmitObjectPropertyChange(object obj, string property, object? value)
    {
        if (_wireableObjects.TryGetValue(obj, out var magicObject))
        {
            try
            {
                var jsonValue = value == null ? null : JsonSerializer.Serialize(value);
                if (!magicObject.Authorities.IsEmpty)
                {
                    foreach (var session in magicObject.Authorities)
                    {
                        session.Value.EmitObjectPropertyChange(magicObject.Name, property, jsonValue);
                    }
                }
                else
                {
                    foreach (var session in _sessions.Values)
                    {
                        session.EmitObjectPropertyChange(magicObject.Name, property, jsonValue);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }
    }

    /// <summary>
    /// Emits an object event to the client. This is used to emit events from the server to the client.
    /// </summary>
    public void EmitEvent(object obj, string eventName, params object?[] args)
    {
        if (_wireableObjects.TryGetValue(obj, out var magicObject))
        {
            try
            {
                var jsonValue = JsonSerializer.Serialize(args);
                if (!magicObject.Authorities.IsEmpty)
                {
                    foreach (var session in magicObject.Authorities)
                    {
                        session.Value.EmitEvent(magicObject.Name, eventName, jsonValue);
                    }
                }
                else
                {
                    foreach (var session in _sessions.Values)
                    {
                        session.EmitEvent(magicObject.Name, eventName, jsonValue);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }
    }

    /// <summary>
    /// Tells the wire container to manage the given object. This will add the object to the list of wireable objects.
    /// </summary>
    public void ManageObject(WireableObject obj)
    {
        if (_wireableObjects.TryGetValue(obj, out _))
        {
            throw new InvalidOperationException($"The object {obj.GetType().FullName} is already managed by the wire container.");
        }

        var magicObject = obj.w__InitializeWireMagic();

        _wireableObjects[obj] = magicObject ?? throw new InvalidOperationException($"The object {obj.GetType().FullName} is not a wireable object.");
        _wireableObjectsByName[magicObject.Name] = magicObject;
    }

    /// <summary>
    /// Clears all authorities of the given object.
    /// </summary>
    public void ClearObjectAuthorities(object obj)
    {
        if (_wireableObjects.TryGetValue(obj, out var magicObject))
        {
            magicObject.Authorities.Clear();
        }
    }
    
    /// <summary>
    /// Logs an error and passes it to the <see cref="Error"/> event. This is used to log errors that occur in the wire container.
    /// </summary>
    internal void LogError(Exception ex)
    {
        Error?.Invoke(ex);
    }

    internal void SetObjectAuthority(object obj, Session session)
    {
        if (_wireableObjects.TryGetValue(obj, out var magicObject))
        {
            if (!magicObject.Authorities.ContainsKey(session.Id))
            {
                magicObject.Authorities.TryAdd(session.Id, session);
            }
        }
    }
    
    internal void RemoveObjectAuthority(object obj, Session session)
    {
        if (_wireableObjects.TryGetValue(obj, out var magicObject))
        {
            magicObject.Authorities.TryRemove(session.Id, out _);
        }
    }

    private IResult HandleInit(HttpContext ctx)
    {
        var session = GetSessionForContext(ctx); // make sure the session is created
        SessionCreated?.Invoke(session);
        
        ctx.Response.StatusCode = 200;
        ctx.Response.Headers.ContentType = "application/json";

        var dict = new JsonObject
        {
            ["__w__Session"] = session.Id
        };

        foreach (var obj in _wireableObjects)
        {
            if (!obj.Value.Authorities.IsEmpty && !obj.Value.Authorities.ContainsKey(session.Id))
            {
                continue;
            }
            
            dict[obj.Value.Name] = obj.Value.GetCurrentState();
        }
        
        return Results.Json(dict);
    }

    private async Task HandleStream(HttpContext ctx)
    {
        var session = GetSessionForContext(ctx);
        if (session.ReconnectTokenSource != null)
        {
            await session.ReconnectTokenSource.CancelAsync();
            session.ReconnectTokenSource.Dispose();
            session.ReconnectTokenSource = null;
            
            SessionReconnected?.Invoke(session);
        }
        
        session.SseContext = ctx;
        
        ctx.Response.Headers.Append("Content-Type", "text/event-stream");
        ctx.Response.StatusCode = 200;

        var clientDisconnected = ctx.RequestAborted;
        while (!clientDisconnected.IsCancellationRequested)
        {
            await Task.Delay(1, clientDisconnected);
        }
        
        SessionDisconnected?.Invoke(session);
        
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            await Task.Delay(1100, cts.Token);
            DestroySession(session);
        }, cts.Token);
    }

    private async Task<object?> HandleObjectInvoke(HttpContext ctx, string objName, string operation)
    {
        if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(operation))
        {
            ctx.Response.StatusCode = 400;
            return Results.Text("Invalid object name or operation.");
        }
        
        if (!_wireableObjectsByName.TryGetValue(objName, out var magicObject))
        {
            ctx.Response.StatusCode = 404;
            return Results.Text("Object not found.");
        }

        if (await JsonNode.ParseAsync(ctx.Request.Body) is not JsonArray args)
        {
            ctx.Response.StatusCode = 400;
            return Results.Text("Invalid arguments.");
        }
        
        var session = GetSessionForContext(ctx);
        if (!magicObject.Authorities.IsEmpty && !magicObject.Authorities.ContainsKey(session.Id))
        {
            ctx.Response.StatusCode = 403;
            return Results.Text("You are not authorized to access this object.");
        }

        var result = await magicObject.CallMethod(session, operation, args);
        
        ctx.Response.StatusCode = 200;
        return result;
    }

    private void DestroySession(Session session)
    {
        session.DisownAll();
        session.SseContext = null;
        
        if (session.ReconnectTokenSource != null)
        {
            session.ReconnectTokenSource.Cancel();
            session.ReconnectTokenSource.Dispose();
            session.ReconnectTokenSource = null;
        }
        
        _sessions.TryRemove(session.Id, out _);
        
        SessionDestroyed?.Invoke(session);
    }
    
    private Session GetSessionForContext(HttpContext ctx)
    {
        var sessionId = ctx.Request.Headers["X-Session"].FirstOrDefault() ?? ctx.Request.Query["stk"];
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = GenerateNewSessionId(ctx);
        }

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        session = new Session(sessionId, this);
        _sessions.TryAdd(sessionId, session);
        return session;
    }
    
    private string GenerateNewSessionId(HttpContext ctx)
    {
        var id = Guid.NewGuid();
        var ip = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var userAgent = ctx.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{id}{ip}{userAgent}")));
        hash = string.Concat(hash.Where(char.IsLetterOrDigit));
        return hash;
    }
}