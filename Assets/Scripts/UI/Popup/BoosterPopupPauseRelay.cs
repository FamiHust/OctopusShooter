using System;
using UnityEngine;

public class BoosterPopupPauseRelay : MonoBehaviour
{
    private Action onPopupClosed;
    private bool hasNotified;

    public void Initialize(Action onClosedCallback)
    {
        onPopupClosed = onClosedCallback;
        hasNotified = false;
    }

    private void OnDisable()
    {
        NotifyClosedOnce();
    }

    private void OnDestroy()
    {
        NotifyClosedOnce();
    }

    private void NotifyClosedOnce()
    {
        if (hasNotified)
        {
            return;
        }

        hasNotified = true;
        onPopupClosed?.Invoke();
    }
}
