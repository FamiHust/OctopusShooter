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
    private bool isFrozen;
    private bool isShaking = false;
    private Tween hitTextTween;

    protected override void OnValidate()
    {
        base.OnValidate();
        AutoAssignVisualReferences();
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

            return;
        }
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
        isFrozen = hitToUnlock > 0;
        currentHits = 0;
        if (isFrozen)
        {
            SetState(ShooterState.Frozen);
            if (smokeEffect != null) smokeEffect.Play();
        }
        UpdateHitDisplay(hitToUnlock, false);
        GameEventHub.Instance.AddListener(GameEventType.OnSeedDestroyed, OnSeedDestroyed);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (GameEventHub.Instance != null)
            GameEventHub.Instance.RemoveListener(GameEventType.OnSeedDestroyed, OnSeedDestroyed);
    }

    public override void CheckShooterState(object obj = null)
    {
        if (isFrozen) return;   // Không cho kiểm tra path khi còn đông băng
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
        // Đặt về Lock trước để base.CheckShooterState pass guard
        SetState(ShooterState.Lock);
        hitCountDisplay?.gameObject.SetActive(false);

        base.CheckShooterState();
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
