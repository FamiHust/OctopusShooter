using DG.Tweening;
using UnityEngine;

public class MagicStoneRowCoin : MonoBehaviour
{
    [Header("Spawn Motion")]
    [SerializeField, Min(0.05f)] private float riseDuration = 0.16f;
    [SerializeField, Min(0.05f)] private float fallDuration = 0.22f;
    [SerializeField, Min(0f)] private float launchHeight = 0.6f;
    [SerializeField, Min(0f)] private float sideScatterRadius = 0.18f;
    [SerializeField, Min(0f)] private float firstBounceHeight = 0.15f;
    [SerializeField, Min(0)] private int bounceCount = 2;
    [SerializeField, Min(0.05f)] private float totalBounceDuration = 0.2f;
    [SerializeField] private Ease riseEase = Ease.OutCubic;
    [SerializeField] private Ease fallEase = Ease.InQuad;
    [SerializeField] private Ease bounceUpEase = Ease.OutQuad;
    [SerializeField] private Ease bounceDownEase = Ease.InQuad;

    [Header("Collect")]
    [SerializeField, Min(0.05f)] private float collectFlyDuration = 0.36f;
    [SerializeField] private Ease collectFlyEase = Ease.InQuad;
    [SerializeField, Min(0.01f)] private float collectPunchDuration = 0.08f;
    [SerializeField, Min(0.2f)] private float collectPunchScale = 1.08f;
    [SerializeField, Min(0.01f)] private float collectEndScale = 0.75f;
    [SerializeField, Min(1)] private int coinRewardValue = 1;
    [SerializeField] private ObjectPoolManager.PoolType selfPoolType = ObjectPoolManager.PoolType.Coin;

    [Header("Tap Collider")]
    [SerializeField] private bool autoAddTapCollider = true;
    [SerializeField, Min(0.05f)] private float tapColliderMinRadius = 0.2f;

    private Sequence spawnSequence;
    private Sequence collectSequence;
    private InGameUIManager inGameUIManager;
    private Camera worldCamera;
    private bool isCollected;

    private void OnEnable()
    {
        isCollected = false;
        EnsureTapCollider();
    }

    private void OnDisable()
    {
        KillTweens();
        inGameUIManager = null;
        worldCamera = null;
        isCollected = false;
    }

    private void OnMouseDown()
    {
        TryCollectByTap();
    }

    public void Configure(InGameUIManager uiManager, Camera cam, int rewardValue)
    {
        inGameUIManager = uiManager != null ? uiManager : InGameUIManager.Instance;
        worldCamera = cam != null ? cam : Camera.main;
        coinRewardValue = Mathf.Max(1, rewardValue);
        isCollected = false;
    }

    public void PlaySpawnMotion()
    {
        KillSpawnSequence();

        Vector3 start = transform.position;
        Vector2 randomCircle = Random.insideUnitCircle * Mathf.Max(0f, sideScatterRadius);
        Vector3 lateral = new Vector3(randomCircle.x, 0f, randomCircle.y);

        Vector3 apex = start + (lateral * 0.4f) + (Vector3.up * Mathf.Max(0f, launchHeight));
        Vector3 landing = start + lateral;

        spawnSequence = DOTween.Sequence().SetUpdate(true);
        spawnSequence.Append(transform.DOMove(apex, Mathf.Max(0.05f, riseDuration)).SetEase(riseEase).SetUpdate(true));
        spawnSequence.Append(transform.DOMove(landing, Mathf.Max(0.05f, fallDuration)).SetEase(fallEase).SetUpdate(true));

        int safeBounceCount = Mathf.Max(0, bounceCount);
        float perBounceDuration = safeBounceCount > 0 ? Mathf.Max(0.03f, totalBounceDuration / safeBounceCount) : 0f;
        for (int i = 0; i < safeBounceCount; i++)
        {
            float bounceHeight = Mathf.Max(0f, firstBounceHeight) * Mathf.Pow(0.55f, i);
            if (bounceHeight <= 0.001f)
            {
                break;
            }

            float upDuration = perBounceDuration * 0.45f;
            float downDuration = perBounceDuration - upDuration;

            spawnSequence.Append(transform.DOMoveY(landing.y + bounceHeight, upDuration).SetEase(bounceUpEase).SetUpdate(true));
            spawnSequence.Append(transform.DOMoveY(landing.y, downDuration).SetEase(bounceDownEase).SetUpdate(true));
        }
    }

    private void TryCollectByTap()
    {
        if (!isActiveAndEnabled || isCollected)
        {
            return;
        }

        isCollected = true;
        KillSpawnSequence();

        if (inGameUIManager == null)
        {
            inGameUIManager = InGameUIManager.Instance;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (inGameUIManager == null || !inGameUIManager.TryGetCoinFlyTargetScreenPosition(out Vector2 targetScreen))
        {
            AwardAndReturn();
            return;
        }

        Vector3 targetWorld = ResolveWorldTargetFromScreen(targetScreen);
        float safeFlyDuration = Mathf.Max(0.05f, collectFlyDuration);

        collectSequence?.Kill(false);
        collectSequence = DOTween.Sequence().SetUpdate(true);
        collectSequence.Append(transform.DOScale(Mathf.Max(0.2f, collectPunchScale), Mathf.Max(0.02f, collectPunchDuration)).SetEase(Ease.OutBack));
        collectSequence.Append(transform.DOMove(targetWorld, safeFlyDuration).SetEase(collectFlyEase).SetUpdate(true));
        collectSequence.Join(transform.DOScale(Mathf.Max(0.01f, collectEndScale), safeFlyDuration * 0.8f).SetEase(Ease.InQuad).SetUpdate(true));
        collectSequence.OnComplete(AwardAndReturn);
    }

    private Vector3 ResolveWorldTargetFromScreen(Vector2 screenPoint)
    {
        if (worldCamera == null)
        {
            return transform.position;
        }

        float z = worldCamera.WorldToScreenPoint(transform.position).z;
        if (z <= 0.01f)
        {
            z = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
            if (z <= 0.01f)
            {
                z = 8f;
            }
        }

        Vector3 target = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, z));
        return target;
    }

    private void AwardAndReturn()
    {
        if (inGameUIManager == null)
        {
            inGameUIManager = InGameUIManager.Instance;
        }

        inGameUIManager?.AddCoinsFromGameplay(Mathf.Max(1, coinRewardValue));
        AudioManager.Instance?.PlaySFX(Const.goldEarnSFX);
        ObjectPoolManager.ReturnObject(gameObject, selfPoolType);
    }

    private void KillSpawnSequence()
    {
        if (spawnSequence != null && spawnSequence.IsActive())
        {
            spawnSequence.Kill(false);
        }

        spawnSequence = null;
    }

    private void KillTweens()
    {
        KillSpawnSequence();

        if (collectSequence != null && collectSequence.IsActive())
        {
            collectSequence.Kill(false);
        }

        collectSequence = null;
    }

    private void EnsureTapCollider()
    {
        if (!autoAddTapCollider)
        {
            return;
        }

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        sphereCollider.isTrigger = true;
        float radius = Mathf.Max(0.05f, tapColliderMinRadius);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                combinedBounds.Encapsulate(renderer.bounds);
            }

            sphereCollider.center = transform.InverseTransformPoint(combinedBounds.center);
            radius = Mathf.Max(radius, combinedBounds.extents.magnitude * 0.75f);
        }

        sphereCollider.radius = radius;
    }
}
