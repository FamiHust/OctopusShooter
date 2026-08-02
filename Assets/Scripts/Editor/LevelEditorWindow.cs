#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelEditorWindow : EditorWindow
{
    [System.Serializable]
    public class TunnelShooterData
    {
        public SeedColor color;
        public int maxBulletCount = 100;
    }

    [System.Serializable]
    public class CellData
    {
        public int row;
        public int col;
        public GridItemType type;

        // Shooter properties
        public SeedColor shooterColor = SeedColor.Red;
        public int shooterMaxBulletCount = 100;

        // Tunnel properties
        public Direction tunnelDir = Direction.Up;
        public List<TunnelShooterData> tunnelShooters = new List<TunnelShooterData>();
    }

    // Prefab file path
    private string m_PrefabPath = "";
    private GameObject m_PrefabObj = null;
    private bool m_Loaded = false;

    // Grid details
    private int m_Rows = 0;
    private int m_Cols = 0;
    private CellData[,] m_Cells = null;

    // Conveyor / Spline details
    private List<SeedColor> m_ListColor = new List<SeedColor>();
    private int m_CountMainRow = 0;
    private List<int> m_CountSideRows = new List<int>();

    // Database details
    private LevelDataBase m_LevelDB = null;
    private string[] m_LevelNames = new string[0];
    private int m_SelectedLevelIndex = -1;

    // Editor UI state
    private Vector2 m_GridScrollPos;
    private Vector2 m_SidebarScrollPos;
    private CellData m_SelectedCell = null;
    private int m_ActiveTab = 0; // 0 = Grid Matrix, 1 = Conveyor Config
    
    // Copy-paste, Swap, Move state
    private CellData m_CopiedCell = null;
    private bool m_SwapMode = false;
    private bool m_MoveMode = false;

    // Map & Mesh configuration
    private List<Mesh> m_MapMeshList = new List<Mesh>();
    private Mesh m_SelectedMapMesh = null;
    private MeshFilter m_ConveyorMeshFilter = null;
    private Mesh m_SelectedConveyorMesh = null;
    private List<Mesh> m_ConveyorMeshList = new List<Mesh>();
    private List<Mesh> m_ShooterMeshList = new List<Mesh>();
    private Mesh m_SelectedShooterMesh = null;
    private Vector2 m_ColorSummaryScrollPos;
    private Vector2 m_ConveyorMeshScrollPos;
    private Vector2 m_TabScrollPos;
    private Vector2 m_SeedStatsScrollPos;
    private float m_GridPanelWidthRatio = 0.55f;
    private bool m_IsResizingSplitter = false;
    private float m_ColorSummaryHeight = 70f;
    private bool m_IsResizingVerticalSplitter = false;

    // Conveyor scan results
    private List<string> m_ScanZeroMainRowNames = null;
    private List<string> m_ScanZeroMainRowPaths = null;
    private Vector2 m_ScanResultScrollPos;



    [MenuItem("FlowBlast Tools/Level Editor Window")]
    public static void OpenWindow()
    {
        LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadDatabase();
        LoadAvailableMapMeshes();
        LoadAvailableShooterMeshes();
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

    private void OnGUI()
    {
        DrawHeader();

        if (!m_Loaded)
        {
            EditorGUILayout.HelpBox("Hãy chọn một Level Prefab và bấm 'Load' để bắt đầu chỉnh sửa.", MessageType.Info);
            return;
        }

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();

        // Left Panel: Grid matrix visualization
        DrawGridPanel();

        // Draggable Splitter Handle between Grid and Inspector
        DrawSplitter();

        // Right Panel: Editor settings and inspector
        DrawRightPanel();

        GUILayout.EndHorizontal();
    }

    private void DrawSplitter()
    {
        Rect splitterRect = GUILayoutUtility.GetRect(6f, 0f, GUILayout.ExpandHeight(true), GUILayout.Width(6f));
        EditorGUI.DrawRect(splitterRect, new Color(0.2f, 0.2f, 0.2f, 1f));
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

        if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
        {
            m_IsResizingSplitter = true;
        }

        if (m_IsResizingSplitter)
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                float mouseX = Event.current.mousePosition.x;
                m_GridPanelWidthRatio = Mathf.Clamp(mouseX / position.width, 0.2f, 0.8f);
                Repaint();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                m_IsResizingSplitter = false;
            }
        }
    }

    private void DrawVerticalSplitter()
    {
        Rect splitterRect = GUILayoutUtility.GetRect(0f, 6f, GUILayout.ExpandWidth(true), GUILayout.Height(6f));
        EditorGUI.DrawRect(splitterRect, new Color(0.2f, 0.2f, 0.2f, 1f));
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

        if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
        {
            m_IsResizingVerticalSplitter = true;
        }

        if (m_IsResizingVerticalSplitter)
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                float mouseY = Event.current.mousePosition.y;
                float windowHeight = position.height;
                m_ColorSummaryHeight = Mathf.Clamp(windowHeight - mouseY, 35f, 300f);
                Repaint();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                m_IsResizingVerticalSplitter = false;
            }
        }
    }

    private void DrawColorSummary()
    {
        if (!m_Loaded || m_Cells == null) return;

        // Statistics data structure
        Dictionary<SeedColor, (int blockCount, int bulletCount)> stats = new Dictionary<SeedColor, (int, int)>();
        int totalBlocks = 0;
        int totalBullets = 0;

        for (int r = 0; r < m_Rows; r++)
        {
            for (int c = 0; c < m_Cols; c++)
            {
                CellData cell = m_Cells[r, c];
                if (cell == null) continue;

                if (cell.type == GridItemType.Shooter)
                {
                    SeedColor color = cell.shooterColor;
                    int bullets = cell.shooterMaxBulletCount;

                    if (!stats.ContainsKey(color))
                    {
                        stats[color] = (0, 0);
                    }
                    var (bCount, bulCount) = stats[color];
                    stats[color] = (bCount + 1, bulCount + bullets);
                    totalBlocks += 1;
                    totalBullets += bullets;
                }
                else if (cell.type == GridItemType.Tunnel)
                {
                    if (cell.tunnelShooters != null)
                    {
                        foreach (var s in cell.tunnelShooters)
                        {
                            if (s == null) continue;
                            SeedColor color = s.color;
                            int bullets = s.maxBulletCount;

                            if (!stats.ContainsKey(color))
                            {
                                stats[color] = (0, 0);
                            }
                            var (bCount, bulCount) = stats[color];
                            stats[color] = (bCount + 1, bulCount + bullets);
                            totalBlocks += 1;
                            totalBullets += bullets;
                        }
                    }
                }
            }
        }

        GUIStyle badgeStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8, 8, 4, 4)
        };

        GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(m_ColorSummaryHeight));

        m_ColorSummaryScrollPos = EditorGUILayout.BeginScrollView(m_ColorSummaryScrollPos, false, false, GUILayout.Height(Mathf.Max(30f, m_ColorSummaryHeight - 12f)));
        GUILayout.BeginHorizontal();

        // 1. Total Blocks Summary Badge
        GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
        GUILayout.Box($"Map: {totalBlocks}B ({totalBullets}đ)", badgeStyle, GUILayout.Height(26));
        GUI.backgroundColor = Color.white;

        // 2. Color Badges for Map Blocks
        foreach (var kvp in stats.OrderBy(k => k.Key.ToString()))
        {
            SeedColor color = kvp.Key;
            var (bCount, bulCount) = kvp.Value;

            Color c = ColorInfo.GetUnityColor(color);
            GUI.backgroundColor = c;
            GUILayout.Box($"{color}: {bCount}B ({bulCount}đ)", badgeStyle, GUILayout.Height(26));
            GUI.backgroundColor = Color.white;
        }

        // 3. Conveyor Seeds Badges
        if (m_ListColor != null && m_ListColor.Count > 0)
        {
            GUILayout.Space(15);
            int totalConveyorBlocks = m_ListColor.Count * 50;
            GUI.backgroundColor = new Color(0.15f, 0.35f, 0.55f);
            GUILayout.Box($"BC: {m_ListColor.Count}H ({totalConveyorBlocks}B)", badgeStyle, GUILayout.Height(26));
            GUI.backgroundColor = Color.white;

            var conveyorStats = m_ListColor.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in conveyorStats.OrderBy(k => k.Key.ToString()))
            {
                SeedColor color = kvp.Key;
                int seeds = kvp.Value;
                int blocks = seeds * 50;

                Color c = ColorInfo.GetUnityColor(color);
                GUI.backgroundColor = c;
                GUILayout.Box($"[BC] {color}: {seeds}H ({blocks}B)", badgeStyle, GUILayout.Height(26));
                GUI.backgroundColor = Color.white;
            }
        }

        GUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    private void DrawHeader()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("CẤU HÌNH LEVEL EDITOR", EditorStyles.boldLabel);
        
        GUILayout.BeginHorizontal();
        
        // Level DB selector
        if (m_LevelDB != null)
        {
            int nextIdx = EditorGUILayout.Popup("Chọn từ DataBase", m_SelectedLevelIndex, m_LevelNames, GUILayout.Width(350));
            if (nextIdx != m_SelectedLevelIndex)
            {
                m_SelectedLevelIndex = nextIdx;
                m_PrefabObj = m_LevelDB.listPrefab[m_SelectedLevelIndex];
                m_PrefabPath = AssetDatabase.GetAssetPath(m_PrefabObj);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Không tìm thấy LevelDataBase asset trong dự án.", MessageType.Warning, true);
        }

        // Direct GameObject drag-and-drop
        GameObject nextPrefabObj = (GameObject)EditorGUILayout.ObjectField("Level Prefab", m_PrefabObj, typeof(GameObject), false, GUILayout.Width(350));
        if (nextPrefabObj != m_PrefabObj)
        {
            m_PrefabObj = nextPrefabObj;
            if (m_PrefabObj != null)
            {
                m_PrefabPath = AssetDatabase.GetAssetPath(m_PrefabObj);
                // Synchronize index in database if present
                if (m_LevelDB != null && m_LevelDB.listPrefab != null)
                {
                    m_SelectedLevelIndex = m_LevelDB.listPrefab.IndexOf(m_PrefabObj);
                }
            }
            else
            {
                m_PrefabPath = "";
                m_SelectedLevelIndex = -1;
            }
        }

        GUILayout.EndHorizontal();

        // Mesh Selection Row (Map Mesh & Conveyor Mesh side-by-side)
        GUILayout.BeginHorizontal();
        
        // 1. Map Mesh Dropdown Popup
        if (m_MapMeshList == null || m_MapMeshList.Count == 0)
        {
            LoadAvailableMapMeshes();
        }

        if (m_MapMeshList != null && m_MapMeshList.Count > 0)
        {
            string[] mapNames = m_MapMeshList.Select(m => m != null ? m.name : "(Empty)").ToArray();
            int currentIdx = m_MapMeshList.IndexOf(m_SelectedMapMesh);
            int selectedIdx = EditorGUILayout.Popup("Chọn từ List Map", currentIdx, mapNames);
            if (selectedIdx >= 0 && selectedIdx < m_MapMeshList.Count && selectedIdx != currentIdx)
            {
                m_SelectedMapMesh = m_MapMeshList[selectedIdx];
                if (m_Loaded && !string.IsNullOrEmpty(m_PrefabPath) && m_SelectedMapMesh != null)
                {
                    ApplyMapMeshToPrefab(m_SelectedMapMesh);
                }
            }
        }

        GUILayout.Space(10);

        // 2. Conveyor Mesh Dropdown Popup
        if (m_ConveyorMeshList == null || m_ConveyorMeshList.Count == 0)
        {
            LoadAvailableConveyorMeshes();
        }

        if (m_ConveyorMeshList != null && m_ConveyorMeshList.Count > 0)
        {
            string[] conveyorNames = m_ConveyorMeshList.Select(m => m != null ? m.name : "(Empty)").ToArray();
            int currentIdx = m_ConveyorMeshList.IndexOf(m_SelectedConveyorMesh);
            int selectedIdx = EditorGUILayout.Popup("Chọn từ List Conveyor", currentIdx, conveyorNames);
            if (selectedIdx >= 0 && selectedIdx < m_ConveyorMeshList.Count && selectedIdx != currentIdx)
            {
                m_SelectedConveyorMesh = m_ConveyorMeshList[selectedIdx];
                if (m_Loaded && !string.IsNullOrEmpty(m_PrefabPath) && m_SelectedConveyorMesh != null)
                {
                    ApplyConveyorMeshToPrefab(m_SelectedConveyorMesh);
                }
            }
        }


        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
        if (GUILayout.Button("Load Level Prefab", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(m_PrefabPath))
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn hoặc kéo thả Level Prefab trước.", "OK");
            }
            else
            {
                LoadPrefabData(m_PrefabPath);
                if (m_PrefabObj != null)
                {
                    AssetDatabase.OpenAsset(m_PrefabObj);
                    EditorGUIUtility.PingObject(m_PrefabObj);
                }
            }
        }

        GUI.backgroundColor = new Color(0.4f, 0.6f, 1f, 1f);
        if (GUILayout.Button("Save Level Prefab", GUILayout.Height(30)))
        {
            if (m_Loaded && !string.IsNullOrEmpty(m_PrefabPath))
            {
                SavePrefabData(m_PrefabPath);
            }
        }

        GUI.backgroundColor = new Color(0.9f, 0.6f, 0.2f, 1f);
        if (GUILayout.Button("Mesh Resizer Tool", GUILayout.Height(30)))
        {
            LevelMeshResizerTool.OpenWindow();
        }

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.6f, 1f);
        if (GUILayout.Button("Shooter Mesh Tool", GUILayout.Height(30)))
        {
            ShooterMeshAssignerTool.OpenWindow();
        }

        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.95f, 1f);
        if (GUILayout.Button("Desk Mesh Tool", GUILayout.Height(30)))
        {
            DeskMeshAssignerTool.OpenWindow();
        }
        
        GUI.backgroundColor = Color.white;
        
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
    }

    private void DrawGridPanel()
    {
        float gridWidth = Mathf.Max(200f, position.width * m_GridPanelWidthRatio);
        GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(gridWidth));
        GUILayout.Label("MA TRẬN LEVEL", EditorStyles.boldLabel);

        if (m_SwapMode)
        {
            EditorGUILayout.HelpBox("SWAP MODE: Hãy click vào ô đích để HOÁN ĐỔI với ô đang chọn.", MessageType.Warning);
            if (GUILayout.Button("Hủy Swap Mode")) m_SwapMode = false;
        }
        else if (m_MoveMode)
        {
            EditorGUILayout.HelpBox("MOVE MODE: Hãy click vào ô đích để DI CHUYỂN ô đang chọn sang đó.", MessageType.Warning);
            if (GUILayout.Button("Hủy Move Mode")) m_MoveMode = false;
        }

        m_GridScrollPos = GUILayout.BeginScrollView(m_GridScrollPos);

        float cellSize = Mathf.Max(40f, (gridWidth - 40f) / Mathf.Max(1, m_Cols));
        
        // Draw rows from top (0) to bottom (m_Rows - 1)
        for (int r = 0; r < m_Rows; r++)
        {
            GUILayout.BeginHorizontal();
            for (int c = 0; c < m_Cols; c++)
            {
                CellData cell = m_Cells[r, c];
                string label = GetCellLabel(cell);
                Color cellColor = GetCellColor(cell);

                Color oldColor = GUI.backgroundColor;
                
                // Highlight if selected
                if (m_SelectedCell == cell)
                {
                    GUI.backgroundColor = Color.Lerp(cellColor, Color.white, 0.4f);
                }
                else
                {
                    GUI.backgroundColor = cellColor;
                }

                // Custom styling for grid item button
                var style = new GUIStyle(GUI.skin.button);
                style.richText = true;
                style.fontSize = 11;
                style.alignment = TextAnchor.MiddleCenter;

                if (GUILayout.Button(label, style, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                {
                    OnCellClicked(cell);
                }

                GUI.backgroundColor = oldColor;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void OnCellClicked(CellData clickedCell)
    {
        if (m_SwapMode)
        {
            if (m_SelectedCell != null && m_SelectedCell != clickedCell)
            {
                SwapCells(m_SelectedCell, clickedCell);
                m_SelectedCell = clickedCell;
            }
            m_SwapMode = false;
        }
        else if (m_MoveMode)
        {
            if (m_SelectedCell != null && m_SelectedCell != clickedCell)
            {
                MoveCell(m_SelectedCell, clickedCell);
                m_SelectedCell = clickedCell;
            }
            m_MoveMode = false;
        }
        else
        {
            m_SelectedCell = clickedCell;
        }
    }

    private string GetCellLabel(CellData cell)
    {
        switch (cell.type)
        {
            case GridItemType.EmptyCell:
                return "<color=grey>Empty</color>";
            case GridItemType.Wall:
                return "<b>WALL</b>";
            case GridItemType.Shooter:
                return $"<b>{cell.shooterColor.ToString().ToUpper()}</b>\n{cell.shooterMaxBulletCount}đ";
            case GridItemType.Tunnel:
                string dirArrow = GetDirectionArrow(cell.tunnelDir);
                return $"<b>TUNNEL {dirArrow}</b>\n({cell.tunnelShooters.Count} S)";
            default:
                return "";
        }
    }

    private string GetDirectionArrow(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return "▲";
            case Direction.Down: return "▼";
            case Direction.Left: return "◀";
            case Direction.Right: return "▶";
            default: return "";
        }
    }

    private Color GetCellColor(CellData cell)
    {
        switch (cell.type)
        {
            case GridItemType.EmptyCell:
                return new Color(0.85f, 0.85f, 0.85f, 1f);
            case GridItemType.Wall:
                return new Color(0.25f, 0.25f, 0.25f, 1f);
            case GridItemType.Tunnel:
                return new Color(0.2f, 0.5f, 0.75f, 1f);
            case GridItemType.Shooter:
                return ColorInfo.GetUnityColor(cell.shooterColor);
            default:
                return Color.white;
        }
    }

    private void DrawRightPanel()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        
        GUIStyle tabStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            fixedHeight = 26
        };

        // Draw Tabs selector with horizontal scrolling
        m_TabScrollPos = EditorGUILayout.BeginScrollView(m_TabScrollPos, false, false, GUILayout.Height(48));
        m_ActiveTab = GUILayout.Toolbar(m_ActiveTab, new string[] { "Cell Inspector", "Conveyor Config", "Map Mesh List", "Conveyor Mesh List", "Shooter Mesh List" }, tabStyle, GUILayout.MinWidth(700), GUILayout.Height(26));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        m_SidebarScrollPos = GUILayout.BeginScrollView(m_SidebarScrollPos);

        switch (m_ActiveTab)
        {
            case 0:
                DrawCellInspectorTab();
                break;
            case 1:
                DrawConveyorConfigTab();
                break;
            case 2:
                DrawMapMeshListTab();
                break;
            case 3:
                DrawConveyorMeshListTab();
                break;
            case 4:
                DrawShooterMeshListTab();
                break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawMapMeshListTab()
    {
        GUILayout.Label("DANH SÁCH MAP MESH CHO LEVEL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Danh sách này giúp chọn và quản lý các Mesh Map dùng để thay đổi visual cho từng level.", MessageType.Info);



        GUILayout.Space(10);

        for (int i = 0; i < m_MapMeshList.Count; i++)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUILayout.Label($"#{i + 1}", GUILayout.Width(30));
            m_MapMeshList[i] = (Mesh)EditorGUILayout.ObjectField(m_MapMeshList[i], typeof(Mesh), false);

            if (GUILayout.Button("▲", GUILayout.Width(25)))
            {
                if (i > 0)
                {
                    var tmp = m_MapMeshList[i];
                    m_MapMeshList[i] = m_MapMeshList[i - 1];
                    m_MapMeshList[i - 1] = tmp;
                }
            }
            if (GUILayout.Button("▼", GUILayout.Width(25)))
            {
                if (i < m_MapMeshList.Count - 1)
                {
                    var tmp = m_MapMeshList[i];
                    m_MapMeshList[i] = m_MapMeshList[i + 1];
                    m_MapMeshList[i + 1] = tmp;
                }
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
            if (GUILayout.Button("Áp Dụng", GUILayout.Width(65)))
            {
                if (m_MapMeshList[i] != null)
                {
                    m_SelectedMapMesh = m_MapMeshList[i];
                    if (m_Loaded && !string.IsNullOrEmpty(m_PrefabPath))
                    {
                        ApplyMapMeshToPrefab(m_SelectedMapMesh);
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Xóa", GUILayout.Width(45)))
            {
                m_MapMeshList.RemoveAt(i);
                break;
            }

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Thêm Slot Map Mesh"))
        {
            m_MapMeshList.Add(null);
        }
    }

    private void LoadAvailableMapMeshes()
    {
        if (m_MapMeshList == null) m_MapMeshList = new List<Mesh>();

        string[] guids = AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets/Mesh" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && !m_MapMeshList.Contains(mesh))
            {
                m_MapMeshList.Add(mesh);
            }
        }
    }

    private void DrawConveyorMeshListTab()
    {
        GUILayout.Label("DANH SÁCH CONVEYOR MESH CHO LEVEL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Danh sách này giúp chọn và quản lý các Mesh Băng Chuyền (Track/BlockConveyor) dùng để thay đổi visual cho từng level.", MessageType.Info);



        GUILayout.Space(10);

        for (int i = 0; i < m_ConveyorMeshList.Count; i++)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUILayout.Label($"#{i + 1}", GUILayout.Width(30));
            m_ConveyorMeshList[i] = (Mesh)EditorGUILayout.ObjectField(m_ConveyorMeshList[i], typeof(Mesh), false);

            if (GUILayout.Button("▲", GUILayout.Width(25)))
            {
                if (i > 0)
                {
                    var tmp = m_ConveyorMeshList[i];
                    m_ConveyorMeshList[i] = m_ConveyorMeshList[i - 1];
                    m_ConveyorMeshList[i - 1] = tmp;
                }
            }
            if (GUILayout.Button("▼", GUILayout.Width(25)))
            {
                if (i < m_ConveyorMeshList.Count - 1)
                {
                    var tmp = m_ConveyorMeshList[i];
                    m_ConveyorMeshList[i] = m_ConveyorMeshList[i + 1];
                    m_ConveyorMeshList[i + 1] = tmp;
                }
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
            if (GUILayout.Button("Áp Dụng", GUILayout.Width(65)))
            {
                if (m_ConveyorMeshList[i] != null)
                {
                    m_SelectedConveyorMesh = m_ConveyorMeshList[i];
                    if (m_Loaded && !string.IsNullOrEmpty(m_PrefabPath))
                    {
                        ApplyConveyorMeshToPrefab(m_SelectedConveyorMesh);
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Xóa", GUILayout.Width(45)))
            {
                m_ConveyorMeshList.RemoveAt(i);
                break;
            }

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Thêm Slot Conveyor Mesh"))
        {
            m_ConveyorMeshList.Add(null);
        }
    }

    private void LoadAvailableConveyorMeshes()
    {
        if (m_ConveyorMeshList == null) m_ConveyorMeshList = new List<Mesh>();

        string[] guids = AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets/Mesh" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && !m_ConveyorMeshList.Contains(mesh))
            {
                if (mesh.name.StartsWith("Conveyor_") || mesh.name.StartsWith("Track_") || mesh.name.StartsWith("BlockConveyor_") || mesh.name.ToLower().Contains("conveyor"))
                {
                    m_ConveyorMeshList.Add(mesh);
                }
            }
        }
    }

    private void DrawShooterMeshListTab()
    {
        GUILayout.Label("DANH SÁCH SHOOTER MESH CHO LEVEL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Chọn và áp dụng Mesh visual cho các Shooter trong Level Prefab hiện tại.", MessageType.Info);

        GUILayout.Space(10);

        for (int i = 0; i < m_ShooterMeshList.Count; i++)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUILayout.Label($"#{i + 1}", GUILayout.Width(30));
            m_ShooterMeshList[i] = (Mesh)EditorGUILayout.ObjectField(m_ShooterMeshList[i], typeof(Mesh), false);

            if (GUILayout.Button("▲", GUILayout.Width(25)))
            {
                if (i > 0)
                {
                    var tmp = m_ShooterMeshList[i];
                    m_ShooterMeshList[i] = m_ShooterMeshList[i - 1];
                    m_ShooterMeshList[i - 1] = tmp;
                }
            }
            if (GUILayout.Button("▼", GUILayout.Width(25)))
            {
                if (i < m_ShooterMeshList.Count - 1)
                {
                    var tmp = m_ShooterMeshList[i];
                    m_ShooterMeshList[i] = m_ShooterMeshList[i + 1];
                    m_ShooterMeshList[i + 1] = tmp;
                }
            }

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
            if (GUILayout.Button("Áp Dụng Cho Level", GUILayout.Width(130)))
            {
                if (m_ShooterMeshList[i] != null)
                {
                    m_SelectedShooterMesh = m_ShooterMeshList[i];
                    if (m_Loaded && !string.IsNullOrEmpty(m_PrefabPath))
                    {
                        ApplyShooterMeshToCurrentPrefab(m_SelectedShooterMesh);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Thông báo", "Vui lòng load một Level Prefab trước.", "OK");
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Xóa", GUILayout.Width(45)))
            {
                m_ShooterMeshList.RemoveAt(i);
                break;
            }

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Thêm Slot Shooter Mesh"))
        {
            m_ShooterMeshList.Add(null);
        }

        GUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.6f, 1f);
        if (GUILayout.Button("Mở Tool Gán Mesh Shooter Nâng Cao (Batch / Theo Màu)", GUILayout.Height(35)))
        {
            ShooterMeshAssignerTool.OpenWindow();
        }
        GUI.backgroundColor = Color.white;
    }

    private void LoadAvailableShooterMeshes()
    {
        if (m_ShooterMeshList == null) m_ShooterMeshList = new List<Mesh>();

        string[] guids = AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && !m_ShooterMeshList.Contains(mesh))
            {
                string nameLower = mesh.name.ToLower();
                if (nameLower.Contains("shooter") || nameLower.Contains("character") || nameLower.Contains("seed") || nameLower.Contains("block"))
                {
                    m_ShooterMeshList.Add(mesh);
                }
            }
        }
    }

    private void ApplyShooterMeshToCurrentPrefab(Mesh newMesh)
    {
        if (newMesh == null || string.IsNullOrEmpty(m_PrefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(m_PrefabPath);
        if (root == null) return;

        try
        {
            BaseShooter[] shooters = root.GetComponentsInChildren<BaseShooter>(true);
            if (shooters == null || shooters.Length == 0)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy Shooter nào trong Level Prefab này.", "OK");
                return;
            }

            int updatedCount = 0;
            var meshField = typeof(BaseShooter).GetField("mesh", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (BaseShooter shooter in shooters)
            {
                if (shooter == null) continue;

                bool changed = false;

                SkinnedMeshRenderer smr = null;
                if (meshField != null)
                {
                    smr = meshField.GetValue(shooter) as SkinnedMeshRenderer;
                }
                if (smr == null)
                {
                    smr = shooter.GetComponentInChildren<SkinnedMeshRenderer>(true);
                }

                if (smr != null && smr.sharedMesh != newMesh)
                {
                    smr.sharedMesh = newMesh;
                    EditorUtility.SetDirty(smr);
                    changed = true;
                }

                MeshFilter mf = shooter.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != newMesh)
                {
                    mf.sharedMesh = newMesh;
                    EditorUtility.SetDirty(mf);
                    changed = true;
                }

                MeshCollider mc = shooter.GetComponentInChildren<MeshCollider>(true);
                if (mc != null && mc.sharedMesh != newMesh)
                {
                    mc.sharedMesh = newMesh;
                    EditorUtility.SetDirty(mc);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(shooter);
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, m_PrefabPath);
                EditorUtility.DisplayDialog("Thành công", $"Đã gán Mesh '{newMesh.name}' cho {updatedCount} Shooter trong Level!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Thông báo", "Tất cả các Shooter đã sở hữu Mesh này rồi.", "OK");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private MeshFilter FindMapMeshFilter(GameObject root)
    {
        if (root == null) return null;

        LevelController lc = root.GetComponent<LevelController>();
        if (lc != null)
        {
            var mapMfField = typeof(LevelController).GetField("mapMeshFilter", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mapMfField != null)
            {
                MeshFilter mf = mapMfField.GetValue(lc) as MeshFilter;
                if (mf != null) return mf;
            }
        }

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        
        // 1. Check GameObject name starting with Level_
        foreach (var mf in filters)
        {
            if (mf != null && mf.gameObject != null && mf.gameObject.name.StartsWith("Level_")) return mf;
        }

        // 2. Check sharedMesh name starting with Level_
        foreach (var mf in filters)
        {
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Level_")) return mf;
        }

        // 3. Fallback to non-Shooter/Wall/Tunnel/Deck/Canvas mesh
        foreach (var mf in filters)
        {
            if (mf != null && mf.gameObject != null)
            {
                string gName = mf.gameObject.name;
                if (!gName.StartsWith("Shooter") && !gName.StartsWith("Wall") && 
                    !gName.StartsWith("Tunnel") && !gName.StartsWith("Deck") && 
                    !gName.StartsWith("Canvas"))
                {
                    return mf;
                }
            }
        }
        return null;
    }

    private void ApplyMapMeshToPrefab(Mesh newMesh)
    {
        if (newMesh == null || string.IsNullOrEmpty(m_PrefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(m_PrefabPath);
        if (root == null) return;

        MeshFilter mapMf = FindMapMeshFilter(root);
        if (mapMf != null)
        {
            mapMf.sharedMesh = newMesh;
            mapMf.gameObject.name = newMesh.name;
            EditorUtility.SetDirty(mapMf);
            EditorUtility.SetDirty(mapMf.gameObject);

            LevelController lc = root.GetComponent<LevelController>();
            if (lc != null)
            {
                var mapMfField = typeof(LevelController).GetField("mapMeshFilter", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mapMfField != null)
                {
                    mapMfField.SetValue(lc, mapMf);
                    EditorUtility.SetDirty(lc);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, m_PrefabPath);
            Debug.Log($"[LevelEditorWindow] Đã cập nhật Map Mesh thành '{newMesh.name}' cho Level Prefab: {m_PrefabPath}");
        }
        else
        {
            Debug.LogWarning("[LevelEditorWindow] Không tìm thấy MeshFilter đại diện cho Map trong Level Prefab.");
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    private MeshFilter FindConveyorMeshFilter(GameObject root)
    {
        if (root == null) return null;

        LevelController lc = root.GetComponent<LevelController>();
        if (lc != null)
        {
            var conveyorMfField = typeof(LevelController).GetField("conveyorMeshFilter", BindingFlags.NonPublic | BindingFlags.Instance);
            if (conveyorMfField != null)
            {
                MeshFilter mf = conveyorMfField.GetValue(lc) as MeshFilter;
                if (mf != null) return mf;
            }
        }

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in filters)
        {
            if (mf != null && mf.gameObject != null)
            {
                string gName = mf.gameObject.name.ToLower();
                if (gName.Contains("conveyor") || gName.Contains("blockconveyor") || gName.Contains("spline") || gName.Contains("track"))
                {
                    return mf;
                }
            }
        }
        return null;
    }

    private void ApplyConveyorMeshToPrefab(Mesh newMesh)
    {
        if (newMesh == null || string.IsNullOrEmpty(m_PrefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(m_PrefabPath);
        if (root == null) return;

        MeshFilter conveyorMf = FindConveyorMeshFilter(root);
        if (conveyorMf != null)
        {
            conveyorMf.sharedMesh = newMesh;
            EditorUtility.SetDirty(conveyorMf);
            EditorUtility.SetDirty(conveyorMf.gameObject);

            LevelController lc = root.GetComponent<LevelController>();
            if (lc != null)
            {
                var conveyorMfField = typeof(LevelController).GetField("conveyorMeshFilter", BindingFlags.NonPublic | BindingFlags.Instance);
                if (conveyorMfField != null)
                {
                    conveyorMfField.SetValue(lc, conveyorMf);
                    EditorUtility.SetDirty(lc);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, m_PrefabPath);
            Debug.Log($"[LevelEditorWindow] Đã cập nhật Conveyor Mesh thành '{newMesh.name}' cho Level Prefab: {m_PrefabPath}");
        }
        else
        {
            Debug.LogWarning("[LevelEditorWindow] Không tìm thấy MeshFilter đại diện cho Conveyor trong Level Prefab.");
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    private void DrawCellInspectorTab()
    {
        if (m_SelectedCell == null)
        {
            EditorGUILayout.HelpBox("Chọn một ô bên bảng ma trận để xem cấu trúc và chỉnh sửa thuộc tính.", MessageType.Info);
            return;
        }

        GUILayout.Label($"CHI TIẾT Ô Grid [{m_SelectedCell.row}, {m_SelectedCell.col}]", EditorStyles.boldLabel);
        
        GUILayout.BeginVertical(EditorStyles.helpBox);

        // Cell Type
        GridItemType newType = (GridItemType)EditorGUILayout.EnumPopup("Loại Ô (Cell Type)", m_SelectedCell.type);
        if (newType != m_SelectedCell.type)
        {
            m_SelectedCell.type = newType;
        }

        GUILayout.Space(10);

        // Draw properties depending on type
        if (m_SelectedCell.type == GridItemType.Shooter)
        {
            DrawShooterInspectorProperties(m_SelectedCell);
        }
        else if (m_SelectedCell.type == GridItemType.Tunnel)
        {
            DrawTunnelInspectorProperties(m_SelectedCell);
        }

        GUILayout.EndVertical();

        GUILayout.Space(15);
        GUILayout.Label("THAO TÁC NHANH VỚI Ô", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Swap (Hoán đổi)"))
        {
            m_SwapMode = true;
            m_MoveMode = false;
        }

        if (GUILayout.Button("Move (Di chuyển)"))
        {
            m_MoveMode = true;
            m_SwapMode = false;
        }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Copy Ô"))
        {
            m_CopiedCell = CopyCellData(m_SelectedCell);
        }

        GUI.enabled = (m_CopiedCell != null);
        if (GUILayout.Button("Paste Ô"))
        {
            if (m_CopiedCell != null)
            {
                PasteCellData(m_CopiedCell, m_SelectedCell);
            }
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();
    }

    private void DrawShooterInspectorProperties(CellData cell)
    {
        GUILayout.Label("Cấu Hình Shooter", EditorStyles.boldLabel);
        cell.shooterColor = (SeedColor)EditorGUILayout.EnumPopup("Màu Mục Tiêu (Target Color)", cell.shooterColor);
        cell.shooterMaxBulletCount = EditorGUILayout.IntField("Số Lượng Đạn (Max Bullet)", cell.shooterMaxBulletCount);
        if (cell.shooterMaxBulletCount < 0) cell.shooterMaxBulletCount = 0;
    }

    private void DrawTunnelInspectorProperties(CellData cell)
    {
        GUILayout.Label("Cấu Hình Tunnel", EditorStyles.boldLabel);
        cell.tunnelDir = (Direction)EditorGUILayout.EnumPopup("Hướng Bắn (Target Direction)", cell.tunnelDir);

        GUILayout.Space(10);
        GUILayout.Label("Danh Sách Shooter Con Sắp Sinh (Queue)", EditorStyles.boldLabel);

        // List editor for sub-shooters inside tunnel
        for (int i = 0; i < cell.tunnelShooters.Count; i++)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUILayout.Label($"#{i + 1}", GUILayout.Width(25));
            cell.tunnelShooters[i].color = (SeedColor)EditorGUILayout.EnumPopup(cell.tunnelShooters[i].color, GUILayout.Width(100));
            cell.tunnelShooters[i].maxBulletCount = EditorGUILayout.IntField(cell.tunnelShooters[i].maxBulletCount, GUILayout.Width(80));
            if (cell.tunnelShooters[i].maxBulletCount < 0) cell.tunnelShooters[i].maxBulletCount = 0;

            if (GUILayout.Button("▲", GUILayout.Width(20)))
            {
                if (i > 0)
                {
                    var tmp = cell.tunnelShooters[i];
                    cell.tunnelShooters[i] = cell.tunnelShooters[i - 1];
                    cell.tunnelShooters[i - 1] = tmp;
                }
            }
            if (GUILayout.Button("▼", GUILayout.Width(20)))
            {
                if (i < cell.tunnelShooters.Count - 1)
                {
                    var tmp = cell.tunnelShooters[i];
                    cell.tunnelShooters[i] = cell.tunnelShooters[i + 1];
                    cell.tunnelShooters[i + 1] = tmp;
                }
            }
            if (GUILayout.Button("Xóa", GUILayout.Width(40)))
            {
                cell.tunnelShooters.RemoveAt(i);
                break;
            }

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Thêm Shooter vào Tunnel"))
        {
            cell.tunnelShooters.Add(new TunnelShooterData { color = SeedColor.Red, maxBulletCount = 100 });
        }
    }

    private void DrawConveyorConfigTab()
    {
        GUILayout.Label("BĂNG CHUYỀN", EditorStyles.boldLabel);

        GUILayout.BeginVertical(EditorStyles.helpBox);
        m_CountMainRow = EditorGUILayout.IntField("Số Lượng Ô Hàng Chính", m_CountMainRow);
        if (m_CountMainRow < 0) m_CountMainRow = 0;
        GUILayout.EndVertical();

        // --- Scan Button ---
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.9f, 0.6f, 0.15f);
        if (GUILayout.Button("🔍 Quét Level Thiếu Config Hàng Chính", GUILayout.Height(28)))
        {
            ScanLevelsWithZeroMainRow();
        }
        GUI.backgroundColor = Color.white;
        if (m_ScanZeroMainRowNames != null)
        {
            if (GUILayout.Button("✕ Xóa kết quả", GUILayout.Width(100), GUILayout.Height(28)))
            {
                m_ScanZeroMainRowNames = null;
                m_ScanZeroMainRowPaths = null;
            }
        }
        GUILayout.EndHorizontal();

        // --- Scan Results ---
        if (m_ScanZeroMainRowNames != null)
        {
            GUILayout.Space(4);
            if (m_ScanZeroMainRowNames.Count == 0)
            {
                EditorGUILayout.HelpBox("✅ Tất cả level đã có config Hàng Chính > 0.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"⚠️ {m_ScanZeroMainRowNames.Count} level chưa config Hàng Chính (= 0):", MessageType.Warning);
                m_ScanResultScrollPos = EditorGUILayout.BeginScrollView(m_ScanResultScrollPos,
                    GUILayout.Height(Mathf.Min(m_ScanZeroMainRowNames.Count * 26f + 8f, 160f)));
                for (int i = 0; i < m_ScanZeroMainRowNames.Count; i++)
                {
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label($"• {m_ScanZeroMainRowNames[i]}", GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button("Load", GUILayout.Width(48), GUILayout.Height(20)))
                    {
                        m_PrefabPath = m_ScanZeroMainRowPaths[i];
                        m_PrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(m_PrefabPath);
                        if (m_LevelDB != null && m_LevelDB.listPrefab != null)
                            m_SelectedLevelIndex = m_LevelDB.listPrefab.IndexOf(m_PrefabObj);
                        LoadPrefabData(m_PrefabPath);
                        if (m_PrefabObj != null)
                        {
                            AssetDatabase.OpenAsset(m_PrefabObj);
                            EditorGUIUtility.PingObject(m_PrefabObj);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        GUILayout.Space(6);

        GUILayout.Space(10);
        GUILayout.Label("Các Hàng Phụ", EditorStyles.boldLabel);
        for (int i = 0; i < m_CountSideRows.Count; i++)
        {
            GUILayout.BeginHorizontal();
            m_CountSideRows[i] = EditorGUILayout.IntField($"Hàng Phụ #{i + 1}", m_CountSideRows[i]);
            if (m_CountSideRows[i] < 0) m_CountSideRows[i] = 0;
            
            if (GUILayout.Button("Xóa", GUILayout.Width(50)))
            {
                m_CountSideRows.RemoveAt(i);
                break;
            }
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Thêm Hàng Phụ"))
        {
            m_CountSideRows.Add(0);
        }

        GUILayout.Space(15);

        // Total Block Count Summary
        if (m_ListColor != null && m_ListColor.Count > 0)
        {
            GUILayout.Label("Tổng Số Block Trong Conveyor", EditorStyles.boldLabel);
            GUILayout.BeginVertical(EditorStyles.helpBox);

            int totalSeeds = m_ListColor.Count;
            int totalBlocks = totalSeeds * 50;

            GUIStyle totalStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 6, 6)
            };

            GUI.backgroundColor = new Color(0.15f, 0.35f, 0.55f);
            GUILayout.Box($"Tổng: {totalSeeds} Hạt → {totalBlocks} Block", totalStyle, GUILayout.Height(32), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        GUILayout.Label("Màu Hạt", EditorStyles.boldLabel);

        for (int i = 0; i < m_ListColor.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"#{i + 1}", GUILayout.Width(30));
            m_ListColor[i] = (SeedColor)EditorGUILayout.EnumPopup(m_ListColor[i]);

            if (GUILayout.Button("▲", GUILayout.Width(25)))
            {
                if (i > 0)
                {
                    var tmp = m_ListColor[i];
                    m_ListColor[i] = m_ListColor[i - 1];
                    m_ListColor[i - 1] = tmp;
                }
            }
            if (GUILayout.Button("▼", GUILayout.Width(25)))
            {
                if (i < m_ListColor.Count - 1)
                {
                    var tmp = m_ListColor[i];
                    m_ListColor[i] = m_ListColor[i + 1];
                    m_ListColor[i + 1] = tmp;
                }
            }
            if (GUILayout.Button("Xóa", GUILayout.Width(50)))
            {
                m_ListColor.RemoveAt(i);
                break;
            }
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Thêm Màu Hạt"))
        {
            m_ListColor.Add(SeedColor.Red);
        }
    }



    private void ScanLevelsWithZeroMainRow()
    {
        m_ScanZeroMainRowNames = new List<string>();
        m_ScanZeroMainRowPaths = new List<string>();

        if (m_LevelDB == null || m_LevelDB.listPrefab == null || m_LevelDB.listPrefab.Count == 0)
        {
            EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy LevelDataBase hoặc danh sách prefab trống.", "OK");
            return;
        }

        int total = m_LevelDB.listPrefab.Count;
        try
        {
            for (int i = 0; i < total; i++)
            {
                GameObject prefabGo = m_LevelDB.listPrefab[i];
                if (prefabGo == null) continue;

                string path = AssetDatabase.GetAssetPath(prefabGo);
                if (string.IsNullOrEmpty(path)) continue;

                EditorUtility.DisplayProgressBar("Đang quét...", prefabGo.name, (float)i / total);

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    LevelController lc = root.GetComponent<LevelController>();
                    if (lc == null) continue;

                    var dataField = typeof(LevelController).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataField == null) continue;

                    var splineData = dataField.GetValue(lc) as LevelController.SplineData;
                    int mainRow = splineData != null ? splineData.countMainRow : 0;

                    if (mainRow == 0)
                    {
                        m_ScanZeroMainRowNames.Add(prefabGo.name);
                        m_ScanZeroMainRowPaths.Add(path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void DrawGridSizeAdjustTab()
    {
        GUILayout.Label("ĐIỀU CHỈNH KÍCH THƯỚC LƯỚI (GRID SIZE)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Việc thay đổi kích thước Grid sẽ thêm các ô EmptyCell mới hoặc xóa bỏ các ô nằm ngoài phạm vi mới. Hãy cẩn thận!", MessageType.Warning);
        
        int nextRows = EditorGUILayout.IntField("Số Dòng (Rows)", m_Rows);
        int nextCols = EditorGUILayout.IntField("Số Cột (Cols)", m_Cols);

        if (nextRows < 1) nextRows = 1;
        if (nextCols < 1) nextCols = 1;

        if (GUILayout.Button("Áp Dụng Thay Đổi Kích Thước Grid"))
        {
            ResizeGrid(nextRows, nextCols);
        }
    }

    private void ResizeGrid(int newRows, int newCols)
    {
        CellData[,] newCells = new CellData[newRows, newCols];
        for (int r = 0; r < newRows; r++)
        {
            for (int c = 0; c < newCols; c++)
            {
                if (r < m_Rows && c < m_Cols)
                {
                    newCells[r, c] = m_Cells[r, c];
                }
                else
                {
                    newCells[r, c] = new CellData
                    {
                        row = r,
                        col = c,
                        type = GridItemType.EmptyCell,
                        shooterColor = SeedColor.Red,
                        shooterMaxBulletCount = 100,
                        tunnelDir = Direction.Up,
                        tunnelShooters = new List<TunnelShooterData>()
                    };
                }
            }
        }
        
        m_Rows = newRows;
        m_Cols = newCols;
        m_Cells = newCells;
        m_SelectedCell = null;
    }

    private CellData CopyCellData(CellData src)
    {
        var copy = new CellData
        {
            type = src.type,
            shooterColor = src.shooterColor,
            shooterMaxBulletCount = src.shooterMaxBulletCount,
            tunnelDir = src.tunnelDir,
            tunnelShooters = new List<TunnelShooterData>()
        };
        foreach (var ts in src.tunnelShooters)
        {
            copy.tunnelShooters.Add(new TunnelShooterData { color = ts.color, maxBulletCount = ts.maxBulletCount });
        }
        return copy;
    }

    private void PasteCellData(CellData src, CellData dest)
    {
        dest.type = src.type;
        dest.shooterColor = src.shooterColor;
        dest.shooterMaxBulletCount = src.shooterMaxBulletCount;
        dest.tunnelDir = src.tunnelDir;
        dest.tunnelShooters = new List<TunnelShooterData>();
        foreach (var ts in src.tunnelShooters)
        {
            dest.tunnelShooters.Add(new TunnelShooterData { color = ts.color, maxBulletCount = ts.maxBulletCount });
        }
    }

    private void SwapCells(CellData a, CellData b)
    {
        GridItemType tmpType = a.type;
        SeedColor tmpColor = a.shooterColor;
        int tmpBullets = a.shooterMaxBulletCount;
        Direction tmpDir = a.tunnelDir;
        List<TunnelShooterData> tmpTS = a.tunnelShooters;

        a.type = b.type;
        a.shooterColor = b.shooterColor;
        a.shooterMaxBulletCount = b.shooterMaxBulletCount;
        a.tunnelDir = b.tunnelDir;
        a.tunnelShooters = b.tunnelShooters;

        b.type = tmpType;
        b.shooterColor = tmpColor;
        b.shooterMaxBulletCount = tmpBullets;
        b.tunnelDir = tmpDir;
        b.tunnelShooters = tmpTS;
    }

    private void MoveCell(CellData src, CellData dest)
    {
        dest.type = src.type;
        dest.shooterColor = src.shooterColor;
        dest.shooterMaxBulletCount = src.shooterMaxBulletCount;
        dest.tunnelDir = src.tunnelDir;
        dest.tunnelShooters = src.tunnelShooters;

        // Reset source cell to empty cell
        src.type = GridItemType.EmptyCell;
        src.shooterColor = SeedColor.Red;
        src.shooterMaxBulletCount = 100;
        src.tunnelDir = Direction.Up;
        src.tunnelShooters = new List<TunnelShooterData>();
    }

    private void LoadPrefabData(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không thể load được nội dung của prefab: " + path, "OK");
            return;
        }

        m_PrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        LevelController lc = root.GetComponent<LevelController>();
        if (lc == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy LevelController trong prefab.", "OK");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        GridController gc = root.GetComponentInChildren<GridController>(true);
        if (gc == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy GridController trong prefab.", "OK");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var colField = typeof(GridController).GetField("col", BindingFlags.NonPublic | BindingFlags.Instance);
        var rowField = typeof(GridController).GetField("row", BindingFlags.NonPublic | BindingFlags.Instance);
        m_Cols = colField != null ? (int)colField.GetValue(gc) : 0;
        m_Rows = rowField != null ? (int)rowField.GetValue(gc) : 0;

        m_Cells = new CellData[m_Rows, m_Cols];
        for (int r = 0; r < m_Rows; r++)
        {
            for (int c = 0; c < m_Cols; c++)
            {
                m_Cells[r, c] = new CellData
                {
                    row = r,
                    col = c,
                    type = GridItemType.EmptyCell,
                    shooterColor = SeedColor.Red,
                    shooterMaxBulletCount = 100,
                    tunnelDir = Direction.Up,
                    tunnelShooters = new List<TunnelShooterData>()
                };
            }
        }

        GridItem[] items = gc.GetComponentsInChildren<GridItem>(true);
        foreach (var item in items)
        {
            // Exclude items inside Tunnels from initial grid cells load
            if (IsInsideTunnel(item, gc))
            {
                continue;
            }

            int r = item.GetRow();
            int c = item.GetCol();
            if (r >= 0 && r < m_Rows && c >= 0 && c < m_Cols)
            {
                CellData cell = m_Cells[r, c];
                cell.type = item.GetGridItemType();

                if (cell.type == GridItemType.Shooter)
                {
                    BaseShooter shooter = item.GetComponent<BaseShooter>();
                    if (shooter != null)
                    {
                        var targetColorField = typeof(BaseShooter).GetField("targetColor", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (targetColorField != null) cell.shooterColor = (SeedColor)targetColorField.GetValue(shooter);
                        var maxBulletCountField = typeof(BaseShooter).GetField("maxBulletCount", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (maxBulletCountField != null) cell.shooterMaxBulletCount = (int)maxBulletCountField.GetValue(shooter);
                    }
                }
                else if (cell.type == GridItemType.Tunnel)
                {
                    Tunnel tunnel = item.GetComponent<Tunnel>();
                    if (tunnel != null)
                    {
                        var targetDirField = typeof(Tunnel).GetField("targetDir", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (targetDirField != null) cell.tunnelDir = (Direction)targetDirField.GetValue(tunnel);
                        var shooterListField = typeof(Tunnel).GetField("shooterList", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (shooterListField != null)
                        {
                            var shooterList = shooterListField.GetValue(tunnel) as List<BaseShooter>;
                            if (shooterList != null)
                            {
                                foreach (var s in shooterList)
                                {
                                    if (s != null)
                                    {
                                        var targetColorField = typeof(BaseShooter).GetField("targetColor", BindingFlags.NonPublic | BindingFlags.Instance);
                                        var maxBulletCountField = typeof(BaseShooter).GetField("maxBulletCount", BindingFlags.NonPublic | BindingFlags.Instance);
                                        SeedColor sColor = targetColorField != null ? (SeedColor)targetColorField.GetValue(s) : SeedColor.Red;
                                        int sMaxBullets = maxBulletCountField != null ? (int)maxBulletCountField.GetValue(s) : 100;
                                        cell.tunnelShooters.Add(new TunnelShooterData { color = sColor, maxBulletCount = sMaxBullets });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Load conveyor colors list
        var listColorField = typeof(LevelController).GetField("listColor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (listColorField != null)
        {
            var colors = listColorField.GetValue(lc) as List<SeedColor>;
            m_ListColor = colors != null ? new List<SeedColor>(colors) : new List<SeedColor>();
        }

        // Load spline data structure
        var dataField = typeof(LevelController).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
        if (dataField != null)
        {
            var splineData = dataField.GetValue(lc) as LevelController.SplineData;
            if (splineData != null)
            {
                m_CountMainRow = splineData.countMainRow;
                m_CountSideRows = splineData.countSideRows != null ? new List<int>(splineData.countSideRows) : new List<int>();
            }
        }

        // Load map mesh reference & map mesh list from LevelController without discarding scanned meshes
        if (m_MapMeshList == null || m_MapMeshList.Count == 0)
        {
            LoadAvailableMapMeshes();
        }

        var mapMeshListField = typeof(LevelController).GetField("mapMeshList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mapMeshListField != null)
        {
            var lcList = mapMeshListField.GetValue(lc) as List<Mesh>;
            if (lcList != null)
            {
                foreach (var mesh in lcList)
                {
                    if (mesh != null && !m_MapMeshList.Contains(mesh))
                    {
                        m_MapMeshList.Add(mesh);
                    }
                }
            }
        }

        MeshFilter mapMf = FindMapMeshFilter(root);
        if (mapMf != null)
        {
            m_SelectedMapMesh = mapMf.sharedMesh;
        }
        else
        {
            m_SelectedMapMesh = null;
        }

        MeshFilter conveyorMf = FindConveyorMeshFilter(root);
        if (conveyorMf != null)
        {
            m_ConveyorMeshFilter = conveyorMf;
            m_SelectedConveyorMesh = conveyorMf.sharedMesh;
        }
        else
        {
            m_ConveyorMeshFilter = null;
            m_SelectedConveyorMesh = null;
        }

        if (m_ConveyorMeshList == null || m_ConveyorMeshList.Count == 0)
        {
            LoadAvailableConveyorMeshes();
        }

        var conveyorMeshListField = typeof(LevelController).GetField("conveyorMeshList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (conveyorMeshListField != null)
        {
            var lcConveyorList = conveyorMeshListField.GetValue(lc) as List<Mesh>;
            if (lcConveyorList != null)
            {
                foreach (var mesh in lcConveyorList)
                {
                    if (mesh != null && !m_ConveyorMeshList.Contains(mesh))
                    {
                        m_ConveyorMeshList.Add(mesh);
                    }
                }
            }
        }


        PrefabUtility.UnloadPrefabContents(root);
        m_Loaded = true;
        m_SelectedCell = null;
        
        Debug.Log($"Loaded level prefab successfully: {path} (Size: {m_Rows}x{m_Cols})");
    }

    private bool IsInsideTunnel(GridItem item, GridController gc)
    {
        Transform t = item.transform.parent;
        while (t != null && t != gc.transform)
        {
            if (t.GetComponent<Tunnel>() != null)
            {
                return true;
            }
            t = t.parent;
        }
        return false;
    }

    private void SavePrefabData(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không thể load được nội dung của prefab để lưu: " + path, "OK");
            return;
        }

        LevelController lc = root.GetComponent<LevelController>();
        GridController gc = root.GetComponentInChildren<GridController>(true);

        if (lc == null || gc == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Thiếu LevelController hoặc GridController trong prefab root.", "OK");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // 1. Update GridController row/col fields
        var colField = typeof(GridController).GetField("col", BindingFlags.NonPublic | BindingFlags.Instance);
        var rowField = typeof(GridController).GetField("row", BindingFlags.NonPublic | BindingFlags.Instance);
        if (colField != null) colField.SetValue(gc, m_Cols);
        if (rowField != null) rowField.SetValue(gc, m_Rows);

        // 2. Cache old GameObjects representing the grid cells mapping
        var oldCellsMap = new Dictionary<Vector2Int, GameObject>();
        GridItem[] oldItems = gc.GetComponentsInChildren<GridItem>(true);
        foreach (var item in oldItems)
        {
            if (!IsInsideTunnel(item, gc))
            {
                oldCellsMap[new Vector2Int(item.GetRow(), item.GetCol())] = item.gameObject;
            }
        }

        // Measure grid transform (origin and signed step vectors) to position new cells accurately
        var (gridOrigin, deltaX, deltaZ) = CalculateGridTransform(oldCellsMap);

        // 3. Reconstruct grid cells
        int currentIndex = 0;

        for (int r = 0; r < m_Rows; r++)
        {
            // Find farthest column containing a Shooter in row r
            int maxShooterCol = -1;
            for (int c = m_Cols - 1; c >= 0; c--)
            {
                if (m_Cells[r, c].type == GridItemType.Shooter)
                {
                    maxShooterCol = c;
                    break;
                }
            }

            for (int c = 0; c < m_Cols; c++)
            {
                CellData cell = m_Cells[r, c];
                Vector2Int coord = new Vector2Int(r, c);
                GameObject cellGo = null;

                Vector3 targetPos;
                Quaternion targetRot = Quaternion.identity;

                if (oldCellsMap.TryGetValue(coord, out GameObject existingGo) && existingGo != null)
                {
                    targetPos = existingGo.transform.localPosition;
                    targetRot = existingGo.transform.localRotation;

                    GridItem item = existingGo.GetComponent<GridItem>();
                    if (item != null && item.GetGridItemType() == cell.type)
                    {
                        // Match types: modify properties in place
                        cellGo = existingGo;
                        UpdateCellProperties(cellGo, cell, root);
                    }
                    else
                    {
                        // Mismatched types: destroy and instantiate from template
                        DestroyImmediate(existingGo);
                        cellGo = CreateCellFromTemplate(gc.transform, cell, root, targetPos, targetRot);
                    }
                }
                else
                {
                    // Out-of-bounds / new cell: instantiate from template
                    targetPos = gridOrigin + c * deltaX + r * deltaZ;
                    cellGo = CreateCellFromTemplate(gc.transform, cell, root, targetPos, targetRot);
                }

                if (cellGo != null)
                {
                    if (cell.type == GridItemType.Shooter)
                    {
                        string colorStr = cell.shooterColor.ToString().ToUpper();
                        cellGo.name = $"Shooter_{currentIndex}_{colorStr}";
                    }
                    else
                    {
                        cellGo.name = $"{cell.type}_{r}_{c}";
                    }

                    GridItem item = cellGo.GetComponent<GridItem>();
                    if (item != null)
                    {
                        item.SetGridCoordinate(r, c);
                        item.SetGridItemType(cell.type);
                        EditorUtility.SetDirty(item);
                    }
                }

                if (c <= maxShooterCol)
                {
                    currentIndex++;
                }
            }
        }

        // 4. Destroy remaining out-of-bounds GameObjects (if the grid was resized smaller)
        foreach (var kvp in oldCellsMap)
        {
            Vector2Int coord = kvp.Key;
            if (coord.x >= m_Rows || coord.y >= m_Cols)
            {
                if (kvp.Value != null)
                {
                    DestroyImmediate(kvp.Value);
                }
            }
        }

        // 5. Update GridController.nodes array list
        List<GridItem> sortedGridItems = gc.GetComponentsInChildren<GridItem>(true)
            .Where(item => item != null && !IsInsideTunnel(item, gc))
            .OrderBy(item => item.GetRow())
            .ThenBy(item => item.GetCol())
            .ToList();

        var nodesField = typeof(GridController).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        if (nodesField != null)
        {
            nodesField.SetValue(gc, sortedGridItems);
        }

        // Order GameObjects in Hierarchy sequentially for neatness
        for (int i = 0; i < sortedGridItems.Count; i++)
        {
            sortedGridItems[i].transform.SetSiblingIndex(i);
        }

        // 6. Update conveyor belt / spline configurations
        var listColorField = typeof(LevelController).GetField("listColor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (listColorField != null)
        {
            listColorField.SetValue(lc, new List<SeedColor>(m_ListColor));
        }

        var splineDataField = typeof(LevelController).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
        if (splineDataField != null)
        {
            var sData = new LevelController.SplineData
            {
                countMainRow = m_CountMainRow,
                countSideRows = new List<int>(m_CountSideRows)
            };
            splineDataField.SetValue(lc, sData);
        }


        // 7. Update LevelController's tunnelList field
        var tunnelListField = typeof(LevelController).GetField("tunnelList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (tunnelListField != null)
        {
            var tunnels = gc.GetComponentsInChildren<Tunnel>(true).ToList();
            tunnelListField.SetValue(lc, tunnels);
        }

        // 8. Update Map Mesh & mapMeshList on LevelController
        var mapMeshFilterField = typeof(LevelController).GetField("mapMeshFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        var mapMeshListField = typeof(LevelController).GetField("mapMeshList", BindingFlags.NonPublic | BindingFlags.Instance);

        MeshFilter mapMf = FindMapMeshFilter(root);
        if (mapMf != null)
        {
            if (mapMeshFilterField != null) mapMeshFilterField.SetValue(lc, mapMf);

            if (m_SelectedMapMesh != null && mapMf.sharedMesh != m_SelectedMapMesh)
            {
                mapMf.sharedMesh = m_SelectedMapMesh;
                mapMf.gameObject.name = m_SelectedMapMesh.name;
                EditorUtility.SetDirty(mapMf);
                EditorUtility.SetDirty(mapMf.gameObject);
            }
        }

        if (mapMeshListField != null && m_MapMeshList != null)
        {
            mapMeshListField.SetValue(lc, new List<Mesh>(m_MapMeshList));
        }

        // 9. Update Conveyor Mesh Filter & conveyorMeshList on LevelController
        var conveyorMeshFilterField = typeof(LevelController).GetField("conveyorMeshFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        var conveyorMeshListField = typeof(LevelController).GetField("conveyorMeshList", BindingFlags.NonPublic | BindingFlags.Instance);

        MeshFilter conveyorMf = FindConveyorMeshFilter(root);
        if (conveyorMf != null)
        {
            if (conveyorMeshFilterField != null) conveyorMeshFilterField.SetValue(lc, conveyorMf);

            if (m_SelectedConveyorMesh != null && conveyorMf.sharedMesh != m_SelectedConveyorMesh)
            {
                conveyorMf.sharedMesh = m_SelectedConveyorMesh;
                EditorUtility.SetDirty(conveyorMf);
                EditorUtility.SetDirty(conveyorMf.gameObject);
            }
        }

        if (conveyorMeshListField != null && m_ConveyorMeshList != null)
        {
            conveyorMeshListField.SetValue(lc, new List<Mesh>(m_ConveyorMeshList));
        }

        // Run validation methods
        var gcValidate = typeof(GridController).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (gcValidate != null) gcValidate.Invoke(gc, null);

        var lcValidate = typeof(LevelController).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (lcValidate != null) lcValidate.Invoke(lc, null);

        // Mark dirty and serialize prefab back to file system
        EditorUtility.SetDirty(gc);
        EditorUtility.SetDirty(lc);
        EditorUtility.SetDirty(root);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Reload fresh data to synchronize window layout
        LoadPrefabData(path);

        EditorUtility.DisplayDialog("Thành công", "Lưu Level Prefab thành công!", "OK");
    }

    private (Vector3 origin, Vector3 deltaX, Vector3 deltaZ) CalculateGridTransform(Dictionary<Vector2Int, GameObject> map)
    {
        Vector3 deltaX = new Vector3(1.1f, 0f, 0f);
        Vector3 deltaZ = new Vector3(0f, 0f, 1.1f);
        Vector3 origin = Vector3.zero;

        if (map != null && map.Count > 0)
        {
            // Find column step (same row, c2 = c1 + 1)
            foreach (var kvp in map)
            {
                Vector2Int c1 = kvp.Key;
                Vector2Int c2 = new Vector2Int(c1.x, c1.y + 1);
                if (map.TryGetValue(c2, out GameObject go2) && kvp.Value != null && go2 != null)
                {
                    deltaX = go2.transform.localPosition - kvp.Value.transform.localPosition;
                    break;
                }
            }

            // Find row step (same col, r2 = r1 + 1)
            foreach (var kvp in map)
            {
                Vector2Int c1 = kvp.Key;
                Vector2Int c2 = new Vector2Int(c1.x + 1, c1.y);
                if (map.TryGetValue(c2, out GameObject go2) && kvp.Value != null && go2 != null)
                {
                    deltaZ = go2.transform.localPosition - kvp.Value.transform.localPosition;
                    break;
                }
            }

            // Deduce origin (row 0, col 0) from the first non-null cell
            foreach (var kvp in map)
            {
                if (kvp.Value != null)
                {
                    origin = kvp.Value.transform.localPosition - (kvp.Key.y * deltaX + kvp.Key.x * deltaZ);
                    break;
                }
            }
        }

        return (origin, deltaX, deltaZ);
    }

    private GameObject CreateCellFromTemplate(Transform parent, CellData cell, GameObject root, Vector3 targetPos, Quaternion targetRot)
    {
        // 1. Look for template object to duplicate
        GameObject template = GetTemplateForType(root, cell.type);
        GameObject newCellGo = null;

        if (template != null)
        {
            newCellGo = Instantiate(template, parent);
        }
        else
        {
            // Create raw GameObject if no templates found anywhere
            newCellGo = new GameObject(cell.type.ToString());
            newCellGo.transform.SetParent(parent);
            newCellGo.AddComponent<GridItem>();
            if (cell.type == GridItemType.Shooter) newCellGo.AddComponent<BaseShooter>();
            else if (cell.type == GridItemType.Tunnel) newCellGo.AddComponent<Tunnel>();
        }

        // Reposition cell
        newCellGo.transform.localPosition = targetPos;
        newCellGo.transform.localRotation = targetRot;
        newCellGo.transform.localScale = Vector3.one;

        // Apply specific properties
        UpdateCellProperties(newCellGo, cell, root);

        return newCellGo;
    }

    private static Material GetMaterialForSeedColor(SeedColor seedColor)
    {
        string matName = seedColor == SeedColor.Hidden ? "M_BlindShooter" : $"M_{seedColor}";
        string matPath = $"Assets/Material/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null && seedColor == SeedColor.Hidden)
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_Gray.mat");
        }
        return mat;
    }

    private static void ApplyShooterColorAndMaterial(BaseShooter shooter, SeedColor color, int maxBulletCount)
    {
        if (shooter == null) return;

        var targetColorField = typeof(BaseShooter).GetField("targetColor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (targetColorField != null) targetColorField.SetValue(shooter, color);

        var maxBulletCountField = typeof(BaseShooter).GetField("maxBulletCount", BindingFlags.NonPublic | BindingFlags.Instance);
        if (maxBulletCountField != null) maxBulletCountField.SetValue(shooter, maxBulletCount);

        var meshField = typeof(BaseShooter).GetField("mesh", BindingFlags.NonPublic | BindingFlags.Instance);
        SkinnedMeshRenderer mesh = meshField?.GetValue(shooter) as SkinnedMeshRenderer;
        if (mesh == null)
        {
            mesh = shooter.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        if (mesh != null)
        {
            Material targetMat = GetMaterialForSeedColor(color);
            if (targetMat != null && mesh.sharedMaterial != targetMat)
            {
                mesh.sharedMaterial = targetMat;
                EditorUtility.SetDirty(mesh);
            }
        }

        EditorUtility.SetDirty(shooter);
    }

    private void UpdateCellProperties(GameObject go, CellData cell, GameObject root)
    {
        if (cell.type == GridItemType.Shooter)
        {
            BaseShooter shooter = go.GetComponent<BaseShooter>();
            if (shooter != null)
            {
                ApplyShooterColorAndMaterial(shooter, cell.shooterColor, cell.shooterMaxBulletCount);
            }
        }
        else if (cell.type == GridItemType.Tunnel)
        {
            Tunnel tunnel = go.GetComponent<Tunnel>();
            if (tunnel != null)
            {
                var targetDirField = typeof(Tunnel).GetField("targetDir", BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetDirField != null) targetDirField.SetValue(tunnel, cell.tunnelDir);

                // Recreate child shooters in Tunnel
                // First destroy all existing child shooters
                var childShooters = tunnel.GetComponentsInChildren<BaseShooter>(true);
                foreach (var s in childShooters)
                {
                    if (s != null && s.transform.parent == tunnel.transform)
                    {
                        DestroyImmediate(s.gameObject);
                    }
                }

                // Get shooter template
                GameObject shooterTemplate = GetTemplateForType(root, GridItemType.Shooter);
                var gridItemTypeField = typeof(GridItem).GetField("type", BindingFlags.NonPublic | BindingFlags.Instance);

                List<BaseShooter> newShooters = new List<BaseShooter>();

                for (int i = 0; i < cell.tunnelShooters.Count; i++)
                {
                    var sData = cell.tunnelShooters[i];
                    GameObject subGo = null;

                    if (shooterTemplate != null)
                    {
                        subGo = Instantiate(shooterTemplate, tunnel.transform);
                    }
                    else
                    {
                        subGo = new GameObject($"Tunnel_Shooter_{i}");
                        subGo.transform.SetParent(tunnel.transform);
                        subGo.AddComponent<GridItem>();
                        subGo.AddComponent<BaseShooter>();
                    }

                    subGo.name = $"Tunnel_Shooter_{i}";
                    subGo.transform.localPosition = Vector3.zero;
                    subGo.transform.localRotation = Quaternion.identity;
                    subGo.transform.localScale = Vector3.one;
                    subGo.SetActive(false); // Hide inside Tunnel initially

                    BaseShooter subShooter = subGo.GetComponent<BaseShooter>();
                    GridItem subGridItem = subGo.GetComponent<GridItem>();

                    if (subShooter != null)
                    {
                        ApplyShooterColorAndMaterial(subShooter, sData.color, sData.maxBulletCount);
                        newShooters.Add(subShooter);
                    }

                    if (subGridItem != null)
                    {
                        if (gridItemTypeField != null) gridItemTypeField.SetValue(subGridItem, GridItemType.Shooter);
                        subGridItem.SetGridItemType(GridItemType.Shooter);
                        EditorUtility.SetDirty(subGridItem);
                    }
                }

                var shooterListField = typeof(Tunnel).GetField("shooterList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (shooterListField != null)
                {
                    shooterListField.SetValue(tunnel, newShooters);
                }

                EditorUtility.SetDirty(tunnel);
            }
        }
    }

    private GameObject GetTemplateForType(GameObject root, GridItemType type)
    {
        // 1. Try finding existing cell of the same type in prefab
        GridItem[] items = root.GetComponentsInChildren<GridItem>(true);
        foreach (var item in items)
        {
            if (item != null && item.GetGridItemType() == type && !IsInsideTunnel(item, root.GetComponentInChildren<GridController>(true)))
            {
                return item.gameObject;
            }
        }

        // 2. Try loading fallbacks from assets
        string fallbackPath = "";
        switch (type)
        {
            case GridItemType.EmptyCell:
                fallbackPath = "Assets/_Prefab/Item/GridItem.prefab";
                break;
            case GridItemType.Wall:
                fallbackPath = "Assets/GameObject/Wall.prefab";
                if (!File.Exists(fallbackPath)) fallbackPath = "Assets/GameObject/Wall_0.prefab";
                break;
            case GridItemType.Tunnel:
                fallbackPath = "Assets/GameObject/Tunel.prefab";
                if (!File.Exists(fallbackPath)) fallbackPath = "Assets/GameObject/Tunel_0.prefab";
                break;
            case GridItemType.Shooter:
                fallbackPath = "Assets/GameObject/Shooter_ 11_YELLOW.prefab";
                if (!File.Exists(fallbackPath)) fallbackPath = "Assets/GameObject/Shooter_ 11_YELLOW_0.prefab";
                break;
        }

        if (!string.IsNullOrEmpty(fallbackPath) && File.Exists(fallbackPath))
        {
            GameObject fallback = AssetDatabase.LoadAssetAtPath<GameObject>(fallbackPath);
            if (fallback != null) return fallback;
        }

        // 3. Fallback: Search the database for any matching component
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string pPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
            if (p == null) continue;

            GridItem gi = p.GetComponent<GridItem>();
            if (gi != null && gi.GetGridItemType() == type)
            {
                return p;
            }
        }

        return null;
    }
}
#endif
