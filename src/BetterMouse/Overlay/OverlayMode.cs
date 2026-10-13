namespace BetterMouse.Overlay;

/// <summary>How the physical on-display overlay behaves.</summary>
public enum OverlayMode
{
    /// <summary>Hidden.</summary>
    Off,

    /// <summary>Visible but click-through: guides only, the desktop stays usable.</summary>
    View,

    /// <summary>Visible and interactive: handles can be dragged directly on the monitors.</summary>
    Edit,
}
