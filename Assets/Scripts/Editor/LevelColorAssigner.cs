using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR

/// <summary>
/// Editor Tool Ä‘á»ƒ tá»± Ä‘á»™ng gÃ¡n mÃ u tá»« level_colors.json vÃ o táº¥t cáº£ level prefabs
/// 
/// Mapping: JSON color_ids â†’ SeedColor enum
///   0 (Yellow) â†’ SeedColor.Yellow (2)
///   1 (Cobalt_Blue) â†’ SeedColor.Blue (0)
///   2 (Green) â†’ SeedColor.Green (3)
///   3 (Red) â†’ SeedColor.Red (1)
///   4 (Purple) â†’ SeedColor.Purple (4)
///   5 (Sky_Blue) â†’ SeedColor.Cyan (10)
///   6 (Orange) â†’ SeedColor.Orange (6)
///   7 (Gray) â†’ SeedColor.Gray (11)
///   8 (Pink) â†’ SeedColor.Pink (5)
///   9 (Brown) â†’ SeedColor.Brown (9)
///   10 (Hot_Pink) â†’ SeedColor.HotPink (12)
///   11 (Aqua) â†’ SeedColor.Aqua (8)
///   12 (White) â†’ SeedColor.White (13)
/// </summary>
public class LevelColorAssigner
{
    [System.Serializable]
    private class LevelColorData
    {
        public int[] color_ids;
        public string[] color_names;
        public int count;
    }

    [System.Serializable]
    private class LevelColorsWrapper
    {
        public Dictionary<string, LevelColorData> levels = new Dictionary<string, LevelColorData>();
    }

    private static readonly Dictionary<int, SeedColor> ColorIDMapping = new Dictionary<int, SeedColor>
    {
        { 0, SeedColor.Yellow },      // Yellow
        { 1, SeedColor.Blue },        // Cobalt_Blue
        { 2, SeedColor.Green },       // Green
        { 3, SeedColor.Red },         // Red
        { 4, SeedColor.Purple },      // Purple
        { 5, SeedColor.Cyan },        // Sky_Blue â†’ map to Cyan
        { 6, SeedColor.Orange },      // Orange
        { 7, SeedColor.Gray },        // Gray
        { 8, SeedColor.Pink },        // Pink
        { 9, SeedColor.Brown },       // Brown
        { 10, SeedColor.HotPink },    // Hot_Pink
        { 11, SeedColor.Aqua },       // Aqua
        { 12, SeedColor.White },      // White
    };

    [MenuItem("FlowBlast Tools/Level Colors/Assign Colors to Level Prefabs")]
    public static void AssignColorsToLevels()
    {
        string jsonPath = "Assets/Util/level_colors.json";
        
        if (!File.Exists(jsonPath))
        {
            EditorUtility.DisplayDialog("Error", $"File not found: {jsonPath}", "OK");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        
        // Parse JSON manually since Unity's JsonUtility doesn't handle Dictionary well
        var levelColors = ParseLevelColorsJSON(jsonContent);

        if (levelColors.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Failed to parse JSON or no level data found", "OK");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var kvp in levelColors)
        {
            string levelName = kvp.Key;
            int[] colorIds = kvp.Value;

            if (TryAssignColorsToLevelPrefab(levelName, colorIds))
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        EditorUtility.DisplayDialog("Complete", 
            $"âœ“ Success: {successCount} levels\nâœ— Failed: {failCount} levels", "OK");
    }

    private static Dictionary<string, int[]> ParseLevelColorsJSON(string jsonContent)
    {
        Dictionary<string, int[]> result = new Dictionary<string, int[]>();

        // Simple JSON parsing for our specific format
        // Extract level blocks: "LevelXX": { "color_ids": [...], ... }
        int pos = 0;
        while ((pos = jsonContent.IndexOf("\"Level", pos)) != -1)
        {
            // Find level name
            int nameStart = pos + 1;
            int nameEnd = jsonContent.IndexOf("\":", nameStart);
            string levelName = jsonContent.Substring(nameStart, nameEnd - nameStart);

            // Find color_ids array
            int colorIdsStart = jsonContent.IndexOf("\"color_ids\":", nameEnd);
            if (colorIdsStart == -1) break;

            int arrayStart = jsonContent.IndexOf("[", colorIdsStart);
            int arrayEnd = jsonContent.IndexOf("]", arrayStart);
            string arrayStr = jsonContent.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

            // Parse color IDs
            string[] idStrs = arrayStr.Split(',');
            int[] colorIds = new int[idStrs.Length];
            for (int i = 0; i < idStrs.Length; i++)
            {
                if (int.TryParse(idStrs[i].Trim(), out int id))
                    colorIds[i] = id;
            }

            result[levelName] = colorIds;
            pos = arrayEnd + 1;
        }

        return result;
    }

    private static bool TryAssignColorsToLevelPrefab(string levelName, int[] colorIds)
    {
        // Find level prefab
        string[] prefabGuids = AssetDatabase.FindAssets($"{levelName} t:Prefab",
            new[] { "Assets/_GameAssets/Levels" });

        if (prefabGuids.Length == 0)
        {
            ;
            return false;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
        GameObject levelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (levelPrefab == null)
        {
            ;
            return false;
        }

        // Find LevelController in prefab
        LevelController levelController = levelPrefab.GetComponent<LevelController>();
        if (levelController == null)
        {
            ;
            return false;
        }

        // Convert color IDs to SeedColor list
        List<SeedColor> seedColors = new List<SeedColor>();
        foreach (int colorId in colorIds)
        {
            if (ColorIDMapping.TryGetValue(colorId, out SeedColor seedColor))
            {
                seedColors.Add(seedColor);
            }
            else
            {
                ;
                return false;
            }
        }

        // Set listColor via reflection (since it's private)
        var field = typeof(LevelController).GetField("listColor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(levelController, seedColors);
            EditorUtility.SetDirty(levelPrefab);
            AssetDatabase.SaveAssets();
            ;
            return true;
        }

        ;
        return false;
    }

    // â”€â”€ Shooter Color + GridItem Type Assignment â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [MenuItem("FlowBlast Tools/Level Colors/Assign Shooter Colors + GridItem Types")]
    public static void AssignShooterColorsAndGridItemTypes()
    {
        string jsonPath = "Assets/Util/level_colors.json";
        if (!File.Exists(jsonPath))
        {
            EditorUtility.DisplayDialog("Error", $"File not found: {jsonPath}", "OK");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        var levelColors = ParseLevelColorsJSON(jsonContent);

        if (levelColors.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Failed to parse JSON or no level data found", "OK");
            return;
        }

        int successCount = 0, skipCount = 0;

        foreach (var kvp in levelColors)
        {
            if (TryAssignShooterColorsToLevelPrefab(kvp.Key, kvp.Value))
                successCount++;
            else
                skipCount++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Complete",
            $"âœ“ Processed: {successCount} levels\nâ€“ Skipped (no shooters): {skipCount} levels", "OK");
    }

    private static bool TryAssignShooterColorsToLevelPrefab(string levelName, int[] colorIds)
    {
        string[] prefabGuids = AssetDatabase.FindAssets($"{levelName} t:Prefab",
            new[] { "Assets/_GameAssets/Levels" });

        if (prefabGuids.Length == 0) return false;

        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
        GameObject levelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (levelPrefab == null) return false;

        LevelController lc = levelPrefab.GetComponent<LevelController>();
        if (lc == null) return false;

        var shooterListField = typeof(LevelController).GetField("shooterList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (shooterListField == null) return false;

        var shooterList = shooterListField.GetValue(lc) as System.Collections.Generic.List<BaseShooter>;
        if (shooterList == null || shooterList.Count == 0) return false;

        var targetColorField = typeof(BaseShooter).GetField("targetColor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gridItemTypeField = typeof(GridItem).GetField("type",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        int count = Mathf.Min(shooterList.Count, colorIds.Length);
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            BaseShooter shooter = shooterList[i];
            if (shooter == null) continue;

            if (!ColorIDMapping.TryGetValue(colorIds[i], out SeedColor seedColor)) continue;

            if (targetColorField != null)
            {
                targetColorField.SetValue(shooter, seedColor);
                EditorUtility.SetDirty(shooter);
                changed = true;
            }

            GridItem gridItem = shooter.GetComponent<GridItem>();
            if (gridItem != null && gridItemTypeField != null)
            {
                gridItemTypeField.SetValue(gridItem, GridItemType.Shooter);
                EditorUtility.SetDirty(gridItem);
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(levelPrefab);
            ;
        }

        return changed;
    }

    // â”€â”€ Verify â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [MenuItem("FlowBlast Tools/Level Colors/Verify Level Colors")]
    public static void VerifyLevelColors()
    {
        string jsonPath = "Assets/Util/level_colors.json";
        
        if (!File.Exists(jsonPath))
        {
            EditorUtility.DisplayDialog("Error", $"File not found: {jsonPath}", "OK");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        var levelColors = ParseLevelColorsJSON(jsonContent);

        string report = $"Levels in JSON: {levelColors.Count}\n\n";
        int i = 0;
        foreach (var kvp in levelColors)
        {
            report += $"{kvp.Key}: {kvp.Value.Length} colors\n";
            i++;
            if (i >= 20) // Show first 20
            {
                report += $"... and {levelColors.Count - 20} more\n";
                break;
            }
        }

        EditorUtility.DisplayDialog("Level Colors Verification", report, "OK");
    }
}

#endif

