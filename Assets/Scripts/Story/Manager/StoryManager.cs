using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý trung tâm cho toàn bộ các trang Comic Story trong game:
/// - Quản lý danh sách các StoryUI theo từng StoryType (Intro, Blocker mới, Booster mới...).
/// - Cung cấp API gọi phát Story theo StoryType.
/// - Hỗ trợ tự động lưu trạng thái đã xem (PlayOnlyOnce).
/// </summary>
public class StoryManager : MonoBehaviour
{
    private const string STORY_PREFS_PREFIX = "Story_Completed_";

    public static StoryManager Instance { get; private set; }

    [Header("1. Story Registry (Danh sách các trang truyện)")]
    [Tooltip("Danh sách tất cả các trang StoryUI (mỗi StoryUI mang một StoryType riêng)")]
    [SerializeField] private List<StoryUI> storyList = new List<StoryUI>();

    [Header("2. UI Hierarchy Container")]
    [Tooltip("Transform parent để chứa các StoryUI (nếu để trống sẽ tự tìm UIManager hoặc Canvas)")]
    [SerializeField] private Transform storyUIContainer;

    [Header("3. Settings")]
    [Tooltip("Chỉ bật nếu StoryManager được đặt trong Bootstrap Scene riêng biệt")]
    [SerializeField] private bool dontDestroyOnLoad = false;

    [Header("4. Debug & Quick Test")]
    [Tooltip("Chọn StoryType để test nhanh")]
    [SerializeField] private StoryType debugStoryTypeToTest = StoryType.Intro;

    [Tooltip("Phím tắt để test nhanh trong Play Mode (mặc định F8)")]
    [SerializeField] private KeyCode testHotkey = KeyCode.F8;

    // Runtime state
    private Dictionary<StoryType, StoryUI> storyMap = new Dictionary<StoryType, StoryUI>();
    private StoryUI currentPlayingStoryUI;

    // Events
    public event Action<StoryType> OnStoryStarted;
    public event Action<StoryType, int> OnPanelShown;
    public event Action<StoryType> OnStoryCompleted;

    public bool IsPlayingStory => currentPlayingStoryUI != null && currentPlayingStoryUI.IsPlaying;
    public StoryUI CurrentActiveStoryUI => currentPlayingStoryUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        BuildStoryMap();
    }

    private void Start()
    {
        // Ẩn tất cả StoryUI trong scene khi khởi động
        HideAllStories();
    }

    /// <summary>
    /// Xây dựng bảng tra cứu StoryType -> StoryUI
    /// </summary>
    private void BuildStoryMap()
    {
        storyMap.Clear();

        if (storyList == null || storyList.Count == 0)
        {
            AutoFindAllStoryUI();
        }

        foreach (StoryUI storyUI in storyList)
        {
            if (storyUI == null) continue;

            StoryType type = storyUI.StoryType;
            if (!storyMap.ContainsKey(type))
            {
                storyMap.Add(type, storyUI);
            }
            else
            {
                Debug.LogWarning($"[StoryManager] Phát hiện trùng lặp StoryType '{type}' trên GameObject '{storyUI.gameObject.name}'. Story đầu tiên sẽ được ưu tiên sử dụng.");
            }
        }
    }

    /// <summary>
    /// Lấy hoặc khởi tạo instance StoryUI theo StoryType
    /// </summary>
    public StoryUI GetStoryUI(StoryType type)
    {
        if (storyMap.TryGetValue(type, out StoryUI ui) && ui != null)
        {
            // Kiểm tra nếu là Prefab (chưa có trong scene) -> Instantiate vào Scene
            if (!ui.gameObject.scene.IsValid())
            {
                Transform parent = GetOrCreateContainer();
                StoryUI spawnedUI = Instantiate(ui, parent);
                spawnedUI.gameObject.name = ui.gameObject.name;
                spawnedUI.gameObject.SetActive(false);
                storyMap[type] = spawnedUI;
                return spawnedUI;
            }
            return ui;
        }

        // Thử tìm lại trong scene nếu chưa có trong map
        StoryUI[] allInScene = FindObjectsOfType<StoryUI>(true);
        foreach (StoryUI item in allInScene)
        {
            if (item != null && item.StoryType == type)
            {
                storyMap[type] = item;
                if (!storyList.Contains(item))
                {
                    storyList.Add(item);
                }
                return item;
            }
        }

        return null;
    }

    private Transform GetOrCreateContainer()
    {
        if (storyUIContainer != null) return storyUIContainer;

        if (UIManager.Instance != null)
        {
            if (UIManager.Instance.PopupContainer != null)
            {
                storyUIContainer = UIManager.Instance.PopupContainer;
                return storyUIContainer;
            }
            if (UIManager.Instance.UIContainer != null)
            {
                storyUIContainer = UIManager.Instance.UIContainer;
                return storyUIContainer;
            }
            storyUIContainer = UIManager.Instance.transform;
            return storyUIContainer;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            storyUIContainer = canvas.transform;
            return storyUIContainer;
        }

        return transform;
    }

    // ==========================================
    // PUBLIC APIS - PLAY STORY
    // ==========================================

    /// <summary>
    /// Bắt đầu phát trang truyện theo StoryType (Intro, Blocker mới, Booster mới...)
    /// </summary>
    public void PlayStory(StoryType type, Action onComplete = null)
    {
        StoryUI targetStoryUI = GetStoryUI(type);

        if (targetStoryUI == null)
        {
            Debug.LogWarning($"[StoryManager] Không tìm thấy StoryUI nào được gán cho StoryType: {type}");
            onComplete?.Invoke();
            return;
        }

        // Nếu có một Story khác đang phát -> Dừng lại
        if (currentPlayingStoryUI != null && currentPlayingStoryUI.IsPlaying && currentPlayingStoryUI != targetStoryUI)
        {
            currentPlayingStoryUI.FinishStory();
        }

        currentPlayingStoryUI = targetStoryUI;

        // Lắng nghe sự kiện
        UnbindStoryUIEvents(currentPlayingStoryUI);
        BindStoryUIEvents(currentPlayingStoryUI, type);

        // Phát Story
        currentPlayingStoryUI.ShowStory(() =>
        {
            // Tự động đánh dấu hoàn tất nếu bật PlayOnlyOnce
            if (targetStoryUI.PlayOnlyOnce)
            {
                MarkStoryCompleted(type);
            }

            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Phát Story chỉ khi người chơi chưa từng xem Story này trước đây (lưu trong PlayerPrefs)
    /// Trả về true nếu bắt đầu phát, false nếu đã xem rồi hoặc chưa có StoryUI.
    /// </summary>
    public bool PlayStoryIfFirstTime(StoryType type, Action onComplete = null)
    {
        if (IsStoryCompleted(type))
        {
            onComplete?.Invoke();
            return false;
        }

        StoryUI targetStoryUI = GetStoryUI(type);
        if (targetStoryUI == null)
        {
            onComplete?.Invoke();
            return false;
        }

        PlayStory(type, () =>
        {
            MarkStoryCompleted(type);
            onComplete?.Invoke();
        });
        return true;
    }

    /// <summary>
    /// Phát Story mặc định (StoryType.Intro hoặc Story đầu tiên trong danh sách)
    /// </summary>
    public void PlayStory(Action onComplete = null)
    {
        if (storyList != null && storyList.Count > 0 && storyList[0] != null)
        {
            PlayStory(storyList[0].StoryType, onComplete);
        }
        else
        {
            PlayStory(StoryType.Intro, onComplete);
        }
    }

    private void BindStoryUIEvents(StoryUI ui, StoryType type)
    {
        if (ui == null) return;

        ui.OnStoryStarted += () => OnStoryStarted?.Invoke(type);
        ui.OnPanelShown += (index) => OnPanelShown?.Invoke(type, index);
        ui.OnStoryFinished += () => OnStoryCompleted?.Invoke(type);
    }

    private void UnbindStoryUIEvents(StoryUI ui)
    {
        // UI tự quản lý callback nội bộ
    }

    /// <summary>
    /// Ẩn tất cả các trang StoryUI đang có
    /// </summary>
    public void HideAllStories()
    {
        if (storyList == null) return;

        foreach (StoryUI ui in storyList)
        {
            if (ui != null && ui.gameObject.scene.IsValid())
            {
                ui.gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // CONTROLS & UTILITIES
    // ==========================================

    /// <summary>
    /// Chuyển sang ô truyện tiếp theo của Story đang phát
    /// </summary>
    public void NextPanel()
    {
        if (currentPlayingStoryUI != null && currentPlayingStoryUI.IsPlaying)
        {
            currentPlayingStoryUI.OnNextClicked();
        }
    }

    /// <summary>
    /// Bỏ qua story đang phát (hiện ngay toàn bộ các ô)
    /// </summary>
    public void SkipCurrentStory()
    {
        if (currentPlayingStoryUI != null && currentPlayingStoryUI.IsPlaying)
        {
            currentPlayingStoryUI.OnSkipClicked();
        }
    }

    /// <summary>
    /// Dừng khẩn cấp story đang phát
    /// </summary>
    public void StopStory(bool triggerCallback = false)
    {
        if (currentPlayingStoryUI != null && currentPlayingStoryUI.IsPlaying)
        {
            if (triggerCallback)
            {
                currentPlayingStoryUI.FinishStory();
            }
            else
            {
                currentPlayingStoryUI.gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // PROGRESS / PLAYERPREFS PERSISTENCE
    // ==========================================

    public bool IsStoryCompleted(StoryType type)
    {
        return PlayerPrefs.GetInt(STORY_PREFS_PREFIX + type.ToString(), 0) == 1;
    }

    public void MarkStoryCompleted(StoryType type)
    {
        PlayerPrefs.SetInt(STORY_PREFS_PREFIX + type.ToString(), 1);
        PlayerPrefs.Save();
    }

    public void ResetStoryProgress(StoryType type)
    {
        PlayerPrefs.DeleteKey(STORY_PREFS_PREFIX + type.ToString());
        PlayerPrefs.Save();
        Debug.Log($"[StoryManager] Đã reset tiến trình cho Story: {type}");
    }

    [ContextMenu("🔄 Reset All Story Progress")]
    public void ResetAllStoriesProgress()
    {
        foreach (StoryType type in Enum.GetValues(typeof(StoryType)))
        {
            PlayerPrefs.DeleteKey(STORY_PREFS_PREFIX + type.ToString());
        }
        PlayerPrefs.Save();
        Debug.Log("[StoryManager] Đã reset tiến trình của toàn bộ các Story trong game!");
    }

    // ==========================================
    // EDITOR HELPERS & CONTEXT MENUS
    // ==========================================

    /// <summary>
    /// Tự động tìm tất cả các StoryUI trong Scene và thêm vào storyList
    /// </summary>
    [ContextMenu("🔍 Auto Find All StoryUI In Scene")]
    public void AutoFindAllStoryUI()
    {
        storyList.Clear();
        StoryUI[] allFound = FindObjectsOfType<StoryUI>(true);

        foreach (StoryUI ui in allFound)
        {
            if (!storyList.Contains(ui))
            {
                storyList.Add(ui);
            }
        }

        Debug.Log($"[StoryManager] Đã tự động tìm thấy và thêm {storyList.Count} StoryUI vào danh sách!");
    }

    [ContextMenu("▶️ Test Play Selected StoryType")]
    public void TestPlaySelectedStory()
    {
        Debug.Log($"[StoryManager] Đang test phát Story: {debugStoryTypeToTest}...");
        PlayStory(debugStoryTypeToTest, () =>
        {
            Debug.Log($"[StoryManager] Đã hoàn thành Story: {debugStoryTypeToTest}!");
        });
    }

    private void Update()
    {
        if (testHotkey != KeyCode.None && Input.GetKeyDown(testHotkey))
        {
            TestPlaySelectedStory();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
