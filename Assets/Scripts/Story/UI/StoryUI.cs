using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Cấu hình cho từng ô truyện riêng biệt (bao gồm Image và hiệu ứng riêng của từng ô)
/// </summary>
[System.Serializable]
public class StoryPanelItem
{
    [Tooltip("Image ô truyện đã gắn sẵn Sprite trên Canvas")]
    public Image image;

    [Tooltip("Hiệu ứng xuất hiện riêng cho ô này")]
    public PanelAnimationEffect effect = PanelAnimationEffect.PopScale;

    [Tooltip("Thời gian chạy hiệu ứng riêng (nếu <= 0 sẽ dùng thời gian mặc định chung)")]
    public float customDuration = 0f;

    [Tooltip("Âm thanh SFX riêng cho ô này (để trống sẽ dùng âm thanh chung)")]
    public string customSfxKey = "";
}

/// <summary>
/// Quản lý hiển thị trang truyện tranh:
/// - Kéo trực tiếp các Image ô truyện vào danh sách, mỗi ô có thể chọn hiệu ứng xuất hiện riêng.
/// - Không tự động chuyển ô: Bắt buộc bấm nút Next (hoặc chạm màn hình) để mở từng ô tiếp theo.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class StoryUI : MonoBehaviour
{
    [Header("0. Story Configuration")]
    [Tooltip("Loại truyện (Intro, Blocker, Booster...)")]
    [SerializeField] private StoryType storyType = StoryType.Intro;

    [Tooltip("Chỉ cho phép phát 1 lần duy nhất trong toàn bộ game (lưu trong PlayerPrefs)")]
    [SerializeField] private bool playOnlyOnce = false;

    [Header("1. Main UI")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("2. Comic Panels (Cấu hình từng ô truyện & hiệu ứng riêng)")]
    [Tooltip("Danh sách các ô truyện theo thứ tự xuất hiện (kèm hiệu ứng riêng của từng ô)")]
    [SerializeField] private List<StoryPanelItem> panelImages = new List<StoryPanelItem>();

    [Header("3. General Effect Settings")]
    [Tooltip("Thời gian chạy hiệu ứng mặc định của mỗi ô truyện (giây)")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float effectDuration = 0.45f;

    [Tooltip("Độ trượt (pixel) áp dụng cho các hiệu ứng Slide")]
    [SerializeField] private float slideDistance = 120f;

    [Header("4. Controls")]
    [Tooltip("Nút Next để bấm mở ô tiếp theo")]
    [SerializeField] private Button nextButton;

    [Tooltip("Nút Skip để hiển thị ngay toàn bộ các ô")]
    [SerializeField] private Button skipButton;

    [Tooltip("Nút vô hình phủ toàn màn hình (nếu muốn chạm vào đâu cũng Next được)")]
    [SerializeField] private Button fullScreenTapButton;

    [Tooltip("Icon bàn tay/mũi tên nhấp nháy báo hiệu người chơi bấm Next")]
    [SerializeField] private GameObject nextHintObject;

    [Header("5. Audio (Tùy chọn)")]
    [Tooltip("Key SFX khi người chơi tap/chạm mở ô truyện hoặc bấm Skip (mặc định 'TapHintStory')")]
    [SerializeField] private string tapSfxKey = Const.tapHintStorySFX;

    [Tooltip("Key SFX chung trong AudioManager khi một ô truyện xuất hiện")]
    [SerializeField] private string panelShowSfxKey = "";

    [Tooltip("Key BGM phát khi mở trang truyện")]
    [SerializeField] private string storyBgmKey = "";

    // Runtime Cached Transforms & CanvasGroups
    private List<CanvasGroup> panelCanvasGroups = new List<CanvasGroup>();
    private List<Vector2> originalAnchoredPositions = new List<Vector2>();
    private List<Vector3> originalScales = new List<Vector3>();
    private List<Vector3> originalEulerAngles = new List<Vector3>();

    // Runtime state
    private Action onStoryCompleteCallback;
    private int currentPanelIndex = -1;
    private bool isPlaying = false;
    private bool isCurrentPanelAnimating = false;
    private Tween currentAnimTween;
    private Tween currentFadeTween;
    private Tween hintBounceTween;

    public bool IsPlaying => isPlaying;
    public int CurrentPanelIndex => currentPanelIndex;
    public int TotalPanels => panelImages != null ? panelImages.Count : 0;
    public StoryType StoryType => storyType;
    public bool PlayOnlyOnce => playOnlyOnce;

    public void SetStoryType(StoryType type) => storyType = type;

    // Events
    public event Action OnStoryStarted;
    public event Action<int> OnPanelShown;
    public event Action OnStoryFinished;

    private void Awake()
    {
        EnsureComponents();
        SetupButtons();
        CacheOriginalTransforms();
    }

    private void EnsureComponents()
    {
        if (mainCanvasGroup == null)
        {
            mainCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (panelImages == null || panelImages.Count == 0)
        {
            AutoFindPanels();
        }

        CacheCanvasGroups();
    }

    private void CacheCanvasGroups()
    {
        panelCanvasGroups.Clear();
        if (panelImages == null) return;

        for (int i = 0; i < panelImages.Count; i++)
        {
            if (panelImages[i] == null || panelImages[i].image == null) continue;

            CanvasGroup cg = panelImages[i].image.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = panelImages[i].image.gameObject.AddComponent<CanvasGroup>();
            }
            panelCanvasGroups.Add(cg);
        }
    }

    private void CacheOriginalTransforms()
    {
        originalAnchoredPositions.Clear();
        originalScales.Clear();
        originalEulerAngles.Clear();

        if (panelImages == null) return;

        for (int i = 0; i < panelImages.Count; i++)
        {
            if (panelImages[i] == null || panelImages[i].image == null) continue;

            RectTransform rect = panelImages[i].image.rectTransform;
            originalAnchoredPositions.Add(rect.anchoredPosition);
            originalScales.Add(rect.localScale);
            originalEulerAngles.Add(rect.localEulerAngles);
        }
    }

    private void SetupButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextClicked);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkipClicked);
        }

        if (fullScreenTapButton != null)
        {
            fullScreenTapButton.onClick.RemoveAllListeners();
            fullScreenTapButton.onClick.AddListener(OnNextClicked);
        }
    }

    /// <summary>
    /// Bắt đầu hiển thị trang truyện
    /// </summary>
    public void ShowStory(Action onComplete = null)
    {
        EnsureComponents();
        if (originalAnchoredPositions.Count != panelImages.Count)
        {
            CacheOriginalTransforms();
        }

        onStoryCompleteCallback = onComplete;
        isPlaying = true;
        currentPanelIndex = -1;
        isCurrentPanelAnimating = false;

        // Kích hoạt GameObject và hierarchy cha
        gameObject.SetActive(true);
        Transform parentTransform = transform.parent;
        while (parentTransform != null)
        {
            if (!parentTransform.gameObject.activeSelf)
            {
                parentTransform.gameObject.SetActive(true);
            }
            parentTransform = parentTransform.parent;
        }

        // Bật BGM nếu có
        PlayBGM();

        // Ẩn tất cả các ô truyện ban đầu
        HideAllPanelsImmediate();

        // Fade in Canvas chính
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.DOKill();
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;
            mainCanvasGroup.DOFade(1f, 0.25f).SetUpdate(true).OnComplete(() =>
            {
                OnStoryStarted?.Invoke();
                // Mở ngay ô đầu tiên (#1)
                ShowNextPanel();
            });
        }
        else
        {
            OnStoryStarted?.Invoke();
            ShowNextPanel();
        }
    }

    /// <summary>
    /// Ẩn toàn bộ các ô ngay lập tức về trạng thái chuẩn bị xuất hiện
    /// </summary>
    private void HideAllPanelsImmediate()
    {
        for (int i = 0; i < panelImages.Count; i++)
        {
            if (panelImages[i] == null || panelImages[i].image == null) continue;

            panelImages[i].image.gameObject.SetActive(true);

            if (i < panelCanvasGroups.Count && panelCanvasGroups[i] != null)
            {
                panelCanvasGroups[i].DOKill();
                panelCanvasGroups[i].alpha = 0f;
            }

            ResetPanelTransformToOriginal(i);
        }

        SetNextHintVisible(false);
    }

    private void ResetPanelTransformToOriginal(int index)
    {
        if (index < 0 || index >= panelImages.Count || panelImages[index] == null || panelImages[index].image == null) return;

        RectTransform rect = panelImages[index].image.rectTransform;
        rect.DOKill();

        if (index < originalAnchoredPositions.Count)
            rect.anchoredPosition = originalAnchoredPositions[index];

        if (index < originalScales.Count)
            rect.localScale = originalScales[index];

        if (index < originalEulerAngles.Count)
            rect.localEulerAngles = originalEulerAngles[index];
    }

    /// <summary>
    /// Xử lý khi người chơi bấm nút Next hoặc chạm màn hình
    /// </summary>
    public void OnNextClicked()
    {
        if (!isPlaying) return;

        // Phát âm thanh tap/click giống nút bấm button
        PlayTapSFX();

        // 1. Nếu ô hiện tại đang trong quá trình chạy hiệu ứng -> Fast-forward hoàn tất ngay lập tức
        if (isCurrentPanelAnimating)
        {
            CompleteCurrentPanelAnimationImmediate();
            return;
        }

        // 2. Nếu ô hiện tại đã hiện xong -> Chuyển sang ô tiếp theo
        ShowNextPanel();
    }

    /// <summary>
    /// Hiển thị ô truyện tiếp theo
    /// </summary>
    private void ShowNextPanel()
    {
        int nextIndex = currentPanelIndex + 1;

        // Nếu đã hết toàn bộ các ô -> Kết thúc Story
        if (nextIndex >= panelImages.Count)
        {
            FinishStory();
            return;
        }

        currentPanelIndex = nextIndex;
        AnimatePanelEntry(currentPanelIndex);
    }

    /// <summary>
    /// Chạy hiệu ứng xuất hiện cho ô truyện theo hiệu ứng riêng đã cấu hình cho ô đó
    /// </summary>
    private void AnimatePanelEntry(int index)
    {
        if (index < 0 || index >= panelImages.Count || panelImages[index] == null || panelImages[index].image == null) return;

        StoryPanelItem item = panelImages[index];
        Image img = item.image;
        CanvasGroup cg = (index < panelCanvasGroups.Count) ? panelCanvasGroups[index] : null;
        RectTransform rect = img.rectTransform;
        PanelAnimationEffect effect = item.effect;
        float duration = item.customDuration > 0 ? item.customDuration : effectDuration;

        isCurrentPanelAnimating = true;
        SetNextHintVisible(false);

        // Chuẩn bị trạng thái xuất phát cho hiệu ứng
        PreparePanelStartingState(index, rect, cg, effect);

        // Phát âm thanh SFX riêng hoặc SFX chung
        PlayPanelSFX(item);

        Vector2 targetPos = (index < originalAnchoredPositions.Count) ? originalAnchoredPositions[index] : rect.anchoredPosition;
        Vector3 targetScale = (index < originalScales.Count) ? originalScales[index] : Vector3.one;
        Vector3 targetRot = (index < originalEulerAngles.Count) ? originalEulerAngles[index] : Vector3.zero;

        // 1. Fade Alpha 0 -> 1
        if (cg != null)
        {
            currentFadeTween?.Kill();
            currentFadeTween = cg.DOFade(1f, duration * 0.8f).SetUpdate(true);
        }

        // 2. Chạy Animation Transform tương ứng
        currentAnimTween?.Kill();

        switch (effect)
        {
            case PanelAnimationEffect.FadeOnly:
                currentAnimTween = DOVirtual.DelayedCall(duration, () => { }, true);
                break;

            case PanelAnimationEffect.PopScale:
                currentAnimTween = rect.DOScale(targetScale, duration)
                    .SetEase(Ease.OutBack, 1.4f)
                    .SetUpdate(true);
                break;

            case PanelAnimationEffect.ZoomIn:
                currentAnimTween = rect.DOScale(targetScale, duration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
                break;

            case PanelAnimationEffect.SlideFromLeft:
            case PanelAnimationEffect.SlideFromRight:
            case PanelAnimationEffect.SlideFromBottom:
            case PanelAnimationEffect.SlideFromTop:
                currentAnimTween = rect.DOAnchorPos(targetPos, duration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
                break;

            case PanelAnimationEffect.FlipHorizontal:
                currentAnimTween = rect.DOLocalRotate(targetRot, duration)
                    .SetEase(Ease.OutBack, 1.2f)
                    .SetUpdate(true);
                break;

            case PanelAnimationEffect.PunchShake:
                currentAnimTween = rect.DOPunchScale(targetScale * 0.25f, duration, 6, 0.5f)
                    .SetUpdate(true);
                break;
        }

        // Khi hiệu ứng hoàn thành
        currentAnimTween.OnComplete(() =>
        {
            OnPanelAnimationCompleted(index);
        });

        OnPanelShown?.Invoke(index);
    }

    /// <summary>
    /// Thiết lập trạng thái bắt đầu của ô trước khi bay vào theo hiệu ứng riêng
    /// </summary>
    private void PreparePanelStartingState(int index, RectTransform rect, CanvasGroup cg, PanelAnimationEffect effect)
    {
        rect.DOKill();
        if (cg != null)
        {
            cg.DOKill();
            cg.alpha = 0f;
        }

        Vector2 origPos = (index < originalAnchoredPositions.Count) ? originalAnchoredPositions[index] : rect.anchoredPosition;
        Vector3 origScale = (index < originalScales.Count) ? originalScales[index] : Vector3.one;

        switch (effect)
        {
            case PanelAnimationEffect.FadeOnly:
                rect.anchoredPosition = origPos;
                rect.localScale = origScale;
                break;

            case PanelAnimationEffect.PopScale:
                rect.anchoredPosition = origPos;
                rect.localScale = origScale * 0.7f;
                break;

            case PanelAnimationEffect.ZoomIn:
                rect.anchoredPosition = origPos;
                rect.localScale = Vector3.zero;
                break;

            case PanelAnimationEffect.SlideFromLeft:
                rect.anchoredPosition = origPos - new Vector2(slideDistance, 0);
                rect.localScale = origScale;
                break;

            case PanelAnimationEffect.SlideFromRight:
                rect.anchoredPosition = origPos + new Vector2(slideDistance, 0);
                rect.localScale = origScale;
                break;

            case PanelAnimationEffect.SlideFromBottom:
                rect.anchoredPosition = origPos - new Vector2(0, slideDistance);
                rect.localScale = origScale;
                break;

            case PanelAnimationEffect.SlideFromTop:
                rect.anchoredPosition = origPos + new Vector2(0, slideDistance);
                rect.localScale = origScale;
                break;

            case PanelAnimationEffect.FlipHorizontal:
                rect.anchoredPosition = origPos;
                rect.localScale = origScale;
                rect.localEulerAngles = new Vector3(0, 90f, 0);
                break;

            case PanelAnimationEffect.PunchShake:
                rect.anchoredPosition = origPos;
                rect.localScale = origScale;
                break;
        }
    }

    /// <summary>
    /// Hoàn tất ngay lập tức hiệu ứng của ô hiện tại nếu người chơi nhấn Next trong lúc đang chạy
    /// </summary>
    private void CompleteCurrentPanelAnimationImmediate()
    {
        if (currentPanelIndex < 0 || currentPanelIndex >= panelImages.Count) return;

        currentAnimTween?.Kill();
        currentFadeTween?.Kill();

        // Đặt ngay về trạng thái chuẩn
        ResetPanelTransformToOriginal(currentPanelIndex);

        if (currentPanelIndex < panelCanvasGroups.Count && panelCanvasGroups[currentPanelIndex] != null)
        {
            panelCanvasGroups[currentPanelIndex].DOKill();
            panelCanvasGroups[currentPanelIndex].alpha = 1f;
        }

        OnPanelAnimationCompleted(currentPanelIndex);
    }

    private void OnPanelAnimationCompleted(int index)
    {
        isCurrentPanelAnimating = false;
        // Bật icon chỉ dẫn báo hiệu người chơi bấm Next để tiếp tục
        SetNextHintVisible(true);
    }

    /// <summary>
    /// Nút Skip: Hiển thị trọn vẹn cả 6 ô ngay lập tức và hoàn tất
    /// </summary>
    public void OnSkipClicked()
    {
        if (!isPlaying) return;

        // Phát âm thanh tap/click button
        PlayTapSFX();

        currentAnimTween?.Kill();
        currentFadeTween?.Kill();

        for (int i = 0; i < panelImages.Count; i++)
        {
            ResetPanelTransformToOriginal(i);

            if (i < panelCanvasGroups.Count && panelCanvasGroups[i] != null)
            {
                panelCanvasGroups[i].DOKill();
                panelCanvasGroups[i].alpha = 1f;
            }
        }

        FinishStory();
    }

    /// <summary>
    /// Đóng trang truyện và kích hoạt callback
    /// </summary>
    public void FinishStory()
    {
        if (!isPlaying) return;

        isPlaying = false;
        isCurrentPanelAnimating = false;
        SetNextHintVisible(false);

        currentAnimTween?.Kill();
        currentFadeTween?.Kill();

        RestoreBGM();

        OnStoryFinished?.Invoke();

        Action callback = onStoryCompleteCallback;
        onStoryCompleteCallback = null;

        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
            mainCanvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }
        else
        {
            gameObject.SetActive(false);
        }

        callback?.Invoke();
    }

    private void SetNextHintVisible(bool visible)
    {
        if (nextHintObject == null) return;
        nextHintObject.SetActive(visible);

        if (visible)
        {
            hintBounceTween?.Kill();
            nextHintObject.transform.localScale = Vector3.one;
            hintBounceTween = nextHintObject.transform.DOScale(1.15f, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
        else
        {
            hintBounceTween?.Kill();
        }
    }

    private void PlayTapSFX()
    {
        string sfxKey = (!string.IsNullOrEmpty(tapSfxKey) && tapSfxKey != Const.popUISFX) ? tapSfxKey : Const.tapHintStorySFX;
        if (!string.IsNullOrEmpty(sfxKey))
        {
            AudioManager.Instance?.PlaySFX(sfxKey);
        }
    }

    private void PlayPanelSFX(StoryPanelItem item)
    {
        string sfxKey = (item != null && !string.IsNullOrEmpty(item.customSfxKey)) ? item.customSfxKey : panelShowSfxKey;
        if (!string.IsNullOrEmpty(sfxKey))
        {
            AudioManager.Instance?.PlaySFX(sfxKey);
        }
    }

    private void PlayBGM()
    {
        if (!string.IsNullOrEmpty(storyBgmKey))
        {
            AudioManager.Instance?.PlayBGM(storyBgmKey, true);
        }
    }

    private void RestoreBGM()
    {
        if (!string.IsNullOrEmpty(storyBgmKey))
        {
            AudioManager.Instance?.PlayBGM(Const.BGM, true);
        }
    }

    /// <summary>
    /// Tự động tìm tất cả Image con và đưa vào list panelImages với hiệu ứng mặc định
    /// </summary>
    [ContextMenu("Auto Find Child Images As Panels")]
    public void AutoFindPanels()
    {
        panelImages.Clear();
        Image[] allImages = GetComponentsInChildren<Image>(true);

        foreach (Image img in allImages)
        {
            if (img == backgroundImage) continue;
            if (img.gameObject == gameObject) continue;
            if (img.GetComponent<Button>() != null) continue;

            panelImages.Add(new StoryPanelItem
            {
                image = img,
                effect = PanelAnimationEffect.PopScale
            });
        }

        CacheCanvasGroups();
        CacheOriginalTransforms();
        Debug.Log($"[StoryUI] Đã tự động tìm thấy {panelImages.Count} ô Image!");
    }

    [ContextMenu("Test Play In Editor")]
    public void TestPlayInEditor()
    {
        ShowStory(() => Debug.Log("[StoryUI] Story playback completed!"));
    }
}
