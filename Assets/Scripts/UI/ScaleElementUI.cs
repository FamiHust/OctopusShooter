using UnityEngine;
using UnityEngine.UI;

public class ScaleElementUI : MonoBehaviour
{
    [Header("Layout Element")]
    [SerializeField] private LayoutElement targetLayoutElement;

    private void OnEnable()
    {
        ResolveReferences();
        SyncPreferredSize();
    }

    private void OnRectTransformDimensionsChange()
    {
        SyncPreferredSize();
    }

    private void ResolveReferences()
    {
        if (targetLayoutElement == null)
        {
            targetLayoutElement = GetComponent<LayoutElement>();
        }
    }

    private void SyncPreferredSize()
    {
        if (targetLayoutElement == null)
        {
            return;
        }

        RectTransform canvasRect = GetCanvasRect();
        if (canvasRect == null)
        {
            targetLayoutElement.preferredWidth = Screen.width;
            targetLayoutElement.preferredHeight = Screen.height;
            return;
        }

        Vector2 size = canvasRect.rect.size;
        targetLayoutElement.preferredWidth = size.x;
        targetLayoutElement.preferredHeight = size.y;
    }

    private RectTransform GetCanvasRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        return canvas.transform as RectTransform;
    }
}
