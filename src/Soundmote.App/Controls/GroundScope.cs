using Avalonia;
using Avalonia.Controls;

namespace Soundmote.App.Controls;

/// <summary>
/// A part of a view on its own ground (D12), such as a library card. A scope that joins a window on the ground the
/// window already has keeps the resources its children found while detached, which are none (L1);
/// stepping out to the ground's base theme and back makes them look again.
/// </summary>
public class GroundScope : ThemeVariantScope
{
    protected override Type StyleKeyOverride => typeof(ThemeVariantScope);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (RequestedThemeVariant is not { InheritVariant: { } inherit } ground) return;
        RequestedThemeVariant = inherit;
        RequestedThemeVariant = ground;
    }
}
