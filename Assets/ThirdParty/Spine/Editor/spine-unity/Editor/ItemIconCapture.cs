#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class BatchItemIconCaptureWindow : EditorWindow
{
    // =========================
    // Camera & Items
    // =========================
    private Camera captureCamera;
    private readonly List<Transform> items = new List<Transform>();

    // =========================
    // Output
    // =========================
    private int resolution = 1024;
    private string outputFolder = "Assets/ItemIcons";

    // =========================
    // Camera Fit
    // =========================
    private float padding = 1.2f;

    // =========================
    // Trim
    // =========================
    private float alphaThreshold = 0.01f;

    // =========================
    // UI
    // =========================
    private Vector2 scrollPos;

    // =========================
    // Cache active state
    // =========================
    private readonly Dictionary<Transform, bool> activeStateCache =
        new Dictionary<Transform, bool>();

    // =========================
    // Menu
    // =========================
    [MenuItem("Tools/Icon Capture/Batch Item Capture")]
    public static void Open()
    {
        GetWindow<BatchItemIconCaptureWindow>("Batch Item Capture");
    }

    // =========================
    // GUI
    // =========================
    private void OnGUI()
    {
        GUILayout.Label("Batch Item Icon Capture", EditorStyles.boldLabel);

        captureCamera = (Camera)EditorGUILayout.ObjectField(
            "Capture Camera",
            captureCamera,
            typeof(Camera),
            true
        );

        resolution = EditorGUILayout.IntPopup(
            "Resolution",
            resolution,
            new[] { "256", "512", "1024", "2048" },
            new[] { 256, 512, 1024, 2048 }
        );

        padding = EditorGUILayout.Slider("Camera Padding", padding, 1f, 2f);
        alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 0.1f);

        EditorGUILayout.Space(8);

        DrawDragAndDropArea();
        EditorGUILayout.Space(6);

        DrawItemList();
        EditorGUILayout.Space(6);

        DrawOutputFolder();
        EditorGUILayout.Space(10);

        // =========================
        // Sticky bottom bar
        // =========================
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.enabled = captureCamera != null && items.Count > 0;
        if (GUILayout.Button($"CAPTURE ALL ({items.Count})", GUILayout.Height(42)))
        {
            CaptureAll();
        }
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    // =========================
    // Drag & Drop
    // =========================
    private void DrawDragAndDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        GUI.Box(
            dropArea,
            "DRAG & DROP ITEMS HERE\n(Scene GameObjects)",
            EditorStyles.helpBox
        );

        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Object[] dragged = DragAndDrop.objectReferences;

                for (int i = 0; i < dragged.Length; i++)
                {
                    if (dragged[i] is GameObject go)
                    {
                        Transform t = go.transform;
                        if (!items.Contains(t))
                        {
                            items.Add(t);
                        }
                    }
                }
            }

            evt.Use();
        }
    }

    // =========================
    // Item List (ScrollView)
    // =========================
    private void DrawItemList()
    {
        GUILayout.Label($"Items ({items.Count})", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(
            scrollPos,
            GUILayout.Height(260)
        );

        int removeIndex = -1;

        for (int i = 0; i < items.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(items[i], typeof(Transform), true);

            if (GUILayout.Button("X", GUILayout.Width(22)))
                removeIndex = i;

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            items.RemoveAt(removeIndex);

        EditorGUILayout.EndScrollView();

        if (items.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear List"))
                items.Clear();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Total: {items.Count}");
            EditorGUILayout.EndHorizontal();
        }
    }

    // =========================
    // Output Folder
    // =========================
    private void DrawOutputFolder()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Output Folder", outputFolder);

        if (GUILayout.Button("Select", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFolderPanel(
                "Select Output Folder",
                "Assets",
                ""
            );

            if (!string.IsNullOrEmpty(selected) &&
                selected.StartsWith(Application.dataPath))
            {
                outputFolder = "Assets" +
                               selected.Substring(Application.dataPath.Length);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================
    // Capture Logic
    // =========================
    private void CaptureAll()
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                CaptureSingle(items[i]);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("Batch item icon capture completed.");
    }

    private void CaptureSingle(Transform item)
    {
        DisableOtherItems(item);

        FitCameraToItem(item);

        RenderTexture rt = new RenderTexture(
            resolution,
            resolution,
            24,
            RenderTextureFormat.ARGB32
        );

        captureCamera.targetTexture = rt;

        Texture2D tex = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false
        );

        captureCamera.Render();

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tex.Apply();

        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);

        Texture2D trimmed = TrimAlpha(tex, alphaThreshold);
        DestroyImmediate(tex);

        string filePath = Path.Combine(outputFolder, item.name + ".png");
        File.WriteAllBytes(filePath, trimmed.EncodeToPNG());
        DestroyImmediate(trimmed);

        RestoreItems();
    }

    // =========================
    // Disable / Restore Items
    // =========================
    private void DisableOtherItems(Transform current)
    {
        activeStateCache.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            Transform t = items[i];
            if (t == null) continue;

            bool isActive = t.gameObject.activeSelf;
            activeStateCache[t] = isActive;

            if (t != current && isActive)
                t.gameObject.SetActive(false);
        }
    }

    private void RestoreItems()
    {
        foreach (var kv in activeStateCache)
        {
            if (kv.Key != null)
                kv.Key.gameObject.SetActive(kv.Value);
        }

        activeStateCache.Clear();
    }

    // =========================
    // Camera Fit
    // =========================
    private void FitCameraToItem(Transform item)
    {
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 center = bounds.center;
        float radius = bounds.extents.magnitude * padding;

        float fovRad = captureCamera.fieldOfView * Mathf.Deg2Rad;
        float distance = radius / Mathf.Sin(fovRad * 0.5f);

        captureCamera.transform.position =
            center - captureCamera.transform.forward * distance;

        captureCamera.transform.LookAt(center);
    }

    // =========================
    // Trim Alpha
    // =========================
    private Texture2D TrimAlpha(Texture2D source, float threshold)
    {
        int w = source.width;
        int h = source.height;
        Color32[] pixels = source.GetPixels32();

        int minX = w, minY = h, maxX = 0, maxY = 0;
        bool found = false;

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (pixels[row + x].a > threshold * 255f)
                {
                    found = true;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (!found)
            return source;

        int newW = maxX - minX + 1;
        int newH = maxY - minY + 1;

        Texture2D result = new Texture2D(
            newW,
            newH,
            TextureFormat.RGBA32,
            false
        );

        Color32[] newPixels = new Color32[newW * newH];

        for (int y = 0; y < newH; y++)
        {
            int srcRow = (minY + y) * w;
            int dstRow = y * newW;

            for (int x = 0; x < newW; x++)
            {
                newPixels[dstRow + x] =
                    pixels[srcRow + (minX + x)];
            }
        }

        result.SetPixels32(newPixels);
        result.Apply();

        return result;
    }
}
#endif
