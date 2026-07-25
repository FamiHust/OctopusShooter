#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards Unity Inspector from null references when selection contains destroyed objects
/// (commonly after play mode transitions or editor tools destroying scene objects).
/// </summary>
[InitializeOnLoad]
public static class SelectionNullGuard
{
    static SelectionNullGuard()
    {
        EditorApplication.update += CleanupNullSelection;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            CleanupNullSelection();
        }
    }

    private static void CleanupNullSelection()
    {
        Object[] current = Selection.objects;
        if (current == null || current.Length == 0)
        {
            return;
        }

        bool hasNull = false;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] == null)
            {
                hasNull = true;
                break;
            }
        }

        if (!hasNull)
        {
            return;
        }

        Object[] cleaned = current.Where(obj => obj != null).ToArray();
        Selection.objects = cleaned;

        if (Selection.activeObject == null && cleaned.Length > 0)
        {
            Selection.activeObject = cleaned[0];
        }
    }
}
#endif
