using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FlowBlast/Seed Color Palette Config", fileName = "SeedColorPaletteConfig")]
public class SeedColorPaletteConfig : ScriptableObject
{
    [Serializable]
    public struct SeedColorEntry
    {
        public SeedColor seedColor;
        public Color color;
    }

    [SerializeField] private List<SeedColorEntry> entries = new List<SeedColorEntry>();

    public bool TryGetColor(SeedColor seedColor, out Color color)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].seedColor != seedColor)
            {
                continue;
            }

            color = entries[i].color;
            return true;
        }

        color = Color.white;
        return false;
    }
}
