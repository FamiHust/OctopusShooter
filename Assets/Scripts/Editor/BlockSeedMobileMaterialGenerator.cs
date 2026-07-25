using UnityEditor;
using UnityEngine;

public static class BlockSeedMobileMaterialGenerator
{
    private const string OutputFolder = "Assets/Material/Mobile";
    private const string OutputPath = OutputFolder + "/M_BlockSeed_MobileOptimized.mat";
    private const string ReferenceBlockMaterialPath = "Assets/Material/M_BaseBlock.mat";

    [InitializeOnLoadMethod]
    private static void EnsureMaterialExistsOnEditorLoad()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(OutputPath) != null)
        {
            return;
        }

        CreateOrUpdateMaterial(logResult: false);
    }

    [MenuItem("Tools/FlowBlast/Create Mobile Block Seed Material")]
    private static void CreateMaterialFromMenu()
    {
        CreateOrUpdateMaterial(logResult: true);
    }

    private static void CreateOrUpdateMaterial(bool logResult)
    {
        EnsureFolderExists(OutputFolder);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutputPath);
        bool isNew = material == null;

        Shader shader = ResolveBestMobileShader();
        if (shader == null)
        {
            if (logResult)
            {
                ;
            }
            return;
        }

        if (isNew)
        {
            material = new Material(shader)
            {
                name = "M_BlockSeed_MobileOptimized"
            };
            AssetDatabase.CreateAsset(material, OutputPath);
        }
        else
        {
            material.shader = shader;
        }

        ApplyLowCostSettings(material, shader);
        TryCopyBaseTexture(material);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        if (logResult)
        {
            ;
        }
    }

    private static Shader ResolveBestMobileShader()
    {
        string[] candidates =
        {
            "Mobile/Diffuse",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Lit",
            "Legacy Shaders/Diffuse",
            "Standard"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Shader shader = Shader.Find(candidates[i]);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static void ApplyLowCostSettings(Material material, Shader shader)
    {
        if (material == null || shader == null)
        {
            return;
        }

        material.enableInstancing = true;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        // Keep PBR controls active so metallic/smoothness edits have visible impact.
        if (material.HasProperty("_SpecularHighlights"))
        {
            material.SetFloat("_SpecularHighlights", 1f);
        }

        if (material.HasProperty("_GlossyReflections"))
        {
            material.SetFloat("_GlossyReflections", 1f);
        }

        if (material.HasProperty("_EnvironmentReflections"))
        {
            material.SetFloat("_EnvironmentReflections", 1f);
        }

        if (material.HasProperty("_ReceiveShadows"))
        {
            material.SetFloat("_ReceiveShadows", 0f);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }
    }

    private static void TryCopyBaseTexture(Material target)
    {
        if (target == null)
        {
            return;
        }

        Material source = AssetDatabase.LoadAssetAtPath<Material>(ReferenceBlockMaterialPath);
        if (source == null)
        {
            return;
        }

        Texture sourceTex = null;
        if (source.HasProperty("_MainTex"))
        {
            sourceTex = source.GetTexture("_MainTex");
        }

        if (sourceTex == null && source.HasProperty("_TextureSample0"))
        {
            sourceTex = source.GetTexture("_TextureSample0");
        }

        if (sourceTex == null)
        {
            return;
        }

        if (target.HasProperty("_MainTex"))
        {
            target.SetTexture("_MainTex", sourceTex);
        }

        if (target.HasProperty("_BaseMap"))
        {
            target.SetTexture("_BaseMap", sourceTex);
        }

        if (target.HasProperty("_TextureSample0"))
        {
            target.SetTexture("_TextureSample0", sourceTex);
        }
    }

    private static void EnsureFolderExists(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        if (parts.Length < 2)
        {
            return;
        }

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}

