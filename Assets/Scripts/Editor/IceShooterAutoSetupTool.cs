using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IceShooterAutoSetupTool
{
    private const string TargetNameToken = "Shooter_Ice";

    [MenuItem("Tools/FlowBlast/Shooter/Convert Shooter_Ice To IceShooter")]
    public static void ConvertShooterIceObjects()
    {
        int totalMatched = 0;
        int totalAddedIceShooter = 0;
        int totalRemovedBaseShooter = 0;

        ProcessLoadedScenes(ref totalMatched, ref totalAddedIceShooter, ref totalRemovedBaseShooter);
        ProcessPrefabAssets(ref totalMatched, ref totalAddedIceShooter, ref totalRemovedBaseShooter);

        string summary =
            "[IceShooterAutoSetupTool] Done. " +
            "Matched=" + totalMatched +
            ", Added IceShooter=" + totalAddedIceShooter +
            ", Removed BaseShooter=" + totalRemovedBaseShooter;

        ;
        EditorUtility.DisplayDialog("Ice Shooter Convert", summary, "OK");
    }

    private static void ProcessLoadedScenes(ref int totalMatched, ref int totalAddedIceShooter, ref int totalRemovedBaseShooter)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    Transform tr = transforms[t];
                    if (tr == null)
                    {
                        continue;
                    }

                    GameObject go = tr.gameObject;
                    if (!ContainsTargetName(go.name))
                    {
                        continue;
                    }

                    totalMatched++;
                    if (ConvertGameObject(go, true, out int added, out int removed))
                    {
                        sceneChanged = true;
                        totalAddedIceShooter += added;
                        totalRemovedBaseShooter += removed;
                    }
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }

    private static void ProcessPrefabAssets(ref int totalMatched, ref int totalAddedIceShooter, ref int totalRemovedBaseShooter)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null)
            {
                continue;
            }

            bool prefabChanged = false;

            try
            {
                Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    Transform tr = transforms[t];
                    if (tr == null)
                    {
                        continue;
                    }

                    GameObject go = tr.gameObject;
                    if (!ContainsTargetName(go.name))
                    {
                        continue;
                    }

                    totalMatched++;
                    if (ConvertGameObject(go, false, out int added, out int removed))
                    {
                        prefabChanged = true;
                        totalAddedIceShooter += added;
                        totalRemovedBaseShooter += removed;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static bool ConvertGameObject(GameObject go, bool useUndo, out int addedIceShooter, out int removedBaseShooter)
    {
        addedIceShooter = 0;
        removedBaseShooter = 0;

        if (go == null)
        {
            return false;
        }

        bool changed = false;

        IceShooter existingIceShooter = go.GetComponent<IceShooter>();
        if (existingIceShooter == null)
        {
            if (useUndo)
            {
                Undo.AddComponent<IceShooter>(go);
            }
            else
            {
                go.AddComponent<IceShooter>();
            }

            addedIceShooter = 1;
            changed = true;
        }

        BaseShooter[] baseShooters = go.GetComponents<BaseShooter>();
        for (int i = 0; i < baseShooters.Length; i++)
        {
            BaseShooter baseShooter = baseShooters[i];
            if (baseShooter == null || baseShooter.GetType() != typeof(BaseShooter))
            {
                continue;
            }

            if (useUndo)
            {
                Undo.DestroyObjectImmediate(baseShooter);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(baseShooter);
            }

            removedBaseShooter++;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(go);
        }

        return changed;
    }

    private static bool ContainsTargetName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
               && objectName.IndexOf(TargetNameToken, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

