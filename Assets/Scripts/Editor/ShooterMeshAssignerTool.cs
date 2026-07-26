#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool đổi Mesh cho các Shooter trong Level Prefab.
/// Hỗ trợ gán 1 Mesh chung cho toàn bộ Shooter hoặc gán Mesh riêng theo từng màu (SeedColor),
/// có thể áp dụng cho 1 Level Prefab hoặc Batch toàn bộ Level Prefabs trong LevelDataBase.
/// </summary>
public class ShooterMeshAssignerTool : EditorWindow
{
    public enum TargetScope
    {
        SingleLevelPrefab,
        AllLevelPrefabsInDB,
        ActiveScene
    }

    public enum AssignmentMode
    {
        AllShootersUseSingleMesh,
        BySpecificColor,
        ColorToMeshMapping
    }

    // Target fields
    private TargetScope m_TargetScope = TargetScope.SingleLevelPrefab;
    private AssignmentMode m_AssignmentMode = AssignmentMode.AllShootersUseSingleMesh;

    private GameObject m_SelectedPrefab = null;
    private LevelDataBase m_LevelDB = null;
    private string[] m_LevelNames = new string[0];
    private int m_SelectedLevelIndex = -1;

    // Single Mesh mode
    private Mesh m_SingleShooterMesh = null;

    // By Specific Color mode
    private SeedColor m_TargetColorFilter = SeedColor.Red;
    private Mesh m_ColorFilteredMesh = null;

    // Color to Mesh Mapping mode
    [System.Serializable]
    public class ColorMeshPair
    {
        public SeedColor color;
        public Mesh mesh;
    }
    private List<ColorMeshPair> m_ColorMeshMappings = new List<ColorMeshPair>();

    // Available meshes pool
    private List<Mesh> m_AvailableShooterMeshes = new List<Mesh>();

    // Material assignment
    private bool m_AssignMaterial = false;
    private Material m_TargetShooterMaterial = null;
    private List<Material> m_AvailableShooterMaterials = new List<Material>();
    private int m_MaterialSlotIndex = 0; // which slot to assign (0 = body, 1 = eye, -1 = all slots)

    // UI Scroll
    private Vector2 m_ScrollPos;
    private Vector2 m_MappingScrollPos;

    [MenuItem("FlowBlast Tools/Shooter Mesh Assigner Tool")]
    public static void OpenWindow()
    {
        ShooterMeshAssignerTool window = GetWindow<ShooterMeshAssignerTool>("Shooter Mesh Assigner");
        window.minSize = new Vector2(520, 680);
        window.Show();
    }

    private void OnEnable()
    {
        LoadDatabase();
        LoadAvailableShooterMeshes();
        LoadAvailableShooterMaterials();
        InitColorMappingDefault();
    }

    private void LoadDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelDataBase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            m_LevelDB = AssetDatabase.LoadAssetAtPath<LevelDataBase>(path);
            if (m_LevelDB != null && m_LevelDB.listPrefab != null)
            {
                m_LevelNames = new string[m_LevelDB.listPrefab.Count];
                for (int i = 0; i < m_LevelDB.listPrefab.Count; i++)
                {
                    GameObject go = m_LevelDB.listPrefab[i];
                    m_LevelNames[i] = go != null ? go.name : $"Level {i + 1} (Empty)";
                }
            }
        }
    }

    private void LoadAvailableShooterMeshes()
    {
        if (m_AvailableShooterMeshes == null) m_AvailableShooterMeshes = new List<Mesh>();
        m_AvailableShooterMeshes.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && !m_AvailableShooterMeshes.Contains(mesh))
            {
                string nameLower = mesh.name.ToLower();
                if (nameLower.Contains("shooter") || nameLower.Contains("character") || nameLower.Contains("seed") || nameLower.Contains("block"))
                {
                    m_AvailableShooterMeshes.Add(mesh);
                }
            }
        }
    }

    private void LoadAvailableShooterMaterials()
    {
        if (m_AvailableShooterMaterials == null) m_AvailableShooterMaterials = new List<Material>();
        m_AvailableShooterMaterials.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !m_AvailableShooterMaterials.Contains(mat))
            {
                string nameLower = mat.name.ToLower();
                if (nameLower.Contains("shooter") || nameLower.Contains("character") ||
                    nameLower.Contains("seed") || nameLower.Contains("block") || nameLower.Contains("eye"))
                {
                    m_AvailableShooterMaterials.Add(mat);
                }
            }
        }
    }

    private void InitColorMappingDefault()
    {
        if (m_ColorMeshMappings == null || m_ColorMeshMappings.Count == 0)
        {
            m_ColorMeshMappings = new List<ColorMeshPair>();
            foreach (SeedColor color in Enum.GetValues(typeof(SeedColor)))
            {
                m_ColorMeshMappings.Add(new ColorMeshPair { color = color, mesh = null });
            }
        }
    }

    private void OnGUI()
    {
        m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

        DrawHeader();
        GUILayout.Space(10);

        DrawTargetScopeSection();
        GUILayout.Space(10);

        DrawAssignmentModeSection();
        GUILayout.Space(10);

        DrawMaterialSection();
        GUILayout.Space(10);

        DrawAvailableMeshesSection();
        GUILayout.Space(15);

        DrawActionButtons();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("CÔNG CỤ ĐỔI MESH CHO SHOOTER LEVEL", titleStyle);
        EditorGUILayout.HelpBox(
            "Tool này thay đổi Mesh visual (SkinnedMeshRenderer / MeshFilter) cho toàn bộ Shooter hoặc theo từng màu (SeedColor).\n" +
            "Áp dụng cho 1 Level Prefab hoặc Batch toàn bộ Level Prefabs trong LevelDataBase.",
            MessageType.Info);
        GUILayout.EndVertical();
    }

    private void DrawTargetScopeSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("1. CHỌN ĐỐI TƯỢNG MỤC TIÊU", EditorStyles.boldLabel);

        m_TargetScope = (TargetScope)EditorGUILayout.EnumPopup("Phạm vi áp dụng", m_TargetScope);

        if (m_TargetScope == TargetScope.SingleLevelPrefab)
        {
            if (m_LevelDB != null && m_LevelNames.Length > 0)
            {
                int nextIdx = EditorGUILayout.Popup("Chọn từ LevelDataBase", m_SelectedLevelIndex, m_LevelNames);
                if (nextIdx != m_SelectedLevelIndex && nextIdx >= 0 && nextIdx < m_LevelDB.listPrefab.Count)
                {
                    m_SelectedLevelIndex = nextIdx;
                    m_SelectedPrefab = m_LevelDB.listPrefab[m_SelectedLevelIndex];
                }
            }

            GameObject nextPrefab = (GameObject)EditorGUILayout.ObjectField("Level Prefab", m_SelectedPrefab, typeof(GameObject), false);
            if (nextPrefab != m_SelectedPrefab)
            {
                m_SelectedPrefab = nextPrefab;
                if (m_SelectedPrefab != null && m_LevelDB != null && m_LevelDB.listPrefab != null)
                {
                    m_SelectedLevelIndex = m_LevelDB.listPrefab.IndexOf(m_SelectedPrefab);
                }
            }
        }
        else if (m_TargetScope == TargetScope.AllLevelPrefabsInDB)
        {
            if (m_LevelDB == null) LoadDatabase();
            if (m_LevelDB != null && m_LevelDB.listPrefab != null)
            {
                EditorGUILayout.HelpBox($"Sẽ cập nhật Mesh cho Shooter trong TOÀN BỘ {m_LevelDB.listPrefab.Count} Level Prefab trong LevelDataBase.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Không tìm thấy LevelDataBase trong dự án!", MessageType.Error);
            }
        }
        else if (m_TargetScope == TargetScope.ActiveScene)
        {
            EditorGUILayout.HelpBox("Sẽ thay đổi Mesh cho tất cả Shooter trong Scene đang mở.", MessageType.Info);
        }

        GUILayout.EndVertical();
    }

    private void DrawAssignmentModeSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("2. CHẾ ĐỘ GÁN MESH CHO SHOOTER", EditorStyles.boldLabel);

        m_AssignmentMode = (AssignmentMode)EditorGUILayout.EnumPopup("Chế độ đổi Mesh", m_AssignmentMode);
        GUILayout.Space(5);

        switch (m_AssignmentMode)
        {
            case AssignmentMode.AllShootersUseSingleMesh:
                m_SingleShooterMesh = (Mesh)EditorGUILayout.ObjectField("Mesh cho tất cả Shooter", m_SingleShooterMesh, typeof(Mesh), false);
                EditorGUILayout.HelpBox("Tất cả các Shooter trong level sẽ được gán cùng 1 Mesh visual này.", MessageType.None);
                break;

            case AssignmentMode.BySpecificColor:
                m_TargetColorFilter = (SeedColor)EditorGUILayout.EnumPopup("Chỉ áp dụng cho màu", m_TargetColorFilter);
                m_ColorFilteredMesh = (Mesh)EditorGUILayout.ObjectField($"Mesh cho màu {m_TargetColorFilter}", m_ColorFilteredMesh, typeof(Mesh), false);
                EditorGUILayout.HelpBox($"Chỉ các Shooter có targetColor = {m_TargetColorFilter} mới được thay đổi Mesh.", MessageType.None);
                break;

            case AssignmentMode.ColorToMeshMapping:
                EditorGUILayout.HelpBox("Thiết lập Mesh riêng cho từng SeedColor. Shooter màu nào sẽ nhận Mesh của màu đó.", MessageType.None);
                m_MappingScrollPos = EditorGUILayout.BeginScrollView(m_MappingScrollPos, GUILayout.Height(200));

                for (int i = 0; i < m_ColorMeshMappings.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    Color colorBox = ColorInfo.GetUnityColor(m_ColorMeshMappings[i].color);
                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = colorBox;
                    GUILayout.Box(m_ColorMeshMappings[i].color.ToString(), GUILayout.Width(100), GUILayout.Height(20));
                    GUI.backgroundColor = oldColor;

                    m_ColorMeshMappings[i].mesh = (Mesh)EditorGUILayout.ObjectField(m_ColorMeshMappings[i].mesh, typeof(Mesh), false);
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
                break;
        }

        GUILayout.EndVertical();
    }

    private void DrawMaterialSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("3. GÁN MATERIAL CHO SHOOTER", EditorStyles.boldLabel);

        m_AssignMaterial = EditorGUILayout.Toggle("Gán Material mới cho Shooter", m_AssignMaterial);
        if (m_AssignMaterial)
        {
            m_TargetShooterMaterial = (Material)EditorGUILayout.ObjectField("Material Shooter", m_TargetShooterMaterial, typeof(Material), false);

            string[] slotLabels = new string[] { "Slot 0 (Body/Color)", "Slot 1 (Eye)", "Tất cả Slot" };
            int[] slotValues = new int[] { 0, 1, -1 };
            int currentDisplayIdx = System.Array.IndexOf(slotValues, m_MaterialSlotIndex);
            if (currentDisplayIdx < 0) currentDisplayIdx = 0;
            int newDisplayIdx = EditorGUILayout.Popup("Gán vào Slot", currentDisplayIdx, slotLabels);
            m_MaterialSlotIndex = slotValues[newDisplayIdx];

            EditorGUILayout.HelpBox(
                m_MaterialSlotIndex == -1
                    ? "Sẽ gán Material này vào TẤT CẢ các material slot của Renderer trên Shooter."
                    : $"Sẽ gán Material này vào slot [{m_MaterialSlotIndex}] (giữ nguyên các slot khác).",
                MessageType.None);

            // Suggested materials list
            if (m_AvailableShooterMaterials.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("Material gợi ý:", EditorStyles.miniBoldLabel);
                for (int i = 0; i < m_AvailableShooterMaterials.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField($"Mat #{i + 1}", m_AvailableShooterMaterials[i], typeof(Material), false);
                    if (GUILayout.Button("Chọn", GUILayout.Width(60)))
                        m_TargetShooterMaterial = m_AvailableShooterMaterials[i];
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawAvailableMeshesSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.BeginHorizontal();
        GUILayout.Label("4. DANH SÁCH SHOOTER MESH TÌM THẤY TRONG DỰ ÁN", EditorStyles.boldLabel);
        if (GUILayout.Button("Scan lại", GUILayout.Width(80)))
        {
            LoadAvailableShooterMeshes();
            LoadAvailableShooterMaterials();
        }
        GUILayout.EndHorizontal();

        if (m_AvailableShooterMeshes.Count == 0)
        {
            EditorGUILayout.HelpBox("Chưa tự động quét thấy Mesh có tên 'Shooter' hoặc 'Character'. Hãy kéo thủ công file Mesh vào ô phía trên.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < m_AvailableShooterMeshes.Count; i++)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField($"#{i + 1}", m_AvailableShooterMeshes[i], typeof(Mesh), false);
                
                if (m_AssignmentMode == AssignmentMode.AllShootersUseSingleMesh)
                {
                    if (GUILayout.Button("Chọn dùng", GUILayout.Width(80)))
                    {
                        m_SingleShooterMesh = m_AvailableShooterMeshes[i];
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f, 1f);
        string btnText = m_TargetScope == TargetScope.AllLevelPrefabsInDB
            ? "ĐỔI MESH BATCH TOÀN BỘ LEVEL PREFABS"
            : "ĐỔI MESH CHO SHOOTER";

        if (GUILayout.Button(btnText, GUILayout.Height(40)))
        {
            ExecuteShooterMeshAssignment();
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    private void ExecuteShooterMeshAssignment()
    {
        switch (m_TargetScope)
        {
            case TargetScope.SingleLevelPrefab:
                if (m_SelectedPrefab == null)
                {
                    EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn Level Prefab trước.", "OK");
                    return;
                }
                string path = AssetDatabase.GetAssetPath(m_SelectedPrefab);
                if (string.IsNullOrEmpty(path))
                {
                    EditorUtility.DisplayDialog("Lỗi", "Path của Prefab không hợp lệ.", "OK");
                    return;
                }
                int count = ProcessSinglePrefabPath(path);
                EditorUtility.DisplayDialog("Hoàn tất", $"Đã cập nhật Mesh cho {count} Shooter trong Prefab '{m_SelectedPrefab.name}'.", "OK");
                break;

            case TargetScope.AllLevelPrefabsInDB:
                if (m_LevelDB == null || m_LevelDB.listPrefab == null || m_LevelDB.listPrefab.Count == 0)
                {
                    EditorUtility.DisplayDialog("Lỗi", "LevelDataBase rỗng hoặc chưa được load.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Xác nhận Batch",
                    $"Bạn có chắc muốn cập nhật Mesh Shooter cho TOÀN BỘ {m_LevelDB.listPrefab.Count} Level Prefabs không?", "Đồng ý", "Hủy"))
                {
                    return;
                }

                int totalUpdatedShooters = 0;
                int updatedPrefabCount = 0;

                for (int i = 0; i < m_LevelDB.listPrefab.Count; i++)
                {
                    GameObject p = m_LevelDB.listPrefab[i];
                    if (p == null) continue;

                    string pPath = AssetDatabase.GetAssetPath(p);
                    if (string.IsNullOrEmpty(pPath)) continue;

                    EditorUtility.DisplayProgressBar("Updating Shooter Meshes Batch", $"Đang xử lý {p.name} ({i + 1}/{m_LevelDB.listPrefab.Count})...", (float)i / m_LevelDB.listPrefab.Count);

                    int numChanged = ProcessSinglePrefabPath(pPath);
                    if (numChanged > 0)
                    {
                        totalUpdatedShooters += numChanged;
                        updatedPrefabCount++;
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Hoàn tất Batch", $"Đã cập nhật tổng cộng {totalUpdatedShooters} Shooter trong {updatedPrefabCount}/{m_LevelDB.listPrefab.Count} Level Prefabs.", "OK");
                break;

            case TargetScope.ActiveScene:
                BaseShooter[] sceneShooters = UnityEngine.Object.FindObjectsOfType<BaseShooter>(true);
                int sceneCount = ApplyMeshToShooterArray(sceneShooters);
                EditorUtility.DisplayDialog("Hoàn tất", $"Đã cập nhật Mesh cho {sceneCount} Shooter trong Scene.", "OK");
                break;
        }
    }

    private int ProcessSinglePrefabPath(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) return 0;

        BaseShooter[] shooters = root.GetComponentsInChildren<BaseShooter>(true);
        int updatedCount = ApplyMeshToShooterArray(shooters);

        if (updatedCount > 0)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[ShooterMeshAssignerTool] Đã cập nhật {updatedCount} Mesh Shooter cho Level Prefab: {prefabPath}");
        }

        PrefabUtility.UnloadPrefabContents(root);
        return updatedCount;
    }

    private int ApplyMeshToShooterArray(BaseShooter[] shooters)
    {
        if (shooters == null || shooters.Length == 0) return 0;

        int count = 0;
        FieldInfo targetColorField = typeof(BaseShooter).GetField("targetColor", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo meshField = typeof(BaseShooter).GetField("mesh", BindingFlags.NonPublic | BindingFlags.Instance);

        Dictionary<SeedColor, Mesh> mapDict = m_ColorMeshMappings.ToDictionary(k => k.color, v => v.mesh);

        foreach (BaseShooter shooter in shooters)
        {
            if (shooter == null) continue;

            SeedColor shooterColor = SeedColor.Red;
            if (targetColorField != null)
            {
                object val = targetColorField.GetValue(shooter);
                if (val != null) shooterColor = (SeedColor)val;
            }

            Mesh targetMeshToApply = null;

            switch (m_AssignmentMode)
            {
                case AssignmentMode.AllShootersUseSingleMesh:
                    targetMeshToApply = m_SingleShooterMesh;
                    break;

                case AssignmentMode.BySpecificColor:
                    if (shooterColor == m_TargetColorFilter)
                    {
                        targetMeshToApply = m_ColorFilteredMesh;
                    }
                    break;

                case AssignmentMode.ColorToMeshMapping:
                    if (mapDict.ContainsKey(shooterColor))
                    {
                        targetMeshToApply = mapDict[shooterColor];
                    }
                    break;
            }

            if (targetMeshToApply == null) continue;

            bool changed = false;

            // 1. Check SkinnedMeshRenderer (field or children)
            SkinnedMeshRenderer smr = null;
            if (meshField != null)
            {
                smr = meshField.GetValue(shooter) as SkinnedMeshRenderer;
            }
            if (smr == null)
            {
                smr = shooter.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            if (smr != null)
            {
                if (smr.sharedMesh != targetMeshToApply)
                {
                    Undo.RecordObject(smr, "Change Shooter Mesh");
                    smr.sharedMesh = targetMeshToApply;
                    EditorUtility.SetDirty(smr);
                    EditorUtility.SetDirty(smr.gameObject);
                    changed = true;
                }
            }

            // 2. Check MeshFilter (children)
            MeshFilter mf = shooter.GetComponentInChildren<MeshFilter>(true);
            if (mf != null)
            {
                if (mf.sharedMesh != targetMeshToApply)
                {
                    Undo.RecordObject(mf, "Change Shooter MeshFilter");
                    mf.sharedMesh = targetMeshToApply;
                    EditorUtility.SetDirty(mf);
                    EditorUtility.SetDirty(mf.gameObject);
                    changed = true;
                }
            }

            // 3. Check MeshCollider
            MeshCollider mc = shooter.GetComponentInChildren<MeshCollider>(true);
            if (mc != null && mc.sharedMesh != targetMeshToApply)
            {
                Undo.RecordObject(mc, "Change Shooter MeshCollider");
                mc.sharedMesh = targetMeshToApply;
                EditorUtility.SetDirty(mc);
                changed = true;
            }

            // 4. Apply Material
            if (m_AssignMaterial && m_TargetShooterMaterial != null)
            {
                Renderer[] renderers = shooter.GetComponentsInChildren<Renderer>(true);
                foreach (var rend in renderers)
                {
                    if (rend == null) continue;
                    if (m_MaterialSlotIndex == -1)
                    {
                        // All slots
                        Material[] mats = rend.sharedMaterials;
                        bool anyDiff = false;
                        for (int si = 0; si < mats.Length; si++)
                        {
                            if (mats[si] != m_TargetShooterMaterial) { mats[si] = m_TargetShooterMaterial; anyDiff = true; }
                        }
                        if (anyDiff)
                        {
                            Undo.RecordObject(rend, "Change Shooter Material All Slots");
                            rend.sharedMaterials = mats;
                            EditorUtility.SetDirty(rend);
                            EditorUtility.SetDirty(rend.gameObject);
                            changed = true;
                        }
                    }
                    else
                    {
                        // Specific slot
                        Material[] mats = rend.sharedMaterials;
                        if (m_MaterialSlotIndex < mats.Length && mats[m_MaterialSlotIndex] != m_TargetShooterMaterial)
                        {
                            Undo.RecordObject(rend, $"Change Shooter Material Slot {m_MaterialSlotIndex}");
                            mats[m_MaterialSlotIndex] = m_TargetShooterMaterial;
                            rend.sharedMaterials = mats;
                            EditorUtility.SetDirty(rend);
                            EditorUtility.SetDirty(rend.gameObject);
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(shooter);
                count++;
            }
        }

        return count;
    }

    [MenuItem("FlowBlast Tools/Update Shooter Text Pivot Y to 0.65")]
    public static void BatchUpdateShooterTextPivotY()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            TMPro.TextMeshProUGUI[] tmps = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            bool dirty = false;
            foreach (TMPro.TextMeshProUGUI tmp in tmps)
            {
                if (tmp != null && tmp.rectTransform != null)
                {
                    Vector2 p = tmp.rectTransform.pivot;
                    if (!Mathf.Approximately(p.y, 0.65f))
                    {
                        tmp.rectTransform.pivot = new Vector2(p.x, 0.65f);
                        EditorUtility.SetDirty(tmp);
                        EditorUtility.SetDirty(tmp.gameObject);
                        dirty = true;
                        count++;
                    }
                }
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Scene objects
        TMPro.TextMeshProUGUI[] sceneTmps = UnityEngine.Object.FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        foreach (TMPro.TextMeshProUGUI tmp in sceneTmps)
        {
            if (tmp != null && tmp.rectTransform != null && tmp.GetComponentInParent<BaseShooter>() != null)
            {
                Vector2 p = tmp.rectTransform.pivot;
                if (!Mathf.Approximately(p.y, 0.65f))
                {
                    Undo.RecordObject(tmp.rectTransform, "Update Shooter Text Pivot Y");
                    tmp.rectTransform.pivot = new Vector2(p.x, 0.65f);
                    EditorUtility.SetDirty(tmp.rectTransform);
                }
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Hoàn tất", $"Đã cập nhật Pivot Y = 0.65 cho text số lượng đạn của {count} Shooter/Text Prefabs.", "OK");
    }
}
#endif
