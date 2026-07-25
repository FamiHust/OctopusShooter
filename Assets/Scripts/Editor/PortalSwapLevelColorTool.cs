#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PortalSwapLevelColorTool : EditorWindow
{
    private const int RowsPerColor = 10;

    private static readonly SeedColor[] SeedColorValues =
        (SeedColor[])Enum.GetValues(typeof(SeedColor));

    [SerializeField] private GameObject levelPrefabOrRoot;

    private string lastReport = string.Empty;
    private Vector2 scrollPosition;

    [MenuItem("FlowBlast Tools/Level Colors/Portal Swap Enforcer")]
    public static void Open()
    {
        GetWindow<PortalSwapLevelColorTool>("Portal Swap Enforcer");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Drag a level prefab (with LevelController) and apply an ordering that encourages mandatory portal swap usage.",
            MessageType.Info);

        levelPrefabOrRoot = (GameObject)EditorGUILayout.ObjectField(
            "Level Prefab",
            levelPrefabOrRoot,
            typeof(GameObject),
            false);

        EditorGUILayout.Space(6f);

        using (new EditorGUI.DisabledScope(levelPrefabOrRoot == null))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Analyze", GUILayout.Height(28f)))
            {
                RunTool(false);
            }

            if (GUILayout.Button("Apply Enforced Order", GUILayout.Height(28f)))
            {
                RunTool(true);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);

        using (var scope = new EditorGUILayout.ScrollViewScope(scrollPosition, GUILayout.MinHeight(220f)))
        {
            scrollPosition = scope.scrollPosition;
            EditorGUILayout.TextArea(string.IsNullOrEmpty(lastReport) ? "No report yet." : lastReport, GUILayout.ExpandHeight(true));
        }
    }

    private void RunTool(bool apply)
    {
        if (levelPrefabOrRoot == null)
        {
            lastReport = "No level prefab selected.";
            return;
        }

        bool isPersistentAsset = EditorUtility.IsPersistent(levelPrefabOrRoot);

        if (isPersistentAsset)
        {
            string path = AssetDatabase.GetAssetPath(levelPrefabOrRoot);
            if (string.IsNullOrEmpty(path))
            {
                lastReport = "Could not resolve prefab path.";
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!TryProcessLevelRoot(prefabRoot, apply, out string report, out bool changed))
                {
                    lastReport = report;
                    return;
                }

                if (apply && changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                lastReport = report;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return;
        }

        if (!TryProcessLevelRoot(levelPrefabOrRoot, apply, out string sceneReport, out bool sceneChanged))
        {
            lastReport = sceneReport;
            return;
        }

        if (apply && sceneChanged)
        {
            EditorSceneMarkDirty(levelPrefabOrRoot.scene);
        }

        lastReport = sceneReport;
    }

    private static void EditorSceneMarkDirty(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        EditorSceneManagerBridge.MarkSceneDirty(scene);
    }

    private bool TryProcessLevelRoot(GameObject root, bool apply, out string report, out bool changed)
    {
        changed = false;
        if (root == null)
        {
            report = "Root object is null.";
            return false;
        }

        LevelController levelController = root.GetComponent<LevelController>();
        if (levelController == null)
        {
            levelController = root.GetComponentInChildren<LevelController>(true);
        }

        if (levelController == null)
        {
            report = "Could not find LevelController in selected object/prefab.";
            return false;
        }

        PortalShooter[] portalShooters = root.GetComponentsInChildren<PortalShooter>(true);
        if (portalShooters == null || portalShooters.Length == 0)
        {
            report = "No PortalShooter found. Tool skipped because portal swap mechanic is not present in this level.";
            return false;
        }

        HashSet<SeedColor> normalShooterColors = CollectNormalShooterColors(root);
        HashSet<SeedColor> portalShooterColors = CollectPortalShooterColors(root);

        SerializedObject so = new SerializedObject(levelController);
        SerializedProperty listColorProp = so.FindProperty("listColor");
        if (listColorProp == null || !listColorProp.isArray)
        {
            report = "Could not locate listColor on LevelController.";
            return false;
        }

        List<SeedColor> original = ReadSeedColorList(listColorProp);
        if (original.Count == 0)
        {
            report = "listColor is empty. Nothing to enforce.";
            return false;
        }

        int mainRows = ReadMainRowCount(so);
        List<int> sideRows = ReadSideRowCounts(so);

        if (!BuildPortalSwapEnforcedOrder(
                original,
                mainRows,
                sideRows,
                normalShooterColors,
                portalShooterColors,
                out List<SeedColor> proposed,
                out string reasoning))
        {
            report = reasoning;
            return false;
        }

        changed = !AreListsEqual(original, proposed);

        if (apply && changed)
        {
            Undo.RecordObject(levelController, "Enforce Portal Swap listColor");
            WriteSeedColorList(listColorProp, proposed);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(levelController);

            if (!EditorUtility.IsPersistent(levelController))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(levelController);
            }
        }

        report = BuildReport(
            levelController,
            original,
            proposed,
            changed,
            apply,
            mainRows,
            sideRows,
            normalShooterColors,
            portalShooterColors,
            reasoning);

        return true;
    }

    private static HashSet<SeedColor> CollectNormalShooterColors(GameObject root)
    {
        var colors = new HashSet<SeedColor>();
        if (root == null)
        {
            return colors;
        }

        BaseShooter[] shooters = root.GetComponentsInChildren<BaseShooter>(true);
        for (int i = 0; i < shooters.Length; i++)
        {
            BaseShooter shooter = shooters[i];
            if (shooter == null)
            {
                continue;
            }

            if (shooter is PortalShooter)
            {
                continue;
            }

            colors.Add(shooter.GetTargetColor());
        }

        return colors;
    }

    private static HashSet<SeedColor> CollectPortalShooterColors(GameObject root)
    {
        var colors = new HashSet<SeedColor>();
        if (root == null)
        {
            return colors;
        }

        PortalShooter[] portalShooters = root.GetComponentsInChildren<PortalShooter>(true);
        for (int i = 0; i < portalShooters.Length; i++)
        {
            PortalShooter shooter = portalShooters[i];
            if (shooter != null)
            {
                colors.Add(shooter.GetTargetColor());
            }
        }

        return colors;
    }

    private static int ReadMainRowCount(SerializedObject so)
    {
        SerializedProperty mainRowsProp = so.FindProperty("data.countMainRow");
        return mainRowsProp != null ? Mathf.Max(0, mainRowsProp.intValue) : 0;
    }

    private static List<int> ReadSideRowCounts(SerializedObject so)
    {
        var result = new List<int>();
        SerializedProperty sideRowsProp = so.FindProperty("data.countSideRows");
        if (sideRowsProp == null || !sideRowsProp.isArray)
        {
            return result;
        }

        for (int i = 0; i < sideRowsProp.arraySize; i++)
        {
            SerializedProperty element = sideRowsProp.GetArrayElementAtIndex(i);
            if (element != null)
            {
                result.Add(Mathf.Max(0, element.intValue));
            }
        }

        return result;
    }

    private static List<SeedColor> ReadSeedColorList(SerializedProperty listColorProp)
    {
        var list = new List<SeedColor>(listColorProp.arraySize);
        for (int i = 0; i < listColorProp.arraySize; i++)
        {
            SerializedProperty p = listColorProp.GetArrayElementAtIndex(i);
            int enumIndex = p != null ? p.enumValueIndex : 0;
            if (enumIndex < 0 || enumIndex >= SeedColorValues.Length)
            {
                enumIndex = 0;
            }

            list.Add(SeedColorValues[enumIndex]);
        }

        return list;
    }

    private static void WriteSeedColorList(SerializedProperty listColorProp, List<SeedColor> values)
    {
        listColorProp.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            SerializedProperty element = listColorProp.GetArrayElementAtIndex(i);
            if (element == null)
            {
                continue;
            }

            int enumIndex = Array.IndexOf(SeedColorValues, values[i]);
            element.enumValueIndex = Mathf.Max(0, enumIndex);
        }
    }

    private static bool BuildPortalSwapEnforcedOrder(
        List<SeedColor> source,
        int mainRows,
        List<int> sideRows,
        HashSet<SeedColor> normalShooterColors,
        HashSet<SeedColor> portalShooterColors,
        out List<SeedColor> result,
        out string reasoning)
    {
        result = new List<SeedColor>(source.Count);

        if (source.Count == 0)
        {
            reasoning = "listColor has no entries.";
            return false;
        }

        if (normalShooterColors.Count == 0)
        {
            reasoning = "No non-portal shooters found. Tool cannot infer normal playable color set.";
            return false;
        }

        int mainSlots = Mathf.Clamp(Mathf.CeilToInt(mainRows / (float)RowsPerColor), 0, source.Count);

        int sideASlots = sideRows != null && sideRows.Count > 0
            ? Mathf.Max(0, Mathf.CeilToInt(sideRows[0] / (float)RowsPerColor))
            : 0;

        int sideBSlots = sideRows != null && sideRows.Count > 1
            ? Mathf.Max(0, Mathf.CeilToInt(sideRows[1] / (float)RowsPerColor))
            : 0;

        int remainingAfterMain = Mathf.Max(0, source.Count - mainSlots);
        sideASlots = Mathf.Min(sideASlots, remainingAfterMain);
        sideBSlots = Mathf.Min(sideBSlots, Mathf.Max(0, remainingAfterMain - sideASlots));

        if (sideASlots <= 0 || sideBSlots <= 0)
        {
            reasoning =
                "Level does not expose at least two side-route color segments (after main route capacity), so enforcing portal swap dependency is unreliable.";
            return false;
        }

        Dictionary<SeedColor, int> pool = BuildCountMap(source);
        SeedColor requiredPlayableColor = PickBestColor(pool, normalShooterColors, null);
        SeedColor blockerColor = PickBestBlockerColor(pool, normalShooterColors, requiredPlayableColor, portalShooterColors);

        if (!pool.ContainsKey(requiredPlayableColor) || pool[requiredPlayableColor] <= 0)
        {
            reasoning = "Could not find a playable shooter color in listColor to stage as swap-required color.";
            return false;
        }

        int reserveForSideB = Mathf.Min(sideBSlots, pool[requiredPlayableColor]);
        if (reserveForSideB <= 0)
        {
            reserveForSideB = 1;
        }

        // Segment 1: Main route colors (prefer normal shooter colors, but keep required color for side B).
        for (int i = 0; i < mainSlots; i++)
        {
            if (!TryTakeColor(pool, c =>
                    normalShooterColors.Contains(c) &&
                    (c != requiredPlayableColor || pool[c] > reserveForSideB),
                    out SeedColor picked))
            {
                if (!TryTakeColor(pool, c => c != requiredPlayableColor || pool[c] > reserveForSideB, out picked))
                {
                    if (!TryTakeAny(pool, out picked))
                    {
                        break;
                    }
                }
            }

            result.Add(picked);
        }

        // Segment 2: Side A colors (prefer blocker colors not in normal shooter colors).
        for (int i = 0; i < sideASlots; i++)
        {
            if (!TryTakeColor(pool, c => c.Equals(blockerColor), out SeedColor picked))
            {
                if (!TryTakeColor(pool, c => !normalShooterColors.Contains(c), out picked))
                {
                    if (!TryTakeAny(pool, out picked))
                    {
                        break;
                    }
                }
            }

            result.Add(picked);
        }

        // Segment 3: Side B colors (prioritize required playable color).
        for (int i = 0; i < sideBSlots; i++)
        {
            if (!TryTakeColor(pool, c => c.Equals(requiredPlayableColor), out SeedColor picked))
            {
                if (!TryTakeColor(pool, c => normalShooterColors.Contains(c), out picked))
                {
                    if (!TryTakeAny(pool, out picked))
                    {
                        break;
                    }
                }
            }

            result.Add(picked);
        }

        // Segment 4: Remaining colors.
        while (result.Count < source.Count && TryTakeAny(pool, out SeedColor rest))
        {
            result.Add(rest);
        }

        if (result.Count != source.Count)
        {
            reasoning = "Generated listColor count mismatch. Aborting for safety.";
            return false;
        }

        reasoning =
            $"Enforced pattern: main={mainSlots} slots, sideA={sideASlots} slots (blockers), sideB={sideBSlots} slots (playable {requiredPlayableColor}). " +
            $"Portal colors in level: {string.Join(", ", portalShooterColors.OrderBy(c => c.ToString()))}.";

        return true;
    }

    private static Dictionary<SeedColor, int> BuildCountMap(List<SeedColor> source)
    {
        var map = new Dictionary<SeedColor, int>();
        for (int i = 0; i < source.Count; i++)
        {
            SeedColor color = source[i];
            if (!map.TryGetValue(color, out int count))
            {
                count = 0;
            }

            map[color] = count + 1;
        }

        return map;
    }

    private static SeedColor PickBestColor(Dictionary<SeedColor, int> pool, HashSet<SeedColor> candidates, SeedColor? except)
    {
        SeedColor best = default;
        int bestCount = -1;

        foreach (SeedColor color in candidates)
        {
            if (except.HasValue && color.Equals(except.Value))
            {
                continue;
            }

            if (!pool.TryGetValue(color, out int count) || count <= 0)
            {
                continue;
            }

            if (count > bestCount)
            {
                best = color;
                bestCount = count;
            }
        }

        if (bestCount >= 0)
        {
            return best;
        }

        foreach (var kv in pool)
        {
            if (except.HasValue && kv.Key.Equals(except.Value))
            {
                continue;
            }

            if (kv.Value > bestCount)
            {
                best = kv.Key;
                bestCount = kv.Value;
            }
        }

        return best;
    }

    private static SeedColor PickBestBlockerColor(
        Dictionary<SeedColor, int> pool,
        HashSet<SeedColor> normalShooterColors,
        SeedColor requiredColor,
        HashSet<SeedColor> portalShooterColors)
    {
        SeedColor best = default;
        int bestCount = -1;

        foreach (var kv in pool)
        {
            if (kv.Value <= 0)
            {
                continue;
            }

            if (kv.Key.Equals(requiredColor))
            {
                continue;
            }

            if (normalShooterColors.Contains(kv.Key))
            {
                continue;
            }

            if (kv.Value > bestCount)
            {
                best = kv.Key;
                bestCount = kv.Value;
            }
        }

        if (bestCount >= 0)
        {
            return best;
        }

        foreach (SeedColor portalColor in portalShooterColors)
        {
            if (portalColor.Equals(requiredColor))
            {
                continue;
            }

            if (pool.TryGetValue(portalColor, out int count) && count > bestCount)
            {
                best = portalColor;
                bestCount = count;
            }
        }

        if (bestCount >= 0)
        {
            return best;
        }

        return PickBestColor(pool, new HashSet<SeedColor>(pool.Keys), requiredColor);
    }

    private static bool TryTakeColor(
        Dictionary<SeedColor, int> pool,
        Func<SeedColor, bool> predicate,
        out SeedColor picked)
    {
        SeedColor best = default;
        int bestCount = -1;
        bool found = false;

        foreach (var kv in pool)
        {
            if (kv.Value <= 0)
            {
                continue;
            }

            if (!predicate(kv.Key))
            {
                continue;
            }

            if (kv.Value > bestCount)
            {
                best = kv.Key;
                bestCount = kv.Value;
                found = true;
            }
        }

        if (!found)
        {
            picked = default;
            return false;
        }

        pool[best] = pool[best] - 1;
        picked = best;
        return true;
    }

    private static bool TryTakeAny(Dictionary<SeedColor, int> pool, out SeedColor picked)
    {
        return TryTakeColor(pool, _ => true, out picked);
    }

    private static bool AreListsEqual(List<SeedColor> a, List<SeedColor> b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildReport(
        LevelController levelController,
        List<SeedColor> original,
        List<SeedColor> proposed,
        bool changed,
        bool apply,
        int mainRows,
        List<int> sideRows,
        HashSet<SeedColor> normalShooterColors,
        HashSet<SeedColor> portalShooterColors,
        string reasoning)
    {
        string levelName = levelController != null ? levelController.name : "Unknown";
        string sideRowsText = sideRows == null || sideRows.Count == 0
            ? "none"
            : string.Join(", ", sideRows);

        string status;
        if (!apply)
        {
            status = changed ? "ANALYZE: Reordering suggested." : "ANALYZE: listColor already matches enforced pattern.";
        }
        else
        {
            status = changed ? "APPLY: listColor updated." : "APPLY: no changes required.";
        }

        return
            $"{status}\n" +
            $"Level: {levelName}\n" +
            $"MainRows: {mainRows} | SideRows: [{sideRowsText}]\n" +
            $"Normal shooter colors: {FormatColorSet(normalShooterColors)}\n" +
            $"Portal shooter colors: {FormatColorSet(portalShooterColors)}\n" +
            $"Reasoning: {reasoning}\n\n" +
            $"Original listColor:\n{FormatColorList(original)}\n\n" +
            $"Proposed listColor:\n{FormatColorList(proposed)}";
    }

    private static string FormatColorSet(HashSet<SeedColor> colors)
    {
        if (colors == null || colors.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", colors.OrderBy(c => c.ToString()));
    }

    private static string FormatColorList(List<SeedColor> colors)
    {
        if (colors == null || colors.Count == 0)
        {
            return "(empty)";
        }

        return string.Join(", ", colors);
    }

    private static class EditorSceneManagerBridge
    {
        public static void MarkSceneDirty(UnityEngine.SceneManagement.Scene scene)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
