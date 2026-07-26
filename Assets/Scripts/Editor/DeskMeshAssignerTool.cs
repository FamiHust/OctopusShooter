#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool gán/thay đổi Mesh visual và Material cho tất cả các Desk / Deck / Slot trong Level Prefab.
/// Hỗ trợ xử lý cho 1 Level Prefab, hàng loạt Prefab trong LevelDataBase hoặc trong Active Scene.
/// </summary>
public class DeskMeshAssignerTool : EditorWindow
{
    public enum TargetScope
    {
        SingleLevelPrefab,
        AllLevelPrefabsInDB,
        ActiveScene
    }

    // Target fields
    private TargetScope m_TargetScope = TargetScope.SingleLevelPrefab;

    private GameObject m_SelectedPrefab = null;
    private LevelDataBase m_LevelDB = null;
    private string[] m_LevelNames = new string[0];
    private int m_SelectedLevelIndex = -1;

    // Mesh & Material targets
    private Mesh m_TargetDeskMesh = null;
    private bool m_AssignMaterial = true;
    private Material m_TargetDeskMaterial = null;

    // Filter options
    private bool m_ExactMatchDeckOnly = true;
    private bool m_TargetSlotComponent = false;
    private bool m_TargetDeskNameKeywords = false;

    // Available assets pool
    private List<Mesh> m_AvailableDeskMeshes = new List<Mesh>();
    private List<Material> m_AvailableDeskMaterials = new List<Material>();

    // UI Scroll
    private Vector2 m_ScrollPos;

    [MenuItem("FlowBlast Tools/Desk Mesh Assigner Tool")]
    public static void OpenWindow()
    {
        DeskMeshAssignerTool window = GetWindow<DeskMeshAssignerTool>("Desk Mesh & Material Assigner");
        window.minSize = new Vector2(520, 680);
        window.Show();
    }

    private void OnEnable()
    {
        LoadDatabase();
        LoadAvailableDeskMeshes();
        LoadAvailableDeskMaterials();
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

    private void LoadAvailableDeskMeshes()
    {
        if (m_AvailableDeskMeshes == null) m_AvailableDeskMeshes = new List<Mesh>();
        m_AvailableDeskMeshes.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && !m_AvailableDeskMeshes.Contains(mesh))
            {
                string nameLower = mesh.name.ToLower();
                if (nameLower.Contains("desk") || nameLower.Contains("deck") || nameLower.Contains("slot") ||
                    nameLower.Contains("stand") || nameLower.Contains("table") || nameLower.Contains("board") || nameLower.Contains("bench"))
                {
                    m_AvailableDeskMeshes.Add(mesh);
                }
            }
        }
    }

    private void LoadAvailableDeskMaterials()
    {
        if (m_AvailableDeskMaterials == null) m_AvailableDeskMaterials = new List<Material>();
        m_AvailableDeskMaterials.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !m_AvailableDeskMaterials.Contains(mat))
            {
                string nameLower = mat.name.ToLower();
                if (nameLower.Contains("desk") || nameLower.Contains("deck") || nameLower.Contains("slot") ||
                    nameLower.Contains("stand") || nameLower.Contains("table") || nameLower.Contains("board") ||
                    nameLower.Contains("wood") || nameLower.Contains("metal"))
                {
                    m_AvailableDeskMaterials.Add(mat);
                }
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

        DrawMeshAndMaterialSelectionSection();
        GUILayout.Space(10);

        DrawAvailableAssetsSection();
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
        GUILayout.Label("CÔNG CỤ ĐỔI MESH & MATERIAL CHO DESK / DECK / SLOT LEVEL", titleStyle);
        EditorGUILayout.HelpBox(
            "Tool này tự động tìm toàn bộ các đối tượng Desk/Deck/Slot trong Level Prefab và gán Mesh visual & Material mới.\n" +
            "Áp dụng cho 1 Level Prefab đơn lẻ hoặc Batch cho TOÀN BỘ Level Prefabs trong LevelDataBase.",
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
                EditorGUILayout.HelpBox($"Sẽ cập nhật Mesh/Material cho Desk/Slot trong TOÀN BỘ {m_LevelDB.listPrefab.Count} Level Prefab trong LevelDataBase.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Không tìm thấy LevelDataBase trong dự án!", MessageType.Error);
            }
        }
        else if (m_TargetScope == TargetScope.ActiveScene)
        {
            EditorGUILayout.HelpBox("Sẽ thay đổi Mesh/Material cho tất cả Desk/Slot trong Scene đang mở.", MessageType.Info);
        }

        GUILayout.EndVertical();
    }

    private void DrawMeshAndMaterialSelectionSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("2. CHỌN MESH VÀ MATERIAL DESK MỚI", EditorStyles.boldLabel);

        m_TargetDeskMesh = (Mesh)EditorGUILayout.ObjectField("Mesh Desk mới", m_TargetDeskMesh, typeof(Mesh), false);

        GUILayout.Space(5);
        m_AssignMaterial = EditorGUILayout.Toggle("Gán Material mới cho Desk", m_AssignMaterial);
        if (m_AssignMaterial)
        {
            m_TargetDeskMaterial = (Material)EditorGUILayout.ObjectField("Material Desk mới", m_TargetDeskMaterial, typeof(Material), false);
        }

        GUILayout.Space(5);
        GUILayout.Label("Tiêu chí tìm kiếm đối tượng trong Prefab:", EditorStyles.miniBoldLabel);
        m_ExactMatchDeckOnly = EditorGUILayout.Toggle("Chỉ lọc GameObject có tên chính xác: 'Deck'", m_ExactMatchDeckOnly);
        if (!m_ExactMatchDeckOnly)
        {
            EditorGUI.indentLevel++;
            m_TargetSlotComponent = EditorGUILayout.Toggle("Tìm theo Component 'Slot'", m_TargetSlotComponent);
            m_TargetDeskNameKeywords = EditorGUILayout.Toggle("Tìm theo Từ khóa rộng ('Desk', 'Slot', 'Stand',...)", m_TargetDeskNameKeywords);
            EditorGUI.indentLevel--;
        }

        GUILayout.EndVertical();
    }

    private void DrawAvailableAssetsSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.BeginHorizontal();
        GUILayout.Label("3. DANH SÁCH DESK MESH & MATERIAL SCAN ĐƯỢC", EditorStyles.boldLabel);
        if (GUILayout.Button("Scan lại Assets", GUILayout.Width(110)))
        {
            LoadAvailableDeskMeshes();
            LoadAvailableDeskMaterials();
        }
        GUILayout.EndHorizontal();

        // 1. Meshes list
        GUILayout.Label("Mesh gợi ý:", EditorStyles.miniBoldLabel);
        if (m_AvailableDeskMeshes.Count == 0)
        {
            EditorGUILayout.HelpBox("Chưa tự động quét thấy Mesh có chứa chữ 'Desk', 'Deck', 'Slot', 'Stand'. Hãy kéo thủ công file Mesh vào ô phía trên.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < m_AvailableDeskMeshes.Count; i++)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField($"Mesh #{i + 1}", m_AvailableDeskMeshes[i], typeof(Mesh), false);
                
                if (GUILayout.Button("Chọn Mesh", GUILayout.Width(85)))
                {
                    m_TargetDeskMesh = m_AvailableDeskMeshes[i];
                }
                GUILayout.EndHorizontal();
            }
        }

        // 2. Materials list
        if (m_AssignMaterial)
        {
            GUILayout.Space(5);
            GUILayout.Label("Material gợi ý:", EditorStyles.miniBoldLabel);
            if (m_AvailableDeskMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("Chưa tự động quét thấy Material gợi ý cho Desk. Hãy kéo thủ công file Material vào ô phía trên.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < m_AvailableDeskMaterials.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField($"Mat #{i + 1}", m_AvailableDeskMaterials[i], typeof(Material), false);
                    
                    if (GUILayout.Button("Chọn Mat", GUILayout.Width(85)))
                    {
                        m_TargetDeskMaterial = m_AvailableDeskMaterials[i];
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.95f, 1f);
        string btnText = m_TargetScope == TargetScope.AllLevelPrefabsInDB
            ? "ĐỔI MESH & MATERIAL DESK BATCH TOÀN BỘ LEVEL PREFABS"
            : "ĐỔI MESH & MATERIAL CHO DESK";

        if (GUILayout.Button(btnText, GUILayout.Height(40)))
        {
            ExecuteDeskMeshAssignment();
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    private void ExecuteDeskMeshAssignment()
    {
        if (m_TargetDeskMesh == null && (!m_AssignMaterial || m_TargetDeskMaterial == null))
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn Mesh Desk mới hoặc Material mới trước khi thực hiện.", "OK");
            return;
        }

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
                EditorUtility.DisplayDialog("Hoàn tất", $"Đã cập nhật Mesh/Material cho {count} Desk/Slot trong Prefab '{m_SelectedPrefab.name}'.", "OK");
                break;

            case TargetScope.AllLevelPrefabsInDB:
                if (m_LevelDB == null || m_LevelDB.listPrefab == null || m_LevelDB.listPrefab.Count == 0)
                {
                    EditorUtility.DisplayDialog("Lỗi", "LevelDataBase rỗng hoặc chưa được load.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Xác nhận Batch",
                    $"Bạn có chắc muốn cập nhật Mesh & Material Desk cho TOÀN BỘ {m_LevelDB.listPrefab.Count} Level Prefabs không?", "Đồng ý", "Hủy"))
                {
                    return;
                }

                int totalUpdatedDesks = 0;
                int updatedPrefabCount = 0;

                for (int i = 0; i < m_LevelDB.listPrefab.Count; i++)
                {
                    GameObject p = m_LevelDB.listPrefab[i];
                    if (p == null) continue;

                    string pPath = AssetDatabase.GetAssetPath(p);
                    if (string.IsNullOrEmpty(pPath)) continue;

                    EditorUtility.DisplayProgressBar("Updating Desk Assets Batch", $"Đang xử lý {p.name} ({i + 1}/{m_LevelDB.listPrefab.Count})...", (float)i / m_LevelDB.listPrefab.Count);

                    int numChanged = ProcessSinglePrefabPath(pPath);
                    if (numChanged > 0)
                    {
                        totalUpdatedDesks += numChanged;
                        updatedPrefabCount++;
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Hoàn tất Batch", $"Đã cập nhật tổng cộng {totalUpdatedDesks} Desk trong {updatedPrefabCount}/{m_LevelDB.listPrefab.Count} Level Prefabs.", "OK");
                break;

            case TargetScope.ActiveScene:
                List<GameObject> targetObjects = FindDeskObjectsInHierarchy(null);
                int sceneCount = ApplyMeshAndMaterialToDeskObjects(targetObjects);
                EditorUtility.DisplayDialog("Hoàn tất", $"Đã cập nhật Mesh/Material cho {sceneCount} Desk trong Scene.", "OK");
                break;
        }
    }

    private int ProcessSinglePrefabPath(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) return 0;

        List<GameObject> deskObjects = FindDeskObjectsInHierarchy(root);
        int updatedCount = ApplyMeshAndMaterialToDeskObjects(deskObjects);

        if (updatedCount > 0)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[DeskMeshAssignerTool] Đã cập nhật {updatedCount} Desk (Mesh/Material) cho Level Prefab: {prefabPath}");
        }

        PrefabUtility.UnloadPrefabContents(root);
        return updatedCount;
    }

    private List<GameObject> FindDeskObjectsInHierarchy(GameObject root)
    {
        List<GameObject> results = new List<GameObject>();

        if (root != null)
        {
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

            if (m_ExactMatchDeckOnly)
            {
                foreach (var t in allTransforms)
                {
                    if (t != null && t.gameObject.name.Equals("Deck", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!results.Contains(t.gameObject))
                        {
                            results.Add(t.gameObject);
                        }
                    }
                }
            }
            else
            {
                // 1. Check Slot components
                if (m_TargetSlotComponent)
                {
                    Slot[] slots = root.GetComponentsInChildren<Slot>(true);
                    foreach (var s in slots)
                    {
                        if (s != null && !results.Contains(s.gameObject))
                        {
                            results.Add(s.gameObject);
                        }
                    }
                }

                // 2. Check Name Keywords
                if (m_TargetDeskNameKeywords)
                {
                    foreach (var t in allTransforms)
                    {
                        if (t == null) continue;
                        string nameLower = t.gameObject.name.ToLower();
                        if (nameLower.Contains("desk") || nameLower.Contains("deck") || nameLower.Contains("slot") ||
                            nameLower.Contains("stand") || nameLower.Contains("table") || nameLower.Contains("bench") || nameLower.Contains("board"))
                        {
                            if (!results.Contains(t.gameObject))
                            {
                                results.Add(t.gameObject);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // Scene mode
            if (m_ExactMatchDeckOnly)
            {
                Transform[] allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t != null && t.gameObject.name.Equals("Deck", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!results.Contains(t.gameObject))
                        {
                            results.Add(t.gameObject);
                        }
                    }
                }
            }
            else
            {
                if (m_TargetSlotComponent)
                {
                    Slot[] slots = UnityEngine.Object.FindObjectsOfType<Slot>(true);
                    foreach (var s in slots)
                    {
                        if (s != null && !results.Contains(s.gameObject))
                        {
                            results.Add(s.gameObject);
                        }
                    }
                }

                if (m_TargetDeskNameKeywords)
                {
                    MeshFilter[] mfs = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true);
                    foreach (var mf in mfs)
                    {
                        if (mf == null || mf.gameObject == null) continue;
                        string nameLower = mf.gameObject.name.ToLower();
                        if (nameLower.Contains("desk") || nameLower.Contains("deck") || nameLower.Contains("slot") ||
                            nameLower.Contains("stand") || nameLower.Contains("table") || nameLower.Contains("bench") || nameLower.Contains("board"))
                        {
                            if (!results.Contains(mf.gameObject))
                            {
                                results.Add(mf.gameObject);
                            }
                        }
                    }
                }
            }
        }

        return results;
    }

    private int ApplyMeshAndMaterialToDeskObjects(List<GameObject> deskObjects)
    {
        if (deskObjects == null || deskObjects.Count == 0) return 0;

        int count = 0;

        foreach (GameObject go in deskObjects)
        {
            if (go == null) continue;

            bool changed = false;

            // 1. Update Mesh on MeshFilter
            if (m_TargetDeskMesh != null)
            {
                MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                {
                    if (mf != null && mf.sharedMesh != m_TargetDeskMesh)
                    {
                        Undo.RecordObject(mf, "Change Desk MeshFilter");
                        mf.sharedMesh = m_TargetDeskMesh;
                        EditorUtility.SetDirty(mf);
                        EditorUtility.SetDirty(mf.gameObject);
                        changed = true;
                    }
                }

                SkinnedMeshRenderer[] smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    if (smr != null && smr.sharedMesh != m_TargetDeskMesh)
                    {
                        Undo.RecordObject(smr, "Change Desk SkinnedMeshRenderer");
                        smr.sharedMesh = m_TargetDeskMesh;
                        EditorUtility.SetDirty(smr);
                        EditorUtility.SetDirty(smr.gameObject);
                        changed = true;
                    }
                }

                MeshCollider[] mcs = go.GetComponentsInChildren<MeshCollider>(true);
                foreach (var mc in mcs)
                {
                    if (mc != null && mc.sharedMesh != m_TargetDeskMesh)
                    {
                        Undo.RecordObject(mc, "Change Desk MeshCollider");
                        mc.sharedMesh = m_TargetDeskMesh;
                        EditorUtility.SetDirty(mc);
                        changed = true;
                    }
                }
            }

            // 2. Update Material on Renderer
            if (m_AssignMaterial && m_TargetDeskMaterial != null)
            {
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var rend in renderers)
                {
                    if (rend != null && rend.sharedMaterial != m_TargetDeskMaterial)
                    {
                        Undo.RecordObject(rend, "Change Desk Material");
                        rend.sharedMaterial = m_TargetDeskMaterial;
                        EditorUtility.SetDirty(rend);
                        EditorUtility.SetDirty(rend.gameObject);
                        changed = true;
                    }
                }
            }

            // 3. Enforce local rotation Z = 180 on the Deck GameObject
            {
                Transform t = go.transform;
                Vector3 currentEuler = t.localEulerAngles;
                if (!Mathf.Approximately(currentEuler.z, 180f))
                {
                    Undo.RecordObject(t, "Fix Deck Rotation Z=180");
                    t.localEulerAngles = new Vector3(currentEuler.x, currentEuler.y, 180f);
                    EditorUtility.SetDirty(t);
                    EditorUtility.SetDirty(go);
                    changed = true;
                }
            }

            if (changed)
            {
                count++;
            }
        }

        return count;
    }

}
#endif
