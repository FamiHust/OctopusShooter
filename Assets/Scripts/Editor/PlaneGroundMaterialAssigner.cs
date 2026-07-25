using System;
using UnityEditor;
using UnityEngine;

public static class PlaneGroundMaterialAssigner
{
    private const string TargetMaterialName = "M_Ground";

    [MenuItem("Tools/FlowBlast/Assign M_Ground To Plane Objects")]
    public static void AssignGroundMaterialToPlaneObjects()
    {
        Material targetMaterial = FindTargetMaterial();
        if (targetMaterial == null)
        {
            ;
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int processedPrefabs = 0;
        int matchedObjects = 0;
        int changedRenderers = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(prefabPath))
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                continue;
            }

            bool prefabChanged = false;

            try
            {
                processedPrefabs++;
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                    {
                        continue;
                    }

                    GameObject go = renderer.gameObject;
                    if (go == null)
                    {
                        continue;
                    }

                    if (go.name.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    matchedObjects++;

                    Material[] mats = renderer.sharedMaterials;
                    if (mats == null || mats.Length == 0)
                    {
                        continue;
                    }

                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != targetMaterial)
                        {
                            mats[m] = targetMaterial;
                            changed = true;
                        }
                    }

                    if (!changed)
                    {
                        continue;
                    }

                    renderer.sharedMaterials = mats;
                    EditorUtility.SetDirty(renderer);
                    changedRenderers++;
                    prefabChanged = true;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ;
    }

    private static Material FindTargetMaterial()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material M_Ground");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.name == TargetMaterialName)
            {
                return mat;
            }
        }

        return null;
    }
}

