#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool đơn giản để thay đổi kích thước gốc (Vertex positions) của file Mesh Asset.
/// Người dùng chỉ cần kéo file Mesh từ cửa sổ Project vào và chỉnh kích thước/tỷ lệ mong muốn.
/// </summary>
public class LevelMeshResizerTool : EditorWindow
{
    public enum AdjustmentMode
    {
        ScaleMultiplier,
        TargetBoundsSize
    }

    public enum PivotPoint
    {
        BoundsCenter,
        LocalOrigin,
        BottomCenter
    }

    public enum SaveOption
    {
        CreateNewAsset,
        OverwriteOriginalAsset
    }

    // Config fields
    private Mesh m_TargetMesh = null;
    private List<Mesh> m_TargetMeshList = new List<Mesh>();
    private bool m_BatchMode = false;

    private AdjustmentMode m_Mode = AdjustmentMode.ScaleMultiplier;
    private PivotPoint m_Pivot = PivotPoint.BoundsCenter;
    private SaveOption m_SaveOption = SaveOption.CreateNewAsset;

    // Scale Multiplier settings
    private Vector3 m_ScaleMultiplier = Vector3.one;
    private bool m_UniformScale = true;

    // Target Bounds Size settings
    private Vector3 m_TargetBoundsSize = new Vector3(10f, 2f, 10f);

    // Position Offset setting
    private Vector3 m_PositionOffset = Vector3.zero;

    // Save path
    private string m_CustomSaveDirectory = "Assets/Mesh/Resized";

    // Recalculation options
    private bool m_RecalculateNormals = true;
    private bool m_RecalculateTangents = true;

    // UI Scroll
    private Vector2 m_ScrollPos;
    private Vector2 m_ListScrollPos;

    [MenuItem("FlowBlast Tools/Mesh Resizer Tool")]
    public static void OpenWindow()
    {
        LevelMeshResizerTool window = GetWindow<LevelMeshResizerTool>("Mesh Resizer");
        window.minSize = new Vector2(460, 580);
        window.Show();
    }

    private void OnGUI()
    {
        m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

        DrawHeader();
        GUILayout.Space(10);

        DrawMeshInputSection();
        GUILayout.Space(10);

        DrawSettingsSection();
        GUILayout.Space(10);

        DrawSaveAndOptionsSection();
        GUILayout.Space(10);

        DrawPreviewSection();
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
        GUILayout.Label("CÔNG CỤ ĐỔI SIZE GỐC CỦA MESH (VERTEX RESIZER)", titleStyle);
        EditorGUILayout.HelpBox(
            "Kéo file Mesh từ Project vào bên dưới để thay đổi trực tiếp kích thước Vertex của Mesh.\n" +
            "Cách này đổi size vật lý của Mesh asset mà không cần đụng đến Transform Scale trong Prefab/Scene.",
            MessageType.Info);
        GUILayout.EndVertical();
    }

    private void DrawMeshInputSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("1. CHỌN MESH TỪ PROJECT", EditorStyles.boldLabel);

        m_BatchMode = EditorGUILayout.Toggle("Resize nhiều Mesh cùng lúc (Batch)", m_BatchMode);
        GUILayout.Space(5);

        if (!m_BatchMode)
        {
            Mesh nextMesh = (Mesh)EditorGUILayout.ObjectField("Kéo Mesh Asset vào đây", m_TargetMesh, typeof(Mesh), false);
            if (nextMesh != m_TargetMesh)
            {
                m_TargetMesh = nextMesh;
                if (m_TargetMesh != null)
                {
                    m_TargetBoundsSize = m_TargetMesh.bounds.size;
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Kéo thả các file Mesh vào danh sách dưới đây để resize đồng loạt.", MessageType.None);
            m_ListScrollPos = EditorGUILayout.BeginScrollView(m_ListScrollPos, GUILayout.Height(150));

            for (int i = 0; i < m_TargetMeshList.Count; i++)
            {
                GUILayout.BeginHorizontal();
                m_TargetMeshList[i] = (Mesh)EditorGUILayout.ObjectField($"Mesh #{i + 1}", m_TargetMeshList[i], typeof(Mesh), false);
                if (GUILayout.Button("Xóa", GUILayout.Width(45)))
                {
                    m_TargetMeshList.RemoveAt(i);
                    break;
                }
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Thêm ô chọn Mesh"))
            {
                m_TargetMeshList.Add(null);
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawSettingsSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("2. CHỈNH TỶ LỆ / KÍCH THƯỚC MỚI", EditorStyles.boldLabel);

        m_Mode = (AdjustmentMode)EditorGUILayout.EnumPopup("Chế độ điều chỉnh", m_Mode);
        m_Pivot = (PivotPoint)EditorGUILayout.EnumPopup("Tâm biến đổi (Pivot)", m_Pivot);

        GUILayout.Space(5);

        if (m_Mode == AdjustmentMode.ScaleMultiplier)
        {
            m_UniformScale = EditorGUILayout.Toggle("Uniform Scale (Tỷ lệ đều)", m_UniformScale);

            if (m_UniformScale)
            {
                float uniform = EditorGUILayout.FloatField("Hệ số Scale (Multiplier)", m_ScaleMultiplier.x);
                m_ScaleMultiplier = new Vector3(uniform, uniform, uniform);
            }
            else
            {
                m_ScaleMultiplier = EditorGUILayout.Vector3Field("Hệ số Scale (X, Y, Z)", m_ScaleMultiplier);
            }

            EditorGUILayout.HelpBox("Ví dụ: (1.2, 1.0, 1.2) tăng chiều rộng X và chiều dài Z lên 20%, giữ nguyên chiều cao Y.", MessageType.None);
        }
        else if (m_Mode == AdjustmentMode.TargetBoundsSize)
        {
            m_TargetBoundsSize = EditorGUILayout.Vector3Field("Kích thước Bounds mục tiêu (X, Y, Z)", m_TargetBoundsSize);
            EditorGUILayout.HelpBox("Tool sẽ tự tính hệ số scale cho từng trục để Mesh đạt đúng kích thước Bounds mục tiêu.", MessageType.None);
        }

        GUILayout.Space(8);
        m_PositionOffset = EditorGUILayout.Vector3Field("Offset vị trí (X, Y, Z)", m_PositionOffset);
        EditorGUILayout.HelpBox("Dùng Offset (ví dụ Y > 0) để nâng Mesh lên cao hơn nếu Mesh bị thấp sau khi resize.", MessageType.None);

        GUILayout.EndVertical();
    }

    private void DrawSaveAndOptionsSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("3. TÙY CHỌN LƯU VÀ TÍNH TOÁN MESH", EditorStyles.boldLabel);

        m_SaveOption = (SaveOption)EditorGUILayout.EnumPopup("Tùy chọn lưu file", m_SaveOption);

        if (m_SaveOption == SaveOption.CreateNewAsset)
        {
            m_CustomSaveDirectory = EditorGUILayout.TextField("Thư mục lưu Mesh mới", m_CustomSaveDirectory);
            EditorGUILayout.HelpBox($"Mesh mới sẽ được lưu dạng .asset trong thư mục '{m_CustomSaveDirectory}'.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("CẢNH BÁO: Ghi đè trực tiếp lên file Mesh gốc trong Project. Không thể Undo sau khi lưu!", MessageType.Warning);
        }

        GUILayout.Space(5);
        m_RecalculateNormals = EditorGUILayout.Toggle("Recalculate Normals", m_RecalculateNormals);
        m_RecalculateTangents = EditorGUILayout.Toggle("Recalculate Tangents", m_RecalculateTangents);

        GUILayout.EndVertical();
    }

    private void DrawPreviewSection()
    {
        if (m_BatchMode || m_TargetMesh == null) return;

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("4. PREVIEW THÔNG SỐ MESH", EditorStyles.boldLabel);

        Bounds b = m_TargetMesh.bounds;
        EditorGUILayout.LabelField("Mesh hiện tại:", m_TargetMesh.name);
        EditorGUILayout.LabelField("Số lượng Vertices:", m_TargetMesh.vertexCount.ToString());
        EditorGUILayout.LabelField("Bounds hiện tại (X, Y, Z):", $"{b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3}");

        Vector3 scaleFactors = GetCalculatedScaleFactors(b);
        Vector3 newBoundsSize = Vector3.Scale(b.size, scaleFactors);

        GUIStyle greenStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.1f, 0.7f, 0.1f) }, fontStyle = FontStyle.Bold };
        EditorGUILayout.LabelField("Bounds sau khi Resize:", $"{newBoundsSize.x:F3} x {newBoundsSize.y:F3} x {newBoundsSize.z:F3}", greenStyle);
        EditorGUILayout.LabelField("Hệ số Scale thực tế:", $"({scaleFactors.x:F3}, {scaleFactors.y:F3}, {scaleFactors.z:F3})");

        GUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        string btnText = m_BatchMode ? "RESIZE TOÀN BỘ MESH TRONG DANH SÁCH" : "RESIZE MESH";

        if (GUILayout.Button(btnText, GUILayout.Height(40)))
        {
            ExecuteResizing();
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    private Vector3 GetCalculatedScaleFactors(Bounds bounds)
    {
        if (m_Mode == AdjustmentMode.ScaleMultiplier)
        {
            return m_ScaleMultiplier;
        }
        else
        {
            float scaleX = bounds.size.x > 0.0001f ? m_TargetBoundsSize.x / bounds.size.x : 1f;
            float scaleY = bounds.size.y > 0.0001f ? m_TargetBoundsSize.y / bounds.size.y : 1f;
            float scaleZ = bounds.size.z > 0.0001f ? m_TargetBoundsSize.z / bounds.size.z : 1f;
            return new Vector3(scaleX, scaleY, scaleZ);
        }
    }

    private void ExecuteResizing()
    {
        if (!m_BatchMode)
        {
            if (m_TargetMesh == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng kéo 1 file Mesh từ Project vào ô chọn trước.", "OK");
                return;
            }

            Mesh result = ResizeSingleMeshAsset(m_TargetMesh);
            if (result != null)
            {
                EditorUtility.DisplayDialog("Hoàn tất", $"Đã resize Mesh '{m_TargetMesh.name}' thành công!", "OK");
            }
        }
        else
        {
            List<Mesh> validMeshes = m_TargetMeshList.Where(m => m != null).ToList();
            if (validMeshes.Count == 0)
            {
                EditorUtility.DisplayDialog("Lỗi", "Danh sách Mesh rỗng. Vui lòng thêm Mesh vào danh sách.", "OK");
                return;
            }

            int count = 0;
            for (int i = 0; i < validMeshes.Count; i++)
            {
                Mesh m = validMeshes[i];
                EditorUtility.DisplayProgressBar("Resizing Meshes", $"Đang xử lý {m.name} ({i + 1}/{validMeshes.Count})...", (float)i / validMeshes.Count);
                if (ResizeSingleMeshAsset(m) != null)
                {
                    count++;
                }
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Hoàn tất Batch", $"Đã resize thành công {count}/{validMeshes.Count} Mesh assets!", "OK");
        }
    }

    private Mesh ResizeSingleMeshAsset(Mesh sourceMesh)
    {
        if (sourceMesh == null) return null;

        Vector3[] vertices = sourceMesh.vertices;
        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogWarning($"[LevelMeshResizerTool] Mesh '{sourceMesh.name}' không có vertices.");
            return null;
        }

        Bounds bounds = sourceMesh.bounds;
        Vector3 pivot = Vector3.zero;

        switch (m_Pivot)
        {
            case PivotPoint.BoundsCenter:
                pivot = bounds.center;
                break;
            case PivotPoint.LocalOrigin:
                pivot = Vector3.zero;
                break;
            case PivotPoint.BottomCenter:
                pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                break;
        }

        Vector3 scaleFactors = GetCalculatedScaleFactors(bounds);
        Vector3[] newVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            Vector3 offset = v - pivot;
            Vector3 scaledOffset = Vector3.Scale(offset, scaleFactors);
            newVertices[i] = pivot + scaledOffset + m_PositionOffset;
        }

        Mesh targetMesh;
        if (m_SaveOption == SaveOption.CreateNewAsset)
        {
            targetMesh = UnityEngine.Object.Instantiate(sourceMesh);
            targetMesh.name = sourceMesh.name + "_Resized";
        }
        else
        {
            targetMesh = sourceMesh;
            Undo.RecordObject(targetMesh, "Resize Mesh Vertices");
        }

        targetMesh.vertices = newVertices;
        targetMesh.RecalculateBounds();

        if (m_RecalculateNormals)
        {
            targetMesh.RecalculateNormals();
        }
        if (m_RecalculateTangents)
        {
            targetMesh.RecalculateTangents();
        }

        if (m_SaveOption == SaveOption.CreateNewAsset)
        {
            string saveFolder = string.IsNullOrEmpty(m_CustomSaveDirectory) ? "Assets/Mesh/Resized" : m_CustomSaveDirectory;
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            string assetPath = $"{saveFolder}/{targetMesh.name}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(targetMesh, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelMeshResizerTool] Đã tạo file Mesh mới tại: {assetPath}");
        }
        else
        {
            EditorUtility.SetDirty(targetMesh);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelMeshResizerTool] Đã cập nhật đè file Mesh gốc: {sourceMesh.name}");
        }

        return targetMesh;
    }
}
#endif
