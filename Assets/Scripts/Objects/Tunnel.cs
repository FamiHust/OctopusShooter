using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public enum Direction { Left, Right, Up, Down }

/// <summary>
/// Tunnel giá»¯ má»™t stack BaseShooter vÃ  tá»± Ä‘á»™ng "báº¯n" shooter tiáº¿p theo
/// vÃ o Ã´ lÆ°á»›i liá»n ká» (theo targetDir) khi Ã´ Ä‘Ã³ trá»‘ng.
/// Trigger: má»—i khi cÃ³ shooter Ä‘Æ°á»£c thÃªm vÃ o SlotBar (OnShooterAddedToSlot).
/// </summary>
public class Tunnel : MonoBehaviour
{
    #region --- [INSPECTOR VARIABLES] ---
    [Header("Tunnel Settings")]
    [SerializeField] private Direction targetDir;
    [SerializeField] private List<BaseShooter> shooterList;

    [Header("Dependencies")]
    [SerializeField] private SlotBar slotBar;
    [SerializeField] private GridController gridController;

    [Header("Visuals & UI")]
    [SerializeField] private Renderer tunnelBorderRenderer;
    [SerializeField] public ParticleSystem releaseVFX;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Shooter Exit State")]
    [SerializeField] private bool inheritStateFromPreviousReleasedShooter = true;


    [Header("Prefabs")]
    [SerializeField] private GridItem emptyCellPrefab; // KÃ©o tháº£ prefab Ã´ Ä‘áº¥t trá»‘ng vÃ o Ä‘Ã¢y
    #endregion

    #region --- [PRIVATE STATE] ---
    private GridItem gridItem;
    private GridItem targetGridItem;
    private int targetRow;
    private int targetCol;
    private Vector3 targetLocalPos;
    private bool hasResolvedTargetCell;
    private bool targetIsEndNode = false;

    private Material borderMatInstance;
    private Color defaultBorderColor;
    private int remainShooterCount;
    private Camera _camera;
    private bool hasPreviousReleasedShooterState;
    private ShooterState previousReleasedShooterState = ShooterState.Empty;
    #endregion

    #region --- [UNITY LIFECYCLE] ---
    private void Awake()
    {
        gridItem = GetComponent<GridItem>();
        GameEventHub.Instance.AddListener(GameEventType.OnShooterAddedToSlot, OnShooterAddedToSlot);
    }

    private void Start()
    {
        Init();
    }

    private void OnDestroy()
    {
        if (GameEventHub.Instance != null)
        {
            GameEventHub.Instance.RemoveListener(GameEventType.OnShooterAddedToSlot, OnShooterAddedToSlot);
        }
    }
    #endregion

    #region --- [INITIALIZATION] ---
    public void SetCamera(Camera cam) => _camera = cam;
    public int GetInitialShooterCount() => shooterList != null ? shooterList.Count : 0;

    public void AppendInitialShooters(HashSet<BaseShooter> uniqueShooters)
    {
        if (uniqueShooters == null || shooterList == null)
        {
            return;
        }

        for (int i = 0; i < shooterList.Count; i++)
        {
            BaseShooter shooter = shooterList[i];
            if (shooter != null)
            {
                uniqueShooters.Add(shooter);
            }
        }
    }

    public void Init()
    {
        InitializeMaterialInstance();

        remainShooterCount = shooterList.Count;
        hasPreviousReleasedShooterState = false;
        previousReleasedShooterState = ShooterState.Empty;
        UpdateCountText();

        FindTargetGridItem();
        UpdateMaterialColor();

        if (gridController != null && targetGridItem!=null)
        {
            gridController.SetLockItemByTunel(targetCol, targetRow);
        }
    }

    private void InitializeMaterialInstance()
    {
        if (tunnelBorderRenderer == null || tunnelBorderRenderer.sharedMaterials.Length <= 0) return;

        Material[] mats = tunnelBorderRenderer.sharedMaterials;
        if (mats.Length > 1 && mats[1] != null)
        {
            borderMatInstance = new Material(mats[1]);
            mats[1] = borderMatInstance;
            tunnelBorderRenderer.sharedMaterials = mats;
            defaultBorderColor = borderMatInstance.color;
        }
    }

    private void FindTargetGridItem()
    {
        if (gridItem == null || gridController == null) return;

        int r = gridItem.GetRow();
        int c = gridItem.GetCol();
        targetRow = r;
        targetCol = c;

        switch (targetDir)
        {
            case Direction.Up: targetRow = r - 1; break;
            case Direction.Down: targetRow = r + 1; break;
            case Direction.Left: targetCol = c - 1; break;
            case Direction.Right: targetCol = c + 1; break;
        }

        foreach (var node in gridController.GetAllNodes())
        {
            if (node.GetRow() == targetRow && node.GetCol() == targetCol)
            {
                targetGridItem = node;
                targetLocalPos = node.transform.localPosition;
                hasResolvedTargetCell = true;
                targetIsEndNode = gridController.IsEndNode(node);
                return; // TÃ¬m tháº¥y thÃ¬ thoÃ¡t luÃ´n, khÃ´ng cáº§n break
            }
        }

        hasResolvedTargetCell = false;

        ;
    }
    #endregion

    #region --- [EVENT HANDLERS] ---
    private void OnShooterAddedToSlot(object data)
    {
        if (!IsTargetCellEmpty()) return;

        ClearTargetGridItem(); // XÃ³a tháº±ng cÅ© vá»«a bay Ä‘i

        if (CanSpawnShooter())
        {
            SpawnNextShooter(); // Äáº» tháº±ng Shooter má»›i
        }
        else
        {
            SpawnEmptyCellToFillHole(); // Háº¿t Shooter rá»“i -> Láº¥p Ã´ Ä‘áº¥t trá»‘ng Ä‘á»ƒ thÃ´ng Ä‘Æ°á»ng!
        }
    }

    private bool IsTargetCellEmpty()
    {
        return targetGridItem == null || targetGridItem.GetGridItemType() != GridItemType.Shooter;
    }

    private void ClearTargetGridItem()
    {
        if (targetGridItem != null)
        {
            targetIsEndNode = gridController.RemoveNode(targetGridItem);
            targetGridItem = null;
        }
    }
    #endregion

    #region --- [CORE SPAWN LOGIC] ---


    private void SpawnEmptyCellToFillHole()
    {
        if (emptyCellPrefab == null || !hasResolvedTargetCell || gridController == null) return;

        // 1. Táº¡o ra EmptyCell táº¡i Ä‘Ãºng vá»‹ trÃ­ [1,1]
        GridItem emptyCell = Instantiate(emptyCellPrefab, gridController.transform);
        emptyCell.transform.localPosition = targetLocalPos;

        // 2. Khá»Ÿi táº¡o nÃ³ vá»›i Type = EmptyCell
        emptyCell.Initialize(targetRow, targetCol, GridItemType.EmptyCell);

        // 3. ÄÄƒng kÃ½ vÃ o Grid (HÃ m RegisterNode cá»§a báº¡n Ä‘Ã£ tá»± Ä‘á»™ng ná»‘i dÃ¢y Ä‘iá»‡n 2 chiá»u vá»›i cÃ¡c hÃ ng xÃ³m)
        gridController.RegisterNode(emptyCell, targetIsEndNode);
        targetGridItem = emptyCell;

        ;

        // (TÃ™Y CHá»ŒN): Náº¿u game cá»§a báº¡n khÃ´ng tá»± Ä‘á»™ng update má»—i frame, báº¡n cáº§n báº¯n 1 Event Ä‘á»ƒ 
        // bÃ¡o cho bá»n [2,1] vÃ  [3,1] biáº¿t mÃ  cháº¡y láº¡i hÃ m CheckShooterState() cá»§a chÃºng nÃ³.
        // VÃ­ dá»¥: GameEventHub.Instance.Invoke(GameEventType.OnGridChanged, null);
    }
    private void SpawnNextShooter()
    {
        if (!CanSpawnShooter()) return;

        // 1. Láº¥y shooter cÃ³ sáºµn trong list theo thá»© tá»±
        BaseShooter shooterInList = ExtractNextShooterFromList();
        if (shooterInList == null) return;

        // 2. ChÆ¡i hiá»‡u á»©ng
        PlayReleaseVFX();

        // 3. Báº­t shooter cÃ³ sáºµn, khÃ´ng Instantiate má»›i
        BaseShooter shooter = ActivateShooterFromList(shooterInList);
        if (shooter == null) return;


        // 4. Thiáº¿t láº­p dá»¯ liá»‡u Grid
        SetupShooterGridData(shooter);

        // 5. BÆ¡m dependencies
        
        InjectDependencies(shooter);

        // 6. KÃ­ch hoáº¡t logic Shooter
        ApplyExitStateForReleasedShooter(shooter);

        // 7. Cháº¡y Animation di chuyá»ƒn
        AnimateShooterSpawn(shooter);

        // 8. Cáº­p nháº­t tráº¡ng thÃ¡i Tunnel
        UpdateTunnelStateAfterSpawn();
    }

    private bool CanSpawnShooter()
    {
        return shooterList != null && shooterList.Count > 0 && hasResolvedTargetCell;
    }

    private BaseShooter ExtractNextShooterFromList()
    {
        BaseShooter prefab = shooterList[0];
        shooterList.RemoveAt(0);
        return prefab;
    }

    private BaseShooter ActivateShooterFromList(BaseShooter shooter)
    {
        if (shooter == null)
        {
            return null;
        }

        if (gridController != null)
        {
            shooter.transform.SetParent(gridController.transform);
        }

        shooter.transform.localRotation = transform.localRotation;
        shooter.gameObject.SetActive(true);

        return shooter;
    }

    private void SetupShooterGridData(BaseShooter shooter)
    {
        GridItem newGI = shooter.GetComponent<GridItem>();
        if (newGI != null)
        {
            newGI.Initialize(targetRow, targetCol, GridItemType.Shooter);
            shooter.transform.localPosition = targetLocalPos;

            gridController.RegisterNode(newGI, targetIsEndNode);

            targetGridItem = newGI;
            shooter.SetGridItem(newGI);
        }
    }

    private void InjectDependencies(BaseShooter shooter)
    {
        if (slotBar != null) shooter.SetSlotBar(slotBar);
        if (gridController != null) shooter.SetGridController(gridController);
        if (_camera != null) shooter.SetCamera(_camera);
    }

    private void ApplyExitStateForReleasedShooter(BaseShooter shooter)
    {
        if (shooter == null)
        {
            return;
        }

        if (inheritStateFromPreviousReleasedShooter && hasPreviousReleasedShooterState)
        {
            shooter.SetState(previousReleasedShooterState);

            // Náº¿u káº¿ thá»«a Lock/Empty, váº«n cáº§n check path táº¡i thá»i Ä‘iá»ƒm spawn hiá»‡n táº¡i
            // Ä‘á»ƒ trÃ¡nh giá»¯ Lock cÅ© khi player vá»«a má»Ÿ Ä‘Æ°á»ng báº±ng booster/pick.
            if (previousReleasedShooterState == ShooterState.Lock ||
                previousReleasedShooterState == ShooterState.Empty)
            {
                shooter.CheckShooterState();
            }
        }
        else
        {
            shooter.CheckShooterState();
        }

        previousReleasedShooterState = shooter.GetCurrentState();
        hasPreviousReleasedShooterState = true;
    }

    private void AnimateShooterSpawn(BaseShooter shooter)
    {
        if (gridController == null)
        {
            return;
        }

        Vector3 spawnStartLocalPos = gridController.transform.InverseTransformPoint(transform.position);
        shooter.transform.localPosition = spawnStartLocalPos;
        shooter.transform.DOLocalMove(targetLocalPos, 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                shooter.transform.localPosition = targetLocalPos;
            });
    }

    private void UpdateTunnelStateAfterSpawn()
    {
        remainShooterCount--;
        UpdateCountText();
        UpdateMaterialColor();

        if (remainShooterCount <= 0)
        {
            if (gridController != null)
            {
                // Gá»i hÃ m báº¡n vá»«a viáº¿t bÃªn GridController
                gridController.RemoveLockItemByTunel(targetCol, targetRow);
                ;
            }
        }
    }
    #endregion

    #region --- [VISUALS & UI UPDATERS] ---
    private void PlayReleaseVFX()
    {
        if (releaseVFX != null) releaseVFX.Play();
    }

    private void UpdateMaterialColor()
    {
        if (borderMatInstance == null) return;

        if (shooterList != null && shooterList.Count > 0 && shooterList[0] != null)
        {
            borderMatInstance.color = ColorInfo.GetUnityColor(shooterList[0].GetTargetColor());
        }
        else
        {
            borderMatInstance.color = defaultBorderColor;
        }
    }

    private void UpdateCountText()
    {
        if (countText == null) return;

        if (remainShooterCount <= 0)
        {
            countText.gameObject.SetActive(false);
        }
        else
        {
            countText.gameObject.SetActive(true); // ThÃªm dÃ²ng nÃ y Ä‘á»ƒ cháº¯c cháº¯n nÃ³ hiá»‡n láº¡i náº¿u cÃ³ náº¡p thÃªm
            countText.text = remainShooterCount.ToString();
        }
    }
    #endregion
}
