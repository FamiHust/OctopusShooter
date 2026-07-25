using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SeedInfo : MonoBehaviour
{
    private static readonly int ColorShaderId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int MainColorShaderId = Shader.PropertyToID("_MainColor");
    private static readonly int GradColorShaderId = Shader.PropertyToID("_GradColor");
    private static readonly Dictionary<int, Dictionary<SeedColor, Material>> SeedMaterialPaletteCache =
        new Dictionary<int, Dictionary<SeedColor, Material>>();

    [Header("Seed Information")]
    [SerializeField] private SeedColor seedColor;
    [SerializeField] private int seedIndex; // Index trong BlockRow (0-4)
    [SerializeField] private int blockRowID; // ID cá»§a BlockRow chá»©a seed nÃ y
    
    [Header("Runtime Info")]
    [SerializeField] private bool isDestroyed = false;
    [SerializeField] private float spawnTime;

    private Renderer cachedRenderer;
    private Material baseSeedMaterial;
    private MaterialPropertyBlock colorPropertyBlock;

    void Awake()
    {
        spawnTime = Time.time;
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            baseSeedMaterial = cachedRenderer.sharedMaterial;
        }
    }

    public void SetSeedData(SeedColor color, int index, int blockRowId = -1)
    {
        seedColor = color;
        seedIndex = index;
        blockRowID = blockRowId;
        isDestroyed = false;
    }

    public SeedColor GetSeedColor()
    {
        return seedColor;
    }

    public int GetSeedIndex()
    {
        return seedIndex;
    }

    public int GetBlockRowID()
    {
        return blockRowID;
    }

    public bool IsDestroyed()
    {
        return isDestroyed;
    }

    public float GetSpawnTime()
    {
        return spawnTime;
    }

    public void MarkAsDestroyed()
    {
        isDestroyed = true;
    }

    public void SetSeedColor(SeedColor newColor)
    {
        seedColor = newColor;

        Color color = ColorInfo.GetUnityColor(newColor);
        if (!TryApplySharedColorMaterial(newColor, color))
        {
            ApplySeedColorWithPropertyBlock(color);
        }
    }

    private bool TryApplySharedColorMaterial(SeedColor seedColorId, Color color)
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return false;
        }

        Material baseMaterial = baseSeedMaterial != null ? baseSeedMaterial : cachedRenderer.sharedMaterial;
        if (baseMaterial == null)
        {
            return false;
        }

        Material tintedShared = GetOrCreateSharedMaterialByColor(baseMaterial, seedColorId, color);
        if (tintedShared == null)
        {
            return false;
        }

        if (cachedRenderer.sharedMaterial != tintedShared)
        {
            cachedRenderer.sharedMaterial = tintedShared;
        }

        return true;
    }

    private static Material GetOrCreateSharedMaterialByColor(Material baseMaterial, SeedColor seedColorId, Color color)
    {
        if (baseMaterial == null)
        {
            return null;
        }

        int baseId = baseMaterial.GetInstanceID();
        if (!SeedMaterialPaletteCache.TryGetValue(baseId, out Dictionary<SeedColor, Material> palette))
        {
            palette = new Dictionary<SeedColor, Material>();
            SeedMaterialPaletteCache[baseId] = palette;
        }

        if (palette.TryGetValue(seedColorId, out Material cached) && cached != null)
        {
            return cached;
        }

        Material tinted = new Material(baseMaterial)
        {
            name = $"{baseMaterial.name}_Seed_{seedColorId}",
            hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
        };

        tinted.enableInstancing = true;
        ApplyColorToMaterial(tinted, color);
        palette[seedColorId] = tinted;
        return tinted;
    }

    private static void ApplyColorToMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ColorShaderId))
        {
            material.SetColor(ColorShaderId, color);
        }

        if (material.HasProperty(BaseColorShaderId))
        {
            material.SetColor(BaseColorShaderId, color);
        }

        if (material.HasProperty(MainColorShaderId))
        {
            material.SetColor(MainColorShaderId, color);
        }

        if (material.HasProperty(GradColorShaderId))
        {
            Color grad = Color.Lerp(color, Color.white, 0.12f);
            material.SetColor(GradColorShaderId, grad);
        }
    }

    private void ApplySeedColorWithPropertyBlock(Color color)
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        if (colorPropertyBlock == null)
        {
            colorPropertyBlock = new MaterialPropertyBlock();
        }

        cachedRenderer.GetPropertyBlock(colorPropertyBlock);

        Material sharedMaterial = cachedRenderer.sharedMaterial;
        if (sharedMaterial != null)
        {
            if (sharedMaterial.HasProperty(ColorShaderId))
            {
                colorPropertyBlock.SetColor(ColorShaderId, color);
            }

            if (sharedMaterial.HasProperty(BaseColorShaderId))
            {
                colorPropertyBlock.SetColor(BaseColorShaderId, color);
            }

            if (sharedMaterial.HasProperty(MainColorShaderId))
            {
                colorPropertyBlock.SetColor(MainColorShaderId, color);
            }
        }

        cachedRenderer.SetPropertyBlock(colorPropertyBlock);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogSeedInfo()
    {
        ;
    }

    void OnValidate()
    {
        seedIndex = Mathf.Clamp(seedIndex, 0, 4);
    }
}
