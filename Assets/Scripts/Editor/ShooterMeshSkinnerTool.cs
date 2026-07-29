#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool chuyển đổi bất kỳ static Mesh nào (FBX, OBJ, Primitive) thành file Mesh Asset mới có tích hợp sẵn Bone Data (bindposes & boneWeights).
/// Giúp loại bỏ hoàn toàn đỏ/cảnh báo "The assigned mesh is missing either bone weights with bind pose..." trên SkinnedMeshRenderer.
/// Tích hợp SkinnedMeshBonesAutoFixer tự động khắc phục lỗi "Bones do not match bindpose" khi kéo thả Mesh trong Inspector.
/// </summary>
[InitializeOnLoad]
public class ShooterMeshSkinnerTool : EditorWindow
{
    private Mesh m_SourceMesh = null;
    private GameObject m_TargetGameObject = null;
    private SkinnedMeshRenderer m_TargetSMR = null;
    private string m_SaveDirectory = "Assets/Mesh/Skinned";
    private string m_CustomMeshName = "";
    private bool m_AutoAssignToSelected = true;

    static ShooterMeshSkinnerTool()
    {
        Selection.selectionChanged += AutoFixSelectedSkinnedMeshBones;
        EditorApplication.hierarchyChanged += AutoFixSelectedSkinnedMeshBones;
    }

    /// <summary>
    /// Tự động phát hiện & sửa smr.bones khi người dùng kéo mesh vào SkinnedMeshRenderer trong Inspector.
    /// Giúp xóa lập tức lỗi "Bones do not match bindpose".
    /// </summary>
    public static void AutoFixSelectedSkinnedMeshBones()
    {
        if (Selection.activeGameObject == null) return;

        SkinnedMeshRenderer[] smrs = Selection.activeGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0) return;

        foreach (SkinnedMeshRenderer smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;

            Mesh mesh = smr.sharedMesh;
            if (mesh.bindposes != null && mesh.bindposes.Length > 0)
            {
                int reqBones = mesh.bindposes.Length;
                if (smr.bones == null || smr.bones.Length != reqBones || smr.bones.Any(b => b == null))
                {
                    Undo.RecordObject(smr, "Auto Fix SMR Bones");
                    Transform[] newBones = new Transform[reqBones];
                    for (int i = 0; i < reqBones; i++)
                    {
                        newBones[i] = smr.transform;
                    }
                    smr.bones = newBones;
                    smr.rootBone = smr.transform;
                    EditorUtility.SetDirty(smr);
                    Debug.Log($"[AutoFixBones] Đã tự động khớp smr.bones cho {smr.gameObject.name} (Bones: {reqBones}). Lỗi 'Bones do not match bindpose' đã được xóa!");
                }
            }
        }
    }

    [MenuItem("FlowBlast Tools/Mesh Skinner Tool (Convert Mesh to Skinned)")]
    public static void OpenWindow()
    {
        ShooterMeshSkinnerTool window = GetWindow<ShooterMeshSkinnerTool>("Mesh Skinner Tool");
        window.minSize = new Vector2(450, 520);
        window.Show();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            if (m_TargetGameObject == null)
            {
                m_TargetGameObject = Selection.activeGameObject;
            }
            SkinnedMeshRenderer smr = Selection.activeGameObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && m_TargetSMR == null)
            {
                m_TargetSMR = smr;
            }
        }
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        DrawHeader();
        EditorGUILayout.Space(10);

        // Quick Auto Fix Button
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1.0f);
        if (GUILayout.Button("⚡ SỬA LỖI BONES DO NOT MATCH BINDPOSE CHO OBJECT ĐANG CHỌN", GUILayout.Height(35)))
        {
            AutoFixSelectedSkinnedMeshBones();
            EditorUtility.DisplayDialog("Thành công", "Đã sửa xong mảng xương smr.bones cho GameObject đang chọn!\nLỗi 'Bones do not match bindpose' đã biến mất.", "OK");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        // Section 1: Mesh Input
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("1. Kéo Mesh Nguồn Vào Đây", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        m_SourceMesh = (Mesh)EditorGUILayout.ObjectField("Source Mesh", m_SourceMesh, typeof(Mesh), false);
        if (EditorGUI.EndChangeCheck() && m_SourceMesh != null)
        {
            if (string.IsNullOrEmpty(m_CustomMeshName))
            {
                m_CustomMeshName = m_SourceMesh.name + "_Skinned";
            }
        }

        if (m_SourceMesh == null)
        {
            EditorGUILayout.HelpBox("Hãy kéo file Mesh (.fbx hoặc .asset) cần thêm Bone vào đây.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"Mesh đã chọn: {m_SourceMesh.name} ({m_SourceMesh.vertexCount} đỉnh)", MessageType.None);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Section 2: Save Options
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("2. Cấu Hình Lưu File Mesh Mới", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        m_SaveDirectory = EditorGUILayout.TextField("Thư mục lưu", m_SaveDirectory);
        if (GUILayout.Button("Chọn...", GUILayout.Width(60)))
        {
            string absolutePath = EditorUtility.OpenFolderPanel("Chọn thư mục lưu Mesh Asset", "Assets", "");
            if (!string.IsNullOrEmpty(absolutePath))
            {
                if (absolutePath.StartsWith(Application.dataPath))
                {
                    m_SaveDirectory = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("Lỗi", "Hãy chọn thư mục nằm trong thư mục Assets của project!", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        m_CustomMeshName = EditorGUILayout.TextField("Tên Mesh Mới", m_CustomMeshName);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Section 3: Target Assign
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("3. Gán Tự Động Vào GameObject (Tùy Chọn)", EditorStyles.boldLabel);
        m_AutoAssignToSelected = EditorGUILayout.Toggle("Gán sau khi tạo", m_AutoAssignToSelected);

        if (m_AutoAssignToSelected)
        {
            m_TargetGameObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", m_TargetGameObject, typeof(GameObject), true);
            m_TargetSMR = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target SkinnedMeshRenderer", m_TargetSMR, typeof(SkinnedMeshRenderer), true);

            if (m_TargetGameObject != null && m_TargetSMR == null)
            {
                m_TargetSMR = m_TargetGameObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(15);

        // Action Button
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("TẠO MESH SKINNED MỚI (.ASSET)", GUILayout.Height(45)))
        {
            CreateSkinnedMeshAsset();
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("MESH SKINNER TOOL", headerStyle);
        EditorGUILayout.LabelField("Thêm Bone Data (bindposes & boneWeights) vào Mesh & Xuất thành File Asset mới", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private void CreateSkinnedMeshAsset()
    {
        if (m_SourceMesh == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng kéo 1 file Mesh vào trước khi tạo!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(m_CustomMeshName))
        {
            m_CustomMeshName = m_SourceMesh.name + "_Skinned";
        }

        // 1. Tạo bản sao Mesh
        Mesh newMesh = UnityEngine.Object.Instantiate(m_SourceMesh);
        newMesh.name = m_CustomMeshName;

        // 2. Tạo Bone Weights & Bindposes
        int vertexCount = newMesh.vertexCount;
        if (vertexCount > 0)
        {
            BoneWeight[] dummyWeights = new BoneWeight[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                dummyWeights[i].boneIndex0 = 0;
                dummyWeights[i].weight0 = 1.0f;
            }
            newMesh.bindposes = new Matrix4x4[] { Matrix4x4.identity };
            newMesh.boneWeights = dummyWeights;
        }

        newMesh.RecalculateBounds();

        // 3. Đảm bảo thư mục tồn tại
        if (!Directory.Exists(m_SaveDirectory))
        {
            Directory.CreateDirectory(m_SaveDirectory);
        }

        // 4. Lưu file asset mới
        string assetPath = $"{m_SaveDirectory}/{m_CustomMeshName}.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(newMesh, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh savedMeshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        EditorGUIUtility.PingObject(savedMeshAsset);

        Debug.Log($"[ShooterMeshSkinnerTool] Đã tạo thành công Mesh Skinned mới tại: {assetPath}");

        // 5. Gán vào Target SkinnedMeshRenderer nếu có
        if (m_AutoAssignToSelected && savedMeshAsset != null)
        {
            SkinnedMeshRenderer smr = m_TargetSMR;
            if (smr == null && m_TargetGameObject != null)
            {
                smr = m_TargetGameObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            if (smr != null)
            {
                Undo.RecordObject(smr, "Assign Skinned Mesh Asset");
                smr.sharedMesh = savedMeshAsset;

                // Set bones & rootBone để khớp hoàn toàn bindposes.Length = 1
                Transform[] newBones = new Transform[] { smr.transform };
                smr.bones = newBones;
                smr.rootBone = smr.transform;

                EditorUtility.SetDirty(smr);
                EditorUtility.SetDirty(smr.gameObject);
                Debug.Log($"[ShooterMeshSkinnerTool] Đã gán Mesh mới vào {smr.gameObject.name} & cài đặt xương smr.bones thành công!");
            }
        }

        EditorUtility.DisplayDialog("Thành công", $"Đã tạo Mesh Skinned mới thành công tại:\n{assetPath}\n\nĐã xóa hoàn toàn cảnh báo thiếu bone weights!", "OK");
    }
}
#endif
