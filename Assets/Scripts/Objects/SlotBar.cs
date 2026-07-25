using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quáº£n lÃ½ cÃ¡c slot trong slotbar Ä‘á»ƒ chá»©a shooter
/// DÃ¹ng Queue Ä‘á»ƒ quáº£n lÃ½ cÃ¡c slot trá»‘ng
/// </summary>
public class SlotBar : MonoBehaviour
{
    public static SlotBar Instance { get; private set; }

    [SerializeField] private SlotBarConfig slotBarConfig;
    [SerializeField] private int slotCount = 5;
    [SerializeField] private Slot slotPrefab;
    [SerializeField] private float slotSpacing = 1.5f;
    [SerializeField] private List<Slot> slots = new List<Slot>();
    private Queue<Slot> availableSlots = new Queue<Slot>();
    private readonly HashSet<Slot> reservedSlots = new HashSet<Slot>();

    // â”€â”€â”€ Centering constants â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Slot index 2 (chÃ­nh giá»¯a) luÃ´n náº±m á»Ÿ vá»‹ trÃ­ local X = 0
    // CÃ¡c slot cÃ²n láº¡i offset Ä‘á»u sang hai bÃªn theo slotSpacing.

    // Danh sÃ¡ch shooter theo thá»© tá»± Ä‘áº¿n â€” dÃ¹ng Ä‘á»ƒ xÃ¡c Ä‘á»‹nh ai Ä‘Æ°á»£c báº¯n khi cÃ¹ng mÃ u
    private readonly List<BaseShooter> arrivedShooters = new List<BaseShooter>();

    void Awake()
    {
        Instance = this;
        ApplyConfigValues();
        // Khá»Ÿi táº¡o slots tá»« prefab
        //InitializeSlots();  
        InitQueue();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        GameEventHub.Instance.Invoke(GameEventType.OnSlotBarInit, this);
    }

    void OnValidate()
    {
        TryAutoAssignSlotBarConfig();
        ApplyConfigValues();
        GetSlot();
    }

    private void ApplyConfigValues()
    {
        if (slotBarConfig != null)
        {
            slotCount = Mathf.Max(1, slotBarConfig.slotCount);
            slotSpacing = slotBarConfig.slotSpacing;

            if (slotBarConfig.slotPrefab != null)
            {
                slotPrefab = slotBarConfig.slotPrefab;
            }
        }

        slotCount = Mathf.Max(1, slotCount);
    }

    private void TryAutoAssignSlotBarConfig()
    {
        if (slotBarConfig != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:SlotBarConfig");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            SlotBarConfig foundConfig = AssetDatabase.LoadAssetAtPath<SlotBarConfig>(path);
            if (foundConfig != null)
            {
                slotBarConfig = foundConfig;
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }

    private void GetSlot()
    {
        Slot[] slotsInChildren = GetComponentsInChildren<Slot>(true);

        if (slots == null)
        {
            slots = new List<Slot>();
        }
        else
        {
            slots.Clear();
        }

        foreach (Slot slot in slotsInChildren)
        {
            if (slot != null && slot.gameObject != gameObject)
            {
                slots.Add(slot);
            }
        }
    }
    /// <summary>
    /// Khá»Ÿi táº¡o cÃ¡c slot báº±ng cÃ¡ch instantiate tá»« prefab
    /// </summary>
    private void InitQueue()
    {
        reservedSlots.Clear();
        RebuildAvailableSlotsQueue();
    }

    private void RebuildAvailableSlotsQueue()
    {
        availableSlots.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (!slot.IsEmpty())
            {
                continue;
            }

            if (reservedSlots.Contains(slot))
            {
                continue;
            }

            availableSlots.Enqueue(slot);
        }
    }

    public void ToggleGlowVFX(bool isOn)
    {
        foreach(var slot in slots)
        {
            if (isOn)
            {
                slot.PlayGlowVFX();
            }
            else
            {
                slot.StopGlowVFX();
            }
        }
    }

    /// <summary>
    /// Toggle glow VFX only on slots containing Idle shooters (selectable for hero booster).
    /// Other slots remain non-glowing.
    /// </summary>
    public void ToggleHeroShooterSelectionGlow(bool isOn)
    {
        foreach (var slot in slots)
        {
            BaseShooter shooter = slot.GetShooter();
            bool isSelectable = shooter != null && shooter.GetCurrentState() == ShooterState.Idle;

            if (isOn && isSelectable)
            {
                slot.PlayGlowVFX();
            }
            else
            {
                slot.StopGlowVFX();
            }
        }
    }

    /// <summary>
    /// ThÃªm shooter vÃ o slot trá»‘ng Ä‘áº§u tiÃªn (queue Ä‘áº§u)
    /// Náº¿u táº¥t cáº£ slot Ä‘áº§y thÃ¬ tráº£ vá» false
    /// </summary>
    /// <param name="shooter">Shooter cáº§n thÃªm</param>
    /// <returns>True náº¿u thÃªm thÃ nh cÃ´ng, False náº¿u Ä‘áº§y</returns>
    public bool AddShooter(BaseShooter shooter)
    {
        if (shooter == null)
            return false;

        RebuildAvailableSlotsQueue();

        // Kiá»ƒm tra cÃ³ slot trá»‘ng khÃ´ng
        if (availableSlots.Count == 0)
        {
            // Táº¥t cáº£ slot Ä‘áº§y
            GameEventHub.Instance.Invoke(GameEventType.OnSlotBarFull); // SlotBar.AddShooter
            ;
            return false;
        }
        return true;
    }
    public void PlayGlowVFX(Slot slot)
    {
        slot.PlayGlowVFX();
    }

    /// <summary>
    /// XÃ³a shooter khá»i slot
    /// </summary>
    /// <param name="shooter">Shooter cáº§n xÃ³a</param>
    public void RemoveShooter(BaseShooter shooter)
    {
        if (shooter == null)
            return;

        // TÃ¬m slot chá»©a shooter nÃ y
        foreach (var slot in slots)
        {
            if (slot.GetShooter() == shooter)
            {
                slot.ClearShooter();
                reservedSlots.Remove(slot);
                RebuildAvailableSlotsQueue();
                ;
                arrivedShooters.Remove(shooter);
                return;
            }
        }

        // Shooter khÃ´ng náº±m trong slot (chÆ°a gá»i SetShooter) â€” váº«n cáº§n xÃ³a khá»i danh sÃ¡ch
        arrivedShooters.Remove(shooter);
        ;
    }

    /// <summary>
    /// Láº¥y vá»‹ trÃ­ world cá»§a slot trá»‘ng Ä‘áº§u tiÃªn
    /// </summary>
    /// <returns>Vá»‹ trÃ­ world slot trá»‘ng Ä‘áº§u tiÃªn</returns>
    public Vector3 GetAvailableSlotPosition()
    {
        RebuildAvailableSlotsQueue();
        if (availableSlots.Count > 0)
        {
            return availableSlots.Peek().GetPosition();  // World position
        }
        return slots[0].GetPosition();
    }

    /// <summary>
    /// Láº¥y slot trá»‘ng Ä‘áº§u tiÃªn (Ä‘á»ƒ reparent shooter vÃ o)
    /// </summary>
    /// <returns>Transform cá»§a slot trá»‘ng</returns>
    public Slot GetAvailableSlotTransform()
    {
        RebuildAvailableSlotsQueue();
        if (availableSlots.Count > 0)
        {
            Slot slot = availableSlots.Peek();
            return slot;
        }
        return null;

    }

    public bool TryReserveNextAvailableSlot(out Slot reservedSlot, out Vector3 worldPosition)
    {
        reservedSlot = null;
        worldPosition = Vector3.zero;

        RebuildAvailableSlotsQueue();
        if (availableSlots.Count == 0)
        {
            return false;
        }

        reservedSlot = availableSlots.Dequeue();
        if (reservedSlot == null)
        {
            return false;
        }

        reservedSlots.Add(reservedSlot);
        worldPosition = reservedSlot.GetPosition();
        return true;
    }

    /// <summary>
    /// Kiá»ƒm tra xem slotbar cÃ³ Ä‘áº§y khÃ´ng
    /// </summary>
    public bool IsFull()
    {
        RebuildAvailableSlotsQueue();
        return availableSlots.Count == 0;
    }

    /// <summary>
    /// Láº¥y sá»‘ slot trá»‘ng hiá»‡n táº¡i
    /// </summary>
    public int GetEmptySlotCount()
    {
        RebuildAvailableSlotsQueue();
        return availableSlots.Count;
    }

    /// <summary>
    /// Láº¥y sá»‘ shooter hiá»‡n cÃ³ trong slotbar
    /// </summary>
    public int GetShooterCount()
    {
        return slots.Count - GetEmptySlotCount();
    }

    public void RemoveAvailableSlotFromQueue()
    {
        RebuildAvailableSlotsQueue();
        if (availableSlots.Count == 0)
        {
            return;
        }

        Slot reservedSlot = availableSlots.Dequeue();
        if (reservedSlot != null)
        {
            reservedSlots.Add(reservedSlot);
        }
    }

    /// <summary>
    /// ÄÄƒng kÃ½ shooter vá»«a Ä‘Ã¡p xuá»‘ng slot (ghi nháº­n thá»© tá»± Ä‘áº¿n)
    /// </summary>
    public void RegisterShooter(BaseShooter shooter)
    {
        if (shooter != null && !arrivedShooters.Contains(shooter))
            arrivedShooters.Add(shooter);

        if (shooter == null)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (slot.GetShooter() == shooter)
            {
                reservedSlots.Remove(slot);
                break;
            }
        }

        RebuildAvailableSlotsQueue();
    }

    /// <summary>
    /// Tráº£ vá» true náº¿u shooter nÃ y lÃ  ngÆ°á»i Äáº¦U TIÃŠN cÃ¹ng mÃ u trong danh sÃ¡ch Ä‘áº¿n.
    /// Shooter thá»© 2 trá»Ÿ Ä‘i cÃ¹ng mÃ u pháº£i chá».
    /// </summary>
    public bool IsFirstShooterOfColor(BaseShooter shooter)
    {
        SeedColor color = shooter.GetTargetColor();
        foreach (BaseShooter s in arrivedShooters)
        {
            if (s == null) continue;
            if (s.GetTargetColor() == color)
                return s == shooter; // first match wins
        }
        return true;
    }

    /// <summary>
    /// Láº¥y danh sÃ¡ch táº¥t cáº£ shooter trong slotbar
    /// </summary>
    public List<BaseShooter> GetAllShooters()
    {
        List<BaseShooter> result = new List<BaseShooter>();
        foreach (var slot in slots)
        {
            BaseShooter shooter = slot.GetShooter();
            if (shooter != null)
                result.Add(shooter);
        }
        return result;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Booster: Add Slot
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Tá»•ng sá»‘ slot hiá»‡n táº¡i (ká»ƒ cáº£ Ä‘áº§y láº«n trá»‘ng).
    /// </summary>
    public int GetSlotCount() => slots.Count;

    /// <summary>
    /// TÃ­nh máº£ng local-X chuáº©n Ä‘á»ƒ cÄƒn giá»¯a toÃ n bá»™ SlotBar
    /// DÃ nh cho há»‡ trá»¥c ngÆ°á»£c: TrÃ¡i (+), Pháº£i (-)
    /// </summary>
    private float[] CalculateCenteredXPositions(int count)
    {
        float[] xs = new float[count];
        float centerOffset = (count - 1) / 2f;
        for (int i = 0; i < count; i++)
        {
            // Äáº¢O NGÆ¯á»¢C CÃ”NG THá»¨C: Thay vÃ¬ (i - centerOffset), ta dÃ¹ng (centerOffset - i)
            xs[i] = (centerOffset - i) * slotSpacing;
        }
        return xs;
    }

    /// <summary>
    /// ThÃªm Slot má»›i: TÃ­nh vá»‹ trÃ­ spawn, sau Ä‘Ã³ lÃ¹i cÃ¡c slot cÅ©
    /// Hiá»‡u á»©ng Slot má»›i: Chá»“i lÃªn tá»« dÆ°á»›i vÃ  to dáº§n
    /// </summary>
    public void AddSlotWithAnimation(AddSlotBoosterConfig cfg, System.Action onComplete = null)
    {
        if (slotPrefab == null) return;

        int newIndex = slots.Count;
        int newCount = slots.Count + 1;
        slotCount = newCount;

        // 1. TÃ­nh toÃ¡n máº£ng vá»‹ trÃ­ ÄÃCH má»›i (Ä‘Ã£ Ä‘áº£o trá»¥c)
        float[] targetXs = CalculateCenteredXPositions(newCount);

        // 2. TÃ­nh khoáº£ng cÃ¡ch lÃ¹i
        float shiftAmount = targetXs[2] - slots[2].transform.localPosition.x;

        // Láº¥y Y vÃ  Z chuáº©n tá»« cÃ¡c slot hiá»‡n táº¡i
        float currentY = slots[0].transform.localPosition.y;
        float currentZ = slots[0].transform.localPosition.z;

        // 3. Táº¡o Slot má»›i
        Slot newSlot = Instantiate(slotPrefab, transform);

        // TÃ­nh toÃ¡n vá»‹ trÃ­ X chuáº©n cho Slot má»›i
        float newSlotTargetX = targetXs[newIndex];

  

        // Äáº·t vá»‹ trÃ­ ban Ä‘áº§u: Náº±m Ä‘Ãºng X Ä‘Ã­ch, nhÆ°ng Y bá»‹ tá»¥t xuá»‘ng dÆ°á»›i
        newSlot.transform.localPosition = new Vector3(newSlotTargetX, currentY, currentZ);

        // Äáº·t Scale ban Ä‘áº§u vá» 0 (tÃ ng hÃ¬nh)
        newSlot.transform.localScale = Vector3.zero;

        newSlot.Initialize(newIndex, newSlot.transform);

        slots.Add(newSlot);
        RebuildAvailableSlotsQueue();

        // 4. ANIMATION
        float riseDur = cfg != null ? cfg.riseDuration : 0.45f;
        float shiftDur = cfg != null ? cfg.shiftDuration : 0.35f;
        DG.Tweening.Ease riseEase = cfg != null ? cfg.riseEase : DG.Tweening.Ease.OutBack;
        DG.Tweening.Ease shiftEase = cfg != null ? cfg.shiftEase : DG.Tweening.Ease.OutCubic;

        Sequence seq = DOTween.Sequence();

        // A. LÃ¹i Táº¤T Cáº¢ cÃ¡c slot cÅ© Ä‘i má»™t khoáº£ng = shiftAmount (TrÆ°á»£t ngang)
        for (int i = 0; i < newIndex; i++)
        {
            Slot s = slots[i];
            float targetXForOldSlot = s.transform.localPosition.x + shiftAmount;
            seq.Join(s.transform.DOLocalMoveX(targetXForOldSlot, shiftDur).SetEase(shiftEase));
        }
        // C. Slot má»›i to dáº§n (Scale) tá»« 0 lÃªn 1
        seq.Join(newSlot.transform.DOScale(Vector3.one*0.25f, riseDur).SetEase(riseEase));

        seq.OnComplete(() => onComplete?.Invoke());
    }
    /// <summary>
    /// (TÃ¹y chá»n) CÄƒn chá»‰nh ngay láº­p tá»©c táº¥t cáº£ slot vá» vá»‹ trÃ­ chuáº©n
    /// â€” gá»i sau khi load scene hoáº·c khi khÃ´ng cáº§n animation.
    /// </summary>
    public void CenterSlotsImmediate()
    {
        float[] xs = CalculateCenteredXPositions(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            Vector3 p = slots[i].transform.localPosition;
            slots[i].transform.localPosition = new Vector3(xs[i], p.y, p.z);
        }
    }
}

