#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class MissingScriptCleanerWindow : EditorWindow
{
    private GameObject targetPrefab;
    private int lastScanMissingCount;
    private Vector2 scroll;
    private string lastResult = string.Empty;

    [MenuItem("Tools/FlowBlast/Missing Script Cleaner")]
    public static void OpenWindow()
    {
        MissingScriptCleanerWindow window = GetWindow<MissingScriptCleanerWindow>("Missing Script Cleaner");
        window.minSize = new Vector2(420f, 260f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prefab Missing Script Cleaner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag a prefab asset here, scan missing scripts, then remove them from the prefab and all child objects.", MessageType.Info);

        DrawPrefabField();
        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(targetPrefab == null))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Scan Missing Scripts", GUILayout.Height(28f)))
            {
                ScanMissingScripts();
            }

            if (GUILayout.Button("Remove Missing Scripts", GUILayout.Height(28f)))
            {
                RemoveMissingScripts();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Last Scan Missing Count: {lastScanMissingCount}");

        if (!string.IsNullOrEmpty(lastResult))
        {
            EditorGUILayout.HelpBox(lastResult, MessageType.None);
        }

        DrawDropArea();
    }

    void DrawPrefabField()
    {
        EditorGUI.BeginChangeCheck();
        GameObject picked = (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            targetPrefab = IsPrefabAsset(picked) ? picked : null;
            lastScanMissingCount = 0;
            lastResult = targetPrefab == null && picked != null
                ? "Selected object is not a prefab asset. Please choose a prefab from Project window."
                : string.Empty;
        }
    }

    void DrawDropArea()
    {
        EditorGUILayout.Space(8f);
        Rect dropRect = GUILayoutUtility.GetRect(0f, 72f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drop Prefab Here", EditorStyles.helpBox);

        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            bool hasPrefab = false;
            Object[] refs = DragAndDrop.objectReferences;
            for (int i = 0; i < refs.Length; i++)
            {
                GameObject go = refs[i] as GameObject;
                if (IsPrefabAsset(go))
                {
                    hasPrefab = true;
                    break;
                }
            }

            DragAndDrop.visualMode = hasPrefab ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (evt.type == EventType.DragPerform && hasPrefab)
            {
                DragAndDrop.AcceptDrag();
                for (int i = 0; i < refs.Length; i++)
                {
                    GameObject go = refs[i] as GameObject;
                    if (IsPrefabAsset(go))
                    {
                        targetPrefab = go;
                        lastScanMissingCount = 0;
                        lastResult = string.Empty;
                        Repaint();
                        break;
                    }
                }
            }

            evt.Use();
        }
    }

    void ScanMissingScripts()
    {
        if (targetPrefab == null)
        {
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(targetPrefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            lastResult = "Could not resolve prefab asset path.";
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            lastResult = "Failed to load prefab contents.";
            return;
        }

        int count = CountMissingScriptsInHierarchy(root.transform);
        lastScanMissingCount = count;
        lastResult = count > 0
            ? $"Found {count} missing script reference(s)."
            : "No missing scripts found.";

        PrefabUtility.UnloadPrefabContents(root);
    }

    void RemoveMissingScripts()
    {
        if (targetPrefab == null)
        {
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(targetPrefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            lastResult = "Could not resolve prefab asset path.";
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            lastResult = "Failed to load prefab contents.";
            return;
        }

        int removed = RemoveMissingScriptsInHierarchy(root.transform);
        if (removed > 0)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        PrefabUtility.UnloadPrefabContents(root);

        lastScanMissingCount = Mathf.Max(0, lastScanMissingCount - removed);
        lastResult = removed > 0
            ? $"Removed {removed} missing script reference(s) from prefab."
            : "No missing scripts to remove.";
    }

    int CountMissingScriptsInHierarchy(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        int total = 0;
        total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject);

        for (int i = 0; i < root.childCount; i++)
        {
            total += CountMissingScriptsInHierarchy(root.GetChild(i));
        }

        return total;
    }

    int RemoveMissingScriptsInHierarchy(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root.gameObject);

        for (int i = 0; i < root.childCount; i++)
        {
            removed += RemoveMissingScriptsInHierarchy(root.GetChild(i));
        }

        return removed;
    }

    bool IsPrefabAsset(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        return PrefabUtility.IsPartOfPrefabAsset(go);
    }
}
#endif
