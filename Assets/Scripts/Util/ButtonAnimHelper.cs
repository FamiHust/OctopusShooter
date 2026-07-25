using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

/// <summary>
/// Helper class để thêm hiệu ứng scale animation cho buttons
/// </summary>
public static class ButtonAnimHelper
{
    /// <summary>
    /// Thêm animation scale cho button với callbacks tùy chỉnh
    /// </summary>
    /// <param name="button">Button cần thêm animation</param>
    /// <param name="scaleDown">Scale khi nhấn xuống (default: 0.96f)</param>
    /// <param name="scaleNormal">Scale bình thường (default: 1f)</param>
    /// <param name="onPointerDown">Callback khi nhấn xuống (optional)</param>
    /// <param name="onPointerHold">Callback khi giữ (được gọi liên tục trong Update, optional)</param>
    /// <param name="onPointerUp">Callback khi thả ra (optional)</param>
    /// <param name="onPointerExit">Callback khi rời khỏi button (optional)</param>
    public static void AddScaleAnimation(
        Button button, 
        float scaleDown = 0.9f, 
        float scaleNormal = 1f,
        Action onPointerDown = null,
        Action onPointerHold = null,
        Action onPointerUp = null,
        Action onPointerExit = null)
    {
        if (button == null) return;

        // Tắt hiệu ứng tối màu mặc định của button
        button.transition = Selectable.Transition.None;

        // Thêm hoặc lấy ButtonEventHandler component
        ButtonEventHandler eventHandler = button.gameObject.GetComponent<ButtonEventHandler>();
        if (eventHandler == null)
        {
            eventHandler = button.gameObject.AddComponent<ButtonEventHandler>();
        }

        // Setup callbacks
        eventHandler.SetupCallbacks(
            scaleDown, 
            scaleNormal, 
            onPointerDown, 
            onPointerHold, 
            onPointerUp, 
            onPointerExit
        );
    }

    /// <summary>
    /// Thêm animation scale cho nhiều buttons cùng lúc (không có custom callbacks)
    /// </summary>
    public static void AddScaleAnimationToButtons(params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            AddScaleAnimation(button);
        }
    }
}

/// <summary>
/// Component xử lý các sự kiện của button
/// </summary>
public class ButtonEventHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private float scaleDown = 0.96f;
    private float scaleNormal = 1f;
    private const float pressDuration = 0.2f;
    private const float releaseDuration = 0.14f;
    private Action onPointerDownCallback;
    private Action onPointerHoldCallback;
    private Action onPointerUpCallback;
    private Action onPointerExitCallback;
    
    private bool isPointerDown = false;
    private Coroutine holdCoroutine;

    public void SetupCallbacks(
        float scaleDown, 
        float scaleNormal, 
        Action onDown, 
        Action onHold, 
        Action onUp, 
        Action onExit)
    {
        this.scaleDown = scaleDown;
        this.scaleNormal = scaleNormal;
        this.onPointerDownCallback = onDown;
        this.onPointerHoldCallback = onHold;
        this.onPointerUpCallback = onUp;
        this.onPointerExitCallback = onExit;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        
        // Animation scale xuống
        transform.DOKill();
        transform.DOScale(scaleDown, pressDuration).SetEase(Ease.OutQuad).SetUpdate(true);

        if (onPointerHoldCallback != null && holdCoroutine == null)
        {
            holdCoroutine = StartCoroutine(HoldRoutine());
        }
        
        // Custom callback
        onPointerDownCallback?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;

        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
        
        // Animation scale về bình thường
        transform.DOKill();
        transform.DOScale(scaleNormal, releaseDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        
        // Custom callback
        onPointerUpCallback?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Không scale về khi exit - chỉ scale về khi thật sự thả tay (PointerUp)
        // isPointerDown vẫn giữ true để tiếp tục track trạng thái hold
        
        // Custom callback
        onPointerExitCallback?.Invoke();
    }

    private System.Collections.IEnumerator HoldRoutine()
    {
        while (isPointerDown && onPointerHoldCallback != null)
        {
            onPointerHoldCallback.Invoke();
            yield return null;
        }

        holdCoroutine = null;
    }

    private void OnDisable()
    {
        // Reset state khi disable
        isPointerDown = false;

        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }

        transform.DOKill();
    }
}
