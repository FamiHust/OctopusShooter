using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component gắn trên mỗi nút booster trong UI.
/// Trạng thái hiển thị:
///   • Chưa unlock  → lockedSprite, ẩn count/plus, button disabled
///   • Unlock + count = 0 → activeIcon, hiện plusImage, ẩn count → nhấn mở buy popup
///   • Unlock + count ≥ 1 → activeIcon, hiện count, ẩn plus
///       └─ canUse = true  → nhấn dùng booster (1-step: execute; 2-step: show instruction)
///       └─ canUse = false → button disabled (grayed out)
/// </summary>
public class BoosterButtonPrefab : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BoosterStrategyConfig config;

    [Header("UI References")]
    [SerializeField] private Button          button;
    [SerializeField] private Image           iconImage;
    [SerializeField] private Image           plusImage;   // shown when count = 0 (buy prompt)
    [SerializeField] private GameObject      countBg;     // background badge for count
    [SerializeField] private Text countText;
    [Tooltip("Text hiển thị level unlock khi booster đang bị khóa")]
    [SerializeField] private Text            lockLevelText;
    [Tooltip("Sprite hiển thị khi booster chưa unlock")]
    [SerializeField] private Sprite          lockedSprite;
    [Tooltip("Particle / highlight khi booster đang active (2-step mode)")]
    [SerializeField] private ParticleSystem  activeHighlight;

    // Runtime
    private InGameUIManager uiManager;
    private IBoosterStrategy strategy;

    // ──────────────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureLockLevelText();

        if (config != null)
            strategy = config.CreateStrategy();

        button?.onClick.AddListener(OnButtonClicked);
        activeHighlight?.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        button?.onClick.RemoveListener(OnButtonClicked);
    }

    /// <summary>
    /// Gọi từ InGameUIManager.Init() để lưu reference.
    /// </summary>
    public void Initialize(InGameUIManager manager)
    {
        uiManager = manager;
        Refresh();
    }

    public BoosterStrategyConfig GetConfig() => config;

    // ──────────────────────────────────────────────────────────────────
    // Click handler
    // ──────────────────────────────────────────────────────────────────

    private void OnButtonClicked()
    {
        if (config == null || strategy == null) return;

        bool isAnyBoosterActive = BoosterManager.Instance != null && BoosterManager.Instance.IsAnyBoosterModeActive();
        bool isThisBoosterActive = BoosterManager.Instance != null && BoosterManager.Instance.IsBoosterModeActive(config.boosterName);

        if (isAnyBoosterActive && !isThisBoosterActive) return;

        bool isUnlocked = BoosterUnlockPrefs.IsBoosterUnlocked(config.boosterName);
        if (!isUnlocked) return;

        int count = BoosterManager.Instance != null
            ? BoosterManager.Instance.GetBoosterCount(config.boosterName)
            : Mathf.Max(0, config.initialCount);

        if (count == 0)
        {
            // Mở popup mua
            uiManager?.ShowBuyPopup(config.boosterName, config.coinPrice);
            return;
        }

        // Dùng booster
        uiManager?.OnUseBooster(config.boosterName);
    }

    // ──────────────────────────────────────────────────────────────────
    // Refresh UI — gọi bởi InGameUIManager
    // ──────────────────────────────────────────────────────────────────

    public void Refresh()
    {
        if (config == null) return;

        bool isUnlocked = BoosterUnlockPrefs.IsBoosterUnlocked(config.boosterName);
        int  count      = isUnlocked
                      ? (BoosterManager.Instance != null
                          ? BoosterManager.Instance.GetBoosterCount(config.boosterName)
                          : Mathf.Max(0, config.initialCount))
                          : 0;
        bool canUse     = isUnlocked && count >= 1
                          && (uiManager?.CanUseBooster(config.boosterName) ?? false);

        // ── Icon ──────────────────────────────────────────────────────
        if (iconImage != null)
            iconImage.sprite = (!isUnlocked && lockedSprite != null) ? lockedSprite : config.activeIcon;

        // ── Plus (buy) badge ──────────────────────────────────────────
        if (plusImage != null)
            plusImage.gameObject.SetActive(isUnlocked && count == 0);

        // ── Count badge ───────────────────────────────────────────────
        if (countBg != null)
            countBg.SetActive(isUnlocked && count >= 1);

        if (countText != null)
        {
            countText.gameObject.SetActive(isUnlocked && count >= 1);
            if (isUnlocked && count >= 1)
                countText.text = count.ToString();
        }

        // ── Locked level text ────────────────────────────────────────
        if (lockLevelText != null)
        {
            bool showLockLevel = !isUnlocked;
            lockLevelText.gameObject.SetActive(showLockLevel);
            if (showLockLevel)
                lockLevelText.text = $"Level {Mathf.Max(1, config.unlockAtLevel)}";
        }

        // ── Button interactable ───────────────────────────────────────
        if (button != null)
        {
            bool isAnyBoosterActive = BoosterManager.Instance != null && BoosterManager.Instance.IsAnyBoosterModeActive();
            bool isThisBoosterActive = BoosterManager.Instance != null && BoosterManager.Instance.IsBoosterModeActive(config.boosterName);

            if (isAnyBoosterActive && !isThisBoosterActive)
            {
                button.interactable = false;
            }
            else if (!isUnlocked)
            {
                button.interactable = false;
            }
            else if (count == 0)
            {
                button.interactable = true;   // luôn cho nhấn để mở popup mua
            }
            else
            {
                button.interactable = canUse; // chỉ cho dùng khi đủ điều kiện
            }
        }

        // ── Active highlight (2-step mode) ────────────────────────────
        if (activeHighlight != null)
        {
            bool shouldHighlight = BoosterManager.Instance != null
                                   && BoosterManager.Instance.IsBoosterModeActive(config.boosterName);
            if (shouldHighlight)
            {
                if (!activeHighlight.gameObject.activeSelf)
                {
                    activeHighlight.gameObject.SetActive(true);
                    activeHighlight.Play();
                }
            }
            else
            {
                if (activeHighlight.gameObject.activeSelf)
                {
                    activeHighlight.Stop();
                    activeHighlight.Clear();
                    activeHighlight.gameObject.SetActive(false);
                }
            }
        }
    }

    private void EnsureLockLevelText()
    {
        if (lockLevelText != null)
        {
            lockLevelText.gameObject.SetActive(false);
            return;
        }

        Transform existing = transform.Find("LockLevelText");
        if (existing != null)
            lockLevelText = existing.GetComponent<Text>();

        if (lockLevelText == null)
        {
            GameObject textObj = new GameObject("LockLevelText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObj.transform.SetParent(transform, false);
            textObj.transform.SetAsLastSibling();

            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -22f);
            rt.sizeDelta = new Vector2(170f, 30f);

            lockLevelText = textObj.GetComponent<Text>();
        }

        lockLevelText.alignment = TextAnchor.MiddleCenter;
        lockLevelText.fontStyle = FontStyle.Bold;
        lockLevelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lockLevelText.verticalOverflow = VerticalWrapMode.Overflow;
        lockLevelText.resizeTextForBestFit = true;
        lockLevelText.resizeTextMinSize = 10;
        lockLevelText.resizeTextMaxSize = 22;
        lockLevelText.raycastTarget = false;
        lockLevelText.color = new Color(1f, 0.95686275f, 0.54509807f, 1f);

        if (lockLevelText.font == null)
        {
            if (countText != null && countText.font != null)
                lockLevelText.font = countText.font;
            else
                lockLevelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        lockLevelText.gameObject.SetActive(false);
    }
}

 


