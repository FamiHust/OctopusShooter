using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System.Collections;

public class LevelController : MonoBehaviour
{
    [SerializeField] private SplineController splineController;
    [SerializeField] private List<BaseShooter> shooterList;
    [SerializeField] private List<Tunnel> tunnelList;
    [SerializeField] private List<SeedColor> listColor;
    [SerializeField] private GridController gridController;
    [Header("Conveyor Settings")]
    [SerializeField] private MeshFilter conveyorMeshFilter;
    [SerializeField] private List<Mesh> conveyorMeshList = new List<Mesh>();
    [Header("Map Settings")]
    [SerializeField] private MeshFilter mapMeshFilter;
    [SerializeField] private List<Mesh> mapMeshList = new List<Mesh>();

    [Header("Init Performance")]
    [SerializeField] private bool batchShooterStateInitOnLowEnd = true;
    [SerializeField, Min(1)] private int shooterStateBatchSizeOnLowEnd = 8;
    [SerializeField] private int lowEndSystemMemoryMb = 3000;
    [SerializeField] private int lowEndProcessorCount = 4;
    private Camera levelCamera;
    private Coroutine pendingShooterStateInitRoutine;

    [System.Serializable]
    public class SplineData
    {
        public int countMainRow;
        public List<int> countSideRows;
    }
    [SerializeField] private SplineData data;

    private void Awake()
    {
        // Subscribe vào event khi có shooter được add vào SlotBar để re-check state
        GameEventHub.Instance.AddListener(GameEventType.OnShooterAddedToSlot, OnShooterAddedToSlot);
        GameEventHub.Instance.AddListener(GameEventType.OnShooterDisappear, OnShooterDisappear);
    }

    void OnValidate()
    {
        GetBaseShooterList();
        GetGridController();
        GetSplineController();
        GetMapMeshFilter();
        GetConveyorMeshFilter();
#if UNITY_EDITOR
        PopulateDefaultMapMeshes();
        PopulateDefaultConveyorMeshes();
#endif
    }

    private void GetMapMeshFilter()
    {
        if (mapMeshFilter == null)
        {
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf != null && mf.gameObject != null)
                {
                    string gName = mf.gameObject.name;
                    if (gName.StartsWith("Level_") || (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Level_")))
                    {
                        mapMeshFilter = mf;
                        break;
                    }
                }
            }
        }
    }

    private void GetConveyorMeshFilter()
    {
        if (conveyorMeshFilter == null)
        {
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf != null && mf.gameObject != null)
                {
                    string gName = mf.gameObject.name.ToLower();
                    if (gName.Contains("conveyor") || gName.Contains("blockconveyor") || gName.Contains("spline") || gName.Contains("track"))
                    {
                        conveyorMeshFilter = mf;
                        break;
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    private void PopulateDefaultMapMeshes()
    {
        if (mapMeshList == null) mapMeshList = new List<Mesh>();
        if (mapMeshList.Count == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets/Mesh" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null && mesh.name.StartsWith("Level_"))
                {
                    mapMeshList.Add(mesh);
                }
            }
            mapMeshList.Sort((a, b) => string.Compare(a.name, b.name));
        }
    }

    private void PopulateDefaultConveyorMeshes()
    {
        if (conveyorMeshList == null) conveyorMeshList = new List<Mesh>();
        if (conveyorMeshList.Count == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Mesh", new string[] { "Assets/Mesh" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null && (mesh.name.StartsWith("Conveyor_") || mesh.name.StartsWith("Track_") || mesh.name.StartsWith("BlockConveyor_") || mesh.name.ToLower().Contains("conveyor")))
                {
                    conveyorMeshList.Add(mesh);
                }
            }
            conveyorMeshList.Sort((a, b) => string.Compare(a.name, b.name));
        }
    }
#endif

    private void GetBaseShooterList()
    {
        BaseShooter[] shootersInChildren = GetComponentsInChildren<BaseShooter>(true);

        if (shooterList == null)
        {
            shooterList = new List<BaseShooter>();
        }
        else
        {
            shooterList.Clear();
        }

        foreach (BaseShooter shooter in shootersInChildren)
        {
            if (shooter != null && shooter.gameObject != gameObject)
            {
                shooterList.Add(shooter);
            }
        }
    }

    private void GetGridController()
    {
        if (gridController == null)
        {
            gridController = GetComponentInChildren<GridController>(true);
        }
    }

    private void GetSplineController()
    {
        if (splineController == null)
        {
            splineController = GetComponentInChildren<SplineController>(true);
        }
    }

    private void OnDestroy()
    {
        if (pendingShooterStateInitRoutine != null)
        {
            StopCoroutine(pendingShooterStateInitRoutine);
            pendingShooterStateInitRoutine = null;
        }

        // Unsubscribe event
        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.RemoveListener(GameEventType.OnShooterAddedToSlot, OnShooterAddedToSlot);
            GameEventHub.Instance.RemoveListener(GameEventType.OnShooterDisappear, OnShooterDisappear);
        }
    }

    public void InitLevel(Camera cam)
    {
        if (pendingShooterStateInitRoutine != null)
        {
            StopCoroutine(pendingShooterStateInitRoutine);
            pendingShooterStateInitRoutine = null;
        }

        levelCamera = cam;
        foreach (var shooter in shooterList)
            if (shooter != null) shooter.SetCamera(levelCamera);
        if (tunnelList != null)
            foreach (var tunnel in tunnelList)
                if (tunnel != null) tunnel.SetCamera(levelCamera);

        if (ShouldBatchInitialShooterState())
        {
            pendingShooterStateInitRoutine = StartCoroutine(SetStateForAllShooterBatched());
        }
        else
        {
            SetStateForAllShooter();
        }

        if (splineController != null && listColor != null && listColor.Count > 0 && data != null)
        {
            splineController.Initialize(
                listColor,
                data.countMainRow,
                data.countSideRows ?? new List<int>());
        }
    }

    /// <summary>
    /// Event callback khi có shooter được add vào SlotBar
    /// Re-check state của tất cả shooter còn lại trên grid
    /// </summary>
    private void OnShooterAddedToSlot(object data)
    {
        if (pendingShooterStateInitRoutine != null)
        {
            return;
        }

        SetStateForAllShooter();
    }

    private void OnShooterDisappear(object data)
    {
        if (pendingShooterStateInitRoutine != null)
        {
            return;
        }

        SetStateForAllShooter();
    }

    private void SetStateForAllShooter()
    {
        foreach (var shooter in shooterList)
        {
            if (shooter == null)
            {
                continue;
            }

            shooter.CheckShooterState();
        }
    }

    private IEnumerator SetStateForAllShooterBatched()
    {
        int batchSize = Mathf.Max(1, shooterStateBatchSizeOnLowEnd);
        int processed = 0;

        for (int i = 0; i < shooterList.Count; i++)
        {
            BaseShooter shooter = shooterList[i];
            if (shooter != null)
            {
                shooter.CheckShooterState();
            }

            processed++;
            if (processed >= batchSize)
            {
                processed = 0;
                yield return null;
            }
        }

        pendingShooterStateInitRoutine = null;
    }

    private bool ShouldBatchInitialShooterState()
    {
        if (!batchShooterStateInitOnLowEnd)
        {
            return false;
        }

        if (shooterList == null || shooterList.Count <= Mathf.Max(1, shooterStateBatchSizeOnLowEnd))
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
    public int GetShooterCount()
    {
        HashSet<BaseShooter> uniqueShooters = new HashSet<BaseShooter>();

        if (shooterList != null)
        {
            for (int i = 0; i < shooterList.Count; i++)
            {
                BaseShooter shooter = shooterList[i];
                if (shooter != null)
                {
                    uniqueShooters.Add(shooter);
                }
            }
        }

        if (tunnelList != null)
        {
            for (int i = 0; i < tunnelList.Count; i++)
            {
                Tunnel tunnel = tunnelList[i];
                tunnel?.AppendInitialShooters(uniqueShooters);
            }
        }

        return uniqueShooters.Count;
    }

    public SplineRoute GetMainRoute() => splineController != null ? splineController.GetMainRoute() : null;
}

