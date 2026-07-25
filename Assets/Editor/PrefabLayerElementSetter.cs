using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Splines;

public static class PrefabLayerElementSetter
{
    private const string TargetLayerName = "Element";
    private const int FallbackLayerIndex = 6;
    private const string VfxLayerName = "VFX";
    private const int VfxFallbackLayerIndex = 11;
    private const string ShooterLayerName = "Shooter";
    private const int ShooterFallbackLayerIndex = 12;
    private const string HighlightLayerName = "Highlight";
    private const int HighlightFallbackLayerIndex = 9;

    [MenuItem("Tools/Layers/Set Holder And Deck To Element (All Prefabs)")]
    public static void SetHolderAndDeckLayerInAllPrefabs()
    {
        int targetLayer = LayerMask.NameToLayer(TargetLayerName);
        if (targetLayer < 0)
        {
            targetLayer = FallbackLayerIndex;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int changedObjectCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                if (string.IsNullOrEmpty(prefabName)
                    || prefabName.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int j = 0; j < allTransforms.Length; j++)
                {
                    Transform tr = allTransforms[j];
                    if (!IsTargetName(tr.name))
                    {
                        continue;
                    }

                    if (tr.gameObject.layer == targetLayer)
                    {
                        continue;
                    }

                    tr.gameObject.layer = targetLayer;
                    prefabChanged = true;
                    changedObjectCount++;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Layers/Set Name Contains VFX To VFX (Level Prefabs)")]
    public static void SetNameContainsVfxLayerInLevelPrefabs()
    {
        int targetLayer = LayerMask.NameToLayer(VfxLayerName);
        if (targetLayer < 0)
        {
            targetLayer = VfxFallbackLayerIndex;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int changedObjectCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                if (string.IsNullOrEmpty(prefabName)
                    || prefabName.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int j = 0; j < allTransforms.Length; j++)
                {
                    Transform tr = allTransforms[j];
                    if (!IsVfxNameContains(tr.name))
                    {
                        continue;
                    }

                    int changedInBranch = SetLayerRecursively(tr, targetLayer);
                    if (changedInBranch > 0)
                    {
                        prefabChanged = true;
                        changedObjectCount += changedInBranch;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Layers/Set Name Contains Shooter To Shooter (Level Prefabs)")]
    public static void SetNameContainsShooterLayerInLevelPrefabs()
    {
        int targetLayer = LayerMask.NameToLayer(ShooterLayerName);
        if (targetLayer < 0)
        {
            targetLayer = ShooterFallbackLayerIndex;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int changedObjectCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                if (string.IsNullOrEmpty(prefabName)
                    || prefabName.IndexOf("Level", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int j = 0; j < allTransforms.Length; j++)
                {
                    Transform tr = allTransforms[j];
                    if (!IsShooterNameContains(tr.name))
                    {
                        continue;
                    }

                    if (tr.gameObject.layer == targetLayer)
                    {
                        continue;
                    }

                    tr.gameObject.layer = targetLayer;
                    prefabChanged = true;
                    changedObjectCount++;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Layers/Set TriggerSlideRoute And FireRange To Highlight (Level Prefabs)")]
    public static void SetTriggerSlideRouteAndFireRangeToHighlightInLevelPrefabs()
    {
        int targetLayer = LayerMask.NameToLayer(HighlightLayerName);
        if (targetLayer < 0)
        {
            targetLayer = HighlightFallbackLayerIndex;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int changedObjectCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int j = 0; j < allTransforms.Length; j++)
                {
                    Transform tr = allTransforms[j];
                    if (!IsTriggerSlideRouteOrFireRangeNameContains(tr.name))
                    {
                        continue;
                    }

                    if (tr.gameObject.layer == targetLayer)
                    {
                        continue;
                    }

                    tr.gameObject.layer = targetLayer;
                    prefabChanged = true;
                    changedObjectCount++;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add SplineRoute To Belt Or Way (Level Prefabs)")]
    public static void AddSplineRouteToBeltOrWayInLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int matchedObjectCount = 0;
        int addedSplineRouteCount = 0;
        int setSideModeCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int t = 0; t < allTransforms.Length; t++)
                {
                    Transform tr = allTransforms[t];
                    if (tr == null || tr == prefabRoot.transform)
                    {
                        continue;
                    }

                    if (!IsBeltOrWayNameContains(tr.name))
                    {
                        continue;
                    }

                    matchedObjectCount++;
                    SplineRoute splineRoute = tr.GetComponent<SplineRoute>();
                    if (splineRoute == null)
                    {
                        splineRoute = tr.gameObject.AddComponent<SplineRoute>();
                        addedSplineRouteCount++;
                        prefabChanged = true;
                    }

                    if (IsWayNameContains(tr.name) && SetSplineRouteModeSideIfNeeded(splineRoute))
                    {
                        setSideModeCount++;
                        prefabChanged = true;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Remove Nested Child SplineRoute (Level Prefabs)")]
    public static void RemoveNestedChildSplineRouteInLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int parentRouteCount = 0;
        int removedNestedRouteCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                SplineRoute[] allRoutes = prefabRoot.GetComponentsInChildren<SplineRoute>(true);

                for (int r = 0; r < allRoutes.Length; r++)
                {
                    SplineRoute parentRoute = allRoutes[r];
                    if (parentRoute == null)
                    {
                        continue;
                    }

                    parentRouteCount++;
                    SplineRoute[] nestedRoutes = parentRoute.GetComponentsInChildren<SplineRoute>(true);
                    for (int n = 0; n < nestedRoutes.Length; n++)
                    {
                        SplineRoute nestedRoute = nestedRoutes[n];
                        if (nestedRoute == null || nestedRoute == parentRoute)
                        {
                            continue;
                        }

                        Object.DestroyImmediate(nestedRoute, true);
                        removedNestedRouteCount++;
                        prefabChanged = true;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add FireRangeDetector To FireRange-Named Objects (Level Prefabs)")]
    public static void AddFireRangeDetectorToFireRangeNamedObjectsInLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int matchedObjectCount = 0;
        int addedComponentCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int t = 0; t < allTransforms.Length; t++)
                {
                    Transform tr = allTransforms[t];
                    if (tr == null || !IsFireRangeNameContains(tr.name))
                    {
                        continue;
                    }

                    matchedObjectCount++;
                    if (tr.GetComponent<FireRangeDetector>() != null)
                    {
                        continue;
                    }

                    tr.gameObject.AddComponent<FireRangeDetector>();
                    addedComponentCount++;
                    prefabChanged = true;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add SplineRouteRefillTrigger To TriggerSlideRoute (Level Prefabs)")]
    public static void AddSplineRouteRefillTriggerToTriggerSlideRouteInLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int matchedObjectCount = 0;
        int addedComponentCount = 0;
        int setSideIndexCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int t = 0; t < allTransforms.Length; t++)
                {
                    Transform tr = allTransforms[t];
                    if (tr == null || !IsTriggerSlideRouteNameContains(tr.name))
                    {
                        continue;
                    }

                    matchedObjectCount++;
                    SplineRouteRefillTrigger trigger = tr.GetComponent<SplineRouteRefillTrigger>();
                    if (trigger == null)
                    {
                        trigger = tr.gameObject.AddComponent<SplineRouteRefillTrigger>();
                        addedComponentCount++;
                        prefabChanged = true;
                    }

                    if (IsTriggerSlideRouteLNameContains(tr.name) && SetSplineRouteRefillSideIndexIfNeeded(trigger, 1))
                    {
                        setSideIndexCount++;
                        prefabChanged = true;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Normalize ConveyorArrowSystem On Level Prefabs")]
    public static void NormalizeConveyorArrowSystemOnLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int addedOnRootCount = 0;
        int removedFromChildrenCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;

                ConveyorArrowSystem rootSystem = prefabRoot.GetComponent<ConveyorArrowSystem>();
                if (rootSystem == null)
                {
                    prefabRoot.AddComponent<ConveyorArrowSystem>();
                    addedOnRootCount++;
                    prefabChanged = true;
                }

                ConveyorArrowSystem[] allSystems = prefabRoot.GetComponentsInChildren<ConveyorArrowSystem>(true);
                for (int s = 0; s < allSystems.Length; s++)
                {
                    ConveyorArrowSystem system = allSystems[s];
                    if (system == null)
                    {
                        continue;
                    }

                    if (system.gameObject == prefabRoot)
                    {
                        continue;
                    }

                    Object.DestroyImmediate(system, true);
                    removedFromChildrenCount++;
                    prefabChanged = true;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add SplineContainer + BuildSplineFromBelt Under SplineController (Level Prefabs)")]
    public static void AddSplineComponentsUnderSplineControllerInLevelPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int splineControllerCount = 0;
        int addedSplineContainerCount = 0;
        int addedBuildSplineCount = 0;
        int setSecondaryCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                SplineController[] controllers = prefabRoot.GetComponentsInChildren<SplineController>(true);

                for (int c = 0; c < controllers.Length; c++)
                {
                    SplineController controller = controllers[c];
                    if (controller == null)
                    {
                        continue;
                    }

                    splineControllerCount++;
                    Transform controllerTransform = controller.transform;
                    for (int childIndex = 0; childIndex < controllerTransform.childCount; childIndex++)
                    {
                        Transform child = controllerTransform.GetChild(childIndex);
                        if (child == null)
                        {
                            continue;
                        }

                        if (child.GetComponent<SplineContainer>() == null)
                        {
                            child.gameObject.AddComponent<SplineContainer>();
                            addedSplineContainerCount++;
                            prefabChanged = true;
                        }

                        BuildSplineFromBelt builder = child.GetComponent<BuildSplineFromBelt>();
                        if (builder == null)
                        {
                            builder = child.gameObject.AddComponent<BuildSplineFromBelt>();
                            addedBuildSplineCount++;
                            prefabChanged = true;
                        }

                        bool isMainSplineName = child.name.IndexOf("mainspline", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!isMainSplineName && SetBuildSplineSecondaryIfNeeded(builder, true))
                        {
                            setSecondaryCount++;
                            prefabChanged = true;
                        }
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add SlotBar To HolderDeck Under LevelController (All Prefabs)")]
    public static void AddSlotBarToHolderDeckUnderLevelController()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int levelControllerCount = 0;
        int slotBarAddedCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                LevelController[] levelControllers = prefabRoot.GetComponentsInChildren<LevelController>(true);
                if (levelControllers != null)
                {
                    for (int c = 0; c < levelControllers.Length; c++)
                    {
                        LevelController levelController = levelControllers[c];
                        if (levelController == null)
                        {
                            continue;
                        }

                        levelControllerCount++;
                        Transform[] descendants = levelController.GetComponentsInChildren<Transform>(true);
                        for (int d = 0; d < descendants.Length; d++)
                        {
                            Transform tr = descendants[d];
                            if (tr == null || tr == levelController.transform)
                            {
                                continue;
                            }

                            if (!IsHolderDeckNameContains(tr.name))
                            {
                                continue;
                            }

                            if (tr.GetComponent<SlotBar>() != null)
                            {
                                continue;
                            }

                            tr.gameObject.AddComponent<SlotBar>();
                            prefabChanged = true;
                            slotBarAddedCount++;
                        }
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add Slot To Children Of SlotBar (All Prefabs)")]
    public static void AddSlotToChildrenOfSlotBar()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int slotBarCount = 0;
        int slotAddedCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                SlotBar[] slotBars = prefabRoot.GetComponentsInChildren<SlotBar>(true);
                if (slotBars != null)
                {
                    for (int s = 0; s < slotBars.Length; s++)
                    {
                        SlotBar slotBar = slotBars[s];
                        if (slotBar == null)
                        {
                            continue;
                        }

                        slotBarCount++;
                        int addedForSlotBar = AddSlotToDirectChildren(slotBar.transform);
                        if (addedForSlotBar > 0)
                        {
                            prefabChanged = true;
                            slotAddedCount += addedForSlotBar;
                        }
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add GridController To GridController-Named Children Under LevelController (All Prefabs)")]
    public static void AddGridControllerToNamedChildrenUnderLevelController()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int levelControllerCount = 0;
        int gridControllerAddedCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                LevelController[] levelControllers = prefabRoot.GetComponentsInChildren<LevelController>(true);
                if (levelControllers != null)
                {
                    for (int c = 0; c < levelControllers.Length; c++)
                    {
                        LevelController levelController = levelControllers[c];
                        if (levelController == null)
                        {
                            continue;
                        }

                        levelControllerCount++;
                        Transform[] descendants = levelController.GetComponentsInChildren<Transform>(true);
                        for (int d = 0; d < descendants.Length; d++)
                        {
                            Transform tr = descendants[d];
                            if (tr == null || tr == levelController.transform)
                            {
                                continue;
                            }

                            if (!IsGridControllerNameContains(tr.name))
                            {
                                continue;
                            }

                            if (tr.GetComponent<GridController>() != null)
                            {
                                continue;
                            }

                            tr.gameObject.AddComponent<GridController>();
                            prefabChanged = true;
                            gridControllerAddedCount++;
                        }
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add GridItem To Children Of GridController (All Prefabs)")]
    public static void AddGridItemToChildrenOfGridController()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int gridControllerCount = 0;
        int gridItemAddedCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                GridController[] gridControllers = prefabRoot.GetComponentsInChildren<GridController>(true);
                if (gridControllers != null)
                {
                    for (int g = 0; g < gridControllers.Length; g++)
                    {
                        GridController gridController = gridControllers[g];
                        if (gridController == null)
                        {
                            continue;
                        }

                        gridControllerCount++;
                        int addedForGridController = AddGridItemToDirectChildren(gridController.transform);
                        if (addedForGridController > 0)
                        {
                            prefabChanged = true;
                            gridItemAddedCount += addedForGridController;
                        }
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add SplineController To SplineController-Named Children (Level Prefabs)")]
    public static void AddSplineControllerToSplineControllerNamedChildren()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int splineControllerNamedChildCount = 0;
        int splineControllerAddedCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;
                Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                for (int t = 0; t < allTransforms.Length; t++)
                {
                    Transform child = allTransforms[t];
                    if (child == null || child == prefabRoot.transform)
                    {
                        continue;
                    }

                    if (!IsSplineControllerNameContains(child.name))
                    {
                        continue;
                    }

                    splineControllerNamedChildCount++;
                    if (child.GetComponent<SplineController>() != null)
                    {
                        continue;
                    }

                    child.gameObject.AddComponent<SplineController>();
                    prefabChanged = true;
                    splineControllerAddedCount++;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Add Shooter Components To GridItems Under GridController (All Prefabs)")]
    public static void AddShooterComponentsToGridItemsUnderGridController()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int gridControllerCount = 0;
        int baseShooterAddedCount = 0;
        int hiddenShooterAddedCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                GridController[] gridControllers = prefabRoot.GetComponentsInChildren<GridController>(true);
                if (gridControllers != null)
                {
                    for (int g = 0; g < gridControllers.Length; g++)
                    {
                        GridController gridController = gridControllers[g];
                        if (gridController == null)
                        {
                            continue;
                        }

                        gridControllerCount++;
                        int addedBaseForGridController;
                        int addedHiddenForGridController;
                        bool changedForGridController = AddShooterComponentsToDirectChildren(
                            gridController.transform,
                            out addedBaseForGridController,
                            out addedHiddenForGridController);

                        if (changedForGridController)
                        {
                            prefabChanged = true;
                        }

                        baseShooterAddedCount += addedBaseForGridController;
                        hiddenShooterAddedCount += addedHiddenForGridController;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    [MenuItem("Tools/Components/Setup Text Under Shooters (All Prefabs)")]
    public static void SetupTextUnderShooters()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabCount = 0;
        int shooterCount = 0;
        int textObjectConfiguredCount = 0;

        TMP_FontAsset royalKingdomFont = FindRoyalKingdomFont();
        if (royalKingdomFont == null)
        {
        }

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsLevelPrefabPath(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                bool prefabChanged = false;
                HashSet<Transform> shooterRoots = CollectShooterRootsForTextSetup(prefabRoot);
                if (shooterRoots.Count > 0)
                {
                    foreach (Transform shooterRoot in shooterRoots)
                    {
                        if (shooterRoot == null)
                        {
                            continue;
                        }

                        shooterCount++;
                        int configuredForShooter;
                        bool changedForShooter = SetupTextObjectsUnderShooter(
                            shooterRoot,
                            royalKingdomFont,
                            out configuredForShooter);

                        if (changedForShooter)
                        {
                            prefabChanged = true;
                        }

                        textObjectConfiguredCount += configuredForShooter;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedPrefabCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }

    private static HashSet<Transform> CollectShooterRootsForTextSetup(GameObject prefabRoot)
    {
        HashSet<Transform> result = new HashSet<Transform>();
        if (prefabRoot == null)
        {
            return result;
        }

        BaseShooter[] shooters = prefabRoot.GetComponentsInChildren<BaseShooter>(true);
        if (shooters != null)
        {
            for (int i = 0; i < shooters.Length; i++)
            {
                BaseShooter shooter = shooters[i];
                if (shooter != null)
                {
                    result.Add(shooter.transform);
                }
            }
        }

        // Fallback theo tên object để can thiệp cả shooter đang ẩn/chưa gắn component.
        Transform[] descendants = prefabRoot.GetComponentsInChildren<Transform>(true);
        if (descendants != null)
        {
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform tr = descendants[i];
                if (tr == null || tr == prefabRoot.transform)
                {
                    continue;
                }

                if (!IsShooterNameContains(tr.name))
                {
                    continue;
                }

                result.Add(tr);
            }
        }

        return result;
    }

    private static bool IsTargetName(string objectName)
    {
        if (!string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("block", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("level", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return string.Equals(objectName, "holder", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectName, "deck", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectName, "plane", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVfxNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("vfx", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsShooterNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("shooter", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsHolderDeckNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("holderdeck", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsGridControllerNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("gridcontroller", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLevelNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("level", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSplineControllerNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("splinecontroller", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsHiddenNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("hidden", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTriggerSlideRouteOrFireRangeNameContains(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        return objectName.IndexOf("triggerslideroute", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("firerange", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsFireRangeNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("firerange", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTriggerSlideRouteNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("triggerslideroute", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTriggerSlideRouteLNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("triggerslideroutel", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsBeltOrWayNameContains(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        return objectName.IndexOf("belt", System.StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("way", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsWayNameContains(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("way", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool SetSplineRouteModeSideIfNeeded(SplineRoute splineRoute)
    {
        if (splineRoute == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(splineRoute);
        SerializedProperty routeModeProp = serializedObject.FindProperty("routeMode");
        if (routeModeProp == null)
        {
            return false;
        }

        int targetValue = (int)SplineRoute.RouteMode.Side;
        if (routeModeProp.enumValueIndex == targetValue)
        {
            return false;
        }

        routeModeProp.enumValueIndex = targetValue;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static bool SetBuildSplineSecondaryIfNeeded(BuildSplineFromBelt buildSplineFromBelt, bool targetValue)
    {
        if (buildSplineFromBelt == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(buildSplineFromBelt);
        SerializedProperty secondaryProp = serializedObject.FindProperty("isSecondaryBelt");
        if (secondaryProp == null)
        {
            return false;
        }

        if (secondaryProp.boolValue == targetValue)
        {
            return false;
        }

        secondaryProp.boolValue = targetValue;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static bool SetSplineRouteRefillSideIndexIfNeeded(SplineRouteRefillTrigger trigger, int targetValue)
    {
        if (trigger == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(trigger);
        SerializedProperty sideIndexProp = serializedObject.FindProperty("sideIndex");
        if (sideIndexProp == null)
        {
            return false;
        }

        if (sideIndexProp.intValue == targetValue)
        {
            return false;
        }

        sideIndexProp.intValue = targetValue;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static bool IsLevelPrefabPath(string prefabPath)
    {
        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        return !string.IsNullOrEmpty(prefabName)
            && prefabName.IndexOf("level", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int SetLayerRecursively(Transform root, int targetLayer)
    {
        int changedCount = 0;
        if (root == null)
        {
            return changedCount;
        }

        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            GameObject go = allTransforms[i].gameObject;
            if (go.layer == targetLayer)
            {
                continue;
            }

            go.layer = targetLayer;
            changedCount++;
        }

        return changedCount;
    }

    private static int AddSlotToDirectChildren(Transform parent)
    {
        int addedCount = 0;
        if (parent == null)
        {
            return addedCount;
        }

        int maxChildrenToProcess = Mathf.Min(4, parent.childCount);
        for (int i = 0; i < maxChildrenToProcess; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<Slot>() != null)
            {
                continue;
            }

            child.gameObject.AddComponent<Slot>();
            addedCount++;
        }

        return addedCount;
    }

    private static int AddGridItemToDirectChildren(Transform parent)
    {
        int addedCount = 0;
        if (parent == null)
        {
            return addedCount;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<GridItem>() != null)
            {
                continue;
            }

            child.gameObject.AddComponent<GridItem>();
            addedCount++;
        }

        return addedCount;
    }

    private static bool SetupTextObjectsUnderShooter(Transform shooterRoot, TMP_FontAsset royalKingdomFont, out int configuredCount)
    {
        configuredCount = 0;
        bool changed = false;
        if (shooterRoot == null)
        {
            return false;
        }

        Transform[] descendants = shooterRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform tr = descendants[i];
            if (tr == null || tr == shooterRoot)
            {
                continue;
            }

            if (string.IsNullOrEmpty(tr.name)
                || tr.name.IndexOf("canvas", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (tr.childCount <= 0)
            {
                continue;
            }

            Transform textTarget = tr.GetChild(0);
            if (textTarget == null)
            {
                continue;
            }

            if (!tr.gameObject.activeSelf)
            {
                tr.gameObject.SetActive(true);
                changed = true;
            }

            if (!textTarget.gameObject.activeSelf)
            {
                textTarget.gameObject.SetActive(true);
                changed = true;
            }

            TextMeshProUGUI tmp = textTarget.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = textTarget.gameObject.AddComponent<TextMeshProUGUI>();
                changed = true;
            }

            if (royalKingdomFont != null && tmp.font != royalKingdomFont)
            {
                tmp.font = royalKingdomFont;
                changed = true;
            }

            if (!Mathf.Approximately(tmp.fontSize, 10f))
            {
                tmp.fontSize = 10f;
                changed = true;
            }

            if (tmp.alignment != TextAlignmentOptions.Center)
            {
                tmp.alignment = TextAlignmentOptions.Center;
                changed = true;
            }

            RectTransform rect = textTarget.GetComponent<RectTransform>();
            if (rect != null)
            {
                if (rect.anchorMin != Vector2.zero)
                {
                    rect.anchorMin = Vector2.zero;
                    changed = true;
                }

                if (rect.anchorMax != Vector2.one)
                {
                    rect.anchorMax = Vector2.one;
                    changed = true;
                }

                if (rect.offsetMin != Vector2.zero)
                {
                    rect.offsetMin = Vector2.zero;
                    changed = true;
                }

                if (rect.offsetMax != Vector2.zero)
                {
                    rect.offsetMax = Vector2.zero;
                    changed = true;
                }
            }

            configuredCount++;
        }

        return changed;
    }

    private static TMP_FontAsset FindRoyalKingdomFont()
    {
        string[] guids = AssetDatabase.FindAssets("royal_kingdom t:TMP_FontAsset");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset != null)
            {
                return fontAsset;
            }
        }

        guids = AssetDatabase.FindAssets("royal kingdom t:TMP_FontAsset");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        return null;
    }

    private static bool AddShooterComponentsToDirectChildren(Transform parent, out int baseAddedCount, out int hiddenAddedCount)
    {
        baseAddedCount = 0;
        hiddenAddedCount = 0;
        bool changed = false;
        if (parent == null)
        {
            return false;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<GridItem>() == null)
            {
                continue;
            }

            string childName = child.name;
            if (!IsShooterNameContains(childName))
            {
                continue;
            }

            bool isHiddenShooter = IsHiddenNameContains(childName);
            if (isHiddenShooter)
            {
                if (child.GetComponent<HiddenShooter>() == null)
                {
                    child.gameObject.AddComponent<HiddenShooter>();
                    hiddenAddedCount++;
                    changed = true;
                }

                continue;
            }

            if (child.GetComponent<BaseShooter>() == null)
            {
                child.gameObject.AddComponent<BaseShooter>();
                baseAddedCount++;
                changed = true;
            }
        }

        return changed;
    }
}
