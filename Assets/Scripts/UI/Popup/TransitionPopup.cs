using System;
using UnityEngine;
using DG.Tweening;

public class TransitionPopup : BasePopUp
{
    [Header("Transition Settings")]
    [Tooltip("Thời gian chờ trước khi tự động gọi Hide")]
    [SerializeField] private float autoHideDelay = 1f;
    [Tooltip("Độ mờ ban đầu khi bắt đầu hiện (0.5 = 50%)")]
    [SerializeField] private float startAlpha = 0.5f;

    private Tween autoHideTween;

    protected override void OnDisable()
    {
        if (autoHideTween != null && autoHideTween.IsActive())
        {
            autoHideTween.Kill();
        }

        autoHideTween = null;
        base.OnDisable();
    }

    public override void Show(Action onComplete = null)
    {
        if (isShowing) return;

        if (autoHideTween != null && autoHideTween.IsActive())
        {
            autoHideTween.Kill();
        }

        autoHideTween = null;

        gameObject.SetActive(true);
        isShowing = true;

        // Quan trọng: Phải gán lại scale cứng bằng kích thước gốc vì OnEnable ở BasePopUp đã ép nó về 0
        if (contentPanel != null)
        {
            contentPanel.localScale = scaleSize;
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOKill(); // Dọn dẹp tween cũ nếu có tránh xung đột
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // Set alpha bắt đầu (Mặc định 50%)
            canvasGroup.alpha = startAlpha;

            // Xử lý Fade Canvas
            canvasGroup.DOFade(1f, showDuration)
                .SetEase(showEase)
                .OnComplete(() =>
                {
                    OnShowComplete();
                    onComplete?.Invoke();

                    // Tự động gọi Hide sau khi delay 1s
                    autoHideTween = DOVirtual.DelayedCall(autoHideDelay, () =>
                    {
                        // Kiểm tra isShowing đề phòng trường hợp bị ẩn bằng tay trước khi đếm ngược xong
                        if (isShowing) Hide();
                    }).SetUpdate(true);
                });
        }

        // (Tuỳ chọn) Nếu bạn vẫn muốn dùng nền Overlay cho Popup này thì làm mờ nó lên
        if (useOverlay && overlayImage != null)
        {
            overlayImage.DOKill();
            overlayImage.DOFade(overlayColor.a, showDuration);
        }
    }

    public override void Hide(Action onComplete = null)
    {
        if (!isShowing) return;
        isShowing = false;

        if (autoHideTween != null && autoHideTween.IsActive())
        {
            autoHideTween.Kill();
        }

        autoHideTween = null;

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Ngược lại show: Fade từ 100% về 0% để ẩn hoàn toàn
            canvasGroup.DOFade(0f, hideDuration)
                .SetEase(hideEase)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    OnHideComplete(); // Hàm này bên BasePopUp sẽ tự gọi Destroy(gameObject)
                    onComplete?.Invoke();
                });
        }

        // Fade out nền Overlay nếu có
        if (useOverlay && overlayImage != null)
        {
            overlayImage.DOKill();
            overlayImage.DOFade(0f, hideDuration);
        }
    }
}