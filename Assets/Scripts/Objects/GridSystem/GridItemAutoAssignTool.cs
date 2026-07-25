using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class GridItemAutoAssignTool : MonoBehaviour
{
    [SerializeField] private GridController gridController;
    [SerializeField] private bool autoAssignInEditor = true;
    [SerializeField] private bool includeInactive = true;

    private bool isAssigning;

    private void OnValidate()
    {
        if (gridController == null)
        {
            gridController = GetComponent<GridController>();
        }
    }

    private void OnTransformChildrenChanged()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && autoAssignInEditor)
        {
            AssignNow();
        }
#endif
    }

    [ContextMenu("Assign Grid Row/Col Now")]
    public void AssignNow()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || isAssigning)
        {
            return;
        }

        isAssigning = true;
        try
        {
            if (gridController == null)
            {
                gridController = GetComponent<GridController>();
            }

            List<GridItem> items = CollectGridItems();
            if (items.Count == 0)
            {
                return;
            }

            int columnCount = ResolveColumnCount(items.Count);
            if (columnCount <= 0)
            {
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                GridItem item = items[index];
                if (item == null)
                {
                    continue;
                }

                int rowIndex = index / columnCount;
                int colIndex = index % columnCount;

                Undo.RecordObject(item, "Auto Assign Grid Row/Col");
                item.SetGridCoordinate(rowIndex, colIndex);
                EditorUtility.SetDirty(item);
            }

            if (gridController != null)
            {
                EditorUtility.SetDirty(gridController);
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        finally
        {
            isAssigning = false;
        }
#endif
    }

    private List<GridItem> CollectGridItems()
    {
        Transform root = null;
        if (gridController != null)
        {
            root = gridController.transform;
        }
        else
        {
            root = transform;
        }

        List<GridItem> orderedItems = new List<GridItem>();
        if (root != null)
        {
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
        }

        return orderedItems;
    }

    private int ResolveColumnCount(int itemCount)
    {
#if UNITY_EDITOR
        if (gridController != null)
        {
            SerializedObject so = new SerializedObject(gridController);
            SerializedProperty colProp = so.FindProperty("col");
            if (colProp != null && colProp.intValue > 0)
            {
                return colProp.intValue;
            }
        }
#endif

        // Fallback: coi toàn bộ nằm trên 1 hàng nếu không đọc được col.
        if (itemCount > 0)
        {
            return itemCount;
        }

        return 1;
    }
}
