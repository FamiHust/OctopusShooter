using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BasePopUp : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] protected float showDuration = 0.3f;
    [SerializeField] protected float hideDuration = 0.25f;
    [SerializeField] protected Vector3 scaleSize = Vector3.one;
    [SerializeField] protected Ease showEase = Ease.OutBack;
    [SerializeField] protected Ease hideEase = Ease.InBack;

    [Header("References")]
    [SerializeField] protected CanvasGroup canvasGroup;
    [SerializeField] protected Transform popupTitle;
    [SerializeField] protected RectTransform contentPanel; // Panel chính để animate
    [SerializeField] protected Button closeButton; // Nút đóng (optional)

    [Header("Overlay")]
    [SerializeField] protected bool useOverlay = true; // Có dùng nền tối phía sau không
    [SerializeField] protected Image overlayImage;
    [SerializeField] protected Color overlayColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] protected bool destroyOnHide = true;

    protected bool isShowing = false;
    private Sequence currentSequence;

    protected virtual void Awake()
    {
        if (contentPanel != null)
        {
            contentPanel.transform.localScale = Vector3.zero;
        }
        // Auto setup components nếu chưa gán
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (contentPanel == null)
            contentPanel = transform.GetChild(0) as RectTransform; // Lấy child đầu tiên

        // Setup close button
        if (closeButton != null)
        {
            // Thêm animation cho close button
            //ButtonAnimHelper.AddScaleAnimation(closeButton);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX(Const.popUISFX);
                Hide();
            });
        }

        // Thêm animation cho tất cả buttons trong popup
        SetupButtonAnimations();

        // Setup overlay
        if (useOverlay && overlayImage != null)
        {
            overlayImage.color = overlayColor;
        }

        // Note: popups will be instantiated/destroyed by UIManager; do not forcibly deactivate here
    }

    /// <summary>
    /// Override method này để thêm animation cho các buttons trong popup con
    /// </summary>
    protected virtual void SetupButtonAnimations()
    {
        // Base implementation - tìm tất cả buttons và thêm animation
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        foreach (var button in allButtons)
        {
            if (button != null && button != closeButton) // closeButton đã được setup ở trên
            {
                ButtonAnimHelper.AddScaleAnimation(button);
            }
        }
    }

    protected virtual void OnEnable()
    {
        // Reset state khi enable
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
        }

        if (contentPanel != null)
        {
            contentPanel.localScale = Vector3.zero;
        }
    }

    protected virtual void OnDisable()
    {
        // Cleanup tweens
        currentSequence?.Kill();
    }

    // ========= PUBLIC API =========

    public virtual void Show(System.Action onComplete = null)
    {
        if (isShowing) return;

        gameObject.SetActive(true);
        isShowing = true;
        canvasGroup.interactable = true;

        // Phát SFX PopUI mỗi khi popup mở
        AudioManager.Instance?.PlaySFX(Const.popUISFX);

        // Kill tween cũ nếu có
        currentSequence?.Kill();

        currentSequence = DOTween.Sequence();
        currentSequence.SetUpdate(true);
        canvasGroup.alpha = 1f;
        // Fade in overlay
        if (useOverlay && overlayImage != null)
        {
            overlayImage.DOFade(overlayColor.a, showDuration * 0.7f).SetUpdate(true);
        }

        //Fade in canvas

        //Scale in content panel
        if (contentPanel != null)
        {
            currentSequence.Append(
                contentPanel.DOScale(scaleSize, showDuration)
                    .SetEase(showEase)
            );

            //// Thêm punch effect cho sinh động
            //currentSequence.Append(
            //    contentPanel.DOPunchScale(Vector3.one * 0.1f, 0.2f, 5, 0.5f)
            //);
        }
        if (popupTitle != null)
        {
            // Reset title về bé xíu trước khi scale lên
            popupTitle.localScale = Vector3.one * 0.7f; // Hoặc Vector3.zero nếu muốn từ không khí hiện ra

            // Scale lên 1
            currentSequence.Join(
                popupTitle.DOScale(Vector3.one, showDuration + 0.25f)
                    .SetEase(Ease.OutBack) // Dùng OutBack để có độ nảy nhẹ ("scale nhẹ")
            );
        }

        currentSequence.OnComplete(() =>
        {
            if (canvasGroup != null)
            {

                canvasGroup.blocksRaycasts = true;
            }

            OnShowComplete();
            onComplete?.Invoke();
        });
    }

    public virtual void Hide(System.Action onComplete = null)
    {
        if (!isShowing) return;

        isShowing = false;

        // Disable interaction ngay
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Kill tween cũ
        currentSequence?.Kill();

        currentSequence = DOTween.Sequence();
        currentSequence.SetUpdate(true);

        // Fade out overlay
        if (useOverlay && overlayImage != null)
        {
            overlayImage.DOFade(0f, hideDuration).SetUpdate(true);
        }

        // Fade out canvas
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, hideDuration).SetUpdate(true);
        }

        // Scale out content panel
        if (contentPanel != null)
        {
            currentSequence.Append(
                contentPanel.DOScale(Vector3.zero, hideDuration)
                    .SetEase(hideEase)
            );
        }


        currentSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            OnHideComplete();
            onComplete?.Invoke();
        });
    }

    // ========= LIFECYCLE HOOKS (Override trong class con) =========

    protected virtual void OnShowComplete()
    {
        // Override trong class con để xử lý logic khi popup hiện xong
    }

    protected virtual void OnHideComplete()
    {
        if (destroyOnHide)
        {
            Destroy(gameObject);
        }
    }

    // ========= UTILITY =========

    public void SetDestroyOnHide(bool shouldDestroyOnHide)
    {
        destroyOnHide = shouldDestroyOnHide;
    }

    public bool IsShowing()
    {
        return isShowing;
    }

    protected bool ShouldDestroyOnHide()
    {
        return destroyOnHide;
    }
}
