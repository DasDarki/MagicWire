namespace MagicWire;

/// <summary>
/// An interface representing one client connected to the server. It is used to identify the client in the server code
/// and can be used for specific client-based operations. There are only <see cref="IFrontend"/> objects as long as
/// there is a running request or an open SSE connection. If neither of these is the case, any synchronization
/// will fail in an error event on the <see cref="WireContainer"/>.
/// </summary>
public interface IFrontend
{
    /// <summary>
    /// Returns the unique session ID of the frontend. This ID is used to identify the frontend in the server code.
    /// The session ID will be made available to the frontend for each subsequent request.
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// All objects that are owned by this frontend.
    /// </summary>
    IReadOnlyList<object> Authorities { get; }
    
    /// <summary>
    /// Checks if the given key exists in the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    /// <param name="key">The key to check for.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    bool HasData(string key);
    
    /// <summary>
    /// Sets a key/value pair in the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    /// <param name="key">The key to set the value for.</param>
    /// <param name="value">The value to set.</param>
    void SetData(string key, object value);
    
    /// <summary>
    /// Removes a key/value pair from the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    void DeleteData(string key);

    /// <summary>
    /// Removes all key/value pairs from the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    void ClearData();
    
    /// <summary>
    /// Gets a value from the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    /// <param name="key">The key to get the value for.</param>
    /// <returns>The value for the given key or null if the key does not exist.</returns>
    object? GetData(string key);
    
    /// <summary>
    /// An enumerable of all keys in the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    IEnumerable<string> DataKeys { get; }
    
    /// <summary>
    /// Gets a value from the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.
    /// </summary>
    /// <param name="key">The key to get the value for.</param>
    /// <typeparam name="T">The type of the value to get.</typeparam>
    /// <returns>The value for the given key or null if the key does not exist.</returns>
    T? GetData<T>(string key) where T : class
    {
        return GetData(key) as T;
    }
    
    /// <summary>
    /// Gets or sets a value in the frontend's data store. This data is only available for the lifetime of this
    /// frontend session instance and will only be locally stored. It is not synchronized to the client.<br/>
    /// If the value is set to null, the key will be removed from the data store.
    /// </summary>
    /// <param name="key">The key to get or set the value for.</param>
    object? this[string key]
    {
        get => GetData(key);
        set
        {
            if (value == null)
            {
                DeleteData(key);
            }
            else
            {
                SetData(key, value);
            }
        }
    }
    
    /// <summary>
    /// Sets the authority of the given object to this frontend. If called, all synchronizations of fields in that object
    /// are only sent to this frontend. The object must be wireable (marked with <see cref="WireAttribute"/>). Also,
    /// wireable methods are only allowed to invoke methods on this frontend. An object can have multiple authorities.
    /// </summary>
    /// <param name="obj">The object to set the authority of.</param>
    void Own(object obj);

    /// <summary>
    /// Disowns the given object. This will remove the authority of this frontend from the object.
    /// </summary>
    /// <param name="obj">The object to disown.</param>
    void Disown(object obj);

    /// <summary>
    /// Disowns all objects owned by this frontend. This will remove the authority of this frontend from all objects.
    /// </summary>
    void DisownAll();

    /// <summary>
    /// Checks if the given object is owned by this frontend.
    /// </summary>
    bool Owns(object obj);
}