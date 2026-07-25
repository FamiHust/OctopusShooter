using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class ScrollSnapNormalized : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [SerializeField] private ScrollRect scrollRect;

    [Header("Settings")]
    [SerializeField] private int totalPages = 5;
    [SerializeField] private int startPage = 2; // MainMenu giữa
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField][Range(0f, 1f)] private float swipeThreshold = 0.3f;
    [SerializeField] private float fastSwipeTime = 0.25f;
    [SerializeField] private RectTransform[] icon;
    [SerializeField] private Text[] text;
    [SerializeField] private List<int> lockedPages = new List<int>(); // 0-based page indices to lock
    

    private float[] pagePositions;
    private float dragStartPos;
    private int dragStartPage;
    private float dragStartTime;
    private bool isDragging;
    private float dragMinPos;
    private float dragMaxPos;
    private Tween snapTween;

    public Action<int> OnPageChanged;
    public int CurrentPage { get; private set; }

    void Start()
    {
        GeneratePagePositions();
        JumpToPage(GetNearestUnlockedPage(startPage));
    }

    void GeneratePagePositions()
    {
        pagePositions = new float[totalPages];

        if (totalPages == 1)
        {
            pagePositions[0] = 0;
            return;
        }

        float step = 1f / (totalPages - 1);

        for (int i = 0; i < totalPages; i++)
        {
            pagePositions[i] = step * i;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = scrollRect.horizontalNormalizedPosition;
        dragStartPage = GetNearestUnlockedPage(GetNearestPage(dragStartPos));
        dragStartTime = Time.unscaledTime;
        isDragging = true;
        ComputeDragBoundsForPage(dragStartPage);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || pagePositions == null || pagePositions.Length == 0)
        {
            return;
        }

        float clamped = Mathf.Clamp(scrollRect.horizontalNormalizedPosition, dragMinPos, dragMaxPos);
        if (!Mathf.Approximately(clamped, scrollRect.horizontalNormalizedPosition))
        {
            scrollRect.horizontalNormalizedPosition = clamped;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        float currentPos = scrollRect.horizontalNormalizedPosition;
        float delta = currentPos - dragStartPos;
        float dragDuration = Time.unscaledTime - dragStartTime;

        if (Mathf.Abs(delta) > 0.01f)
        {
            AudioManager.Instance?.PlaySFX(Const.shooterDoneSFX);
        }

        int targetPage = dragStartPage;

        bool isFastSwipe = dragDuration <= fastSwipeTime && Mathf.Abs(delta) > 0.01f;

        if (isFastSwipe)
        {
            targetPage += delta > 0 ? 1 : -1;
        }
        else
        {
            float pageStep = totalPages > 1 ? 1f / (totalPages - 1) : 1f;
            float threshold = pageStep * swipeThreshold;

            if (Mathf.Abs(delta) > threshold)
            {
                targetPage += delta > 0 ? 1 : -1;
            }
        }

        targetPage = Mathf.Clamp(targetPage, 0, totalPages - 1);
        if (IsPageLocked(targetPage))
        {
            targetPage = dragStartPage;
        }

        SnapToPage(targetPage);
        AnimTextAndIcon(targetPage);
    }

    int GetNearestPage(float currentPos)
    {
        float minDistance = Mathf.Abs(currentPos - pagePositions[0]);
        int nearest = 0;

        for (int i = 1; i < pagePositions.Length; i++)
        {
            float distance = Mathf.Abs(currentPos - pagePositions[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    void SnapToPage(int index)
    {
        float target = pagePositions[index];

        CurrentPage = index;

        if (snapTween != null && snapTween.IsActive())
        {
            snapTween.Kill();
        }

        snapTween = DOTween.To(
            () => scrollRect.horizontalNormalizedPosition,
            x => scrollRect.horizontalNormalizedPosition = x,
            target,
            snapDuration
        )
        .SetEase(Ease.OutCubic)
        .OnComplete(() =>
        {
            snapTween = null;
            OnPageChanged?.Invoke(CurrentPage);
        });
    }

    void JumpToPage(int index)
    {
        CurrentPage = index;
        scrollRect.horizontalNormalizedPosition = pagePositions[index];
        OnPageChanged?.Invoke(CurrentPage);
    }

    public void GoToPage(int index)
    {
        index = Mathf.Clamp(index, 0, totalPages - 1);
        if (IsPageLocked(index))
        {
            return;
        }

        SnapToPage(index);
    }

    private void OnDisable()
    {
        if (snapTween != null && snapTween.IsActive())
        {
            snapTween.Kill();
        }

        snapTween = null;
    }

    public void LockPage(int index)
    {
        if (index < 0 || index >= totalPages)
        {
            return;
        }

        if (!lockedPages.Contains(index))
        {
            lockedPages.Add(index);
        }

        if (CurrentPage == index)
        {
            SnapToPage(GetNearestUnlockedPage(CurrentPage));
        }
    }

    public void UnlockPage(int index)
    {
        lockedPages.Remove(index);
    }

    public bool IsPageLocked(int index)
    {
        if (index < 0 || index >= totalPages)
        {
            return true;
        }

        return lockedPages.Contains(index);
    }

    private int GetNearestUnlockedPage(int preferredIndex)
    {
        int clamped = Mathf.Clamp(preferredIndex, 0, Mathf.Max(0, totalPages - 1));

        if (!IsPageLocked(clamped))
        {
            return clamped;
        }

        for (int distance = 1; distance < totalPages; distance++)
        {
            int left = clamped - distance;
            if (left >= 0 && !IsPageLocked(left))
            {
                return left;
            }

            int right = clamped + distance;
            if (right < totalPages && !IsPageLocked(right))
            {
                return right;
            }
        }

        return clamped;
    }

    private void ComputeDragBoundsForPage(int pageIndex)
    {
        int leftBound = pageIndex;
        for (int i = pageIndex - 1; i >= 0; i--)
        {
            if (IsPageLocked(i))
            {
                break;
            }

            leftBound = i;
        }

        int rightBound = pageIndex;
        for (int i = pageIndex + 1; i < totalPages; i++)
        {
            if (IsPageLocked(i))
            {
                break;
            }

            rightBound = i;
        }

        dragMinPos = pagePositions[Mathf.Clamp(leftBound, 0, pagePositions.Length - 1)];
        dragMaxPos = pagePositions[Mathf.Clamp(rightBound, 0, pagePositions.Length - 1)];
    }

    public float GetNormalizedPosition()
    {
        return scrollRect.horizontalNormalizedPosition;
    }

    public void AnimTextAndIcon(int pageIndex)
    {
        for (int i = 0; i < icon.Length; i++)
        {
            if (i == pageIndex)
            {
                icon[i].DOLocalMoveY(80f, 0.3f).SetEase(Ease.OutBack);
                text[i].rectTransform.DOLocalMoveY(-50f, 0.3f).SetEase(Ease.OutBack);
            }
            else
            {
                icon[i].DOLocalMoveY(0f, 0.3f).SetEase(Ease.OutBack);
                text[i].rectTransform.DOLocalMoveY(-150f, 0.3f).SetEase(Ease.OutBack);
            }
        }
    }
}