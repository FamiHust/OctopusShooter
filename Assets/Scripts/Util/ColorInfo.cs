using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central color system cho toan bo project.
/// Quan ly SeedColor enum, ID mapping, va Unity Color conversion.
/// Dung chung cho Seed, Shooter, BlockRow, v.v.
/// </summary>
public enum SeedColor
{
    Blue,
    Red,
    Yellow,
    Green,
    Purple,
    Pink,
    Orange,
    Hidden,
    Aqua,
    Brown,
    Cyan,
    Gray,
    HotPink,
    White
}

public static class ColorInfo
{
    private const string PaletteResourcePath = "SeedColorPaletteConfig";
    private static readonly Color WarmWhite = new Color32(255, 246, 220, 255); // #FFF6DC
    private static bool paletteResolved;
    private static Dictionary<SeedColor, Color> paletteOverrides;

    private static void EnsurePaletteLoaded()
    {
        if (paletteResolved)
        {
            return;
        }

        paletteResolved = true;
        paletteOverrides = null;

        SeedColorPaletteConfig paletteConfig = Resources.Load<SeedColorPaletteConfig>(PaletteResourcePath);
        if (paletteConfig == null)
        {
            return;
        }

        paletteOverrides = new Dictionary<SeedColor, Color>();
        foreach (SeedColor seedColor in System.Enum.GetValues(typeof(SeedColor)))
        {
            if (paletteConfig.TryGetColor(seedColor, out Color color))
            {
                paletteOverrides[seedColor] = color;
            }
        }
    }

    public static Color GetUnityColor(SeedColor seedColor)
    {
        EnsurePaletteLoaded();
        if (paletteOverrides != null && paletteOverrides.TryGetValue(seedColor, out Color overrideColor))
        {
            return overrideColor;
        }

        return GetVibrantDefaultColor(seedColor);
    }

    // Stable palette for runtime target-color matching.
    public static Color GetTargetMatchColor(SeedColor seedColor)
    {
        return GetStableMatchingDefaultColor(seedColor);
    }

    private static Color GetVibrantDefaultColor(SeedColor seedColor)
    {
        switch (seedColor)
        {
            case SeedColor.Blue: return new Color32(26, 132, 255, 255);     // #1a84ff
            case SeedColor.Red: return new Color32(255, 82, 70, 255);       // #FF5246
            case SeedColor.Yellow: return new Color32(255, 223, 58, 255);   // #FFDF3A
            case SeedColor.Green: return new Color32(64, 214, 108, 255);    // #40D66C
            case SeedColor.Purple: return new Color32(176, 96, 214, 255);   // #B060D6
            case SeedColor.Pink: return new Color32(255, 173, 203, 255);    // #FFADCB
            case SeedColor.Orange: return new Color32(255, 166, 38, 255);   // #FFA626
            case SeedColor.Hidden: return new Color32(120, 120, 126, 255);  // #78787E
            case SeedColor.Aqua: return new Color32(72, 255, 206, 255);     // #48FFCE
            case SeedColor.Brown: return new Color32(179, 103, 41, 255);    // #B36729
            case SeedColor.Cyan: return new Color32(38, 236, 255, 255);     // #26ECFF
            case SeedColor.Gray: return new Color32(184, 184, 194, 255);    // #B8B8C2
            case SeedColor.HotPink: return new Color32(255, 97, 185, 255);  // #FF61B9
            case SeedColor.White: return WarmWhite;                          // #FFF6DC
            default: return Color.white;
        }
    }

    private static Color GetStableMatchingDefaultColor(SeedColor seedColor)
    {
        switch (seedColor)
        {
            case SeedColor.Blue: return new Color32(0, 102, 255, 255);      // #0066ff
            case SeedColor.Red: return new Color32(255, 59, 48, 255);       // #FF3B30
            case SeedColor.Yellow: return new Color32(255, 214, 10, 255);   // #FFD60A
            case SeedColor.Green: return new Color32(52, 199, 89, 255);     // #34C759
            case SeedColor.Purple: return new Color32(154, 74, 184, 255);   // #9A4AB8
            case SeedColor.Pink: return new Color32(255, 205, 215, 255);    // #FFCDD7
            case SeedColor.Orange: return new Color32(255, 149, 0, 255);    // #FF9500
            case SeedColor.Hidden: return new Color32(111, 111, 111, 255);  // #6F6F6F
            case SeedColor.Aqua: return new Color32(40, 255, 190, 255);     // #28FFBE
            case SeedColor.Brown: return new Color32(150, 75, 0, 255);      // #964B00
            case SeedColor.Cyan: return new Color32(0, 255, 255, 255);      // #00FFFF
            case SeedColor.Gray: return new Color32(168, 168, 176, 255);    // #A8A8B0
            case SeedColor.HotPink: return new Color32(255, 124, 196, 255); // #FF7CC4
            case SeedColor.White: return WarmWhite;                          // #FFF6DC
            default: return Color.white;
        }
    }
}
