using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GridRowColReassignTool : EditorWindow
{
    private enum Axis
    {
        X,
        Y,
        Z
    }

    private GridController targetController;
    private bool includeInactive = true;
    private bool useHierarchyOrderLikeOnValidate = true;
    private bool useLocalPosition = true;
    private Axis rowAxis = Axis.Z;
    private Axis colAxis = Axis.X;
    private bool rowDescending = true;
    private bool colAscending = true;

    [MenuItem("Tools/FlowBlast/Grid/Reassign Row Col")]
    public static void Open()
    {
        GetWindow<GridRowColReassignTool>("Grid Reassign");
    }

    private void OnEnable()
    {
        if (targetController == null && Selection.activeGameObject != null)
        {
            targetController = Selection.activeGameObject.GetComponentInParent<GridController>();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Grid Row/Col Reassign Tool", EditorStyles.boldLabel);

        targetController = (GridController)EditorGUILayout.ObjectField(
            "Grid Controller",
            targetController,
            typeof(GridController),
            true);

        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        useHierarchyOrderLikeOnValidate = EditorGUILayout.Toggle("Use OnValidate Logic", useHierarchyOrderLikeOnValidate);

        using (new EditorGUI.DisabledScope(useHierarchyOrderLikeOnValidate))
        {
            useLocalPosition = EditorGUILayout.Toggle("Use Local Position", useLocalPosition);
            rowAxis = (Axis)EditorGUILayout.EnumPopup("Row Axis", rowAxis);
            colAxis = (Axis)EditorGUILayout.EnumPopup("Col Axis", colAxis);
            rowDescending = EditorGUILayout.Toggle("Row Descending", rowDescending);
            colAscending = EditorGUILayout.Toggle("Col Ascending", colAscending);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(targetController == null))
        {
            if (GUILayout.Button("Reassign Row/Col + Sync Nodes"))
            {
                ReassignNow();
            }
        }

        EditorGUILayout.HelpBox(
            "Use OnValidate Logic = ON: gan row/col theo thu tu child truc tiep duoi GridController (giong OnValidate).\n" +
            "Use OnValidate Logic = OFF: cho phep sap xep theo vi tri.",
            MessageType.Info);
    }

    private void ReassignNow()
    {
        if (targetController == null)
        {
            ;
            return;
        }

        SerializedObject so = new SerializedObject(targetController);
        SerializedProperty colProp = so.FindProperty("col");
        SerializedProperty rowProp = so.FindProperty("row");
        SerializedProperty nodesProp = so.FindProperty("nodes");
        SerializedProperty endNodesProp = so.FindProperty("endNodes");

        if (colProp == null || rowProp == null || nodesProp == null || endNodesProp == null)
        {
            ;
            return;
        }

        int columnCount = Mathf.Max(0, colProp.intValue);
        if (columnCount <= 0)
        {
            ;
            return;
        }

        List<GridItem> items = useHierarchyOrderLikeOnValidate
            ? CollectGridItemsByHierarchyOrder(targetController)
            : CollectAllGridItems(targetController);

        if (items.Count == 0)
        {
            ;
            return;
        }

        HashSet<GridItem> oldEndNodes = new HashSet<GridItem>();
        for (int i = 0; i < endNodesProp.arraySize; i++)
        {
            GridItem gi = endNodesProp.GetArrayElementAtIndex(i).objectReferenceValue as GridItem;
            if (gi != null)
            {
                oldEndNodes.Add(gi);
            }
        }

        List<GridItem> sortedItems = SortItems(items);

        List<Object> undoObjects = new List<Object>(sortedItems.Count + 1) { targetController };
        undoObjects.AddRange(sortedItems);
        Undo.RecordObjects(undoObjects.ToArray(), "Reassign Grid Row/Col");

        for (int index = 0; index < sortedItems.Count; index++)
        {
            GridItem item = sortedItems[index];
            int row = index / columnCount;
            int col = index % columnCount;
            item.SetGridCoordinate(row, col);
            EditorUtility.SetDirty(item);
        }

        rowProp.intValue = Mathf.CeilToInt((float)sortedItems.Count / columnCount);

        nodesProp.ClearArray();
        for (int i = 0; i < sortedItems.Count; i++)
        {
            nodesProp.InsertArrayElementAtIndex(i);
            nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = sortedItems[i];
        }

        List<GridItem> newEndNodes = sortedItems.Where(oldEndNodes.Contains).ToList();
        endNodesProp.ClearArray();
        for (int i = 0; i < newEndNodes.Count; i++)
        {
            endNodesProp.InsertArrayElementAtIndex(i);
            endNodesProp.GetArrayElementAtIndex(i).objectReferenceValue = newEndNodes[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetController);
        EditorSceneManager.MarkSceneDirty(targetController.gameObject.scene);

        ;
    }

    private List<GridItem> SortItems(List<GridItem> items)
    {
        if (useHierarchyOrderLikeOnValidate)
        {
            return items;
        }

        return items
            .OrderBy(item => GetAxisValue(item, rowAxis) * (rowDescending ? -1f : 1f))
            .ThenBy(item => GetAxisValue(item, colAxis) * (colAscending ? 1f : -1f))
            .ToList();
    }

    private List<GridItem> CollectGridItemsByHierarchyOrder(GridController controller)
    {
        var orderedItems = new List<GridItem>();
        if (controller == null)
        {
            return orderedItems;
        }

        Transform root = controller.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!includeInactive && !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            GridItem item = child.GetComponent<GridItem>();
            if (item != null)
            {
                orderedItems.Add(item);
            }
        }

        return orderedItems;
    }

    private List<GridItem> CollectAllGridItems(GridController controller)
    {
        GridItem[] allItems = controller.GetComponentsInChildren<GridItem>(includeInactive);
        return allItems != null ? allItems.Where(i => i != null).ToList() : new List<GridItem>();
    }

    private float GetAxisValue(GridItem item, Axis axis)
    {
        Vector3 p = useLocalPosition ? item.transform.localPosition : item.transform.position;
        switch (axis)
        {
            case Axis.X:
                return p.x;
            case Axis.Y:
                return p.y;
            default:
                return p.z;
        }
    }
}


