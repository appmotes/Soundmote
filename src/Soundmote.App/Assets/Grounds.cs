using Avalonia.Media;
using Avalonia.Styling;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Assets;

/// <summary>
/// Each ground as a theme variant (D12). Night and Ember build on Fluent's dark theme, Paper and Sky on its light
/// one, so Fluent's own controls follow. A view takes a ground by setting <c>RequestedThemeVariant</c>.
/// </summary>
public static class Grounds
{
    public static ThemeVariant Night { get; } = new(nameof(Night), ThemeVariant.Dark);
    public static ThemeVariant Ember { get; } = new(nameof(Ember), ThemeVariant.Dark);
    public static ThemeVariant Paper { get; } = new(nameof(Paper), ThemeVariant.Light);
    public static ThemeVariant Sky { get; } = new(nameof(Sky), ThemeVariant.Light);

    public static ThemeVariant For(Ground ground) => ground switch
    {
        Ground.Ember => Ember,
        Ground.Paper => Paper,
        Ground.Sky => Sky,
        _ => Night
    };

    /// <summary>A sound's colour drawn darker by <paramref name="shade"/> of lightness, so it holds on a light ground.</summary>
    public static Color Shade(Color colour, double shade)
    {
        if (shade <= 0) return colour;
        var hsl = colour.ToHsl();
        return new HslColor(hsl.A, hsl.H, hsl.S, Math.Max(0, hsl.L - shade)).ToRgb();
    }
}
