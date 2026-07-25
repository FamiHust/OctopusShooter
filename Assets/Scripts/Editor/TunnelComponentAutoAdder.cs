#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TunnelComponentAutoAdder
{
    private const string TunnelNameToken = "Tunel_";
    private const string LevelPrefabToken = "Level";

    [MenuItem("Tools/FlowBlast/Tunnel/Add Tunnel Component In Level Prefabs")]
    public static void AddTunnelComponentInLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        if (prefabGuids == null || prefabGuids.Length == 0)
        {
            ;
            return;
        }

        int scannedPrefabs = 0;
        int changedPrefabs = 0;
        int scannedObjects = 0;
        int matchedByName = 0;
        int addedCount = 0;
        int alreadyHasComponent = 0;

        for (int guidIndex = 0; guidIndex < prefabGuids.Length; guidIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[guidIndex]);
            if (string.IsNullOrEmpty(prefabPath))
            {
                continue;
            }

            string prefabFileName = Path.GetFileNameWithoutExtension(prefabPath);
            if (prefabFileName.IndexOf(LevelPrefabToken, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            scannedPrefabs++;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                continue;
            }

            bool prefabChanged = false;
            Transform[] allChildren = prefabRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                Transform tr = allChildren[i];
                if (tr == null)
                {
                    continue;
                }

                scannedObjects++;

                if (tr.name.IndexOf(TunnelNameToken, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                matchedByName++;
                GameObject go = tr.gameObject;
                if (go.GetComponent<Tunnel>() != null)
                {
                    alreadyHasComponent++;
                    continue;
                }

                go.AddComponent<Tunnel>();
                addedCount++;
                prefabChanged = true;
            }

            if (prefabChanged)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                changedPrefabs++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ;
    }
}
#endif

