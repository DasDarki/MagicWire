namespace MagicWire;

/// <summary>
/// The class for all wireable objects.
/// </summary>
public abstract class WireableObject
{
    protected WireableObject()
    {
        WireContainer.Instance.ManageObject(this);
    }
    
    /// <summary>
    /// Internal use only. Do not use manually.
    /// </summary>
    protected internal abstract MagicObject w__InitializeWireMagic();
}