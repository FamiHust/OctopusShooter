using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RemoveMissingScriptsProjectWide
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Project")]
    public static void RemoveAllMissingScriptsInProject()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        int prefabsChanged = 0;
        int prefabObjectsChanged = 0;
        int prefabsRemovedCount = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            int changedObjectsOnPrefab;
            int removedOnPrefab = RemoveMissingScriptsInHierarchy(prefabRoot, out changedObjectsOnPrefab);

            if (removedOnPrefab > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                prefabsChanged++;
                prefabObjectsChanged += changedObjectsOnPrefab;
                prefabsRemovedCount += removedOnPrefab;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        int scenesChanged = 0;
        int sceneObjectsChanged = 0;
        int scenesRemovedCount = 0;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                int changedObjectsInScene = 0;
                int removedInScene = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    int changedObjectsOnRoot;
                    removedInScene += RemoveMissingScriptsInHierarchy(roots[r], out changedObjectsOnRoot);
                    changedObjectsInScene += changedObjectsOnRoot;
                }

                if (removedInScene > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                    scenesChanged++;
                    sceneObjectsChanged += changedObjectsInScene;
                    scenesRemovedCount += removedInScene;
                }
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }

    private static int RemoveMissingScriptsInHierarchy(GameObject root, out int changedObjects)
    {
        changedObjects = 0;
        int removedCount = 0;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject go = transforms[i].gameObject;
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount <= 0)
            {
                continue;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removedCount += missingCount;
            changedObjects++;
        }

        return removedCount;
    }
}
