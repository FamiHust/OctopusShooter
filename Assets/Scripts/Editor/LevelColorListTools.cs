#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class LevelColorListTools
{
    private const string ReverseMenuPath = "FlowBlast Tools/Level Colors/Reverse listColor (Selected Level)";

    [MenuItem(ReverseMenuPath, true)]
    private static bool ValidateReverseSelectedLevelListColor()
    {
        return TryGetTargetLevelController(out _);
    }

    [MenuItem(ReverseMenuPath)]
    private static void ReverseSelectedLevelListColor()
    {
        if (!TryGetTargetLevelController(out LevelController levelController))
        {
            EditorUtility.DisplayDialog(
                "Reverse listColor",
                "Please select a level prefab (or object) that has a LevelController.",
                "OK");
            return;
        }

        ReverseListColor(levelController);
    }

    [MenuItem("CONTEXT/LevelController/Reverse listColor")]
    private static void ReverseLevelControllerListColor(MenuCommand command)
    {
        LevelController levelController = command.context as LevelController;
        if (levelController == null)
        {
            return;
        }

        ReverseListColor(levelController);
    }

    private static void ReverseListColor(LevelController levelController)
    {
        SerializedObject serializedObject = new SerializedObject(levelController);
        SerializedProperty listColorProperty = serializedObject.FindProperty("listColor");

        if (listColorProperty == null || !listColorProperty.isArray)
        {
            EditorUtility.DisplayDialog(
                "Reverse listColor",
                "Could not find listColor on LevelController.",
                "OK");
            return;
        }

        int count = listColorProperty.arraySize;
        if (count <= 1)
        {
            EditorUtility.DisplayDialog(
                "Reverse listColor",
                "listColor has 0 or 1 element, nothing to reverse.",
                "OK");
            return;
        }

        Undo.RecordObject(levelController, "Reverse Level listColor");

        for (int i = 0; i < count / 2; i++)
        {
            int j = count - 1 - i;

            SerializedProperty left = listColorProperty.GetArrayElementAtIndex(i);
            SerializedProperty right = listColorProperty.GetArrayElementAtIndex(j);

            int temp = left.enumValueIndex;
            left.enumValueIndex = right.enumValueIndex;
            right.enumValueIndex = temp;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(levelController);

        if (EditorUtility.IsPersistent(levelController))
        {
            AssetDatabase.SaveAssets();
        }
        else
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(levelController);
        }

        Debug.Log("Reversed listColor successfully.");
    }

    private static bool TryGetTargetLevelController(out LevelController levelController)
    {
        levelController = null;

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            return false;
        }

        levelController = selected.GetComponent<LevelController>();
        if (levelController != null)
        {
            return true;
        }

        levelController = selected.GetComponentInChildren<LevelController>(true);
        return levelController != null;
    }
}
#endif
