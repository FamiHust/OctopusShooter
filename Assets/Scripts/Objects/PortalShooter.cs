using UnityEngine;
using DG.Tweening;

/// <summary>
/// Specialized shooter that requests side-route swap after it finishes jumping into deck.
/// This class extends BaseShooter and only emits an event, leaving base combat flow intact.
/// </summary>
public class PortalShooter : BaseShooter
{
    [Header("Portal Swap Trigger")]
    [SerializeField] private bool triggerSwapOnlyOncePerLifetime = true;

    [Header("Portal VFX")]
    [SerializeField] private ParticleSystem portalLoopVfx;
    [SerializeField] private ParticleSystem portalJumpLandingVfx;
    [SerializeField] private bool playPortalVfxOnEnable = true;
    [SerializeField] private bool forceLoopMode = true;
    [SerializeField] private bool portalVfxUseUnscaledTime = true;

    private bool isListeningShooterSelected;
    private bool waitingForDeckLanding;
    private bool hasSeenJumpingStateSinceSelection;
    private bool hasTriggeredSwapRequest;
    private Tween portalJumpVfxStopTween;

    protected override bool ShouldAlwaysKeepRendererVisible(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (IsRendererUnderVfxRoot(renderer, portalLoopVfx))
        {
            return true;
        }

        return IsRendererUnderVfxRoot(renderer, portalJumpLandingVfx);
    }

    protected override bool ShouldRunJumpTweenUnscaled()
    {
        return true;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BindSelectionListener();
        StopPortalJumpLandingVfx(true);

        if (playPortalVfxOnEnable)
        {
            TryPlayPortalVfxLoop();
        }
    }

    protected override void OnDisable()
    {
        UnbindSelectionListener();
        StopPortalVfx();
        StopPortalJumpLandingVfx(true);
        waitingForDeckLanding = false;
        hasSeenJumpingStateSinceSelection = false;
        base.OnDisable();
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (!waitingForDeckLanding)
        {
            return;
        }

        ShooterState state = GetCurrentState();
        if (state == ShooterState.Jumping)
        {
            hasSeenJumpingStateSinceSelection = true;
            return;
        }

        if (!hasSeenJumpingStateSinceSelection)
        {
            return;
        }

        if (state == ShooterState.Idle)
        {
            waitingForDeckLanding = false;
            hasSeenJumpingStateSinceSelection = false;

            if (triggerSwapOnlyOncePerLifetime)
            {
                hasTriggeredSwapRequest = true;
            }

            GameEventHub.Instance?.Invoke(GameEventType.OnPortalShooterSwapRequest, this);
            return;
        }

        if (state == ShooterState.Disappear || state == ShooterState.Empty)
        {
            waitingForDeckLanding = false;
            hasSeenJumpingStateSinceSelection = false;
        }
    }

    protected override void OnDestroy()
    {
        UnbindSelectionListener();
        StopPortalVfx();
        StopPortalJumpLandingVfx(true);
        base.OnDestroy();
    }

    protected override void OnJumpLandingDeck(Vector3 jumpVfxPosition, Quaternion jumpVfxRotation)
    {
        StopPortalVfx();
        PlayPortalJumpLandingVfx();
    }

    private bool IsRendererUnderVfxRoot(Renderer renderer, ParticleSystem vfx)
    {
        if (renderer == null || vfx == null)
        {
            return false;
        }

        Transform vfxRoot = vfx.transform;
        if (vfxRoot == null)
        {
            return false;
        }

        Transform rendererTransform = renderer.transform;
        return rendererTransform == vfxRoot || rendererTransform.IsChildOf(vfxRoot);
    }

    private void BindSelectionListener()
    {
        if (isListeningShooterSelected || GameEventHub.Instance == null)
        {
            return;
        }

        GameEventHub.Instance.AddListener(GameEventType.OnShooterSelected, OnShooterSelectedForPortalSwap);
        isListeningShooterSelected = true;
    }

    private void UnbindSelectionListener()
    {
        if (!isListeningShooterSelected)
        {
            return;
        }

        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.RemoveListener(GameEventType.OnShooterSelected, OnShooterSelectedForPortalSwap);
        }

        isListeningShooterSelected = false;
    }

    private void OnShooterSelectedForPortalSwap(object data)
    {
        BaseShooter selectedShooter = data as BaseShooter;
        if (selectedShooter != this)
        {
            return;
        }

        if (triggerSwapOnlyOncePerLifetime && hasTriggeredSwapRequest)
        {
            return;
        }

        TryPlayPortalVfxLoop();
        waitingForDeckLanding = true;
        hasSeenJumpingStateSinceSelection = GetCurrentState() == ShooterState.Jumping;
    }

    private void TryPlayPortalVfxLoop()
    {
        if (portalLoopVfx == null)
        {
            return;
        }

        ParticleSystem.MainModule main = portalLoopVfx.main;
        if (forceLoopMode)
        {
            main.loop = true;
        }

        main.useUnscaledTime = portalVfxUseUnscaledTime;

        GameObject vfxObject = portalLoopVfx.gameObject;
        if (vfxObject != null && !vfxObject.activeSelf)
        {
            vfxObject.SetActive(true);
        }

        if (!portalLoopVfx.isPlaying)
        {
            portalLoopVfx.Play(true);
        }
    }

    private void PlayPortalJumpLandingVfx()
    {
        if (portalJumpLandingVfx == null)
        {
            return;
        }

        if (portalJumpVfxStopTween != null)
        {
            portalJumpVfxStopTween.Kill(false);
            portalJumpVfxStopTween = null;
        }

        ApplyUnscaledTimeToVfx(portalJumpLandingVfx);

        ParticleSystem.MainModule rootMain = portalJumpLandingVfx.main;
        rootMain.loop = false;

        GameObject vfxObject = portalJumpLandingVfx.gameObject;
        if (vfxObject != null && !vfxObject.activeSelf)
        {
            vfxObject.SetActive(true);
        }

        portalJumpLandingVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        portalJumpLandingVfx.Play(true);

        float stopDelay = EstimateParticleStopDelay(portalJumpLandingVfx);
        portalJumpVfxStopTween = DOVirtual.DelayedCall(stopDelay, () =>
        {
            StopPortalJumpLandingVfx(true);
        }).SetUpdate(portalVfxUseUnscaledTime);
    }

    private void ApplyUnscaledTimeToVfx(ParticleSystem root)
    {
        if (root == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems == null || particleSystems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.useUnscaledTime = portalVfxUseUnscaledTime;
        }
    }

    private float EstimateParticleStopDelay(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return 0.6f;
        }

        ParticleSystem.MainModule main = particleSystem.main;
        float delay = main.startDelay.constantMax;
        float duration = Mathf.Max(0.05f, main.duration);
        float lifetime = Mathf.Max(0.05f, main.startLifetime.constantMax);
        return Mathf.Max(0.1f, delay + duration + lifetime);
    }

    private void StopPortalJumpLandingVfx(bool hideObject)
    {
        if (portalJumpVfxStopTween != null)
        {
            portalJumpVfxStopTween.Kill(false);
            portalJumpVfxStopTween = null;
        }

        if (portalJumpLandingVfx == null)
        {
            return;
        }

        portalJumpLandingVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (hideObject)
        {
            GameObject vfxObject = portalJumpLandingVfx.gameObject;
            if (vfxObject != null && vfxObject.activeSelf)
            {
                vfxObject.SetActive(false);
            }
        }
    }

    private void StopPortalVfx()
    {
        if (portalLoopVfx == null)
        {
            return;
        }

        portalLoopVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
