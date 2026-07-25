using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BottomBarIndicator : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private RectTransform indicator;
    [SerializeField] private Vector2 indicatorSizeOffset = new Vector2(150f, 0f); // Điều chỉnh kích thước indicator so với width của button
    [SerializeField] private int comingSoonPageIndex = 2; // Page index của "Comming Soon" để disable click và có thể thêm hiệu ứng sau này
    [SerializeField] private float comingSoonClickCooldown = 1f;
    [SerializeField] private GameObject notePrefab;
    private float nextComingSoonClickTime;
    private RectTransform[] buttons;
    private ScrollSnapNormalized scrollSnap;
    private int totalPages;


    void Start()
    {
        scrollSnap = scrollRect.GetComponent<ScrollSnapNormalized>();

        totalPages = buttonContainer.childCount;
        buttons = new RectTransform[totalPages];

        for (int i = 0; i < totalPages; i++)
        {
            buttons[i] = buttonContainer.GetChild(i).GetComponent<RectTransform>();

            int index = i;
            buttons[i].GetComponentInChildren<Button>()?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX(Const.popUISFX);

                if (scrollSnap != null && (scrollSnap.IsPageLocked(index) || index == comingSoonPageIndex))
                {
                    if (index == comingSoonPageIndex && notePrefab != null)
                    {
                        if (Time.unscaledTime < nextComingSoonClickTime)
                        {
                            return;
                        }

                        nextComingSoonClickTime = Time.unscaledTime + Mathf.Max(0.05f, comingSoonClickCooldown);

                        // Hiển thị note "Comming Soon" khi click vào page này
                        GameObject note = Instantiate(notePrefab, scrollRect.transform);
                        Sequence sequence = DOTween.Sequence();
                        sequence.Append(note.GetComponent<RectTransform>().DOAnchorPosY(300f, 2f).SetEase(Ease.Linear))
                                .Join(note.GetComponent<RectTransform>().DOScale(Vector3.one * 1.2f, 1f).SetEase(Ease.OutBack))
                                .SetLoops(1, LoopType.Yoyo)
                                .Join(note.GetComponent<CanvasGroup>().DOFade(0f, 1f).SetEase(Ease.InCubic).SetDelay(1f))
                                .OnComplete(() => Destroy(note));
                    }
                    return;
                }

                scrollSnap.GoToPage(index);
                scrollSnap.AnimTextAndIcon(index);
            });
        }

        indicator.pivot = new Vector2(0.5f, 0f);

        float viewportWidth = scrollRect.viewport != null
            ? scrollRect.viewport.rect.width
            : scrollRect.GetComponent<RectTransform>().rect.width;

        ApplyIndicatorSize(viewportWidth);

        scrollRect.onValueChanged.AddListener(UpdateIndicator);

        if (scrollSnap != null)
            scrollSnap.OnPageChanged += OnPageChanged;
    }

    private void ApplyIndicatorSize(float viewportWidth)
    {
        float baseWidth = totalPages > 0 ? (viewportWidth / totalPages) : viewportWidth;
        float width = Mathf.Max(0f, baseWidth + indicatorSizeOffset.x);
        float height = Mathf.Max(0f, indicator.rect.height + indicatorSizeOffset.y);

        indicator.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        indicator.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    void UpdateIndicator(Vector2 value)
    {
        if (buttons == null || buttons.Length == 0) return;

        float normalized = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
        float pageFloat = normalized * (totalPages - 1);

        int leftIndex = Mathf.Clamp(Mathf.FloorToInt(pageFloat), 0, totalPages - 1);
        int rightIndex = Mathf.Clamp(leftIndex + 1, 0, totalPages - 1);

        float lerp = pageFloat - leftIndex;

        float x = Mathf.Lerp(
            buttons[leftIndex].anchoredPosition.x,
            buttons[rightIndex].anchoredPosition.x,
            lerp
        );

        indicator.anchoredPosition = new Vector2(x, indicator.anchoredPosition.y);
    }

    void OnPageChanged(int index)
    {
        // Nếu sau này bạn muốn scale hoặc đổi màu nút active thì thêm ở đây
        // Hiện tại chỉ giữ đồng bộ index
    }
}