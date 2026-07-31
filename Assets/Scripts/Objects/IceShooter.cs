using DG.Tweening;
using System;
using TMPro;
using UnityEngine;

public class IceShooter : BaseShooter
{
    [SerializeField] public int hitToUnlock;
    [SerializeField] private TextMeshProUGUI hitCountDisplay;
    [SerializeField] private ParticleSystem smokeEffect;
    [SerializeField] private ParticleSystem iceBreakEffect;
    [SerializeField] private GameObject iceMesh;
    [SerializeField] private float hitCountTweenDuration = 0.08f;
    private int currentHits;
    [SerializeField] private float hitCountOffsetY = 0f;
    private bool isFrozen;
    private bool isShaking = false;
    private Tween hitTextTween;

    protected override void OnValidate()
    {
        base.OnValidate();
        AutoAssignVisualReferences();
        EnsureHitCountDisplayRotation();
    }

    public void EnsureHitCountDisplayRotation()
    {
        if (hitCountDisplay != null)
        {
            Vector3 currentEuler = hitCountDisplay.transform.localEulerAngles;
            if (!Mathf.Approximately(currentEuler.x, 20f) || !Mathf.Approximately(currentEuler.z, 180f))
            {
                hitCountDisplay.transform.localEulerAngles = new Vector3(20f, currentEuler.y, 180f);
            }

            if (hitCountDisplay.rectTransform != null)
            {
                Vector2 p = hitCountDisplay.rectTransform.pivot;
                if (!Mathf.Approximately(p.y, 0.65f))
                {
                    hitCountDisplay.rectTransform.pivot = new Vector2(p.x, 0.65f);
                }

                if (hitCountOffsetY != 0f)
                {
                    Vector2 pos = hitCountDisplay.rectTransform.anchoredPosition;
                    pos.y = hitCountOffsetY;
                    hitCountDisplay.rectTransform.anchoredPosition = pos;
                }
            }
            else if (hitCountOffsetY != 0f)
            {
                Vector3 localPos = hitCountDisplay.transform.localPosition;
                localPos.y = hitCountOffsetY;
                hitCountDisplay.transform.localPosition = localPos;
            }
        }
    }

    public void EnsureHitCountDisplayRotationZ()
    {
        EnsureHitCountDisplayRotation();
    }

    private void AutoAssignVisualReferences()
    {
        AutoAssignHitCountDisplay();

        ParticleSystem foundSmoke = FindChildParticleByNameContains("Ice Smoke");
        if (foundSmoke != null)
        {
            smokeEffect = foundSmoke;
        }

        ParticleSystem foundIceBreak = FindChildParticleByNameContains("Ice Break");
        if (foundIceBreak != null)
        {
            iceBreakEffect = foundIceBreak;
        }

        GameObject foundIceMesh = FindChildGameObjectByNameContains("ice_shooter");
        if (foundIceMesh != null)
        {
            iceMesh = foundIceMesh;
        }
    }

    private void AutoAssignHitCountDisplay()
    {
        const string hitTextToken = "Text (TMP) (1)";

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (child.name.IndexOf(hitTextToken, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            TextMeshProUGUI tmpText = child.GetComponent<TextMeshProUGUI>();
            if (tmpText != null)
            {
                hitCountDisplay = tmpText;
            }

            EnsureHitCountDisplayRotationZ();
            return;
        }

        EnsureHitCountDisplayRotationZ();
    }

    private ParticleSystem FindChildParticleByNameContains(string token)
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (child.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            ParticleSystem particle = child.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                return particle;
            }
        }

        return null;
    }

    private GameObject FindChildGameObjectByNameContains(string token)
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (child.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    protected override void Start()
    {
        base.Start();
        EnsureHitCountDisplayRotation();
        isFrozen = hitToUnlock > 0;
        currentHits = 0;
        if (isFrozen)
        {
            SetState(ShooterState.Frozen);
            if (smokeEffect != null) smokeEffect.Play();
            SetShooterVisualsActive(false);
        }
        UpdateHitDisplay(hitToUnlock, false);
        GameEventHub.Instance.AddListener(GameEventType.OnSeedDestroyed, OnSeedDestroyed);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        EnsureHitCountDisplayRotation();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (GameEventHub.Instance != null)
            GameEventHub.Instance.RemoveListener(GameEventType.OnSeedDestroyed, OnSeedDestroyed);
    }

    public override void CheckShooterState(object obj = null)
    {
        if (isFrozen)
        {
            RefreshBlockedStateScale();
            return;
        }
        base.CheckShooterState(obj);
    }

    private void OnSeedDestroyed(object data)
    {
        if (!isFrozen) return;
        int count = data is int n ? n : 0;
        if (count <= 0) return;

        int fromRemaining = Mathf.Max(0, hitToUnlock - currentHits);
        currentHits += count;
        int toRemaining = Mathf.Max(0, hitToUnlock - currentHits);
        UpdateHitDisplay(fromRemaining, toRemaining, true);

        if (currentHits >= hitToUnlock)
            Unfreeze();
    }

    private void Unfreeze()
    {
        isFrozen = false;
        if (smokeEffect != null) smokeEffect.Stop();
        if (iceBreakEffect != null)
        {
            iceBreakEffect.Play();
            AudioManager.Instance?.PlaySFX(Const.iceBreakSFX);
        }
        if (iceMesh != null) iceMesh.SetActive(false);
        SetShooterVisualsActive(true);
        // Đặt về Lock trước để base.CheckShooterState pass guard
        SetState(ShooterState.Lock);
        hitCountDisplay?.gameObject.SetActive(false);

        base.CheckShooterState();

        PlayPunchScaleToBase(new Vector3(0.2f, 0.2f, 0.2f), 0.35f, 6, 0.5f);
    }

    private void SetShooterVisualsActive(bool active)
    {
        Transform visual = GetVisualTransform();
        if (visual != null && visual != transform && visual.gameObject != gameObject)
        {
            visual.gameObject.SetActive(active);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            // Không tắt renderer của iceMesh
            if (iceMesh != null && (r.gameObject == iceMesh || r.transform.IsChildOf(iceMesh.transform)))
            {
                continue;
            }

            // Không tắt renderer của smokeEffect
            if (smokeEffect != null && r.transform.IsChildOf(smokeEffect.transform))
            {
                continue;
            }

            // Không tắt renderer của iceBreakEffect
            if (iceBreakEffect != null && r.transform.IsChildOf(iceBreakEffect.transform))
            {
                continue;
            }

            r.enabled = active;
        }
    }

    private void UpdateHitDisplay(int toValue, bool animate)
    {
        if (hitCountDisplay == null) return;
        int fromValue = int.TryParse(hitCountDisplay.text, out int parsed) ? parsed : toValue;
        UpdateHitDisplay(fromValue, toValue, animate);
    }

    private void UpdateHitDisplay(int fromValue, int toValue, bool animate)
    {
        if (hitCountDisplay == null) return;
        hitTextTween?.Kill();
        if (!animate || fromValue == toValue)
        {
            hitCountDisplay.text = toValue.ToString();
            return;
        }
        hitCountDisplay.text = fromValue.ToString();
        hitTextTween = DOVirtual.Int(fromValue, toValue, hitCountTweenDuration, value =>
        {
            hitCountDisplay.text = value.ToString();
        }).SetEase(Ease.OutQuad);
    }
    public void PlayFrozenShakeAnimation()
    {
        AudioManager.Instance?.PlaySFX(Const.popLockSFX);
        if (isShaking) return;
        isShaking = true;
        transform.DOShakePosition(0.35f, strength: new Vector3(0.08f, 0f, 0f), vibrato: 20, randomness: 0, snapping: false, fadeOut: true)
              .SetEase(Ease.OutQuad)
              .OnComplete(() =>
              {
                  isShaking = false;
              });
    }

}
