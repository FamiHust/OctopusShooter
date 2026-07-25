using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Animate các element chính của level khi bắt đầu chơi.
/// </summary>
public class LevelElementAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject level;
    [SerializeField] private GridController gridController;
    [SerializeField] private SlotBar slotBar;
    [SerializeField] private GameObject outroOnlyObject;
    [SerializeField] private LevelElementAnimatorConfig config;

    [Header("Post Intro Render Optimization")]
    [SerializeField] private bool optimizeRenderersAfterIntro = true;
    [SerializeField] private bool optimizeShooterRenderersAfterIntro = true;
    [SerializeField] private bool optimizeWallRenderersAfterIntro = true;
    [SerializeField] private bool combineStaticWallMeshesAfterIntro = true;
    [SerializeField] private bool preserveShooterShadowCastingAfterIntro = true;
    [SerializeField] private bool disableWallShadowCastingAfterIntro = true;
    [SerializeField] private bool disableShooterReceiveShadowsAfterIntro = true;
    [SerializeField] private bool disableWallReceiveShadowsAfterIntro = true;
    [SerializeField] private bool disableLightAndReflectionProbesForOptimizedRenderers = true;
    [SerializeField] private bool deferPostIntroOptimization = true;
    [SerializeField, Min(0)] private int deferredPostIntroOptimizationFrames = 2;

    [Header("Intro Lite Mode")]
    [SerializeField] private bool enableLowEndIntroLiteMode = true;
    [SerializeField] private int lowEndSystemMemoryMb = 3000;
    [SerializeField] private int lowEndProcessorCount = 4;
    [SerializeField] private bool forceSkipIntroAnimation = false;
    [SerializeField] private bool skipWholeIntroTweenOnLowEnd = true;
    [SerializeField] private bool skipSlotIntroTweenOnLowEnd = true;
    [SerializeField] private bool skipPostIntroOptimizationOnLowEnd = true;
    [SerializeField] private bool skipStaticBatchingOnLowEnd = true;

    private bool PlayOnStart => config != null ? config.playOnStart : true;
    private float LevelStartZ => config != null ? config.levelStartZ : 2f;
    private float LevelEndZ => config != null ? config.levelEndZ : 0.65f;
    private float GridZOffsetFromLevel => config != null ? config.gridZOffsetFromLevel : 0.12f;
    private float AnimDuration => config != null ? config.duration : 1f;
    private float IntroLevelDelay => config != null ? config.introLevelDelay : 0f;
    private float IntroGridDelay => config != null ? config.introGridDelay : 0.08f;
    private float IntroSlotDelay => config != null ? config.introSlotDelay : 0.5f;
    private float OutroLevelDelay => config != null ? config.outroLevelDelay : 0f;
    private float OutroGridDelay => config != null ? config.outroGridDelay : 0.08f;
    private float OutroSlotDelay => config != null ? config.outroSlotDelay : 0.5f;
    private float OutroOnlyObjectDelay => config != null ? config.outroOnlyObjectDelay : 0f;
    private float SlotStartOffsetX => config != null ? config.slotStartOffsetX : 0.12f;
    private float SlotAnimDuration => config != null ? config.slotDuration : 1.15f;
    private float OutroOnlyObjectOffsetZ => config != null ? config.outroOnlyObjectOffsetZ : 1.35f;
    private Ease SlotAnimEase => config != null ? config.slotEase : Ease.InOutSine;
    private Ease AnimEase => config != null ? config.ease : Ease.OutBack;
    private Ease OutroAnimEase => config != null ? config.outroEase : Ease.InBack;

    private Sequence introSequence;
    private readonly System.Collections.Generic.List<Transform> introSlots = new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<Vector3> introSlotTargetLocalPositions = new System.Collections.Generic.List<Vector3>();
    private bool slotTargetsPrepared;
    private Vector3 outroOnlyObjectDefaultLocalPos;
    private bool outroOnlyObjectPoseCaptured;
    private bool hasAppliedPostIntroOptimization;
    private bool useLowEndIntroLiteMode;
    private Coroutine pendingPostIntroOptimizationRoutine;

    private void OnValidate()
    {
        TryAutoAssignConfig();

        Transform searchRoot = transform.root != null ? transform.root : transform;
        Transform[] children = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == searchRoot)
            {
                continue;
            }

            if (child.name.IndexOf("level", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                level = child.gameObject;
                break;
            }
        }

        if (level != null)
        {
            Transform levelRoot = level.transform.root != null ? level.transform.root : level.transform;

            if (gridController == null)
            {
                gridController = levelRoot.GetComponentInChildren<GridController>(true);
            }

            if (slotBar == null)
            {
                slotBar = levelRoot.GetComponentInChildren<SlotBar>(true);
            }
        }

        TryAutoAssignOutroOnlyObjectFromMainRoute();
    }

    private void TryAutoAssignOutroOnlyObjectFromMainRoute()
    {
        Transform searchRoot = transform.root != null ? transform.root : transform;
        SplineRoute[] routes = searchRoot.GetComponentsInChildren<SplineRoute>(true);
        if (routes == null || routes.Length == 0)
        {
            return;
        }

        for (int i = 0; i < routes.Length; i++)
        {
            SplineRoute route = routes[i];
            if (route == null)
            {
                continue;
            }

            if (route.GetRouteMode() != SplineRoute.RouteMode.Main)
            {
                continue;
            }

            Transform routeTransform = route.transform;
            if (routeTransform == null)
            {
                continue;
            }

            Transform parent = routeTransform.parent;
            if (parent == null)
            {
                continue;
            }

            int routeIndex = routeTransform.GetSiblingIndex();

            // Ưu tiên sibling đứng ngay sau Main route.
            if (routeIndex + 1 < parent.childCount)
            {
                Transform next = parent.GetChild(routeIndex + 1);
                if (next != null)
                {
                    outroOnlyObject = next.gameObject;
                    return;
                }
            }

            // Fallback: sibling đứng trước Main route.
            if (routeIndex - 1 >= 0)
            {
                Transform prev = parent.GetChild(routeIndex - 1);
                if (prev != null)
                {
                    outroOnlyObject = prev.gameObject;
                    return;
                }
            }

            // Fallback cuối: sibling đầu tiên khác chính Main route.
            for (int s = 0; s < parent.childCount; s++)
            {
                Transform sibling = parent.GetChild(s);
                if (sibling == null || sibling == routeTransform)
                {
                    continue;
                }

                outroOnlyObject = sibling.gameObject;
                return;
            }

            return;
        }
    }

    private void TryAutoAssignConfig()
    {
        if (config != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:LevelElementAnimatorConfig");
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        LevelElementAnimatorConfig found = AssetDatabase.LoadAssetAtPath<LevelElementAnimatorConfig>(path);
        if (found == null)
        {
            return;
        }

        config = found;
        EditorUtility.SetDirty(this);
#endif
    }

    public void PrepareInitialPose()
    {
        introSequence?.Kill();
        StopPendingPostIntroOptimizationRoutine();
        useLowEndIntroLiteMode = ShouldUseLowEndIntroLiteMode();
        CaptureOutroOnlyObjectDefaultLocalPos();
        hasAppliedPostIntroOptimization = false;
        ApplyInitialZPositions(true);
    }

    private void ApplyInitialZPositions(bool captureSlotTargets)
    {
        if (level != null)
        {
            Vector3 levelPos = level.transform.localPosition;
            levelPos.z = LevelStartZ;
            level.transform.localPosition = levelPos;
        }

        if (gridController != null)
        {
            Vector3 gridPos = gridController.transform.localPosition;
            gridPos.z = LevelStartZ + GridZOffsetFromLevel;
            gridController.transform.localPosition = gridPos;
        }

        ApplyInitialSlotPositions(captureSlotTargets);
    }

    public void PlayIntroAnimation()
    {
        if (!PlayOnStart)
        {
            ApplyInitialZPositions(true);
            return;
        }

        if (ShouldSkipIntroTweenNow())
        {
            introSequence?.Kill();
            ApplyEndZPositions(true);
            return;
        }

        PlayDirectionalAnimation(false);
    }

    public void PlayOutroAnimation()
    {
        PlayDirectionalAnimation(true);
    }

    public float GetIntroDuration()
    {
        if (!PlayOnStart)
        {
            return 0f;
        }

        if (ShouldSkipIntroTweenNow())
        {
            return 0f;
        }

        float levelGridDuration = 0f;
        if (level != null)
        {
            levelGridDuration = Mathf.Max(levelGridDuration, IntroLevelDelay + AnimDuration);
        }
        if (gridController != null)
        {
            levelGridDuration = Mathf.Max(levelGridDuration, IntroGridDelay + AnimDuration);
        }

        float slotDuration = 0f;
        bool allowSlotTween = !(useLowEndIntroLiteMode && skipSlotIntroTweenOnLowEnd);
        if (allowSlotTween && slotBar != null && slotBar.transform != null && slotBar.transform.childCount > 0)
        {
            slotDuration = IntroSlotDelay + SlotAnimDuration;
        }

        return Mathf.Max(levelGridDuration, slotDuration);
    }

    private bool ShouldSkipIntroTweenNow()
    {
        if (forceSkipIntroAnimation)
        {
            return true;
        }

        return useLowEndIntroLiteMode && skipWholeIntroTweenOnLowEnd;
    }

    public float GetOutroDuration()
    {
        float levelGridDuration = 0f;
        if (level != null)
        {
            levelGridDuration = Mathf.Max(levelGridDuration, OutroLevelDelay + AnimDuration);
        }
        if (gridController != null)
        {
            levelGridDuration = Mathf.Max(levelGridDuration, OutroGridDelay + AnimDuration);
        }
        if (outroOnlyObject != null)
        {
            levelGridDuration = Mathf.Max(levelGridDuration, OutroOnlyObjectDelay + AnimDuration);
        }

        float slotDuration = 0f;
        if (slotBar != null && slotBar.transform != null && slotBar.transform.childCount > 0)
        {
            slotDuration = OutroSlotDelay + SlotAnimDuration;
        }

        return Mathf.Max(levelGridDuration, slotDuration);
    }

    private void PlayDirectionalAnimation(bool reverse)
    {
        Ease activeEase = reverse ? OutroAnimEase : AnimEase;
        float levelDelay = reverse ? OutroLevelDelay : IntroLevelDelay;
        float gridDelay = reverse ? OutroGridDelay : IntroGridDelay;
        float slotDelay = reverse ? OutroSlotDelay : IntroSlotDelay;

        introSequence?.Kill();

        if (!reverse)
        {
            ResetOutroOnlyObjectToDefaultLocalPos();
        }

        if (reverse)
        {
            // Re-capture runtime slot layout trước outro để không dùng vị trí cache cũ từ intro.
            ApplyEndZPositions(true);
        }
        else
        {
            ApplyInitialZPositions(false);
        }

        Transform levelTransform = level != null ? level.transform : null;
        Transform gridTransform = gridController != null ? gridController.transform : null;

        if (levelTransform == null && gridTransform == null)
        {
            return;
        }

        introSequence = DOTween.Sequence();

        if (levelTransform != null)
        {
            Vector3 levelPos = levelTransform.localPosition;
            float startZ = reverse ? LevelEndZ : LevelStartZ;
            float endZ = reverse ? LevelStartZ : LevelEndZ;

            levelPos.z = startZ;
            levelTransform.localPosition = levelPos;

            Vector3 levelTargetPos = levelPos;
            levelTargetPos.z = endZ;
            introSequence.Insert(levelDelay, levelTransform.DOLocalMove(levelTargetPos, AnimDuration).SetEase(activeEase));
        }

        if (gridTransform != null)
        {
            float gridStartZ = (reverse ? LevelEndZ : LevelStartZ) + GridZOffsetFromLevel;
            float gridEndZ = (reverse ? LevelStartZ : LevelEndZ) + GridZOffsetFromLevel;

            Vector3 gridPos = gridTransform.localPosition;
            gridPos.z = gridStartZ;
            gridTransform.localPosition = gridPos;

            Vector3 gridTargetPos = gridPos;
            gridTargetPos.z = gridEndZ;
            introSequence.Insert(gridDelay, gridTransform.DOLocalMove(gridTargetPos, AnimDuration).SetEase(activeEase));
        }

        if (reverse)
        {
            AppendOutroOnlyObjectTween(introSequence, activeEase, OutroOnlyObjectDelay);
        }

        bool skipSlotTween = useLowEndIntroLiteMode && skipSlotIntroTweenOnLowEnd && !reverse;
        if (!skipSlotTween)
        {
            AnimateSlotBarSlots(introSequence, reverse, slotDelay);
        }
    }

    private void OnDestroy()
    {
        introSequence?.Kill();
        StopPendingPostIntroOptimizationRoutine();
    }

    private void AnimateSlotBarSlots(Sequence sequence, bool reverse, float slotDelay)
    {
        if (sequence == null || slotBar == null)
        {
            return;
        }

        if (reverse)
        {
            AnimateAllCurrentSlotsOnOutro(sequence, slotDelay);
            return;
        }

        if (introSlots.Count == 0 || introSlotTargetLocalPositions.Count != introSlots.Count)
        {
            return;
        }

        for (int i = 0; i < introSlots.Count; i++)
        {
            Transform slot = introSlots[i];
            if (slot == null)
            {
                continue;
            }

            Vector3 targetLocalPos = introSlotTargetLocalPositions[i];
            Vector3 startLocalPos = GetSlotIntroStartLocalPos(targetLocalPos, i);

            if (reverse)
            {
                slot.localPosition = targetLocalPos;
                sequence.Insert(slotDelay, slot.DOLocalMove(startLocalPos, SlotAnimDuration).SetEase(OutroAnimEase));
            }
            else
            {
                sequence.Insert(slotDelay, slot.DOLocalMove(targetLocalPos, SlotAnimDuration).SetEase(SlotAnimEase));
            }
        }
    }

    private void ApplyInitialSlotPositions(bool captureTargets)
    {
        if (slotBar == null)
        {
            return;
        }

        Transform slotRoot = slotBar.transform;
        if (slotRoot == null || slotRoot.childCount == 0)
        {
            return;
        }

        int maxSlotCount = Mathf.Min(4, slotRoot.childCount);

        if (captureTargets || !slotTargetsPrepared)
        {
            introSlots.Clear();
            introSlotTargetLocalPositions.Clear();

            for (int i = 0; i < maxSlotCount; i++)
            {
                Transform slot = slotRoot.GetChild(i);
                if (slot == null)
                {
                    continue;
                }

                bool fromLeft = i == 0 || i == 1;
                bool fromRight = i == 2 || i == 3;
                if (!fromLeft && !fromRight)
                {
                    continue;
                }

                // Lưu vị trí gốc trước khi set vị trí start.
                introSlots.Add(slot);
                introSlotTargetLocalPositions.Add(slot.localPosition);
            }

            slotTargetsPrepared = true;
        }

        if (!slotTargetsPrepared || introSlots.Count == 0)
        {
            return;
        }

        for (int i = 0; i < introSlots.Count; i++)
        {
            Transform slot = introSlots[i];
            if (slot == null)
            {
                continue;
            }

            Vector3 targetLocalPos = introSlotTargetLocalPositions[i];
            Vector3 startLocalPos = GetSlotIntroStartLocalPos(targetLocalPos, i);

            // Set ngay pose đầu cho slotbar, giống cách level/grid set ngay từ đầu.
            slot.localPosition = startLocalPos;
        }
    }

    private void AnimateAllCurrentSlotsOnOutro(Sequence sequence, float slotDelay)
    {
        if (sequence == null || slotBar == null)
        {
            return;
        }

        Transform slotRoot = slotBar.transform;
        if (slotRoot == null || slotRoot.childCount == 0)
        {
            return;
        }

        List<Transform> slotChildren = new List<Transform>();
        for (int i = 0; i < slotRoot.childCount; i++)
        {
            Transform child = slotRoot.GetChild(i);
            if (child == null || child.GetComponent<Slot>() == null)
            {
                continue;
            }

            slotChildren.Add(child);
        }

        if (slotChildren.Count == 0)
        {
            return;
        }

        for (int i = 0; i < slotChildren.Count; i++)
        {
            Transform slot = slotChildren[i];

            Vector3 targetLocalPos = slot.localPosition;
            Vector3 startLocalPos = GetSlotIntroStartLocalPos(targetLocalPos, i);

            slot.localPosition = targetLocalPos;
            sequence.Insert(slotDelay, slot.DOLocalMove(startLocalPos, SlotAnimDuration).SetEase(OutroAnimEase));
        }
    }

    private void ApplyEndZPositions(bool captureSlotTargets)
    {
        if (level != null)
        {
            Vector3 levelPos = level.transform.localPosition;
            levelPos.z = LevelEndZ;
            level.transform.localPosition = levelPos;
        }

        if (gridController != null)
        {
            Vector3 gridPos = gridController.transform.localPosition;
            gridPos.z = LevelEndZ + GridZOffsetFromLevel;
            gridController.transform.localPosition = gridPos;
        }

        ApplyEndSlotPositions(captureSlotTargets);
    }

    private void ApplyEndSlotPositions(bool captureTargets)
    {
        ApplyInitialSlotPositions(captureTargets);
        if (!slotTargetsPrepared || introSlots.Count == 0)
        {
            return;
        }

        for (int i = 0; i < introSlots.Count; i++)
        {
            Transform slot = introSlots[i];
            if (slot == null)
            {
                continue;
            }

            slot.localPosition = introSlotTargetLocalPositions[i];
        }
    }

    private Vector3 GetSlotIntroStartLocalPos(Vector3 targetLocalPos, int index)
    {
        float startOffset = Mathf.Max(0f, SlotStartOffsetX);
        Vector3 startLocalPos = targetLocalPos;
        // Rule theo index: slot 0,1 đi từ +X; slot 2,3 đi từ -X.
        startLocalPos.x += index <= 1 ? startOffset : -startOffset;
        return startLocalPos;
    }

    private void CaptureOutroOnlyObjectDefaultLocalPos()
    {
        if (outroOnlyObject == null)
        {
            return;
        }

        outroOnlyObjectDefaultLocalPos = outroOnlyObject.transform.localPosition;
        outroOnlyObjectPoseCaptured = true;
    }

    private void ResetOutroOnlyObjectToDefaultLocalPos()
    {
        if (outroOnlyObject == null)
        {
            return;
        }

        if (!outroOnlyObjectPoseCaptured)
        {
            CaptureOutroOnlyObjectDefaultLocalPos();
        }

        outroOnlyObject.transform.localPosition = outroOnlyObjectDefaultLocalPos;
    }

    private void AppendOutroOnlyObjectTween(Sequence sequence, Ease activeEase, float delay)
    {
        if (sequence == null || outroOnlyObject == null)
        {
            return;
        }

        if (!outroOnlyObjectPoseCaptured)
        {
            CaptureOutroOnlyObjectDefaultLocalPos();
        }

        Vector3 startPos = outroOnlyObjectDefaultLocalPos;
        Vector3 endPos = startPos;
        endPos.z += OutroOnlyObjectOffsetZ;

        outroOnlyObject.transform.localPosition = startPos;
        sequence.Insert(delay, outroOnlyObject.transform.DOLocalMove(endPos, AnimDuration).SetEase(activeEase));
    }

    public void ApplyPostIntroRenderOptimization()
    {
        if (hasAppliedPostIntroOptimization || !optimizeRenderersAfterIntro)
        {
            return;
        }

        if (useLowEndIntroLiteMode && skipPostIntroOptimizationOnLowEnd)
        {
            hasAppliedPostIntroOptimization = true;
            return;
        }

        if (deferPostIntroOptimization && isActiveAndEnabled)
        {
            StopPendingPostIntroOptimizationRoutine();
            pendingPostIntroOptimizationRoutine = StartCoroutine(ApplyPostIntroRenderOptimizationDeferred());
            return;
        }

        ApplyPostIntroRenderOptimizationInternal();
    }

    private IEnumerator ApplyPostIntroRenderOptimizationDeferred()
    {
        int waitFrames = Mathf.Max(0, deferredPostIntroOptimizationFrames);
        for (int i = 0; i < waitFrames; i++)
        {
            yield return null;
        }

        pendingPostIntroOptimizationRoutine = null;
        ApplyPostIntroRenderOptimizationInternal();
    }

    private void ApplyPostIntroRenderOptimizationInternal()
    {
        if (hasAppliedPostIntroOptimization || !optimizeRenderersAfterIntro)
        {
            return;
        }

        hasAppliedPostIntroOptimization = true;

        if (optimizeShooterRenderersAfterIntro)
        {
            OptimizeShooterRenderers();
        }

        List<GameObject> wallRoots = null;
        if (optimizeWallRenderersAfterIntro)
        {
            wallRoots = OptimizeWallRenderers();
        }

        bool allowStaticBatching = !(useLowEndIntroLiteMode && skipStaticBatchingOnLowEnd);
        if (allowStaticBatching && combineStaticWallMeshesAfterIntro && wallRoots != null && wallRoots.Count > 0)
        {
            StaticBatchingUtility.Combine(wallRoots.ToArray(), gridController != null ? gridController.gameObject : gameObject);
        }
    }

    private void StopPendingPostIntroOptimizationRoutine()
    {
        if (pendingPostIntroOptimizationRoutine != null)
        {
            StopCoroutine(pendingPostIntroOptimizationRoutine);
            pendingPostIntroOptimizationRoutine = null;
        }
    }

    private bool ShouldUseLowEndIntroLiteMode()
    {
        if (!enableLowEndIntroLiteMode)
        {
            return false;
        }

        int memoryMb = SystemInfo.systemMemorySize;
        if (memoryMb > 0 && memoryMb <= Mathf.Max(512, lowEndSystemMemoryMb))
        {
            return true;
        }

        return SystemInfo.processorCount <= Mathf.Max(1, lowEndProcessorCount);
    }

    private void OptimizeShooterRenderers()
    {
        Transform searchRoot = level != null ? level.transform : (transform.root != null ? transform.root : transform);
        BaseShooter[] shooters = searchRoot.GetComponentsInChildren<BaseShooter>(true);
        if (shooters == null || shooters.Length == 0)
        {
            return;
        }

        for (int i = 0; i < shooters.Length; i++)
        {
            BaseShooter shooter = shooters[i];
            if (shooter == null)
            {
                continue;
            }

            Renderer[] renderers = shooter.GetComponentsInChildren<Renderer>(true);
            OptimizeRenderers(
                renderers,
                keepShadowCasting: preserveShooterShadowCastingAfterIntro,
                disableReceiveShadows: disableShooterReceiveShadowsAfterIntro
            );
        }
    }

    private List<GameObject> OptimizeWallRenderers()
    {
        List<GameObject> wallRoots = new List<GameObject>();
        HashSet<int> trackedRootIds = new HashSet<int>();

        if (gridController != null)
        {
            List<GridItem> nodes = gridController.GetAllNodes();
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    GridItem node = nodes[i];
                    if (node == null || node.GetGridItemType() != GridItemType.Wall)
                    {
                        continue;
                    }

                    Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
                    OptimizeRenderers(
                        renderers,
                        keepShadowCasting: !disableWallShadowCastingAfterIntro,
                        disableReceiveShadows: disableWallReceiveShadowsAfterIntro
                    );

                    int id = node.gameObject.GetInstanceID();
                    if (trackedRootIds.Add(id))
                    {
                        wallRoots.Add(node.gameObject);
                    }
                }
            }
        }

        Transform levelRoot = level != null ? level.transform : (transform.root != null ? transform.root : transform);
        Renderer[] levelRenderers = levelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < levelRenderers.Length; i++)
        {
            Renderer renderer = levelRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!IsLikelyWallRenderer(renderer))
            {
                continue;
            }

            OptimizeRenderers(
                new[] { renderer },
                keepShadowCasting: !disableWallShadowCastingAfterIntro,
                disableReceiveShadows: disableWallReceiveShadowsAfterIntro
            );

            int id = renderer.gameObject.GetInstanceID();
            if (trackedRootIds.Add(id))
            {
                wallRoots.Add(renderer.gameObject);
            }
        }

        return wallRoots;
    }

    private bool IsLikelyWallRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        string objectName = renderer.gameObject.name;
        if (!string.IsNullOrEmpty(objectName) && objectName.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        Transform parent = renderer.transform.parent;
        while (parent != null)
        {
            if (!string.IsNullOrEmpty(parent.name) && parent.name.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    private void OptimizeRenderers(Renderer[] renderers, bool keepShadowCasting, bool disableReceiveShadows)
    {
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

            // Tunnel visuals need to preserve original shadow behavior after intro.
            if (!keepShadowCasting && IsRendererUnderTunnel(renderer))
            {
                continue;
            }

            if (keepShadowCasting)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            else
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (disableReceiveShadows)
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

            if (disableLightAndReflectionProbesForOptimizedRenderers)
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
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

    private static bool IsRendererUnderTunnel(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        return renderer.GetComponentInParent<Tunnel>(true) != null;
    }
}
