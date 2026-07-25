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
    }

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

