using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Solo.MOST_IN_ONE;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ShooterState
{
    Empty,
    Lock,
    Frozen,    // Bị đóng băng — không tương tác được, đợi đủ hitToUnlock
    Jumping,
    Idle,
    IdleGrid,
    Shooting,
    Disappear,
    Hero,      // Booster: hero mode — bay lên, camera focus, auto-bắn, rồi trở về / disappear
}

public class BaseShooter : MonoBehaviour
{
    private static readonly int outlineColorShaderId = Shader.PropertyToID("_OutlineColor");
    private static readonly int baseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int colorShaderId = Shader.PropertyToID("_Color");

    private struct VfxRendererColorState
    {
        public Renderer renderer;
        public int colorPropertyId;
        public Color baseColor;
    }

    [SerializeField] private Animation animationComponent;
    [SerializeField] private int maxBulletCount = 100;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float bulletDecreaseInterval = 0.25f;
    [SerializeField] private int bulletDecreaseAmount = 5;
    [SerializeField] protected SkinnedMeshRenderer mesh;
    [SerializeField] private TextMeshProUGUI countTextGO;
    [SerializeField, Range(0.1f, 1f)] private float blockedCountTextAlpha = 0.45f;
    [SerializeField] private SeedColor targetColor;
    [SerializeField] protected Transform visualTransform;
    [SerializeField] private bool autoSyncTargetColorFromMaterial = true;
    [SerializeField] private float materialColorSyncInterval = 0.2f;
    [SerializeField] private bool syncOutlineColorFromBaseWhenInDeck = true;
    [SerializeField] private bool autoSyncChildrenLayerFromRoot = true;
    [SerializeField] private bool optimizeShooterRendererForMobile = true;
    [SerializeField] private bool forceEnableShooterShadowCasting = true;
    [SerializeField] private bool disableShooterShadowCasting = true;
    [SerializeField] private bool disableShooterReceiveShadows = true;
    [SerializeField] private bool disableShooterLightProbes = true;
    [SerializeField] private bool disableShooterReflectionProbes = true;

    [Header("Adaptive Shooter Rendering")]
    [SerializeField] private bool adaptiveShooterRendering = true;
    [SerializeField] private bool allowAggressiveShooterCullingRuntime = false;
    [SerializeField, Min(0.05f)] private float shooterRenderRefreshInterval = 0.2f;
    [SerializeField] private bool optimizeShooterMaterialByState = true;
    [SerializeField] private bool hideSecondaryRenderersWhenNotCombat = true;
    [SerializeField] private bool simplifyPrimaryMaterialWhenNotCombat = false;
    [SerializeField] private bool cullShooterWhenOffscreen = true;
    [SerializeField] private float shooterCullViewportPadding = 0.08f;
    [SerializeField] private bool reduceSecondaryRenderersWhenFar = true;
    [SerializeField] private float secondaryRendererHideDistance = 9f;
    [SerializeField] private bool adaptiveShooterShadowByDistance = true;
    [SerializeField] private float shooterShadowDistance = 7f;
    [SerializeField] private bool enforceShooterDetailBudget = false;
    [SerializeField, Min(1)] private int maxFullDetailShootersOnScreen = 6;
    [SerializeField] private bool enforceVisibleShooterBudget = false;
    [SerializeField, Min(1)] private int maxVisibleShootersOnScreen = 10;
    [SerializeField, Min(0.05f)] private float shooterDetailBudgetRefreshInterval = 0.2f;
    [SerializeField] private bool hideLowPriorityShootersOverBudget = true;
    [SerializeField, Min(1f)] private float lowPriorityHideDistanceWhenOverBudget = 6f;
    [SerializeField] private bool alwaysKeepHiddenShooterVisible = true;
    [SerializeField] private bool alwaysKeepHiddenShooterShadow = true;

    // Run time variables
    [SerializeField] private ShooterState currentState = ShooterState.Empty;
    private ShooterState previousState;
    private int bulletCount;

    [Header("State Scaling Settings")]
    [SerializeField] private float blockedStateScaleMultiplier = 0.85f;
    [SerializeField] private float stateScaleTweenDuration = 0.2f;

    private Vector3 baseLocalScale = Vector3.one;
    private bool isBaseLocalScaleCached = false;
    private Tween stateScaleTween;

    private static int activeHeroCount = 0;
    public static bool IsAnyHeroActive => activeHeroCount > 0;

    [Header("References")]
    [SerializeField] private SlotBar slotBar;
    [SerializeField] private GridController gridController;
    [SerializeField] protected float slotLandingYOffset = 0.2f;
    private Collider shooterCollider;
    private GridItem gridItem;

    [Header("Shooting System")]
    [SerializeField] private BaseShooterCombatConfig combatConfig;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRate = 20f;
    [SerializeField] private int bulletsPerShot = 2;
    [SerializeField] private float bulletSpreadShotDelay = 0.02f;
    [SerializeField] private float seedShotInterval = 0.08f;
    [SerializeField] private float bulletSpreadRadius = 0.2f;
    [SerializeField] private bool enforceVisualBulletFrameBudget = true;
    [SerializeField, Min(1)] private int maxVisualBulletsPerFrame = 90;
    [SerializeField] private float rowHandoffDelay = 0f;
    [SerializeField, Min(0.02f)] private float targetQueueRefreshInterval = 0.08f;
    [SerializeField, Min(0.05f)] private float targetQueueFallbackRefreshInterval = 0.22f;
    [SerializeField, Range(0.75f, 1f)] private float targetQueueRefreshIntervalScale = 0.9f;
    [Header("Shooting Audio")]
    [SerializeField] private ShooterAudioConfig shootAudioConfig;
    [SerializeField] private bool useSimulatedShootSfx = true;
    [SerializeField, Min(0.05f)] private float simulatedShootSfxInterval = 0.12f;
    [SerializeField, Min(0.02f)] private float animationSpeedRefreshInterval = 0.08f;

    private float rotateTimer = 0;
    private float bulletSpawnTimer = 0f;
    private float bulletDecreaseTimer;
    private BlockRowSeedSpawner targetObject;
    private BlockRowSeedSpawner activeDestroyRow;
    private bool isDestroySequenceRequested;
    private int magicStoneLocalShotComboCount;
    private bool hasSpawnedMagicStoneInCurrentShootingState;
    private int cachedMagicStoneShotThreshold = 50;
    private int cachedMagicStoneShotThresholdSafe = 50;
    private float cachedMagicStoneComboBreakGapSeconds = 0.6f;
    private float magicStoneLastShotTime = -999f;
    private bool cachedSpawnMagicStoneOncePerShootingState = true;
    private bool isMagicStoneFeatureEnabled;
    private bool hasCachedMagicStonePrefabMetadata;
    private GameObject cachedMagicStonePrefabForMetadata;
    private bool cachedMagicStonePrefabHasComponent;
    private GameObject activeMagicStoneComboShooterVfx;
    private Tween magicStoneComboShooterVfxFadeTween;
    private Tween magicStoneComboShooterVfxReleaseTween;
    private readonly List<VfxRendererColorState> magicStoneComboShooterVfxColorStates = new List<VfxRendererColorState>(8);
    private MaterialPropertyBlock magicStoneComboShooterVfxPropertyBlock;

    private float mul => SpeedMultiplierManager.Instance.GetCurrentMultiplier();

    private readonly Queue<BlockRowSeedSpawner> targetQueue = new Queue<BlockRowSeedSpawner>();
    private HashSet<BlockRowSeedSpawner> queuedTargets = new HashSet<BlockRowSeedSpawner>();
    private float nextTargetQueueRefreshTime;
    private float nextTargetQueueFallbackRefreshTime;
    private int lastDetectorTargetsStateVersion = -1;
    private float nextAnimationSpeedRefreshTime;
    private float lastAppliedAnimationSpeed = -1f;

    public GameObject disappearParticle;
    public GameObject jumpDisappearParticle;
    public GameObject jumpEffect;
    
    private Tween idleTween;
    private Tween boosterHighlightTween;

    [Header("Recoil Effect")]
    [SerializeField] private float recoilDistance = 0.2f; // Độ giật lùi
    [SerializeField] private float recoilDuration = 0.05f; // Tốc độ giật (Càng nhỏ càng giật nhanh)

    private Vector3 originalVisualLocalPos;
    private Quaternion originalVisualLocalRot = Quaternion.identity;
    private Sequence recoilSequence;
    private Tween bulletTextTween;

    // ─── Count text tracking ──────────────────────────────────────────
    private Vector3 countTextOffset = Vector3.zero; // Offset từ shooter
    private Quaternion countTextRotationOffset = Quaternion.identity;
    private bool isCountTextInitialized = false;

    // ─── Hero booster mode ────────────────────────────────────────────
    private bool isInHeroMode = false;
    private bool isHeroReturning = false;
    private HeroShooterBoosterConfig heroCfg;
    private Transform heroSlotParent;
    private Vector3 heroSlotWorldPos;
    private Vector3 heroSlotLocalScale = Vector3.one;
    private Transform heroCameraRoot;
    private Vector3 heroOrigCamRootPos;
    private Vector3 heroOrigCamPos;
    private Vector3 heroOrigCamLocalPos;
    private float heroOrigCamSize;
    [SerializeField, Min(0f)] private float heroCameraEdgePaddingOrtho = 0.35f;
    private readonly List<Camera> heroRigCameras = new List<Camera>();
    private readonly List<float> heroRigCameraSizes = new List<float>();
    private readonly List<Transform> heroRigCameraTransforms = new List<Transform>();
    private readonly List<Vector3> heroRigCameraOrigPositions = new List<Vector3>();
    private readonly List<Vector3> heroRigCameraOrigLocalPositions = new List<Vector3>();
    private Camera _heroCamera;
    private Quaternion originTextRotation;
    private float nextMaterialColorSyncTime;
    private Color lastSyncedMaterialColor;
    private bool hasLastSyncedMaterialColor;
    private MaterialPropertyBlock outlinePropertyBlock;
    private Color lastAppliedOutlineColor;
    private bool hasLastAppliedOutlineColor;
    private const float shootSfxMinPitch = 1f;
    private const float shootSfxMaxPitch = 2f;
    private const int shootSfxPitchRampSteps = 50;
    private bool hasPlayedShootSfxSinceReset;
    private static float globalShootSfxStreakStartTime = -999f;
    private static float globalLastShootSfxPlayTime = -999f;
    private Renderer[] cachedShooterRenderers;
    private Renderer cachedPrimaryShooterRenderer;
    private Material[] cachedPrimaryOriginalSharedMaterials;
    private Material[] cachedPrimaryLowDetailSharedMaterials;
    private bool isPrimaryMaterialInLowDetailMode;
    private float nextShooterRenderRefreshTime;
    private Camera cachedShooterRenderCamera;
    private static readonly List<BaseShooter> registeredShooters = new List<BaseShooter>(128);
    private static readonly List<BaseShooter> shooterDetailCandidates = new List<BaseShooter>(128);
    private static readonly HashSet<int> fullDetailShooterIds = new HashSet<int>();
    private static readonly HashSet<int> visibleShooterIds = new HashSet<int>();
    private static float nextShooterDetailBudgetRefreshTime;
    private static int visualBulletBudgetFrame = -1;
    private static int visualBulletBudgetUsed;
    private static int pendingMagicStoneRewardForCurrentLevel;
    private const float defaultDeckOutlineDarkenAmount = 0.16f;
    private const float defaultDeckLandingScaleMultiplier = 1.05f;
    private const float magicStoneSpawnSideOffset = 0.08f;

   
    private void Awake()
    {
        gridItem = GetComponent<GridItem>();
    
    }

    protected virtual void OnEnable()
    {
        RegisterShooter(this);
        RefreshMagicStoneRuntimeConfig();
    }

    protected virtual void OnDisable()
    {
        UnregisterShooter(this);
        stateScaleTween?.Kill();
        CancelMagicStoneComboShooterVfxRelease();
        ResetMagicStoneShotStreak();

        if (isInHeroMode)
        {
            isInHeroMode = false;
            activeHeroCount = Mathf.Max(0, activeHeroCount - 1);
        }
    }

    protected virtual void Start()
    {
        SyncTargetColorFromMaterial(true);
        GetVisualTransform();
        SyncChildrenLayerFromRoot();
        OptimizeShooterRenderersForMobile();
        CacheShooterRenderers();
        RefreshAdaptiveShooterRendering(true);
        bulletCount = maxBulletCount;
        if (countTextGO != null)
        {
            originTextRotation = countTextGO.transform.rotation;
        }
        if (transform.parent == null || transform.parent.GetComponent<Slot>() == null)
        {
            transform.localRotation = Quaternion.Euler(20f, 180f, 0f);
        }
        if (countTextGO != null)
        {
            InitializeCountTextOffset();
        }
        shooterCollider = GetComponent<Collider>();
        GameEventHub.Instance.AddListener(GameEventType.OnShooterJumpStart, OnJumpStart);
        UpdateBulletCountText();
        UpdateCountTextVisibilityAndAlpha();
        Transform recoilTransform = ResolveRecoilTransform();
        if (recoilTransform != null)
        {
            // Lưu lại pose local ban đầu của visual để recoil luôn hồi đúng gốc.
            originalVisualLocalPos = recoilTransform.localPosition;
            originalVisualLocalRot = recoilTransform.localRotation;
        }
    }

    protected virtual void OnValidate()
    {
        GetAnimationComponent();
        GetShootPoint();
        GetMeshRenderer();
        SyncTargetColorFromMaterial(true);
        GetGridController();
        GetVisualTransform();
        ResolveSlotBarReference(false, true);
        GetTMPComponent();
        TryAutoAssignCombatConfig();
        TryAutoAssignShootAudioConfig();
        SyncChildrenLayerFromRoot();
        OptimizeShooterRenderersForMobile();
        CacheShooterRenderers();
    }

    private void OptimizeShooterRenderersForMobile()
    {
        if (!optimizeShooterRendererForMobile)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (forceEnableShooterShadowCasting)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            else if (disableShooterShadowCasting)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (disableShooterReceiveShadows)
            {
                if (renderer is MeshRenderer meshRenderer)
                {
                    meshRenderer.receiveShadows = false;
                }
                else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    skinnedMeshRenderer.receiveShadows = false;
                }
            }

            if (disableShooterLightProbes)
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }

            if (disableShooterReflectionProbes)
            {
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            Material[] mats = renderer.sharedMaterials;
            if (mats == null)
            {
                continue;
            }

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat != null && !mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                }
            }
        }
    }

    private void SyncChildrenLayerFromRoot()
    {
        if (!autoSyncChildrenLayerFromRoot)
        {
            return;
        }

        int rootLayer = gameObject.layer;
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        if (childTransforms == null)
        {
            return;
        }

        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child == null)
            {
                continue;
            }

            if (child.gameObject.layer != rootLayer)
            {
                child.gameObject.layer = rootLayer;
            }
        }
    }

    private void TryAutoAssignCombatConfig()
    {
        if (combatConfig != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:BaseShooterCombatConfig");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            BaseShooterCombatConfig foundConfig = AssetDatabase.LoadAssetAtPath<BaseShooterCombatConfig>(path);
            if (foundConfig != null)
            {
                combatConfig = foundConfig;
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }

    private void TryAutoAssignShootAudioConfig()
    {
        if (shootAudioConfig != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:ShooterAudioConfig");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ShooterAudioConfig foundConfig = AssetDatabase.LoadAssetAtPath<ShooterAudioConfig>(path);
            if (foundConfig != null)
            {
                shootAudioConfig = foundConfig;
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }

    private void GetTMPComponent()
    {
        if (countTextGO == null)
        {
            countTextGO = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void GetAnimationComponent()
    {
        if (transform.childCount > 0)
        {
            animationComponent = transform.GetChild(0).GetComponent<Animation>();
        }
    }

    private void GetShootPoint()
    {
        if (transform.childCount > 0)
        {
            shootPoint = transform.GetChild(0);
            if (shootPoint != null)
            {
                if (shootPoint.childCount > 0)
                {
                    shootPoint = shootPoint.GetChild(0).transform;
                }
            }
        }
    }

    private void GetMeshRenderer()
    {
        if (transform.childCount > 0)
        {
            mesh = transform.GetChild(0).GetComponentInChildren<SkinnedMeshRenderer>();
        }
    }
    private void GetGridController()
    {
        if (gridController == null)
        {
            gridController = GetComponentInParent<GridController>();
        }
    }

    protected Transform GetVisualTransform()
    {
        if (visualTransform == null || visualTransform == transform)
        {
            if (mesh != null && mesh.transform != null && mesh.transform != transform)
            {
                visualTransform = mesh.transform;
            }
            else if (transform.childCount > 0)
            {
                visualTransform = transform.GetChild(0);
            }
        }
        return visualTransform;
    }

    private Transform ResolveRecoilTransform()
    {
        if (visualTransform == null || visualTransform == transform)
        {
            GetVisualTransform();
        }

        if (visualTransform != null && visualTransform != transform)
        {
            return visualTransform;
        }

        if (mesh != null && mesh.transform != null && mesh.transform != transform)
        {
            return mesh.transform;
        }

        return null;
    }

    private void ResolveSlotBarReference(bool allowSceneFallback = true, bool forceRebind = false)
    {
        if (!forceRebind && slotBar != null)
        {
            return;
        }

        slotBar = FindSlotBarInSamePrefabScope();
        if (slotBar != null)
        {
            return;
        }

        Transform parent = transform.parent;
        if (parent != null)
        {
            Transform parentLevel = parent.parent;
            if (parentLevel != null)
            {
                for (int i = 0; i < parentLevel.childCount; i++)
                {
                    Transform siblingOfParent = parentLevel.GetChild(i);
                    if (siblingOfParent == null || siblingOfParent == parent)
                    {
                        continue;
                    }

                    SlotBar found = siblingOfParent.GetComponent<SlotBar>();
                    if (found == null)
                    {
                        found = siblingOfParent.GetComponentInChildren<SlotBar>(true);
                    }

                    if (found != null)
                    {
                        slotBar = found;
                        return;
                    }
                }
            }
        }

        // Fallback for non-standard hierarchies.
        if (allowSceneFallback)
        {
            slotBar = SlotBar.Instance;
        }
    }

    private SlotBar FindSlotBarInSamePrefabScope()
    {
        Transform current = transform;
        while (current != null)
        {
            SlotBar found = current.GetComponentInChildren<SlotBar>(true);
            if (found != null)
            {
                return found;
            }

            current = current.parent;
        }

        return null;
    }

    private void SyncTargetColorFromMaterial(bool force = false)
    {
        if (!autoSyncTargetColorFromMaterial || mesh == null)
        {
            return;
        }

        Material mat = mesh.sharedMaterial;
        if (mat == null)
        {
            return;
        }

        Color baseColor;
        if (!TryGetMaterialBaseColor(mat, out baseColor))
        {
            return;
        }

        if (!force && hasLastSyncedMaterialColor)
        {
            float dr = baseColor.r - lastSyncedMaterialColor.r;
            float dg = baseColor.g - lastSyncedMaterialColor.g;
            float db = baseColor.b - lastSyncedMaterialColor.b;
            float da = baseColor.a - lastSyncedMaterialColor.a;
            float sqrDiff = (dr * dr) + (dg * dg) + (db * db) + (da * da);
            if (sqrDiff <= 0.0001f)
            {
                return;
            }
        }

        lastSyncedMaterialColor = baseColor;
        hasLastSyncedMaterialColor = true;

        targetColor = GetClosestSeedColor(baseColor);
    }

    private bool TryGetMaterialBaseColor(Material mat, out Color color)
    {
        color = Color.white;
        if (mat == null)
        {
            return false;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            color = mat.GetColor("_BaseColor");
            return true;
        }

        if (mat.HasProperty("_Color"))
        {
            color = mat.GetColor("_Color");
            return true;
        }

        return false;
    }

    private void SyncOutlineColorFromBaseMaterial()
    {
        if (!syncOutlineColorFromBaseWhenInDeck || mesh == null)
        {
            return;
        }

        Material sourceMaterial = mesh.sharedMaterial;
        if (sourceMaterial == null)
        {
            return;
        }

        Color baseColor;
        if (!TryGetMaterialBaseColor(sourceMaterial, out baseColor))
        {
            return;
        }

        Material[] sharedMaterials = mesh.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            return;
        }

        bool hasOutlineProperty = false;
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material mat = sharedMaterials[i];
            if (mat != null && mat.HasProperty(outlineColorShaderId))
            {
                hasOutlineProperty = true;
                break;
            }
        }

        if (!hasOutlineProperty)
        {
            return;
        }

        Color deckOutlineColor = Color.Lerp(baseColor, Color.black, GetDeckOutlineDarkenAmount());
        deckOutlineColor.a = baseColor.a;

        if (hasLastAppliedOutlineColor)
        {
            float dr = lastAppliedOutlineColor.r - deckOutlineColor.r;
            float dg = lastAppliedOutlineColor.g - deckOutlineColor.g;
            float db = lastAppliedOutlineColor.b - deckOutlineColor.b;
            float da = lastAppliedOutlineColor.a - deckOutlineColor.a;
            float sqrDiff = (dr * dr) + (dg * dg) + (db * db) + (da * da);
            if (sqrDiff <= 0.0001f)
            {
                return;
            }
        }

        if (outlinePropertyBlock == null)
        {
            outlinePropertyBlock = new MaterialPropertyBlock();
        }

        mesh.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetColor(outlineColorShaderId, deckOutlineColor);
        mesh.SetPropertyBlock(outlinePropertyBlock);

        lastAppliedOutlineColor = deckOutlineColor;
        hasLastAppliedOutlineColor = true;
    }

    private SeedColor GetClosestSeedColor(Color source)
    {
        if (IsMagentaLike(source))
        {
            return SeedColor.Pink;
        }

        Color.RGBToHSV(source, out float srcH, out float srcS, out float srcV);
        if (IsGrayLike(source, srcS, srcV))
        {
            return SeedColor.Gray;
        }

        if (IsWhiteLike(source, srcS, srcV))
        {
            return SeedColor.White;
        }

        if (IsBrownLike(source, srcH, srcS, srcV))
        {
            return SeedColor.Brown;
        }

        bool sourceLooksAqua = IsAquaLike(source, srcH, srcS, srcV);

        SeedColor[] candidates =
        {
            SeedColor.Blue,
            SeedColor.Red,
            SeedColor.Yellow,
            SeedColor.Green,
            SeedColor.Purple,
            SeedColor.Pink,
            SeedColor.Orange,
            SeedColor.Hidden,
            SeedColor.Aqua,
            SeedColor.Brown,
            SeedColor.Cyan,
            SeedColor.Gray,
            SeedColor.HotPink,
            SeedColor.White
        };

        bool sourceLooksChromatic = srcS >= 0.18f;
        SeedColor best = candidates[0];
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            SeedColor candidate = candidates[i];
            Color palette = ColorInfo.GetTargetMatchColor(candidate);

            if (sourceLooksChromatic && IsNeutralSeedColor(candidate))
            {
                continue;
            }

            if (candidate == SeedColor.Aqua && !sourceLooksAqua)
            {
                continue;
            }

            Color.RGBToHSV(palette, out float pH, out float pS, out float pV);
            float hueDelta = GetCircularHueDistance(srcH, pH);
            float satDelta = Mathf.Abs(srcS - pS);
            float valDelta = Mathf.Abs(srcV - pV);
            float valWeight = sourceLooksChromatic ? 0.15f : 0.35f;

            // HSV score ưu tiên hue/saturation để tránh Blue bị kéo về Gray.
            float hsvScore = (hueDelta * 2.2f) + (satDelta * 1.2f) + (valDelta * valWeight);

            // RGB score giữ vai trò tie-break khi màu gần nhau.
            float dr = source.r - palette.r;
            float dg = source.g - palette.g;
            float db = source.b - palette.b;
            float rgbScore = (dr * dr) + (dg * dg) + (db * db);

            float finalScore = hsvScore + (rgbScore * 0.25f);
            if (finalScore < bestScore)
            {
                bestScore = finalScore;
                best = candidate;
            }
        }

        return best;
    }

    private bool IsMagentaLike(Color source)
    {
        // Ưu tiên hard-rule cho nhóm magenta/pink để tránh match nhầm Purple.
        float minR = 0.85f;
        float minB = 0.85f;
        float maxG = 0.2f;
        float rbDelta = Mathf.Abs(source.r - source.b);

        return source.r >= minR &&
               source.b >= minB &&
               source.g <= maxG &&
               rbDelta <= 0.2f;
    }

    private bool IsAquaLike(Color source, float srcH, float srcS, float srcV)
    {
        if (srcS < 0.35f || srcV < 0.35f)
        {
            return false;
        }

        bool hueInAquaRange = srcH >= 0.39f && srcH <= 0.48f;
        if (!hueInAquaRange)
        {
            return false;
        }

        float greenOverBlue = source.g - source.b;
        float greenOverRed = source.g - source.r;
        return greenOverBlue >= 0.08f && greenOverRed >= 0.1f;
    }

    private bool IsWhiteLike(Color source, float srcS, float srcV)
    {
        if (srcV < 0.74f || srcS > 0.2f)
        {
            return false;
        }

        float maxChannel = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
        float minChannel = Mathf.Min(source.r, Mathf.Min(source.g, source.b));
        float channelSpread = maxChannel - minChannel;
        return channelSpread <= 0.14f;
    }

    private bool IsGrayLike(Color source, float srcS, float srcV)
    {
        if (srcV < 0.22f || srcV > 0.8f)
        {
            return false;
        }

        if (srcS > 0.14f)
        {
            return false;
        }

        float maxChannel = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
        float minChannel = Mathf.Min(source.r, Mathf.Min(source.g, source.b));
        float channelSpread = maxChannel - minChannel;
        return channelSpread <= 0.1f;
    }

    private bool IsBrownLike(Color source, float srcH, float srcS, float srcV)
    {
        if (srcS < 0.25f || srcV < 0.15f || srcV > 0.72f)
        {
            return false;
        }

        bool hueInBrownRange = srcH >= 0.03f && srcH <= 0.12f;
        if (!hueInBrownRange)
        {
            return false;
        }

        if (!(source.r > source.g && source.g > source.b))
        {
            return false;
        }

        float redOverGreen = source.r - source.g;
        float greenOverBlue = source.g - source.b;
        return redOverGreen <= 0.3f && greenOverBlue >= 0.03f;
    }

    private bool IsNeutralSeedColor(SeedColor color)
    {
        return color == SeedColor.Gray || color == SeedColor.White || color == SeedColor.Hidden;
    }

    private float GetCircularHueDistance(float a, float b)
    {
        float delta = Mathf.Abs(a - b);
        return Mathf.Min(delta, 1f - delta);
    }

    protected virtual void LateUpdate()
    {
        // Cập nhật vị trí và rotation của count text theo shooter
        UpdateCountTextTransform();
        RefreshAdaptiveShooterRendering();
    }

    private void CacheShooterRenderers()
    {
        cachedShooterRenderers = GetComponentsInChildren<Renderer>(true);
        cachedPrimaryShooterRenderer = GetPrimaryShooterRenderer();
        CachePrimaryMaterialLodData();
    }

    private void CachePrimaryMaterialLodData()
    {
        if (cachedPrimaryShooterRenderer == null)
        {
            cachedPrimaryOriginalSharedMaterials = null;
            cachedPrimaryLowDetailSharedMaterials = null;
            isPrimaryMaterialInLowDetailMode = false;
            return;
        }

        cachedPrimaryOriginalSharedMaterials = cachedPrimaryShooterRenderer.sharedMaterials;
        // Nếu Shooter có từ 2 Material trở lên (ví dụ Material màu và Material mắt), tuyệt đối không được cắt giảm bớt Material làm mất mắt.
        if (cachedPrimaryOriginalSharedMaterials != null && cachedPrimaryOriginalSharedMaterials.Length == 1)
        {
            cachedPrimaryLowDetailSharedMaterials = new[] { cachedPrimaryOriginalSharedMaterials[0] };
        }
        else
        {
            cachedPrimaryLowDetailSharedMaterials = null;
        }

        isPrimaryMaterialInLowDetailMode = false;
    }

    private static void RegisterShooter(BaseShooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (!registeredShooters.Contains(shooter))
        {
            registeredShooters.Add(shooter);
        }
    }

    private static void UnregisterShooter(BaseShooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        registeredShooters.Remove(shooter);
        fullDetailShooterIds.Remove(shooter.GetInstanceID());
        visibleShooterIds.Remove(shooter.GetInstanceID());
    }

    public static int FillRegisteredShooterBuffer(List<BaseShooter> output, bool includeInactive = true)
    {
        if (output == null)
        {
            return 0;
        }

        output.Clear();
        for (int i = registeredShooters.Count - 1; i >= 0; i--)
        {
            BaseShooter shooter = registeredShooters[i];
            if (shooter == null)
            {
                registeredShooters.RemoveAt(i);
                continue;
            }

            if (!includeInactive && !shooter.isActiveAndEnabled)
            {
                continue;
            }

            output.Add(shooter);
        }

        return output.Count;
    }

    private void RefreshGlobalShooterDetailBudget(Camera renderCamera, bool force)
    {
        if (!enforceShooterDetailBudget && !enforceVisibleShooterBudget)
        {
            return;
        }

        if (!force && Time.time < nextShooterDetailBudgetRefreshTime)
        {
            return;
        }

        nextShooterDetailBudgetRefreshTime = Time.time + Mathf.Max(0.05f, shooterDetailBudgetRefreshInterval);

        for (int i = registeredShooters.Count - 1; i >= 0; i--)
        {
            if (registeredShooters[i] == null)
            {
                registeredShooters.RemoveAt(i);
            }
        }

        fullDetailShooterIds.Clear();
    visibleShooterIds.Clear();
        shooterDetailCandidates.Clear();

        if (registeredShooters.Count == 0)
        {
            return;
        }

        Camera budgetCamera = renderCamera != null ? renderCamera : Camera.main;
        int detailBudgetCount = Mathf.Max(1, maxFullDetailShootersOnScreen);
        int visibleBudgetCount = Mathf.Max(1, maxVisibleShootersOnScreen);

        for (int i = 0; i < registeredShooters.Count; i++)
        {
            BaseShooter shooter = registeredShooters[i];
            if (shooter == null || !shooter.isActiveAndEnabled)
            {
                continue;
            }

            if (shooter.cachedShooterRenderers == null || shooter.cachedShooterRenderers.Length == 0)
            {
                shooter.CacheShooterRenderers();
            }

            bool isHiddenShooter = shooter.alwaysKeepHiddenShooterVisible && shooter is HiddenShooter;
            bool isHighPriority = shooter.IsHighPriorityRenderState();

            if (isHighPriority || isHiddenShooter)
            {
                visibleShooterIds.Add(shooter.GetInstanceID());
            }

            if (isHighPriority)
            {
                fullDetailShooterIds.Add(shooter.GetInstanceID());
                continue;
            }

            if (budgetCamera != null && shooter.cullShooterWhenOffscreen && !shooter.IsShooterInViewport(budgetCamera))
            {
                continue;
            }

            shooterDetailCandidates.Add(shooter);
        }

        if (shooterDetailCandidates.Count <= 0)
        {
            return;
        }

        if (budgetCamera != null)
        {
            shooterDetailCandidates.Sort((a, b) =>
            {
                float da = (a.transform.position - budgetCamera.transform.position).sqrMagnitude;
                float db = (b.transform.position - budgetCamera.transform.position).sqrMagnitude;
                return da.CompareTo(db);
            });
        }

        int visibleRemaining = Mathf.Max(0, visibleBudgetCount - visibleShooterIds.Count);
        int keepVisibleCount = Mathf.Min(visibleRemaining, shooterDetailCandidates.Count);
        for (int i = 0; i < keepVisibleCount; i++)
        {
            BaseShooter shooter = shooterDetailCandidates[i];
            if (shooter != null)
            {
                visibleShooterIds.Add(shooter.GetInstanceID());
            }
        }

        if (!enforceShooterDetailBudget)
        {
            return;
        }

        int detailRemaining = Mathf.Max(0, detailBudgetCount - fullDetailShooterIds.Count);
        int keepDetailCount = Mathf.Min(detailRemaining, shooterDetailCandidates.Count);
        for (int i = 0; i < keepDetailCount; i++)
        {
            BaseShooter shooter = shooterDetailCandidates[i];
            if (shooter != null)
            {
                fullDetailShooterIds.Add(shooter.GetInstanceID());
            }
        }
    }

    private bool IsInFullDetailBudget(bool highPriorityState)
    {
        if (!enforceShooterDetailBudget)
        {
            return true;
        }

        if (highPriorityState)
        {
            return true;
        }

        return fullDetailShooterIds.Contains(GetInstanceID());
    }

    private bool IsInVisibleBudget(bool highPriorityState, bool isHiddenShooter)
    {
        if (!enforceVisibleShooterBudget)
        {
            return true;
        }

        if (highPriorityState || isHiddenShooter)
        {
            return true;
        }

        return visibleShooterIds.Contains(GetInstanceID());
    }

    private void RefreshAdaptiveShooterRendering(bool force = false)
    {
        if (!adaptiveShooterRendering)
        {
            return;
        }

        if (!force && Time.time < nextShooterRenderRefreshTime)
        {
            return;
        }

        nextShooterRenderRefreshTime = Time.time + Mathf.Max(0.05f, shooterRenderRefreshInterval);

        if (cachedShooterRenderers == null || cachedShooterRenderers.Length == 0)
        {
            CacheShooterRenderers();
        }

        if (cachedShooterRenderers == null || cachedShooterRenderers.Length == 0)
        {
            return;
        }

        bool highPriorityState = IsHighPriorityRenderState();
        bool isHiddenShooter = alwaysKeepHiddenShooterVisible && this is HiddenShooter;

        // Stable path: keep shooters visible and shadow-casting to avoid flicker/off artifacts.
        if (!allowAggressiveShooterCullingRuntime)
        {
            RestoreStableShooterRendering(highPriorityState, isHiddenShooter);
            return;
        }

        Camera renderCamera = ResolveShooterRenderCamera();
        RefreshGlobalShooterDetailBudget(renderCamera, force);
        bool inVisibleBudget = IsInVisibleBudget(highPriorityState, isHiddenShooter);
        bool inFullDetailBudget = IsInFullDetailBudget(highPriorityState);
        float sqrDistanceToCamera = GetSqrDistanceToCamera(renderCamera);

        bool isOnScreen = !cullShooterWhenOffscreen || IsShooterInViewport(renderCamera);
        bool shouldRenderShooter = (highPriorityState || isOnScreen || isHiddenShooter) && inVisibleBudget;
        if (hideLowPriorityShootersOverBudget && !highPriorityState && !isHiddenShooter && !inFullDetailBudget)
        {
            float maxDistance = Mathf.Max(1f, lowPriorityHideDistanceWhenOverBudget);
            shouldRenderShooter = isOnScreen && sqrDistanceToCamera <= maxDistance * maxDistance;
        }

        bool keepSecondaryRenderers = ShouldKeepSecondaryRenderersForCurrentState(highPriorityState, isHiddenShooter) ||
                          !reduceSecondaryRenderersWhenFar ||
                                      (inFullDetailBudget && sqrDistanceToCamera <= Mathf.Max(0f, secondaryRendererHideDistance) * Mathf.Max(0f, secondaryRendererHideDistance));

        for (int i = 0; i < cachedShooterRenderers.Length; i++)
        {
            Renderer renderer = cachedShooterRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool isPrimaryRenderer = cachedPrimaryShooterRenderer == null || renderer == cachedPrimaryShooterRenderer;
            bool shouldEnableRenderer = shouldRenderShooter && (isPrimaryRenderer || keepSecondaryRenderers);
            if (!shouldEnableRenderer && ShouldKeepRendererVisibleForCurrentState(renderer, highPriorityState, isHiddenShooter))
            {
                shouldEnableRenderer = true;
            }

            if (renderer.enabled != shouldEnableRenderer)
            {
                renderer.enabled = shouldEnableRenderer;
            }

            ApplyAdaptiveShadowState(renderer, highPriorityState, isHiddenShooter, inFullDetailBudget, sqrDistanceToCamera);
        }
    }

    private void RestoreStableShooterRendering(bool highPriorityState, bool isHiddenShooter)
    {
        if (cachedShooterRenderers == null || cachedShooterRenderers.Length == 0)
        {
            return;
        }

        bool keepSecondaryRenderers = ShouldKeepSecondaryRenderersForCurrentState(highPriorityState, isHiddenShooter) ||
                                      !hideSecondaryRenderersWhenNotCombat;

        bool usePrimaryLowDetailMaterial = optimizeShooterMaterialByState &&
                                           simplifyPrimaryMaterialWhenNotCombat &&
                                           !isHiddenShooter &&
                                           !highPriorityState;

        ApplyPrimaryMaterialLod(usePrimaryLowDetailMaterial, isHiddenShooter);

        for (int i = 0; i < cachedShooterRenderers.Length; i++)
        {
            Renderer renderer = cachedShooterRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool isPrimaryRenderer = cachedPrimaryShooterRenderer == null || renderer == cachedPrimaryShooterRenderer;
            bool shouldEnableRenderer = isPrimaryRenderer || keepSecondaryRenderers;
            if (!shouldEnableRenderer && ShouldKeepRendererVisibleForCurrentState(renderer, highPriorityState, isHiddenShooter))
            {
                shouldEnableRenderer = true;
            }

            if (renderer.enabled != shouldEnableRenderer)
            {
                renderer.enabled = shouldEnableRenderer;
            }

            if (!shouldEnableRenderer)
            {
                continue;
            }

            if (forceEnableShooterShadowCasting)
            {
                if (renderer.shadowCastingMode != ShadowCastingMode.On)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }
        }
    }

    private void ApplyPrimaryMaterialLod(bool useLowDetail, bool isHiddenShooter)
    {
        if (!optimizeShooterMaterialByState || isHiddenShooter)
        {
            if (isPrimaryMaterialInLowDetailMode)
            {
                RestorePrimaryOriginalMaterials();
            }
            return;
        }

        if (cachedPrimaryShooterRenderer == null)
        {
            return;
        }

        if (useLowDetail)
        {
            if (isPrimaryMaterialInLowDetailMode)
            {
                return;
            }

            if (cachedPrimaryLowDetailSharedMaterials == null || cachedPrimaryLowDetailSharedMaterials.Length == 0)
            {
                return;
            }

            cachedPrimaryShooterRenderer.sharedMaterials = cachedPrimaryLowDetailSharedMaterials;
            isPrimaryMaterialInLowDetailMode = true;
            return;
        }

        if (isPrimaryMaterialInLowDetailMode)
        {
            RestorePrimaryOriginalMaterials();
        }
    }

    private void RestorePrimaryOriginalMaterials()
    {
        if (cachedPrimaryShooterRenderer == null)
        {
            return;
        }

        if (cachedPrimaryOriginalSharedMaterials == null || cachedPrimaryOriginalSharedMaterials.Length == 0)
        {
            return;
        }

        cachedPrimaryShooterRenderer.sharedMaterials = cachedPrimaryOriginalSharedMaterials;
        isPrimaryMaterialInLowDetailMode = false;
    }

    private Camera ResolveShooterRenderCamera()
    {
        if (cachedShooterRenderCamera == null || !cachedShooterRenderCamera.isActiveAndEnabled)
        {
            cachedShooterRenderCamera = Camera.main;
        }

        return cachedShooterRenderCamera;
    }

    private bool IsShooterInViewport(Camera camera)
    {
        if (camera == null)
        {
            return true;
        }

        Vector3 viewportPos = camera.WorldToViewportPoint(transform.position);
        float padding = Mathf.Max(0f, shooterCullViewportPadding);
        return viewportPos.z > 0f &&
               viewportPos.x >= -padding && viewportPos.x <= 1f + padding &&
               viewportPos.y >= -padding && viewportPos.y <= 1f + padding;
    }

    private float GetSqrDistanceToCamera(Camera camera)
    {
        if (camera == null)
        {
            return 0f;
        }

        return (transform.position - camera.transform.position).sqrMagnitude;
    }

    private bool IsHighPriorityRenderState()
    {
        return currentState == ShooterState.Shooting ||
               currentState == ShooterState.Jumping ||
               currentState == ShooterState.Hero ||
               currentState == ShooterState.Disappear;
    }

    private bool ShouldKeepSecondaryRenderersForCurrentState(bool highPriorityState, bool isHiddenShooter)
    {
        if (isHiddenShooter || highPriorityState)
        {
            return true;
        }

        // IceShooter can rely on secondary renderers (ice shell/smoke) while frozen.
        return this is IceShooter && currentState == ShooterState.Frozen;
    }

    private bool ShouldKeepRendererVisibleForCurrentState(Renderer renderer, bool highPriorityState, bool isHiddenShooter)
    {
        if (renderer == null || isHiddenShooter || highPriorityState)
        {
            return false;
        }

        // IceShooter VFX uses ParticleSystemRenderer children; keep them enabled so smoke/ice-break remains visible.
        if (this is IceShooter && renderer is ParticleSystemRenderer)
        {
            return true;
        }

        if (ShouldAlwaysKeepRendererVisible(renderer))
        {
            return true;
        }

        return false;
    }

    protected virtual bool ShouldAlwaysKeepRendererVisible(Renderer renderer)
    {
        return false;
    }

    private void ApplyAdaptiveShadowState(Renderer renderer, bool highPriorityState, bool isHiddenShooter, bool inFullDetailBudget, float sqrDistanceToCamera)
    {
        if (renderer == null || !forceEnableShooterShadowCasting)
        {
            return;
        }

        if (!allowAggressiveShooterCullingRuntime)
        {
            if (renderer.shadowCastingMode != ShadowCastingMode.On)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            return;
        }

        if (isHiddenShooter && alwaysKeepHiddenShooterShadow)
        {
            if (renderer.shadowCastingMode != ShadowCastingMode.On)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            return;
        }

        bool shouldCastShadow = inFullDetailBudget || highPriorityState;
        if (adaptiveShooterShadowByDistance)
        {
            float shadowDistance = Mathf.Max(0f, shooterShadowDistance);
            shouldCastShadow = (inFullDetailBudget || highPriorityState) &&
                               (highPriorityState || sqrDistanceToCamera <= shadowDistance * shadowDistance);
        }

        ShadowCastingMode targetMode = shouldCastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (renderer.shadowCastingMode != targetMode)
        {
            renderer.shadowCastingMode = targetMode;
        }
    }

    /// <summary>
    /// Cập nhật vị trí và rotation của bullet count text để theo dõi shooter
    /// </summary>
    private void UpdateCountTextTransform()
    {
        if (countTextGO == null || !countTextGO.gameObject.activeSelf) return;
        if (!isCountTextInitialized) return;

        // Giữ vị trí local theo offset gốc
        countTextGO.transform.localPosition = countTextOffset;

        // Xoay mặt text theo Camera (khi nhảy lên hoặc nằm trên Slot thì nghiêng trục X 20 độ)
        Camera mainCam = ResolveShooterRenderCamera();
        if (mainCam != null)
        {
            Quaternion camRot = mainCam.transform.rotation;
            bool isSlotMode = (transform.parent != null && (transform.parent.GetComponent<Slot>() != null || transform.parent.name.Contains("Slot") || transform.parent.GetComponentInParent<SlotBar>() != null)) || currentState == ShooterState.Jumping || currentState == ShooterState.Idle || isHeroReturning;
            countTextGO.transform.rotation = isSlotMode ? camRot * Quaternion.Euler(20f, 0f, 0f) : camRot;
        }
        else
        {
            countTextGO.transform.rotation = originTextRotation;
        }
    }

    /// <summary>
    /// Khởi tạo offset của count text từ vị trí local ban đầu
    /// </summary>
    private void InitializeCountTextOffset()
    {
        if (countTextGO == null) return;
        if (isCountTextInitialized) return;

        // Lưu localPosition và rotation ban đầu từ prefab
        countTextOffset = countTextGO.transform.localPosition;
        originTextRotation = countTextGO.transform.rotation;
        countTextRotationOffset = Quaternion.Inverse(transform.rotation) * originTextRotation;
        isCountTextInitialized = true;
    }

    /// <summary>
    /// Kiểm tra xem text số lượng đạn có nên được hiển thị hay không dựa trên trạng thái và mechanic.
    /// </summary>
    protected virtual bool ShouldShowBulletCountText()
    {
        if (countTextGO == null) return false;
        if (bulletCount <= 0) return false;
        if (currentState == ShooterState.Lock || currentState == ShooterState.Empty || currentState == ShooterState.Disappear || currentState == ShooterState.Frozen) return false;
        if (IsMechanicActive()) return false;
        return true;
    }

    /// <summary>
    /// Cho phép các lớp kế thừa ghi đè để ẩn text đạn khi mechanic đặc biệt đang kích hoạt.
    /// </summary>
    protected virtual bool IsMechanicActive()
    {
        return false;
    }

    /// <summary>
    /// Cập nhật hiển thị (SetActive) của text đạn.
    /// Text chỉ hiển thị ở trạng thái có thể hoạt động/di chuyển.
    /// </summary>
    public virtual void UpdateCountTextVisibilityAndAlpha()
    {
        if (countTextGO == null) return;

        bool shouldShow = ShouldShowBulletCountText();
        if (!shouldShow)
        {
            if (countTextGO.gameObject.activeSelf)
            {
                countTextGO.gameObject.SetActive(false);
            }
            return;
        }

        if (!countTextGO.gameObject.activeSelf)
        {
            countTextGO.gameObject.SetActive(true);
        }

        InitializeCountTextOffset();

        Color c = countTextGO.color;
        if (!Mathf.Approximately(c.a, 1f))
        {
            c.a = 1f;
            countTextGO.color = c;
        }
    }

    public virtual void CheckShooterState(object obj = null)
    {
        if (currentState != ShooterState.Lock && currentState != ShooterState.Empty) return;

        bool hasPath = gridController.HasPathToAnyEndNode(gridItem);
        if (hasPath)
        {
            SetState(ShooterState.IdleGrid);
        }
        else
        {
            SetState(ShooterState.Lock);
        }
        UpdateCountTextVisibilityAndAlpha();
    }

    void FixedUpdate()
    {
        SyncTargetColorFromMaterialByInterval();

        // Liên tục cập nhật tốc độ Animation lỡ như mul thay đổi giữa chừng
        UpdateAnimationSpeed();

        if (currentState == ShooterState.Idle || currentState == ShooterState.Shooting)
        {
            RefreshTargetQueue();

            if (currentState == ShooterState.Idle)
            {
                HandleIdleState();
            }
            else
            {
                HandleShootingState();
            }
        }
    }



    private bool ShouldBlockShootingForHeroBooster()
    {
        if (isInHeroMode)
        {
            return false;
        }

        if (IsAnyHeroActive)
        {
            return true;
        }

        if (BoosterManager.Instance != null && BoosterManager.Instance.IsHeroShooterModeActive())
        {
            return true;
        }

        return false;
    }

    private void SuspendShootingForHeroBooster()
    {
        targetObject = null;
        isDestroySequenceRequested = false;

        RequestImmediateTargetQueueRefresh();

        if (currentState == ShooterState.Shooting)
        {
            SetState(ShooterState.Idle);
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, this);
        }
    }

    private bool ShouldBlockShootingForMagicStoneClear()
    {
        GamePlayController gamePlayController = GamePlayController.Instance;
        return gamePlayController != null && gamePlayController.IsMagicStoneClearRunning();
    }

    private bool ShouldBlockShootingForRouteSwap()
    {
        return SideRouteSeedExchangeMechanic.IsAnyExchangeInProgress;
    }

    private bool ShouldBlockShootingForTutorial()
    {
        TutorialManager tutorialManager = TutorialManager.Instance;
        if (tutorialManager == null)
        {
            return false;
        }

        return tutorialManager.IsTutorialActive && !tutorialManager.IsStepTransitionDelayActive;
    }

    private bool IsFinishingCurrentRowForMagicStoneInternal()
    {
        if (activeDestroyRow != null && activeDestroyRow.IsDestroyingSeedsSequentially)
        {
            return true;
        }

        if (isDestroySequenceRequested && targetObject != null && targetObject.IsDestroyingSeedsSequentially)
        {
            return true;
        }

        return false;
    }

    public bool IsFinishingCurrentRowForMagicStoneGate()
    {
        return IsFinishingCurrentRowForMagicStoneInternal();
    }

    public void SuspendShootingForMagicStoneClear()
    {
        if (IsFinishingCurrentRowForMagicStoneInternal())
        {
            return;
        }

        targetObject = null;
        isDestroySequenceRequested = false;

        RequestImmediateTargetQueueRefresh();

        if (currentState == ShooterState.Shooting)
        {
            SetState(ShooterState.Idle);
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, this);
        }
    }

    private void SuspendShootingForRouteSwap()
    {
        targetObject = null;
        isDestroySequenceRequested = false;

        RequestImmediateTargetQueueRefresh();

        if (currentState == ShooterState.Shooting)
        {
            SetState(ShooterState.Idle);
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, this);
        }
    }

    private void SuspendShootingForTutorial()
    {
        targetObject = null;
        isDestroySequenceRequested = false;

        RequestImmediateTargetQueueRefresh();

        if (currentState == ShooterState.Shooting)
        {
            SetState(ShooterState.Idle);
            GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, this);
        }
    }

    private void SyncTargetColorFromMaterialByInterval()
    {
        if (!autoSyncTargetColorFromMaterial)
        {
            return;
        }

        if (Time.time < nextMaterialColorSyncTime)
        {
            return;
        }

        nextMaterialColorSyncTime = Time.time + Mathf.Max(0.05f, materialColorSyncInterval);
        SyncTargetColorFromMaterial();
    }

    public void Initialize(SlotBar slotBar, GridController gridController)
    {
        this.slotBar = slotBar;
        this.gridController = gridController;

        if (countTextGO != null)
        {
            countTextGO.text = maxBulletCount.ToString();
        }
        SetState(ShooterState.Lock);
        PlayAnimation("Idle_BaseLock", true);
    }


    public void SetState(ShooterState newState)
    {
        if (currentState == newState) return;

        previousState = currentState;
        currentState = newState;

        UpdateStateScale(currentState);

        if (previousState == ShooterState.Shooting && currentState != ShooterState.Shooting)
        {
            ResetRecoilPoseImmediate();
            ResetMagicStoneSpawnGateForCurrentShootingState();
            ScheduleMagicStoneComboShooterVfxRelease();
        }

        if (currentState != ShooterState.Lock)
        {
            KillBoosterHighlightTween();
        }

        switch (currentState)
        {
            case ShooterState.Lock:
                idleTween?.Kill();
                PlayAnimation("TouchLock", false);
                break;
            case ShooterState.Frozen:
                idleTween?.Kill();
                PlayAnimation("TouchLock", false);
                break;
            case ShooterState.Idle:
                idleTween?.Kill();
                SyncOutlineColorFromBaseMaterial();
                PlayAnimation("Idle_Deck", true);
                break;
            case ShooterState.IdleGrid:
                idleTween?.Kill();
                PlayAnimation("ShooterAppear", false);
                StartIdleLogic();
                break;
            case ShooterState.Shooting:
                idleTween?.Kill();
                bulletSpawnTimer = 0f;
                isDestroySequenceRequested = false;
                ResetMagicStoneSpawnGateForCurrentShootingState();
                RefreshMagicStoneRuntimeConfig();
                CancelMagicStoneComboShooterVfxRelease();
                break;
            case ShooterState.Disappear:
                idleTween?.Kill();
                HandleDisappear();
                break;
            case ShooterState.Hero:
                idleTween?.Kill();
                break;
        }

            RefreshAdaptiveShooterRendering(true);
            UpdateCountTextVisibilityAndAlpha();
    }

    private void CacheBaseLocalScaleIfNeeded()
    {
        if (!isBaseLocalScaleCached)
        {
            if (transform.localScale != Vector3.zero)
            {
                baseLocalScale = transform.localScale;
            }
            isBaseLocalScaleCached = true;
        }
    }

    public bool IsCurrentlyBlockedOnGrid()
    {
        if (currentState == ShooterState.Lock) return true;
        if (currentState == ShooterState.Frozen)
        {
            if (gridController != null && gridItem != null)
            {
                return !gridController.HasPathToAnyEndNode(gridItem);
            }
            return false;
        }
        return false;
    }

    public void RefreshBlockedStateScale()
    {
        UpdateStateScale(currentState);
    }

    private void UpdateStateScale(ShooterState newState)
    {
        // Only apply blocked/unblocked scaling while on the Grid.
        // Deck states (Idle, Jumping, Shooting, Hero, Disappear) manage their own local scale.
        if (newState != ShooterState.Lock && newState != ShooterState.Frozen && newState != ShooterState.IdleGrid)
        {
            return;
        }

        CacheBaseLocalScaleIfNeeded();

        if (newState == ShooterState.Hero || isInHeroMode || isHeroReturning)
        {
            return;
        }

        stateScaleTween?.Kill();

        bool isPickLockedActive = BoosterManager.Instance != null && BoosterManager.Instance.IsPickLockedShooterModeActive();

        bool isBlocked;
        if (newState == ShooterState.Lock)
        {
            isBlocked = !isPickLockedActive;
        }
        else if (newState == ShooterState.Frozen)
        {
            isBlocked = IsCurrentlyBlockedOnGrid();
        }
        else
        {
            isBlocked = false;
        }

        Vector3 targetScale = isBlocked ? (baseLocalScale * blockedStateScaleMultiplier) : baseLocalScale;

        if (previousState == ShooterState.Empty || !Application.isPlaying || !gameObject.activeInHierarchy)
        {
            transform.localScale = targetScale;
        }
        else
        {
            float dur = GetEffectiveAnimDuration(stateScaleTweenDuration);
            stateScaleTween = transform.DOScale(targetScale, dur).SetEase(Ease.OutQuad);
        }
    }

    private void StartIdleLogic()
    {
        float randomDelay = GetEffectiveAnimDuration(UnityEngine.Random.Range(3f, 6f));
        idleTween = DOVirtual.DelayedCall(randomDelay, () =>
        {
            float intervalDelay = GetEffectiveAnimDuration(UnityEngine.Random.Range(3, 6));
            idleTween = DOTween.Sequence()
                .AppendCallback(() => PlayAnimation("Idle_BaseDock", false))
                .AppendInterval(intervalDelay)
                .SetLoops(-1);
        });
    }

    private void HandleIdleState()
    {
        if (isHeroReturning) return;

        if (ShouldBlockShootingForHeroBooster())
        {
            targetObject = null;
            isDestroySequenceRequested = false;
            return;
        }

        if (ShouldBlockShootingForTutorial())
        {
            targetObject = null;
            isDestroySequenceRequested = false;
            return;
        }

        if (ShouldBlockShootingForRouteSwap())
        {
            targetObject = null;
            isDestroySequenceRequested = false;
            return;
        }

        if (ShouldBlockShootingForMagicStoneClear())
        {
            targetObject = null;
            isDestroySequenceRequested = false;
            return;
        }

        if (bulletCount <= 0)
        {
            HandleOutOfAmmo();
            return;
        }

        if (targetObject == null && TryDequeueNextValidTarget(out BlockRowSeedSpawner nextTarget))
        {
            targetObject = nextTarget;
            isDestroySequenceRequested = false;
        }

        // Hero mode: no more targets → fly back to slot
        if (isInHeroMode && targetObject == null)
        {
            HeroReturnToSlot();
            return;
        }

        rotateTimer += Time.deltaTime * mul;
        if (rotateTimer >= 2)
        {
            transform.DORotate(new Vector3(0, 0, 0), GetEffectiveAnimDuration(0.5f), RotateMode.Fast);
            rotateTimer = 0;
        }
        if (targetObject != null)
        {
            // Bypass IsFirstShooterOfColor in hero mode — hero shoots independently
            if (!isInHeroMode && slotBar != null && !slotBar.IsFirstShooterOfColor(this))
                return;

            // Rotate ngay tại tick chuyển state để frame đầu bắn đã thấy hướng nòng.
            RotateTowardsTarget();

            // invoke để refesh booster button mỗi khi đổi state
            GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, this);
            SetState(ShooterState.Shooting);
        }
    }

    private void  HandleShootingState()
    {
        if (currentState == ShooterState.Disappear) return;

        if (ShouldBlockShootingForHeroBooster())
        {
            SuspendShootingForHeroBooster();
            return;
        }

        if (ShouldBlockShootingForTutorial())
        {
            SuspendShootingForTutorial();
            return;
        }

        if (ShouldBlockShootingForRouteSwap())
        {
            SuspendShootingForRouteSwap();
            return;
        }

        bool blockByMagicStone = ShouldBlockShootingForMagicStoneClear();
        if (blockByMagicStone && !IsFinishingCurrentRowForMagicStoneInternal())
        {
            SuspendShootingForMagicStoneClear();
            return;
        }

        if (bulletCount <= 0)
        {
            HandleOutOfAmmo();
            return;
        }

        if (targetObject == null ||
            (targetObject.GetSeedCount() <= 0 && !targetObject.IsDestroyingSeedsSequentially))
        {
            targetObject = null;
            isDestroySequenceRequested = false;

            if (TryDequeueNextValidTarget(out BlockRowSeedSpawner nextTarget))
            {
                targetObject = nextTarget;
            }
            else
            {
                RefreshTargetQueue(forceRefreshNow: true);
                if (TryDequeueNextValidTarget(out nextTarget))
                {
                    targetObject = nextTarget;
                }
                else
                {
                SetState(ShooterState.Idle);
                // invoke để refesh booster button mỗi khi đổi state
                GameEventHub.Instance.Invoke(GameEventType.OnBoosterButtonRefresh, this);
                return;
                }
            }
        }
        rotateTimer = 0;

        RotateTowardsTarget();
        PlayAnimationIfNotPlaying("ShooterInDeck", false);

        // Bắn/trừ đạn theo callback bắt đầu xóa từng hạt để sync chính xác theo seed.

        if (!isDestroySequenceRequested && targetObject != null && !targetObject.IsDestroyingSeedsSequentially)
        {
            isDestroySequenceRequested = true;
            int capturedSeedCount = targetObject.GetSeedCount();
            GameObject destroyedRowGO = targetObject.gameObject;
            BlockRowSeedSpawner destroyingRow = targetObject;
            // Capture route reference TRƯỚC khi row bị destroy (tránh parent unset)
            SplineRoute route = targetObject?.transform.parent?.GetComponent<SplineRoute>();
            int queuedSeedCount = 0;
            int ammoBudget = Mathf.Max(0, bulletCount);

            bool destroyStarted = destroyingRow.TryStartDestroyAllSeedsSequential(
                out queuedSeedCount,
                ammoBudget,
                (seedTransform, _) =>
                {
                    if (seedTransform == null || this == null || shootPoint == null || currentState == ShooterState.Disappear)
                    {
                        return;
                    }

                    if (ShouldBlockShootingForTutorial())
                    {
                        return;
                    }

                    if (ShouldBlockShootingForRouteSwap())
                    {
                        return;
                    }

                    // Callback được trigger đúng nhịp wave destroy, bắn trực tiếp để giảm số tween schedule.
                    SpawnBulletAtTarget(seedTransform);
                    RegisterShotForMagicStone();
                    GameEventHub.Instance.Invoke(GameEventType.OnSeedDestroyed, 1);
                },
                () =>
            {
                if (activeDestroyRow == destroyingRow)
                {
                    activeDestroyRow = null;
                }

                if (targetObject == destroyingRow)
                {
                    targetObject = null;
                }
                GameEventHub.Instance.Invoke(GameEventType.OnSeedRowDestroyed, capturedSeedCount);

                // Thông báo cho SplineRoute rằng row đã bị phá hủy — nó sẽ dồn các row phía trước lên
                // CHỈ dồn nếu là Side route (Main route tự handle với RefreshMainPositions)
                // Defer callback sang next frame để tránh race condition với refill trigger
                if (destroyedRowGO != null && route != null && route.GetRouteMode() == SplineRoute.RouteMode.Side)
                {
                    DOVirtual.DelayedCall(0.01f, () =>
                    {
                        if (destroyedRowGO != null && route != null)
                        {
                            route.OnRowDestroyed(destroyedRowGO);
                        }
                    });
                }
            });

            if (destroyStarted)
            {
                activeDestroyRow = destroyingRow;

                // Trừ đạn ngay tại thời điểm hạt được đưa vào queue xóa để không bao giờ vượt quá ammo thật.
                DecreaseBulletByDestroyedRow(queuedSeedCount);

                float handoffDelay = GetEffectiveAnimDuration(Mathf.Max(0.0f, GetRowHandoffDelay()));
                DOVirtual.DelayedCall(handoffDelay, () =>
                {
                    if (targetObject == destroyingRow)
                    {
                        targetObject = null;
                    }

                    isDestroySequenceRequested = false;
                    RequestImmediateTargetQueueRefresh();
                });
            }
            else
            {
                if (activeDestroyRow == destroyingRow)
                {
                    activeDestroyRow = null;
                }

                isDestroySequenceRequested = false;
                RequestImmediateTargetQueueRefresh();
            }

        }


    }

    // Đổi thành kiểu bool để báo về cho HandleShootingState biết đạn có được bắn ra không
    private bool SpawnBullet(BlockRowSeedSpawner targetRow)
    {
        GameObject configuredBulletPrefab = GetBulletPrefab();
        if (configuredBulletPrefab == null || shootPoint == null || targetRow == null) return false;

        int bulletsToSpawn = Mathf.Max(0, GetBulletsPerShot());
        if (bulletsToSpawn <= 0)
        {
            return false;
        }
        int scheduledCount = 0;
        float perBulletDelay = GetEffectiveAnimDuration(Mathf.Max(0f, GetBulletSpreadShotDelay()));
        float spreadRadius = Mathf.Max(0f, GetBulletSpreadRadius());

        for (int i = 0; i < bulletsToSpawn; i++)
        {
            if (!targetRow.TryGetNextVisualTargetSeed(out Transform visualTarget) || visualTarget == null)
            {
                continue;
            }

            Vector3 targetPos = visualTarget.position;
            targetPos.y = shootPoint.position.y;
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-spreadRadius, spreadRadius),
                UnityEngine.Random.Range(-spreadRadius * 0.35f, spreadRadius * 0.35f),
                UnityEngine.Random.Range(-spreadRadius, spreadRadius)
            );
            Vector3 finalTargetPos = targetPos + randomOffset;
            finalTargetPos = ClampPointToRowCollider(targetRow, finalTargetPos);
            finalTargetPos.y = shootPoint.position.y;
            float delay = i * perBulletDelay;

            if (delay <= 0.0001f)
            {
                SpawnSpreadBulletNow(configuredBulletPrefab, finalTargetPos);
                scheduledCount++;
                continue;
            }

            DOVirtual.DelayedCall(delay, () =>
            {
                if (this == null || shootPoint == null)
                {
                    return;
                }

                SpawnSpreadBulletNow(configuredBulletPrefab, finalTargetPos);
            });

            scheduledCount++;
        }

        return scheduledCount > 0;
    }

    private void SpawnSpreadBulletNow(GameObject configuredBulletPrefab, Vector3 finalTargetPos)
    {
        if (configuredBulletPrefab == null || shootPoint == null)
        {
            return;
        }

        if (!TryConsumeVisualBulletBudget())
        {
            return;
        }

        GameObject bulletInstance = ObjectPoolManager.SpawnObject(
            configuredBulletPrefab,
            shootPoint.position,
            Quaternion.identity,
            ObjectPoolManager.PoolType.Bullet
        );
        PlayShootSfx();
        Bullet bulletComponent = bulletInstance.GetComponent<Bullet>();

        if (bulletComponent == null)
        {
            ObjectPoolManager.ReturnObject(bulletInstance, ObjectPoolManager.PoolType.Bullet);
            return;
        }

        RotateTowardsWorldPoint(finalTargetPos, true);

        // Bắn theo nhịp so le và lệch hướng ngẫu nhiên quanh mục tiêu để tạo cảm giác tỏa.
        bulletComponent.ShootToWorldPosition(shootPoint, finalTargetPos);
        PlayRecoilEffect();
    }

    private bool SpawnBulletAtTarget(Transform visualTarget)
    {
        if (GetBulletsPerShot() <= 0)
        {
            return false;
        }

        GameObject configuredBulletPrefab = GetBulletPrefab();
        if (configuredBulletPrefab == null || shootPoint == null || visualTarget == null)
        {
            return false;
        }

        if (!TryConsumeVisualBulletBudget())
        {
            return false;
        }

        GameObject bulletInstance = ObjectPoolManager.SpawnObject(
            configuredBulletPrefab,
            shootPoint.position,
            Quaternion.identity,
            ObjectPoolManager.PoolType.Bullet
        );
        PlayShootSfx();
        Bullet bulletComponent = bulletInstance.GetComponent<Bullet>();
        if (bulletComponent == null)
        {
            ObjectPoolManager.ReturnObject(bulletInstance, ObjectPoolManager.PoolType.Bullet);
            return false;
        }

        Vector3 worldTargetPos = visualTarget.position;
        if (!isInHeroMode)
        {
            worldTargetPos.y = shootPoint.position.y;
        }

        RotateTowardsWorldPoint(worldTargetPos, true);

        bulletComponent.ShootToWorldPosition(shootPoint, worldTargetPos);
        PlayRecoilEffect();
        return true;
    }

    private bool TryConsumeVisualBulletBudget()
    {
        if (!enforceVisualBulletFrameBudget)
        {
            return true;
        }

        int frame = Time.frameCount;
        if (visualBulletBudgetFrame != frame)
        {
            visualBulletBudgetFrame = frame;
            visualBulletBudgetUsed = 0;
        }

        int frameBudget = Mathf.Max(1, maxVisualBulletsPerFrame);
        if (visualBulletBudgetUsed >= frameBudget)
        {
            return false;
        }

        visualBulletBudgetUsed++;
        return true;
    }

    private float GetHeroFocusOrthoSize(HeroShooterBoosterConfig cfg)
    {
        if (cfg == null || cfg.cameraZoomSize <= 0f)
        {
            return 0f;
        }

        return cfg.cameraZoomSize + Mathf.Max(0f, heroCameraEdgePaddingOrtho);
    }

    private float GetHeroCameraFocusOffsetY(HeroShooterBoosterConfig cfg)
    {
        if (cfg == null)
        {
            return 1.2f;
        }

        return cfg.cameraFocusOffsetY;
    }

    private Vector3 ClampPointToRowCollider(BlockRowSeedSpawner targetRow, Vector3 worldPoint)
    {
        if (targetRow == null)
        {
            return worldPoint;
        }

        Collider rowCollider = targetRow.GetComponent<Collider>();
        if (rowCollider == null || !rowCollider.enabled)
        {
            return worldPoint;
        }

        return rowCollider.ClosestPoint(worldPoint);
    }

    private void DecreaseBulletByDestroyedRow(int destroyedSeedCount)
    {
        int fromValue = bulletCount;
        int decreaseAmount = Mathf.Max(1, destroyedSeedCount);
        bulletCount = Mathf.Max(0, bulletCount - decreaseAmount);

        AnimateBulletCountText(fromValue, bulletCount, 0.28f);

        GameEventHub.Instance.Invoke(GameEventType.OnBulletCountChanged, bulletCount);

        if (bulletCount <= 0)
        {
            HandleOutOfAmmo();
        }
    }

    private void PlayShootSfx()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            return;
        }

        bool shouldUseSimulated = shootAudioConfig != null
            ? shootAudioConfig.useSimulatedShootSfx
            : useSimulatedShootSfx;
        float configuredInterval = shootAudioConfig != null
            ? shootAudioConfig.simulatedShootSfxInterval
            : simulatedShootSfxInterval;
        string configuredSfxKey = shootAudioConfig != null
            ? shootAudioConfig.shootSfxKey
            : Const.popShootSFX;
        float sfxVolume = shootAudioConfig != null
            ? shootAudioConfig.shootSfxVolume
            : 1f;
        bool scaleByMultiplier = shootAudioConfig == null || shootAudioConfig.scaleIntervalBySpeedMultiplier;
        float multiplierInfluence = shootAudioConfig != null ? shootAudioConfig.multiplierInfluence : 1f;

        if (string.IsNullOrEmpty(configuredSfxKey))
        {
            configuredSfxKey = Const.popShootSFX;
        }

        float effectiveInterval = configuredInterval;
        if (shouldUseSimulated && scaleByMultiplier)
        {
            float safeMul = Mathf.Max(0.1f, mul);
            float mulFactor = Mathf.Pow(safeMul, Mathf.Clamp(multiplierInfluence, 0f, 2f));
            effectiveInterval = configuredInterval / Mathf.Max(0.1f, mulFactor);
        }

        float interval = Mathf.Max(0.05f, effectiveInterval);
        // Nếu có khoảng nghỉ đủ lớn thì coi như streak mới, còn bắn nối tiếp giữa nhiều shooter thì giữ streak.
        float resetGap = Mathf.Max(0.75f, interval * 3f);
        if (Time.time - globalLastShootSfxPlayTime > resetGap)
        {
            globalShootSfxStreakStartTime = Time.time;
        }

        if (globalShootSfxStreakStartTime < 0f)
        {
            globalShootSfxStreakStartTime = Time.time;
        }

        float rampDuration = Mathf.Max(0.05f, interval * shootSfxPitchRampSteps);
        float ramp01 = Mathf.Clamp01((Time.time - globalShootSfxStreakStartTime) / rampDuration);
        float currentPitch = Mathf.Lerp(shootSfxMinPitch, shootSfxMaxPitch, ramp01);

        if (!shouldUseSimulated)
        {
            audioManager.PlaySFX(configuredSfxKey, currentPitch, sfxVolume);
            TryPlayShootHaptic(interval);
            hasPlayedShootSfxSinceReset = true;
            globalLastShootSfxPlayTime = Time.time;
            return;
        }

        // Keep first shot tightly synced when a shooter just enters active combat (e.g. PickLocked flow).
        if (!hasPlayedShootSfxSinceReset)
        {
            audioManager.PlaySFX(configuredSfxKey, currentPitch, sfxVolume);
            TryPlayShootHaptic(interval);
            hasPlayedShootSfxSinceReset = true;
            globalLastShootSfxPlayTime = Time.time;
            return;
        }

        bool played = audioManager.TryPlaySFXWithCooldown(configuredSfxKey, interval, currentPitch, sfxVolume);
        if (played)
        {
            TryPlayShootHaptic(interval);
            hasPlayedShootSfxSinceReset = true;
            globalLastShootSfxPlayTime = Time.time;
        }
    }

    private static void TryPlayShootHaptic(float requestedCooldownSeconds)
    {
        int defaultEnabledValue = MOST_HapticFeedback.HapticsEnabled ? 1 : 0;
        bool isEnabled = PlayerPrefs.GetInt(Const.player_vibration_key, defaultEnabledValue) == 1;
        if (MOST_HapticFeedback.HapticsEnabled != isEnabled)
        {
            MOST_HapticFeedback.HapticsEnabled = isEnabled;
        }

        if (!isEnabled)
        {
            return;
        }

        float cooldown = requestedCooldownSeconds > 0f ? requestedCooldownSeconds : 0.12f;
        cooldown = Mathf.Max(0.08f, cooldown);
        MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.LightImpact, cooldown);
    }

    private void RegisterShotForMagicStone()
    {
        if (!isMagicStoneFeatureEnabled)
        {
            return;
        }

        if (currentState != ShooterState.Shooting)
        {
            return;
        }

        if (cachedSpawnMagicStoneOncePerShootingState && hasSpawnedMagicStoneInCurrentShootingState)
        {
            return;
        }

        float now = Time.time;
        float breakGap = Mathf.Max(0f, cachedMagicStoneComboBreakGapSeconds);
        float effectiveBreakGap = breakGap;
        if (effectiveBreakGap > 0f)
        {
            float safeMul = Mathf.Max(0.1f, mul);
            effectiveBreakGap /= safeMul;
        }

        if (effectiveBreakGap > 0f && (now - magicStoneLastShotTime) > effectiveBreakGap)
        {
            magicStoneLocalShotComboCount = 0;
        }
        magicStoneLastShotTime = now;

        magicStoneLocalShotComboCount++;
        int threshold = cachedMagicStoneShotThresholdSafe;
        if (magicStoneLocalShotComboCount < threshold)
        {
            return;
        }

        bool spawned = SpawnMagicStone();
        if (!spawned)
        {
            return;
        }

        SpawnMagicStoneComboShooterVfx();

        hasSpawnedMagicStoneInCurrentShootingState = true;

        if (cachedSpawnMagicStoneOncePerShootingState)
        {
            magicStoneLocalShotComboCount = 0;
            return;
        }

        // Keep per-shooter overflow for repeated spawn mode without costly modulo.
        magicStoneLocalShotComboCount = Mathf.Max(0, magicStoneLocalShotComboCount - threshold);
    }

    private bool SpawnMagicStone()
    {
        GameObject prefab = GetMagicStoneVfxPrefab();
        if (prefab == null)
        {
            return false;
        }

        Vector3 spawnPosition = shootPoint != null ? shootPoint.position : transform.position;
        Vector3 sideDirection = transform.right;
        if (sideDirection.sqrMagnitude <= 0.0001f)
        {
            sideDirection = Vector3.right;
        }

        float sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        spawnPosition += sideDirection.normalized * (sideSign * magicStoneSpawnSideOffset);

        Quaternion spawnRotation = Quaternion.identity;
        CacheMagicStonePrefabMetadata(prefab);

        if (cachedMagicStonePrefabHasComponent)
        {
            GameObject spawnedStone = ObjectPoolManager.SpawnObject(
                prefab,
                spawnPosition,
                spawnRotation,
                ObjectPoolManager.PoolType.Particle
            );

            if (spawnedStone != null)
            {
                MagicStone magicStone = spawnedStone.GetComponent<MagicStone>();
                if (magicStone == null)
                {
                    magicStone = spawnedStone.GetComponentInChildren<MagicStone>(true);
                }

                if (magicStone != null)
                {
                    magicStone.BindOwnerShooter(this);
                    magicStone.PlayMotion();
                }
            }

            if (spawnedStone != null)
            {
                AudioManager.Instance?.PlaySFX(Const.spawnMagicStoneSFX);
            }

            return spawnedStone != null;
        }

        GameObject spawnedVfx = SpawnOneShotPooledVfx(prefab, spawnPosition, spawnRotation, 2f);
        if (spawnedVfx != null)
        {
            AudioManager.Instance?.PlaySFX(Const.spawnMagicStoneSFX);
            AccumulateMagicStoneRewardForCurrentLevel(1);
            return true;
        }

        return false;
    }

    private void SpawnMagicStoneComboShooterVfx()
    {
        CancelMagicStoneComboShooterVfxRelease();

        GameObject comboVfxPrefab = GetMagicStoneComboShooterVfxPrefab();
        if (comboVfxPrefab == null)
        {
            return;
        }

        if (activeMagicStoneComboShooterVfx != null)
        {
            if (!activeMagicStoneComboShooterVfx.activeSelf)
            {
                activeMagicStoneComboShooterVfx.SetActive(true);
            }

            activeMagicStoneComboShooterVfx.transform.localPosition = GetMagicStoneComboShooterVfxLocalOffset();
            activeMagicStoneComboShooterVfx.transform.localRotation = Quaternion.identity;
            activeMagicStoneComboShooterVfx.transform.localScale = comboVfxPrefab.transform.localScale;

            CacheMagicStoneComboShooterVfxColorStates(activeMagicStoneComboShooterVfx);
            ApplyMagicStoneComboShooterVfxAlpha(0f);
            RestartMagicStoneComboShooterVfxParticles(activeMagicStoneComboShooterVfx);

            magicStoneComboShooterVfxFadeTween?.Kill();
            float replayFadeDuration = Mathf.Max(0.01f, GetMagicStoneComboShooterVfxFadeInDuration());
            magicStoneComboShooterVfxFadeTween = DOVirtual.Float(0f, 1f, replayFadeDuration, alpha =>
            {
                if (activeMagicStoneComboShooterVfx == null)
                {
                    return;
                }

                ApplyMagicStoneComboShooterVfxAlpha(alpha);
            }).SetEase(Ease.OutSine).SetUpdate(true);
            return;
        }

        GameObject spawnedComboVfx = ObjectPoolManager.SpawnObject(
            comboVfxPrefab,
            transform,
            ObjectPoolManager.PoolType.Particle
        );

        if (spawnedComboVfx == null)
        {
            return;
        }

        activeMagicStoneComboShooterVfx = spawnedComboVfx;
        spawnedComboVfx.transform.localPosition = GetMagicStoneComboShooterVfxLocalOffset();
        spawnedComboVfx.transform.localRotation = Quaternion.identity;
        spawnedComboVfx.transform.localScale = comboVfxPrefab.transform.localScale;

        CacheMagicStoneComboShooterVfxColorStates(spawnedComboVfx);
        ApplyMagicStoneComboShooterVfxAlpha(0f);
        RestartMagicStoneComboShooterVfxParticles(spawnedComboVfx);

        magicStoneComboShooterVfxFadeTween?.Kill();
        float fadeDuration = Mathf.Max(0.01f, GetMagicStoneComboShooterVfxFadeInDuration());
        magicStoneComboShooterVfxFadeTween = DOVirtual.Float(0f, 1f, fadeDuration, alpha =>
        {
            if (activeMagicStoneComboShooterVfx == null)
            {
                return;
            }

            ApplyMagicStoneComboShooterVfxAlpha(alpha);
        }).SetEase(Ease.OutSine).SetUpdate(true);
    }

    private void CacheMagicStoneComboShooterVfxColorStates(GameObject vfxRoot)
    {
        magicStoneComboShooterVfxColorStates.Clear();
        if (vfxRoot == null)
        {
            return;
        }

        Renderer[] renderers = vfxRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] mats = renderer.sharedMaterials;
            if (mats == null)
            {
                continue;
            }

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                {
                    continue;
                }

                if (mat.HasProperty(baseColorShaderId))
                {
                    magicStoneComboShooterVfxColorStates.Add(new VfxRendererColorState
                    {
                        renderer = renderer,
                        colorPropertyId = baseColorShaderId,
                        baseColor = mat.GetColor(baseColorShaderId)
                    });
                    break;
                }

                if (mat.HasProperty(colorShaderId))
                {
                    magicStoneComboShooterVfxColorStates.Add(new VfxRendererColorState
                    {
                        renderer = renderer,
                        colorPropertyId = colorShaderId,
                        baseColor = mat.GetColor(colorShaderId)
                    });
                    break;
                }
            }
        }
    }

    private void ApplyMagicStoneComboShooterVfxAlpha(float alpha01)
    {
        if (magicStoneComboShooterVfxColorStates.Count == 0)
        {
            return;
        }

        if (magicStoneComboShooterVfxPropertyBlock == null)
        {
            magicStoneComboShooterVfxPropertyBlock = new MaterialPropertyBlock();
        }

        float clampedAlpha = Mathf.Clamp01(alpha01);
        for (int i = 0; i < magicStoneComboShooterVfxColorStates.Count; i++)
        {
            VfxRendererColorState state = magicStoneComboShooterVfxColorStates[i];
            if (state.renderer == null)
            {
                continue;
            }

            Color target = state.baseColor;
            target.a *= clampedAlpha;

            state.renderer.GetPropertyBlock(magicStoneComboShooterVfxPropertyBlock);
            magicStoneComboShooterVfxPropertyBlock.SetColor(state.colorPropertyId, target);
            state.renderer.SetPropertyBlock(magicStoneComboShooterVfxPropertyBlock);
        }
    }

    private void RestartMagicStoneComboShooterVfxParticles(GameObject vfxRoot)
    {
        if (vfxRoot == null)
        {
            return;
        }

        ParticleSystem[] systems = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null)
        {
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null)
            {
                continue;
            }

            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void ReleaseMagicStoneComboShooterVfx()
    {
        CancelMagicStoneComboShooterVfxRelease();
        magicStoneComboShooterVfxFadeTween?.Kill();
        magicStoneComboShooterVfxFadeTween = null;
        magicStoneComboShooterVfxColorStates.Clear();

        if (activeMagicStoneComboShooterVfx != null)
        {
            ObjectPoolManager.ReturnObject(activeMagicStoneComboShooterVfx, ObjectPoolManager.PoolType.Particle);
            activeMagicStoneComboShooterVfx = null;
        }
    }

    private void CancelMagicStoneComboShooterVfxRelease()
    {
        if (magicStoneComboShooterVfxReleaseTween != null && magicStoneComboShooterVfxReleaseTween.IsActive())
        {
            magicStoneComboShooterVfxReleaseTween.Kill();
        }

        magicStoneComboShooterVfxReleaseTween = null;
    }

    private void ScheduleMagicStoneComboShooterVfxRelease()
    {
        if (activeMagicStoneComboShooterVfx == null)
        {
            return;
        }

        CancelMagicStoneComboShooterVfxRelease();

        float releaseDelay = Mathf.Max(0.1f, cachedMagicStoneComboBreakGapSeconds);
        magicStoneComboShooterVfxReleaseTween = DOVirtual.DelayedCall(releaseDelay, () =>
        {
            magicStoneComboShooterVfxReleaseTween = null;
            ReleaseMagicStoneComboShooterVfx();
        }).SetUpdate(true);
    }

    private void ResetMagicStoneShotStreak()
    {
        magicStoneLocalShotComboCount = 0;
        magicStoneLastShotTime = -999f;
        ResetMagicStoneSpawnGateForCurrentShootingState();
    }

    private void ResetMagicStoneSpawnGateForCurrentShootingState()
    {
        hasSpawnedMagicStoneInCurrentShootingState = false;
    }

    private void RefreshMagicStoneRuntimeConfig()
    {
        cachedMagicStoneShotThreshold = GetMagicStoneShotStreakThreshold();
        cachedMagicStoneShotThresholdSafe = Mathf.Max(1, cachedMagicStoneShotThreshold);
        cachedMagicStoneComboBreakGapSeconds = GetMagicStoneComboBreakGapSeconds();
        cachedSpawnMagicStoneOncePerShootingState = ShouldSpawnMagicStoneOncePerShootingState();
        isMagicStoneFeatureEnabled = GetMagicStoneVfxPrefab() != null && IsMagicStoneComboFeatureUnlockedForCurrentLevel();

        CacheMagicStonePrefabMetadata(GetMagicStoneVfxPrefab());
    }

    private static bool IsMagicStoneComboFeatureUnlockedForCurrentLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        return currentLevel >= 3;
    }

    private void CacheMagicStonePrefabMetadata(GameObject prefab)
    {
        if (prefab == null)
        {
            hasCachedMagicStonePrefabMetadata = true;
            cachedMagicStonePrefabForMetadata = null;
            cachedMagicStonePrefabHasComponent = false;
            return;
        }

        if (hasCachedMagicStonePrefabMetadata && cachedMagicStonePrefabForMetadata == prefab)
        {
            return;
        }

        cachedMagicStonePrefabForMetadata = prefab;
        cachedMagicStonePrefabHasComponent =
            prefab.GetComponent<MagicStone>() != null ||
            prefab.GetComponentInChildren<MagicStone>(true) != null;
        hasCachedMagicStonePrefabMetadata = true;
    }

    private GameObject GetMagicStoneVfxPrefab()
    {
        if (combatConfig != null && combatConfig.magicStoneVfxPrefab != null)
        {
            return combatConfig.magicStoneVfxPrefab;
        }

        return null;
    }

    private int GetMagicStoneShotStreakThreshold()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(1, combatConfig.magicStoneShotStreakThreshold);
        }

        return 50;
    }

    private bool ShouldSpawnMagicStoneOncePerShootingState()
    {
        if (combatConfig != null)
        {
            return combatConfig.spawnMagicStoneOncePerShootingState;
        }

        return true;
    }
    
    private float GetMagicStoneComboBreakGapSeconds()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.magicStoneComboBreakGapSeconds);
        }

        return 0.6f;
    }

    private GameObject GetMagicStoneComboShooterVfxPrefab()
    {
        if (combatConfig != null)
        {
            return combatConfig.magicStoneComboShooterVfxPrefab;
        }

        return null;
    }

    private Vector3 GetMagicStoneComboShooterVfxLocalOffset()
    {
        if (combatConfig != null)
        {
            return combatConfig.magicStoneComboShooterVfxLocalOffset;
        }

        return Vector3.zero;
    }

    private float GetMagicStoneComboShooterVfxFadeInDuration()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0.01f, combatConfig.magicStoneComboShooterVfxFadeInDuration);
        }

        return 0.2f;
    }

    public static void AccumulateMagicStoneRewardForCurrentLevel(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        pendingMagicStoneRewardForCurrentLevel = Mathf.Min(3, pendingMagicStoneRewardForCurrentLevel + amount);
        NotifyMagicStoneProgressChanged();
    }

    public static bool TryConsumeMagicStoneForCurrentLevel(int amount)
    {
        int safeAmount = Mathf.Max(1, amount);
        if (pendingMagicStoneRewardForCurrentLevel < safeAmount)
        {
            return false;
        }

        pendingMagicStoneRewardForCurrentLevel = Mathf.Max(0, pendingMagicStoneRewardForCurrentLevel - safeAmount);
        NotifyMagicStoneProgressChanged();
        return true;
    }

    public static void ResetMagicStoneForCurrentLevel()
    {
        if (pendingMagicStoneRewardForCurrentLevel == 0)
        {
            return;
        }

        pendingMagicStoneRewardForCurrentLevel = 0;
        NotifyMagicStoneProgressChanged();
    }

    public static int GetCollectedMagicStoneForCurrentLevel()
    {
        return Mathf.Clamp(pendingMagicStoneRewardForCurrentLevel, 0, 3);
    }

    private static void NotifyMagicStoneProgressChanged()
    {
        GameEventHub.Instance?.Invoke(
            GameEventType.OnMagicStoneProgressChanged,
            Mathf.Max(0, pendingMagicStoneRewardForCurrentLevel)
        );
    }

    
    /// <summary>
    /// Lấy danh sách từ Trạm Gác (FireRangeDetector) và đưa vào Queue
    /// </summary>
    private void RefreshTargetQueue(bool forceRefreshNow = false)
    {
        // Hero mode: target queue is pre-filled from BoosterManager, don't touch it
        if (isInHeroMode) return;

        if (slotBar != null && !slotBar.IsFirstShooterOfColor(this))
            return;
        FireRangeDetector detector = FireRangeDetector.Instance;
        if (detector == null) return;

        bool hasCurrentValidTarget = targetObject != null && IsTargetValid(targetObject);
        if (!forceRefreshNow && Time.time < nextTargetQueueRefreshTime)
        {
            return;
        }

        float queueRefreshInterval = Mathf.Max(0.02f, targetQueueRefreshInterval * targetQueueRefreshIntervalScale);
        nextTargetQueueRefreshTime = Time.time + queueRefreshInterval;
        int detectorStateVersion = detector.TargetsStateVersion;
        bool detectorSnapshotChanged = detectorStateVersion != lastDetectorTargetsStateVersion;

        if (!detectorSnapshotChanged)
        {
            if (hasCurrentValidTarget)
            {
                return;
            }

            if (!forceRefreshNow && Time.time < nextTargetQueueFallbackRefreshTime)
            {
                return;
            }
        }

        float queueFallbackInterval = Mathf.Max(0.05f, targetQueueFallbackRefreshInterval * targetQueueRefreshIntervalScale);
        nextTargetQueueFallbackRefreshTime = Time.time + queueFallbackInterval;
        lastDetectorTargetsStateVersion = detectorStateVersion;

        IReadOnlyList<BlockRowSeedSpawner> targetsInRange = detector.GetTargetsInRangeView();
        if (targetsInRange == null) return;

        // Rebuild queue theo snapshot hiện tại để luôn ưu tiên row có X nhỏ hơn (hướng -X).
        targetQueue.Clear();
        queuedTargets.Clear();

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            BlockRowSeedSpawner target = targetsInRange[i];
            if (!IsTargetValid(target, skipRangeCheck: true))
            {
                continue;
            }

            if (target == targetObject)
            {
                continue;
            }

            if (queuedTargets.Add(target))
            {
                targetQueue.Enqueue(target);
            }
        }

        if (targetObject != null && !IsTargetValid(targetObject))
        {
            if (!isDestroySequenceRequested)
            {
                targetObject = null;
            }
            // Nếu đã bắt đầu destroy sequence thì giữ target đến khi hết handoff delay,
            // tránh nhảy tốc độ đột ngột khi row vừa chạm mép range.
        }



    }

    private void RequestImmediateTargetQueueRefresh()
    {
        nextTargetQueueRefreshTime = 0f;
        nextTargetQueueFallbackRefreshTime = 0f;
        lastDetectorTargetsStateVersion = -1;
    }

    private bool TryDequeueNextValidTarget(out BlockRowSeedSpawner target)
    {
        target = null;

        while (targetQueue.Count > 0)
        {
            BlockRowSeedSpawner candidate = targetQueue.Dequeue();
            queuedTargets.Remove(candidate);

            if (IsTargetValid(candidate))
            {
                target = candidate;
                return true;
            }
        }

        return false;
    }

    private bool IsTargetValid(BlockRowSeedSpawner target, bool skipRangeCheck = false)
    {
        if (target == null)
            return false;

        if (target.IsDestroyingSeedsSequentially)
            return false;

        // In hero mode, shoot any color
        if (!isInHeroMode && target.GetCurrentColor() != targetColor)
            return false;

        if (target.GetSeedCount() <= 0)
            return false;

        // Target must still be inside the FireRangeDetector zone
        if (!skipRangeCheck && !IsTargetInRange(target))
            return false;

        return true;
    }

    private bool IsTargetInRange(BlockRowSeedSpawner target)
    {
        if (isInHeroMode) return true;
        if (target == null) return false;
        FireRangeDetector detector = FireRangeDetector.Instance;
        if (detector == null) return false;
        return detector.IsTargetInRange(target);
    }

    private void UpdateBulletCountText()
    {
        if (countTextGO != null)
        {
            countTextGO.text = bulletCount.ToString();
        }
    }

    private void HandleOutOfAmmo()
    {
        if (currentState == ShooterState.Disappear)
        {
            return;
        }

        // Đang còn hạt trong destroy sequence thì chờ xong để tránh vừa bắn phát cuối vừa biến mất.
        if (IsWaitingForDestroyCompletion())
        {
            return;
        }

        targetObject = null;
        isDestroySequenceRequested = false;
        targetQueue.Clear();
        queuedTargets.Clear();
        bulletSpawnTimer = 0f;
        bulletTextTween?.Kill();
        ResetRecoilPoseImmediate();

        if (countTextGO != null)
        {
            countTextGO.gameObject.SetActive(false);
        }

        // Shooter đang nằm trên grid và hết đạn thì phải mở ô ngay,
        // để các shooter xung quanh được unlock path giống flow chọn shooter bình thường.
        if (currentState == ShooterState.Lock || currentState == ShooterState.IdleGrid || currentState == ShooterState.Frozen)
        {
            gridItem?.SetEmptyItem();
        }

        if (isInHeroMode)
        {
            // Camera return, then normal disappear
            isInHeroMode = false;
            isHeroReturning = false;
            activeHeroCount = Mathf.Max(0, activeHeroCount - 1);
            Camera cam = _heroCamera != null ? _heroCamera : Camera.main;

            float returnDur = GetEffectiveAnimDuration(heroCfg.cameraReturnDuration);
            Sequence sequence = DOTween.Sequence();
            Tween cameraMoveTween = null;

            for (int i = 0; i < heroRigCameraTransforms.Count; i++)
            {
                Transform camTransform = heroRigCameraTransforms[i];
                if (camTransform == null)
                {
                    continue;
                }

                Vector3 targetLocalPos = i < heroRigCameraOrigLocalPositions.Count
                    ? heroRigCameraOrigLocalPositions[i]
                    : camTransform.localPosition;

                Tween moveTween = camTransform.DOLocalMove(targetLocalPos, returnDur).SetEase(Ease.InOutSine);
                if (cameraMoveTween == null)
                {
                    sequence.Append(moveTween);
                    cameraMoveTween = moveTween;
                }
                else
                {
                    sequence.Join(moveTween);
                }
            }

            if (cameraMoveTween == null && cam != null)
            {
                sequence.Append(cam.transform.DOLocalMove(heroOrigCamLocalPos, returnDur).SetEase(Ease.InOutSine));
            }
            else if (cameraMoveTween == null)
            {
                sequence.AppendInterval(returnDur);
            }

            bool hasZoomTween = false;
            if (heroCfg.cameraZoomSize > 0f)
            {
                for (int i = 0; i < heroRigCameras.Count; i++)
                {
                    Camera rigCam = heroRigCameras[i];
                    if (rigCam == null || !rigCam.orthographic)
                    {
                        continue;
                    }

                    float origSize = i < heroRigCameraSizes.Count
                        ? heroRigCameraSizes[i]
                        : rigCam.orthographicSize;
                    sequence.Join(rigCam.DOOrthoSize(origSize, returnDur).SetEase(Ease.InOutSine));
                    hasZoomTween = true;
                }

                if (!hasZoomTween && cam != null && cam.orthographic)
                {
                    sequence.Join(cam.DOOrthoSize(heroOrigCamSize, returnDur).SetEase(Ease.InOutSine));
                }
            }

            sequence.OnComplete(() => SetState(ShooterState.Disappear));
        }
        else
        {
            SetState(ShooterState.Disappear);
        }
    }

    private bool IsWaitingForDestroyCompletion()
    {
        if (activeDestroyRow != null && activeDestroyRow.IsDestroyingSeedsSequentially)
        {
            return true;
        }

        return false;
    }

    private void AnimateBulletCountText(int fromValue, int toValue, float duration)
    {
        if (countTextGO == null)
        {
            return;
        }

        bulletTextTween?.Kill();

        countTextGO.text = fromValue.ToString();
        float effectiveDur = GetEffectiveAnimDuration(duration);
        bulletTextTween = DOVirtual.Int(fromValue, toValue, effectiveDur, value =>
        {
            countTextGO.text = value.ToString();
        }).SetEase(Ease.OutQuad);
    }


    private void RotateTowardsTarget()
    {
        if (targetObject == null)
        {
            return;
        }

        RotateTowardsWorldPoint(targetObject.transform.position, false);
    }

    private void RotateTowardsWorldPoint(Vector3 worldTargetPos, bool immediate)
    {
        Vector3 directionToTarget = worldTargetPos - transform.position;

        // Hero mode rotates in full 3D so the shooter visually faces its current shot direction.
        if (!isInHeroMode)
        {
            directionToTarget.y = 0f;
        }

        if (directionToTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(-directionToTarget.normalized);
        if (immediate)
        {
            transform.rotation = targetRotation;
            return;
        }

        float rotationSpeed = 60f * Mathf.Max(0.1f, mul);
        float lerpFactor = Mathf.Clamp01(Time.deltaTime * rotationSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lerpFactor);
    }




    private void HandleDisappear()
    {
        ResetRecoilPoseImmediate();
        ReleaseMagicStoneComboShooterVfx();
        AudioManager.Instance?.PlaySFX(Const.shooterDoneSFX);
        slotBar.RemoveShooter(this);

        float moveDur  = GetEffectiveAnimDuration(0.2f);
        float rotDur   = GetEffectiveAnimDuration(0.6f);
        float scaleDur = rotDur;

        Sequence seq = DOTween.Sequence();

        // ── STEP 1: Spawn + nhảy lên ─────────────────────
        seq.AppendCallback(() =>
        {
            GameObject jumpDisappearVfx = GetJumpDisappearParticle();
            if (jumpDisappearVfx != null)
            {
                SpawnOneShotPooledVfx(jumpDisappearVfx, transform.position, Quaternion.identity, 2f);
            }
        });

        seq.Join(
            transform.DOLocalMoveY(transform.localPosition.y + 1f, moveDur)
                .SetEase(Ease.OutCubic)
        );
        // ── STEP 2: Rotate + Scale cùng lúc ──────────────
        seq.Join(
            DOTween.Sequence()
                .Join(
                    transform.DORotate(
                        new Vector3(0, 1080f, 0), // 3 vòng quanh Y
                        rotDur,
                        RotateMode.FastBeyond360
                    ).SetEase(Ease.OutCubic)
                )
                .Join(
                    transform.DOScale(Vector3.zero, scaleDur)
                        .SetEase(Ease.InBack)
                )
        );

        // ── STEP 3: Spawn VFX + destroy ──────────────────
        seq.AppendCallback(() =>
        {
            GameObject disappearVfx = GetDisappearParticle();
            if (disappearVfx != null)
            {
                SpawnOneShotPooledVfx(disappearVfx, transform.position, Quaternion.identity, 2f);
                AudioManager.Instance?.PlaySFX(Const.fireworkExplodeSFX);
            }

            GameEventHub.Instance.Invoke(GameEventType.OnShooterDisappear, this);

            Destroy(gameObject);
        });
    }



    private void OnJumpStart(object shooterObj)
    {
        if (shooterObj is BaseShooter targetShooter && targetShooter == this)
        {
            HandleJumpState();
        }
    }

    protected void HandleJumpState()
    {
        // Reset cadence so newly picked shooter starts audio rhythm from its own first shot.
        ResetShootSfxState();

        gridItem?.SetEmptyItem();
        SetState(ShooterState.Jumping);

        if (slotBar != null)
        {
            if (!slotBar.TryReserveNextAvailableSlot(out Slot targetParent, out Vector3 targetPosition))
            {
                SetState(ShooterState.IdleGrid);
                return;
            }

            transform.DOKill(false);
            // Keep shooter under the reserved deck slot during jump so restart cleanup cannot miss it.
            transform.SetParent(targetParent.transform, true);
            Vector3 startScale = transform.localScale;
            float safeDeckScaleMultiplier = GetDeckLandingScaleMultiplier();
            Vector3 targetScale = startScale * safeDeckScaleMultiplier;
            bool runJumpTweenUnscaled = ShouldRunJumpTweenUnscaled();
            Vector3 jumpStartVfxPos = GetJumpVfxSpawnPosition();
            Quaternion jumpStartVfxRot = transform.rotation * Quaternion.Euler(GetJumpVfxRotationOffsetEuler());

            GameObject startJumpVfx = GetStartJumpVfx();
            if (startJumpVfx != null)
            {
                Vector3 startJumpSpawnPos = jumpStartVfxPos + Vector3.up * 0.1f;
                SpawnOneShotPooledVfx(startJumpVfx, startJumpSpawnPos, Quaternion.identity, 2f);
            }

            OnJumpStartToDeck(jumpStartVfxPos, jumpStartVfxRot);

            float jumpDur = GetEffectiveAnimDuration(0.25f);
            Ease jumpScaleEase = GetJumpScaleEase();
            Ease jumpMoveEase = GetJumpMoveEase();
            Tween jumpScaleTween = transform.DOScale(targetScale, jumpDur).SetEase(jumpScaleEase);
            Tween jumpMoveTween = transform.DOJump(targetPosition + Vector3.up * slotLandingYOffset, 1.2f, 1, jumpDur).SetEase(jumpMoveEase);
            Tween jumpRotateTween = transform.DOLocalRotate(new Vector3(0f, 180f, 0f), jumpDur).SetEase(jumpMoveEase);
            if (runJumpTweenUnscaled)
            {
                jumpScaleTween.SetUpdate(true);
                jumpMoveTween.SetUpdate(true);
                jumpRotateTween.SetUpdate(true);
            }

            jumpMoveTween.OnComplete(() =>
            {
                AudioManager.Instance?.PlaySFX(Const.landingSFX);

                ResetRecoilPoseImmediate();

                if (targetParent != null)
                {
                    transform.SetParent(targetParent.transform, true);
                    transform.localPosition = new Vector3(0f, slotLandingYOffset, 0f);
                    transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    targetParent.SetShooter(this);
                }
                SetState(ShooterState.Idle);
                slotBar.RegisterShooter(this);
                GameEventHub.Instance?.Invoke(GameEventType.OnBoosterButtonRefresh, null);
                UpdateBulletCountText();
                UpdateCountTextVisibilityAndAlpha();
                Vector3 jumpVfxPos = GetJumpVfxSpawnPosition();
                Quaternion jumpVfxRot = transform.rotation * Quaternion.Euler(GetJumpVfxRotationOffsetEuler());
                OnJumpLandingDeck(jumpVfxPos, jumpVfxRot);

                GameObject jumpEffectVfx = GetJumpEffect();
                if (jumpEffectVfx != null)
                {
                    SpawnOneShotPooledVfx(jumpEffectVfx, jumpVfxPos, jumpVfxRot, 2f);
                }
            });
        }
    }

    protected virtual bool ShouldRunJumpTweenUnscaled()
    {
        return false;
    }

    protected virtual void OnJumpStartToDeck(Vector3 jumpVfxPosition, Quaternion jumpVfxRotation)
    {
    }

    protected virtual void OnJumpLandingDeck(Vector3 jumpVfxPosition, Quaternion jumpVfxRotation)
    {
    }

    public GameObject SpawnOneShotShooterVfx(GameObject vfxPrefab, Vector3 position, Quaternion rotation, float fallbackLifetime = 2f)
    {
        return SpawnOneShotPooledVfx(vfxPrefab, position, rotation, fallbackLifetime);
    }

    private GameObject SpawnOneShotPooledVfx(GameObject vfxPrefab, Vector3 position, Quaternion rotation, float fallbackLifetime)
    {
        if (vfxPrefab == null)
        {
            return null;
        }

        GameObject spawnedVfx = ObjectPoolManager.SpawnObject(
            vfxPrefab,
            position,
            rotation,
            ObjectPoolManager.PoolType.Particle
        );

        float estimatedLifetime = EstimateVfxLifetime(spawnedVfx);
        float returnDelay = estimatedLifetime > 0f ? estimatedLifetime : Mathf.Max(0.1f, fallbackLifetime);

        DOVirtual.DelayedCall(returnDelay, () =>
        {
            if (spawnedVfx != null && spawnedVfx.activeInHierarchy)
            {
                ObjectPoolManager.ReturnObject(spawnedVfx, ObjectPoolManager.PoolType.Particle);
            }
        }, true);

        return spawnedVfx;
    }

    private float EstimateVfxLifetime(GameObject vfxObject)
    {
        if (vfxObject == null)
        {
            return 0f;
        }

        ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems == null || particleSystems.Length == 0)
        {
            return 0f;
        }

        float maxLifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = ps.main;
            if (main.loop)
            {
                continue;
            }

            float systemLifetime = Mathf.Max(0f, main.duration) + GetParticleStartLifetime(main.startLifetime);
            if (systemLifetime > maxLifetime)
            {
                maxLifetime = systemLifetime;
            }
        }

        if (maxLifetime <= 0f)
        {
            return 0f;
        }

        return maxLifetime + 0.15f;
    }

    private float GetParticleStartLifetime(ParticleSystem.MinMaxCurve lifetimeCurve)
    {
        switch (lifetimeCurve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return lifetimeCurve.constant;

            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(lifetimeCurve.constantMin, lifetimeCurve.constantMax);

            case ParticleSystemCurveMode.Curve:
                if (lifetimeCurve.curve != null && lifetimeCurve.curve.length > 0)
                {
                    return lifetimeCurve.curve.keys[lifetimeCurve.curve.length - 1].value * lifetimeCurve.curveMultiplier;
                }
                return lifetimeCurve.constant;

            case ParticleSystemCurveMode.TwoCurves:
                float maxValue = 0f;
                if (lifetimeCurve.curveMin != null && lifetimeCurve.curveMin.length > 0)
                {
                    maxValue = Mathf.Max(maxValue, lifetimeCurve.curveMin.keys[lifetimeCurve.curveMin.length - 1].value);
                }
                if (lifetimeCurve.curveMax != null && lifetimeCurve.curveMax.length > 0)
                {
                    maxValue = Mathf.Max(maxValue, lifetimeCurve.curveMax.keys[lifetimeCurve.curveMax.length - 1].value);
                }
                return maxValue * lifetimeCurve.curveMultiplier;

            default:
                return lifetimeCurve.constant;
        }
    }

    private void PlayAnimation(string animationName, bool loop = false)
    {
        if (animationComponent == null || string.IsNullOrEmpty(animationName))
        {
            return;
        }

        AnimationClip clip = animationComponent.GetClip(animationName);
        if (clip == null)
        {
            return;
        }

        float targetSpeed = Mathf.Max(0.1f, mul);
        WrapMode targetWrapMode = loop ? WrapMode.Loop : WrapMode.Once;

        if (animationComponent.IsPlaying(animationName))
        {
            AnimationState activeState = animationComponent[animationName];
            if (activeState != null)
            {
                if (activeState.wrapMode != targetWrapMode)
                {
                    activeState.wrapMode = targetWrapMode;
                }

                if (Mathf.Abs(activeState.speed - targetSpeed) > 0.001f)
                {
                    activeState.speed = targetSpeed;
                }
            }

            lastAppliedAnimationSpeed = targetSpeed;
            return;
        }

        animationComponent.Stop();
        animationComponent[animationName].wrapMode = targetWrapMode;
        animationComponent[animationName].speed = targetSpeed;
        animationComponent.Play(animationName);
        lastAppliedAnimationSpeed = targetSpeed;
    }

    private void PlayAnimationIfNotPlaying(string animationName, bool loop = false)
    {
        if (animationComponent == null || string.IsNullOrEmpty(animationName))
        {
            return;
        }

        if (animationComponent.IsPlaying(animationName))
        {
            float targetSpeed = Mathf.Max(0.1f, mul);
            AnimationState activeState = animationComponent[animationName];
            if (activeState != null)
            {
                WrapMode targetWrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                if (activeState.wrapMode != targetWrapMode)
                {
                    activeState.wrapMode = targetWrapMode;
                }

                if (Mathf.Abs(activeState.speed - targetSpeed) > 0.001f)
                {
                    activeState.speed = targetSpeed;
                }
            }

            lastAppliedAnimationSpeed = targetSpeed;
            return;
        }

        PlayAnimation(animationName, loop);
    }

    private void ResetShootSfxState()
    {
        hasPlayedShootSfxSinceReset = false;
    }

    // Hàm Update tốc độ Animation cũ (Legacy) nếu lỡ bị đổi speed giữa chừng
    private void UpdateAnimationSpeed()
    {
        if (animationComponent == null || !animationComponent.isPlaying)
        {
            return;
        }

        float targetSpeed = Mathf.Max(0.1f, mul);
        if (Mathf.Abs(targetSpeed - lastAppliedAnimationSpeed) <= 0.001f && Time.time < nextAnimationSpeedRefreshTime)
        {
            return;
        }

        nextAnimationSpeedRefreshTime = Time.time + Mathf.Max(0.02f, animationSpeedRefreshInterval);

        foreach (AnimationState state in animationComponent)
        {
            if (!state.enabled) // Clip này đang chạy
            {
                continue;
            }

            if (Mathf.Abs(state.speed - targetSpeed) > 0.001f)
            {
                state.speed = targetSpeed;
            }
        }

        lastAppliedAnimationSpeed = targetSpeed;
    }

    private float GetEffectiveAnimDuration(float baseDuration)
    {
        // Tránh chia cho 0 hoặc số quá nhỏ gây giật lag
        float safeMul = Mathf.Max(0.1f, SpeedMultiplierManager.GetBaseMultiplier());
        return baseDuration / safeMul;
    }

    private void PlayRecoilEffect()
    {
        Transform recoilTransform = ResolveRecoilTransform();
        if (recoilTransform == null)
        {
            return;
        }

        if (cachedPrimaryShooterRenderer == null)
        {
            cachedPrimaryShooterRenderer = GetPrimaryShooterRenderer();
        }

        if (cachedPrimaryShooterRenderer != null && !cachedPrimaryShooterRenderer.enabled)
        {
            return;
        }

        // 1. Lấy multiplier hiện tại
        float mul = SpeedMultiplierManager.Instance.GetCurrentMultiplier();

        // 2. Giữ recoil luôn thấy rõ khi speedup: tăng lực vừa phải, không tăng tuyến tính quá mạnh.
        float recoilIntensity = Mathf.Lerp(1f, Mathf.Max(1f, mul), 0.35f);

        // Thời gian giật và hồi đã được scale tự động
        float configuredRecoilDuration = GetRecoilDuration();
        float configuredRecoilDistance = GetRecoilDistance();
        // Scale mềm theo speedup + ngưỡng tối thiểu để tránh recoil biến mất khi tốc độ cao.
        float speedScale = Mathf.Sqrt(Mathf.Max(1f, SpeedMultiplierManager.GetBaseMultiplier()));
        float kickTime = Mathf.Max(0.03f, (configuredRecoilDuration * 0.2f) / speedScale);
        float recoverTime = Mathf.Max(0.06f, (configuredRecoilDuration * 0.8f) / speedScale);

        recoilSequence?.Kill();
        recoilTransform.localRotation = originalVisualLocalRot;

        // --- TÍNH TOÁN GÓC GIẬT NGỬA NÒNG (không lùi theo local Z) ---
        float baseUpwardKickAngle = Mathf.Clamp(configuredRecoilDistance * 35f, 1f, 50f);
        float randomKickX = UnityEngine.Random.Range(baseUpwardKickAngle * 0.75f, baseUpwardKickAngle) * recoilIntensity;
        float randomKickY = UnityEngine.Random.Range(-1f, 1f) * (randomKickX * 0.5f);

        Vector3 kickEuler = new Vector3(randomKickX, randomKickY, 0f);
        Vector3 kickTargetEuler = (originalVisualLocalRot * Quaternion.Euler(kickEuler)).eulerAngles;
        recoilSequence = DOTween.Sequence();
        recoilSequence.SetUpdate(true);
        recoilSequence.Append(
            recoilTransform
                .DOLocalRotate(kickTargetEuler, kickTime, RotateMode.Fast)
                .SetEase(Ease.OutFlash)
        );
        recoilSequence.Append(
            recoilTransform
                .DOLocalRotate(originalVisualLocalRot.eulerAngles, recoverTime, RotateMode.Fast)
                .SetEase(Ease.InOutSine)
        );
    }

    private void ResetRecoilPoseImmediate()
    {
        recoilSequence?.Kill();
        recoilSequence = null;

        Transform recoilTransform = ResolveRecoilTransform();
        if (recoilTransform == null)
        {
            return;
        }

        recoilTransform.DOKill(false);
        recoilTransform.localPosition = originalVisualLocalPos;
        recoilTransform.localRotation = originalVisualLocalRot;
    }

    public void PlayTouchLockAnimation()
    {
        bool wasPlaying = animationComponent != null && animationComponent.IsPlaying("TouchLock");
        PlayAnimation("TouchLock", false);

        if (!wasPlaying)
        {
            AudioManager.Instance?.PlaySFX(Const.popLockSFX);
        }
    }

    /// <summary>
    /// [Booster] Phát animation highlight khi PickLockedShooter mode đang active.
    /// </summary>
    public void PlayBoosterHighlightAnimation()
    {
        if (currentState != ShooterState.Lock)
        {
            return;
        }

        KillBoosterHighlightTween();

        if (!HasAnimationClip("Booster"))
        {
            PlayLockIdleLoopAnimation();
            return;
        }

        PlayAnimation("Booster", false);

        float boosterDuration = GetAnimationClipDuration("Booster");
        boosterHighlightTween = DOVirtual.DelayedCall(boosterDuration, () =>
        {
            boosterHighlightTween = null;
            if (currentState == ShooterState.Lock)
            {
                PlayLockIdleLoopAnimation();
            }
        });
    }

    /// <summary>
    /// [Booster] Trả về animation lock bình thường sau khi mode kết thúc.
    /// </summary>
    public void StopBoosterHighlightAnimation()
    {
        KillBoosterHighlightTween();

        if (currentState == ShooterState.Lock)
            PlayAnimation("TouchLock", false);
    }

    private void PlayLockIdleLoopAnimation()
    {
        if (HasAnimationClip("Idle_Booster"))
        {
            PlayAnimation("Idle_Booster", true);
            return;
        }

        if (HasAnimationClip("Idle_BaseLock"))
        {
            PlayAnimation("Idle_BaseLock", true);
            return;
        }

        if (HasAnimationClip("Idle"))
        {
            PlayAnimation("Idle", true);
            return;
        }

        PlayAnimation("TouchLock", false);
    }

    private bool HasAnimationClip(string animationName)
    {
        return animationComponent != null
               && !string.IsNullOrEmpty(animationName)
               && animationComponent.GetClip(animationName) != null;
    }

    private float GetAnimationClipDuration(string animationName)
    {
        if (animationComponent == null || string.IsNullOrEmpty(animationName))
        {
            return 0.01f;
        }

        AnimationClip clip = animationComponent.GetClip(animationName);
        if (clip == null)
        {
            return 0.01f;
        }

        float speed = Mathf.Max(0.1f, mul);
        return Mathf.Max(0.01f, clip.length / speed);
    }

    private void KillBoosterHighlightTween()
    {
        if (boosterHighlightTween != null && boosterHighlightTween.IsActive())
        {
            boosterHighlightTween.Kill();
        }

        boosterHighlightTween = null;
    }


    public int GetBulletCount()
    {
        return bulletCount;
    }

    public int ConsumeAmmoExternally(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return 0;
        }

        int before = Mathf.Max(0, bulletCount);
        if (before <= 0)
        {
            return 0;
        }

        int consumed = Mathf.Min(before, safeAmount);
        bulletCount = Mathf.Max(0, before - consumed);

        if (countTextGO != null && countTextGO.gameObject.activeSelf)
        {
            AnimateBulletCountText(before, bulletCount, 0.22f);
        }
        else
        {
            UpdateBulletCountText();
        }

        GameEventHub.Instance?.Invoke(GameEventType.OnBulletCountChanged, bulletCount);

        if (bulletCount <= 0)
        {
            HandleOutOfAmmo();
        }

        return consumed;
    }

    public ShooterState GetCurrentState()
    {
        return currentState;
    }

    public SeedColor GetTargetColor()
    {
        return targetColor;
    }

    /// <summary>Dùng bởi Tunnel khi "bắn" shooter vào ô lưới mục tiêu.</summary>
    public void SetGridItem(GridItem gi) { gridItem = gi; }
    public void SetSlotBar(SlotBar bar) { slotBar = bar; }
    public void SetGridController(GridController gc) { gridController = gc; }
    public void SetCamera(Camera cam) { _heroCamera = cam; }

    public int GetBulletDecreaseAmount() => Mathf.Max(1, GetBulletDecreaseAmountInternal());

    private GameObject GetBulletPrefab()
    {
        if (combatConfig != null && combatConfig.bulletPrefab != null)
        {
            return combatConfig.bulletPrefab;
        }

        return bulletPrefab;
    }

    private GameObject GetDisappearParticle()
    {
        if (combatConfig != null && combatConfig.disappearParticle != null)
        {
            return combatConfig.disappearParticle;
        }

        return disappearParticle;
    }

    private GameObject GetJumpDisappearParticle()
    {
        if (combatConfig != null && combatConfig.jumpDisappearParticle != null)
        {
            return combatConfig.jumpDisappearParticle;
        }

        return jumpDisappearParticle;
    }

    private GameObject GetJumpEffect()
    {
        if (combatConfig != null && combatConfig.jumpEffect != null)
        {
            return combatConfig.jumpEffect;
        }

        return jumpEffect;
    }

    private Ease GetJumpScaleEase()
    {
        if (combatConfig != null)
        {
            return combatConfig.jumpScaleEase;
        }

        return Ease.OutSine;
    }

    private Ease GetJumpMoveEase()
    {
        if (combatConfig != null)
        {
            return combatConfig.jumpMoveEase;
        }

        return Ease.Linear;
    }

    private GameObject GetStartJumpVfx()
    {
        if (combatConfig != null && combatConfig.startJumpVfx != null)
        {
            return combatConfig.startJumpVfx;
        }

        return null;
    }

    private Renderer GetPrimaryShooterRenderer()
    {
        if (mesh != null)
        {
            return mesh;
        }

        if (visualTransform != null)
        {
            Renderer visualRenderer = visualTransform.GetComponentInChildren<Renderer>(true);
            if (visualRenderer != null)
            {
                return visualRenderer;
            }
        }

        return GetComponentInChildren<Renderer>(true);
    }

    private float GetFireRate()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0.1f, combatConfig.fireRate);
        }

        return Mathf.Max(0.1f, fireRate);
    }

    private int GetBulletsPerShot()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0, combatConfig.bulletsPerShot);
        }

        return Mathf.Max(0, bulletsPerShot);
    }

    private float GetBulletSpreadShotDelay()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.bulletSpreadShotDelay);
        }

        return Mathf.Max(0f, bulletSpreadShotDelay);
    }

    private float GetBulletSpreadRadius()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.bulletSpreadRadius);
        }

        return Mathf.Max(0f, bulletSpreadRadius);
    }

    private float GetSeedShotInterval()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.seedShotInterval);
        }

        return Mathf.Max(0f, seedShotInterval);
    }

    private float GetRowHandoffDelay()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.rowHandoffDelay);
        }

        return Mathf.Max(0f, rowHandoffDelay);
    }

    private float GetJumpVfxYOffset()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.jumpVfxYOffset);
        }

        return 0.01f;
    }

    private float GetJumpVfxTowardCameraOffset()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.jumpVfxTowardCameraOffset);
        }

        return 0.06f;
    }

    private Vector3 GetJumpVfxRotationOffsetEuler()
    {
        if (combatConfig != null)
        {
            return combatConfig.jumpVfxRotationOffsetEuler;
        }

        return Vector3.zero;
    }

    private Vector3 GetJumpVfxSpawnPosition()
    {
        Vector3 spawnPos = transform.position + Vector3.up * GetJumpVfxYOffset();
        float cameraOffset = GetJumpVfxTowardCameraOffset();
        if (cameraOffset <= 0f)
        {
            return spawnPos;
        }

        Camera cam = _heroCamera != null ? _heroCamera : Camera.main;
        if (cam == null)
        {
            return spawnPos;
        }

        return spawnPos - cam.transform.forward * cameraOffset;
    }

    private int GetBulletDecreaseAmountInternal()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(1, combatConfig.bulletDecreaseAmount);
        }

        return Mathf.Max(1, bulletDecreaseAmount);
    }

    private float GetRecoilDuration()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0.001f, combatConfig.recoilDuration);
        }

        return Mathf.Max(0.001f, recoilDuration);
    }

    private float GetRecoilDistance()
    {
        if (combatConfig != null)
        {
            return Mathf.Max(0f, combatConfig.recoilDistance);
        }

        return Mathf.Max(0f, recoilDistance);
    }

    private float GetDeckOutlineDarkenAmount()
    {
        if (combatConfig != null)
        {
            return Mathf.Clamp01(combatConfig.deckOutlineDarkenAmount);
        }

        return Mathf.Clamp01(defaultDeckOutlineDarkenAmount);
    }

    private float GetDeckLandingScaleMultiplier()
    {
        if (combatConfig != null)
        {
            return Mathf.Clamp(combatConfig.deckLandingScaleMultiplier, 0.8f, 1.2f);
        }

        return Mathf.Clamp(defaultDeckLandingScaleMultiplier, 0.8f, 1.2f);
    }

    // ──────────────────────────────────────────────────────────────────
    // Hero Booster sequence
    // ──────────────────────────────────────────────────────────────────

    public void StartHeroSequence(HeroShooterBoosterConfig cfg, List<BlockRowSeedSpawner> targets)
    {
        heroCfg = cfg;
        if (!isInHeroMode)
        {
            isInHeroMode = true;
            activeHeroCount++;
        }
        isHeroReturning = false;

        // Save slot context for potential return
        stateScaleTween?.Kill();
        heroSlotParent = transform.parent;
        heroSlotWorldPos = transform.position;
        heroSlotLocalScale = baseLocalScale;

        // Pre-fill target queue
        targetQueue.Clear();
        queuedTargets.Clear();
        targetObject = null;
        isDestroySequenceRequested = false;

        foreach (var t in targets)
        {
            if (t != null && t.GetSeedCount() > 0)
            {
                targetQueue.Enqueue(t);
                queuedTargets.Add(t);
            }
        }

        // Detach from slot
        transform.SetParent(null);
        SetState(ShooterState.Hero);

        // Play jump effect at launch
        GameObject heroJumpEffect = GetJumpEffect();
        if (heroJumpEffect != null)
        {
            Quaternion heroJumpVfxRot = transform.rotation * Quaternion.Euler(GetJumpVfxRotationOffsetEuler());
            ObjectPoolManager.SpawnObject(
                heroJumpEffect,
                GetJumpVfxSpawnPosition(),
                heroJumpVfxRot,
                ObjectPoolManager.PoolType.Particle);
        }

        Camera cam = _heroCamera != null ? _heroCamera : Camera.main;
        CacheHeroCameraRig(cam);

        Vector3 flyDest = transform.position + Vector3.up * cfg.flyHeight;

        Sequence seq = DOTween.Sequence();

        float flyDur = GetEffectiveAnimDuration(cfg.flyDuration);
        float camFocusDur = GetEffectiveAnimDuration(cfg.cameraFocusDuration);
        float scaleMul = (cfg != null && cfg.heroScaleMultiplier > 0f) ? cfg.heroScaleMultiplier : 1.35f;
        Vector3 targetHeroScale = heroSlotLocalScale * scaleMul;

        // 1. HIỆP 1: Hero nảy lên vị trí cao nhất trước (kèm scale dần lên)
        seq.Append(transform.DOMove(flyDest, flyDur).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(targetHeroScale, flyDur).SetEase(Ease.OutQuad));

        // 2. HIỆP 2: SAU KHI Hero lên tới đỉnh, Camera mới bắt đầu hành động
        float cameraOffsetY = GetHeroCameraFocusOffsetY(cfg);
        float targetCamLocalY = heroOrigCamLocalPos.y + cameraOffsetY;
        Tween cameraMoveTween = null;

        // Camera CHỈ di chuyển trục Y lên trên
        for (int i = 0; i < heroRigCameraTransforms.Count; i++)
        {
            Transform camTransform = heroRigCameraTransforms[i];
            if (camTransform == null)
            {
                continue;
            }

            float originalY = i < heroRigCameraOrigLocalPositions.Count
                ? heroRigCameraOrigLocalPositions[i].y
                : camTransform.localPosition.y;
            Tween moveTween = camTransform.DOLocalMoveY(originalY + cameraOffsetY, camFocusDur).SetEase(Ease.InOutSine);

            if (cameraMoveTween == null)
            {
                seq.Append(moveTween);
                cameraMoveTween = moveTween;
            }
            else
            {
                seq.Join(moveTween);
            }
        }

        if (cameraMoveTween == null && cam != null)
        {
            seq.Append(cam.transform.DOLocalMoveY(targetCamLocalY, camFocusDur).SetEase(Ease.InOutSine));
        }
        else if (cameraMoveTween == null)
        {
            seq.AppendInterval(camFocusDur);
        }

        // Cùng lúc di chuyển Y, Camera giảm size (Zoom in)
        float heroFocusOrthoSize = GetHeroFocusOrthoSize(cfg);
        if (heroFocusOrthoSize > 0f)
        {
            bool hasZoomTween = false;
            for (int i = 0; i < heroRigCameras.Count; i++)
            {
                Camera rigCam = heroRigCameras[i];
                if (rigCam == null || !rigCam.orthographic)
                {
                    continue;
                }

                seq.Join(rigCam.DOOrthoSize(heroFocusOrthoSize, camFocusDur).SetEase(Ease.InOutSine));
                hasZoomTween = true;
            }

            if (!hasZoomTween && cam != null && cam.orthographic)
            {
                seq.Join(cam.DOOrthoSize(heroFocusOrthoSize, camFocusDur).SetEase(Ease.InOutSine));
            }
        }

        seq.OnComplete(() =>
        {
            PlayAnimation("ShooterInDeck", false);
            SetState(ShooterState.Idle);
        });
    }

    /// <summary>
    /// Trả hero về slot cũ. 
    /// Hero rơi xuống ĐỒNG THỜI Camera nhả Zoom và hạ Y về gốc.
    /// Nếu còn đạn, trở về IdleGrid state. Nếu hết đạn, disappear bình thường.
    /// </summary>
    private void HeroReturnToSlot()
    {
        if (isHeroReturning) return;
        isHeroReturning = true;
        if (isInHeroMode)
        {
            isInHeroMode = false;
            activeHeroCount = Mathf.Max(0, activeHeroCount - 1);
        }

        Camera cam = _heroCamera != null ? _heroCamera : Camera.main;

        Sequence seq = DOTween.Sequence();

        float returnDur = GetEffectiveAnimDuration(heroCfg.cameraReturnDuration);
        Tween cameraMoveTween = null;

        // 1. Camera lùi Y về vị trí ban đầu
        for (int i = 0; i < heroRigCameraTransforms.Count; i++)
        {
            Transform camTransform = heroRigCameraTransforms[i];
            if (camTransform == null)
            {
                continue;
            }

            float originalY = i < heroRigCameraOrigLocalPositions.Count
                ? heroRigCameraOrigLocalPositions[i].y
                : camTransform.localPosition.y;

            Tween moveTween = camTransform.DOLocalMoveY(originalY, returnDur).SetEase(Ease.InOutSine);
            if (cameraMoveTween == null)
            {
                seq.Append(moveTween);
                cameraMoveTween = moveTween;
            }
            else
            {
                seq.Join(moveTween);
            }
        }

        if (cameraMoveTween == null && cam != null)
        {
            seq.Append(cam.transform.DOLocalMoveY(heroOrigCamLocalPos.y, returnDur).SetEase(Ease.InOutSine));
        }
        else if (cameraMoveTween == null)
        {
            seq.AppendInterval(returnDur);
        }

        // 2. Cùng lúc đó, Camera nhả Zoom (Orthographic size trở về như cũ)
        if (heroCfg.cameraZoomSize > 0f)
        {
            bool hasZoomTween = false;
            for (int i = 0; i < heroRigCameras.Count; i++)
            {
                Camera rigCam = heroRigCameras[i];
                if (rigCam == null || !rigCam.orthographic)
                {
                    continue;
                }

                float origSize = i < heroRigCameraSizes.Count
                    ? heroRigCameraSizes[i]
                    : rigCam.orthographicSize;
                seq.Join(rigCam.DOOrthoSize(origSize, returnDur).SetEase(Ease.InOutSine));
                hasZoomTween = true;
            }

            if (!hasZoomTween && cam != null && cam.orthographic)
            {
                seq.Join(cam.DOOrthoSize(heroOrigCamSize, returnDur).SetEase(Ease.InOutSine));
            }
        }

        // 3. CŨNG CÙNG LÚC ĐÓ, Hero bắt đầu nhảy (rơi) về vị trí Slot và scale nhỏ lại về scale gốc
        // Dùng seq.Join() để Hero bay về khớp thời gian với lúc Camera lùi lại
        Vector3 targetReturnPos = (heroSlotParent != null ? heroSlotParent.position : heroSlotWorldPos) + Vector3.up * slotLandingYOffset;
        seq.Join(transform.DOJump(targetReturnPos, 1.5f, 1, returnDur).SetEase(Ease.InOutQuad));
        seq.Join(transform.DOScale(heroSlotLocalScale, returnDur).SetEase(Ease.InOutQuad));
        seq.Join(transform.DOLocalRotate(new Vector3(0f, 180f, 0f), returnDur).SetEase(Ease.InOutQuad));

        seq.OnComplete(() =>
        {
            isHeroReturning = false;

            if (heroSlotParent != null)
                transform.SetParent(heroSlotParent);

            // Reset position, rotation & scale
            transform.localPosition = new Vector3(0f, slotLandingYOffset, 0f);
            transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            transform.localScale = heroSlotLocalScale;
            heroSlotParent = null;

            // Nếu còn đạn, quay về IdleGrid; nếu hết đạn thì disappear
            if (bulletCount > 0)
            {
                PlayAnimation("Idle_BaseDock", true);
                SetState(ShooterState.Idle);
            }

        });
    }

    private void CacheHeroCameraRig(Camera referenceCamera)
    {
        heroRigCameras.Clear();
        heroRigCameraSizes.Clear();
        heroRigCameraTransforms.Clear();
        heroRigCameraOrigPositions.Clear();
        heroRigCameraOrigLocalPositions.Clear();
        heroCameraRoot = null;
        heroOrigCamRootPos = Vector3.zero;
        heroOrigCamPos = Vector3.zero;
        heroOrigCamLocalPos = Vector3.zero;
        heroOrigCamSize = 0f;

        if (referenceCamera == null)
        {
            return;
        }

        heroOrigCamPos = referenceCamera.transform.position;
        heroOrigCamLocalPos = referenceCamera.transform.localPosition;
        heroOrigCamSize = referenceCamera.orthographicSize;

        Transform root = referenceCamera.transform.parent != null
            ? referenceCamera.transform.parent
            : referenceCamera.transform;

        heroCameraRoot = root;
        heroOrigCamRootPos = root.position;

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        if (cameras == null || cameras.Length == 0)
        {
            heroRigCameras.Add(referenceCamera);
            heroRigCameraSizes.Add(referenceCamera.orthographicSize);
            heroRigCameraTransforms.Add(referenceCamera.transform);
            heroRigCameraOrigPositions.Add(referenceCamera.transform.position);
            heroRigCameraOrigLocalPositions.Add(referenceCamera.transform.localPosition);
            return;
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null)
            {
                continue;
            }

            heroRigCameras.Add(cam);
            heroRigCameraSizes.Add(cam.orthographicSize);
            heroRigCameraTransforms.Add(cam.transform);
            heroRigCameraOrigPositions.Add(cam.transform.position);
            heroRigCameraOrigLocalPositions.Add(cam.transform.localPosition);
        }
    }

    protected virtual void OnDestroy()
    {
        UnregisterShooter(this);
        ReleaseMagicStoneComboShooterVfx();
        KillBoosterHighlightTween();
        bulletTextTween?.Kill();
        GameEventHub.Instance.RemoveListener(GameEventType.OnShooterJumpStart, OnJumpStart);
    }
}