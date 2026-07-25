using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class BlockRowSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ColorSeedConfig
    {
        public SeedColor color;
        public int seedCount = 100;
    }

    [Header("Spline Movement")]
    [SerializeField] private SplineContainer spline;
    [SerializeField] private float moveSpeed = 0.2f;

    [Header("Spawn Settings")]
    [SerializeField] private GameObject blockRowPrefab;
    [SerializeField] private GameObject seedPrefab;
    [SerializeField] private int maxTotalBlocks = 400;
    [SerializeField] private int totalBlocks = 400;
    [SerializeField] private int blocksPerRow = 5;
    [SerializeField] private SeedColor defaultColor=SeedColor.Blue;

    [Header("Color Row Plan Settings")]
    [SerializeField] private bool useColorBatches = true;
    [SerializeField] private List<ColorSeedConfig> colorSeedConfigs = new List<ColorSeedConfig>
    {
        new ColorSeedConfig { color=SeedColor.Blue  ,   seedCount = 100 },
        new ColorSeedConfig { color=SeedColor.Red, seedCount = 100 },
        new ColorSeedConfig { color=SeedColor.Yellow, seedCount = 100 },
        new ColorSeedConfig { color=SeedColor.Blue, seedCount = 100 }
    };

    [SerializeField] private bool spawnAllAtStart = true; // Spawn táº¥t cáº£ ngay khi start
    [SerializeField] private float spawnInterval = 0.5f; // Chá»‰ dÃ¹ng khi spawnAllAtStart = false
    [SerializeField] private bool autoCalculateSpacing = true; // Tá»± Ä‘á»™ng tÃ­nh spacing dá»±a trÃªn collider
    [SerializeField] private bool distributeEvenlyOnSpline = true; // PhÃ¢n bá»‘ Ä‘á»u trÃªn spline vs ná»‘i sÃ¡t nhau
    [SerializeField] private bool enableRuntimeLogs = false;

    private int totalBlockRowsToSpawn;
    private int blockRowsSpawned = 0;
    private float lastSpawnTime;
    private float globalDistance = 0f;
    private float splineLength;
    private float blockRowLength = 2f; // Äá»™ dÃ i thá»±c táº¿ cá»§a BlockRow (sáº½ Ä‘Æ°á»£c tÃ­nh tá»± Ä‘á»™ng)
    private float calculatedSpacing = 2f; // Khoáº£ng cÃ¡ch Ä‘Æ°á»£c tÃ­nh cho phÃ¢n bá»‘ Ä‘á»u
    private readonly List<SeedColor> rowColorPlan = new List<SeedColor>();
    
    private List<BlockRowMovementData> movingBlockRows = new List<BlockRowMovementData>();

    [System.Serializable]
    public class BlockRowMovementData
    {
        public GameObject blockRow;
        public float spawnDistance;
        
        public BlockRowMovementData(GameObject obj, float distance)
        {
            blockRow = obj;
            spawnDistance = distance;
        }
    }

    void Start()
    {
        // TÃ­nh sá»‘ BlockRows cáº§n spawn
        if (useColorBatches)
        {
            BuildRowColorPlanFromConfigs();
        }
        else
        {
            totalBlocks = Mathf.Min(totalBlocks, maxTotalBlocks);
            totalBlockRowsToSpawn = totalBlocks / Mathf.Max(1, blocksPerRow);
            totalBlocks = totalBlockRowsToSpawn * Mathf.Max(1, blocksPerRow);

            rowColorPlan.Clear();
            for (int i = 0; i < totalBlockRowsToSpawn; i++)
            {
                rowColorPlan.Add(defaultColor);
            }
        }
        
        // Calculate spline length if spline is assigned
        if (spline != null)
        {
            splineLength = spline.CalculateLength();
        }
        else
        {
            ;
            enabled = false;
            return;
        }

        // Calculate BlockRow length from prefab if auto calculate is enabled
        if (autoCalculateSpacing && blockRowPrefab != null)
        {
            CalculateBlockRowLength();
        }

        // Calculate spacing for even distribution
        CalculateEvenSpacing();
        
        LogRuntime($"Total blocks: {totalBlocks}");
        LogRuntime($"Blocks per row: {blocksPerRow}");
        LogRuntime($"BlockRows to spawn: {totalBlockRowsToSpawn}");
        LogRuntime($"Max total blocks: {maxTotalBlocks}");
        LogRuntime($"Use color batches: {useColorBatches}");
        LogRuntime($"Spline length: {splineLength}");
        LogRuntime($"BlockRow length: {blockRowLength}");
        LogRuntime($"Calculated spacing: {calculatedSpacing}");
        LogRuntime($"Distribute evenly: {distributeEvenlyOnSpline}");
        
        lastSpawnTime = Time.time;

        // Spawn táº¥t cáº£ BlockRows ngay láº­p tá»©c náº¿u Ä‘Æ°á»£c enable
        if (spawnAllAtStart)
        {
            SpawnAllBlockRows();
        }
    }

    void BuildRowColorPlanFromConfigs()
    {
        rowColorPlan.Clear();

        if (colorSeedConfigs == null || colorSeedConfigs.Count == 0)
        {
            totalBlocks = 0;
            totalBlockRowsToSpawn = 0;
            return;
        }

        int rowSize = Mathf.Max(1, blocksPerRow);
        int maxRowsAllowed = maxTotalBlocks / rowSize;

        List<ColorRowBucket> buckets = new List<ColorRowBucket>();
        int totalRowsRequested = 0;

        foreach (var cfg in colorSeedConfigs)
        {
            if (cfg == null) continue;

           
            int fullRowsForColor = Mathf.Max(0, cfg.seedCount / rowSize); // chá»‰ nháº­n row Ä‘á»§ 5 háº¡t
            if (fullRowsForColor <= 0) continue;

            buckets.Add(new ColorRowBucket(cfg.color, fullRowsForColor));
            totalRowsRequested += fullRowsForColor;
        }

        int totalRowsAllowed = Mathf.Min(totalRowsRequested, maxRowsAllowed);
        if (totalRowsAllowed <= 0)
        {
            totalBlocks = 0;
            totalBlockRowsToSpawn = 0;
            return;
        }

        int rowsAdded = 0;
        foreach (var bucket in buckets)
        {
            for (int i = 0; i < bucket.rowCount && rowsAdded < totalRowsAllowed; i++)
            {
                rowColorPlan.Add(bucket.color);
                rowsAdded++;
            }
        }

        totalBlockRowsToSpawn = rowColorPlan.Count;
        totalBlocks = totalBlockRowsToSpawn * rowSize;
    }

    class ColorRowBucket
    {
        public SeedColor color;
        public int rowCount;

        public ColorRowBucket(SeedColor colorID, int rowCount)
        {
            this.color = colorID;
            this.rowCount = rowCount;
        }
    }

    void OnValidate()
    {
        blocksPerRow = 5;
        maxTotalBlocks = Mathf.Max(0, maxTotalBlocks);
        totalBlocks = Mathf.Clamp(totalBlocks, 0, maxTotalBlocks);

        if (colorSeedConfigs == null)
        {
            colorSeedConfigs = new List<ColorSeedConfig>();
            return;
        }

        for (int i = 0; i < colorSeedConfigs.Count; i++)
        {
            if (colorSeedConfigs[i] == null)
            {
                colorSeedConfigs[i] = new ColorSeedConfig();
            }

            colorSeedConfigs[i].seedCount = Mathf.Max(0, colorSeedConfigs[i].seedCount);

            // Má»—i row pháº£i Ä‘á»§ 5 háº¡t cÃ¹ng mÃ u -> Ã©p vá» bá»™i sá»‘ cá»§a 5
            colorSeedConfigs[i].seedCount -= colorSeedConfigs[i].seedCount % blocksPerRow;
        }
    }

    void Update()
    {
        // Update global distance for movement
        globalDistance += moveSpeed * Time.deltaTime;

        // Spawn BlockRows theo interval (chá»‰ khi khÃ´ng spawn all at start)
        if (!spawnAllAtStart && blockRowsSpawned < totalBlockRowsToSpawn && 
            Time.time - lastSpawnTime >= spawnInterval)
        {
            SpawnBlockRow();
            lastSpawnTime = Time.time;
        }

        // Move BlockRows along spline
        if (spline != null)
        {
            MoveBlockRowsAlongSpline();
        }

    }

    void CalculateBlockRowLength()
    {
        if (blockRowPrefab == null) return;

        // Create temporary instance to measure
        GameObject tempBlockRow = Instantiate(blockRowPrefab);
        
        // Get all colliders in the BlockRow
        Collider[] colliders = tempBlockRow.GetComponentsInChildren<Collider>();
        
        if (colliders.Length > 0)
        {
            // Calculate bounds along spline direction (forward)
            Bounds totalBounds = colliders[0].bounds;
            
            foreach (Collider col in colliders)
            {
                totalBounds.Encapsulate(col.bounds);
            }
            
            // Use Z size as length (forward direction on spline) - no offset
            blockRowLength = totalBounds.size.z;
        }
        else
        {
            // Fallback: try to get renderer bounds
            Renderer[] renderers = tempBlockRow.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds totalBounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    totalBounds.Encapsulate(renderer.bounds);
                }
                blockRowLength = totalBounds.size.z; // No offset
            }
        }

        // Clean up temporary object
        if (Application.isPlaying)
            Destroy(tempBlockRow);
        else
            DestroyImmediate(tempBlockRow);

        ;
    }

    void CalculateEvenSpacing()
    {
        if (splineLength <= 0 || totalBlockRowsToSpawn <= 1)
        {
            calculatedSpacing = blockRowLength;
            return;
        }

        if (distributeEvenlyOnSpline)
        {
            // PhÃ¢n bá»‘ Ä‘á»u trÃªn toÃ n bá»™ spline, khÃ´ng Ä‘á»ƒ row Ä‘áº§u/cuá»‘i trÃ¹ng nhau
            calculatedSpacing = splineLength / totalBlockRowsToSpawn;
            
            // Äáº£m báº£o spacing khÃ´ng nhá» hÆ¡n kÃ­ch thÆ°á»›c BlockRow
            if (calculatedSpacing < blockRowLength)
            {
                calculatedSpacing = blockRowLength;
                ;
            }
        }
        else
        {
            // Ná»‘i sÃ¡t nhau
            calculatedSpacing = blockRowLength;
        }
    }

    float CalculateSpawnDistance()
    {
        if (distributeEvenlyOnSpline)
        {
            // PhÃ¢n bá»‘ Ä‘á»u: má»—i BlockRow cÃ¡ch nhau calculatedSpacing
            return globalDistance - (blockRowsSpawned * calculatedSpacing);
        }
        else
        {
            // Ná»‘i sÃ¡t: BlockRows ná»‘i tiáº¿p nhau
            return globalDistance - (blockRowsSpawned * blockRowLength);
        }
    }

    void SpawnAllBlockRows()
    {
        LogRuntime($"Spawning all {totalBlockRowsToSpawn} BlockRows at once...");
        
        while (blockRowsSpawned < totalBlockRowsToSpawn)
        {
            SpawnBlockRow();
        }
        
        LogRuntime($"Finished spawning all BlockRows! Total: {blockRowsSpawned}");
    }

    void SpawnBlockRow()
    {
        if (spline == null)
        {
            ;
            return;
        }

        Vector3 position;
        Quaternion rotation;
        SeedColor colorForThisRow = GetColorForRow(blockRowsSpawned);
        string colorNameForThisRow = colorForThisRow.ToString();

        position = spline.EvaluatePosition(0f);
        Vector3 forward = math.normalize(spline.EvaluateTangent(0f));
        rotation = Quaternion.LookRotation(forward);

        GameObject blockRow = blockRowPrefab != null ? 
            Instantiate(blockRowPrefab, position, rotation, transform) :
            CreateDefaultBlockRow(position, rotation);

        ConfigureAndSpawnSeeds(blockRow, colorForThisRow);

        // Äáº·t tÃªn cho BlockRow
        blockRow.name = $"BlockRow_{blockRowsSpawned + 1:000}_{colorNameForThisRow}";
        
        // Add to movement tracking
        float spawnDistance = CalculateSpawnDistance();
        movingBlockRows.Add(new BlockRowMovementData(blockRow, spawnDistance));
        
        blockRowsSpawned++;

        if (spawnAllAtStart)
        {
            LogRuntime($"Spawned {blockRow.name} (Batch: {blockRowsSpawned}/{totalBlockRowsToSpawn}) - Color: {colorNameForThisRow}");
        }
        else
        {
            LogRuntime($"Spawned {blockRow.name} at position {position} ({blockRowsSpawned}/{totalBlockRowsToSpawn}) - Color: {colorNameForThisRow}");
        }

        // Callback khi spawn xong
        if (blockRowsSpawned >= totalBlockRowsToSpawn)
        {
            OnSpawningComplete();
        }
    }

    SeedColor GetColorForRow(int rowIndex)
    {
        if (!useColorBatches || rowColorPlan.Count == 0)
            return defaultColor;

        if (rowIndex < 0 || rowIndex >= rowColorPlan.Count)
            return defaultColor;

        return rowColorPlan[rowIndex];
    }

    private void LogRuntime(string message)
    {
        if (!enableRuntimeLogs)
        {
            return;
        }

        ;
    }

    void ConfigureAndSpawnSeeds(GameObject blockRow, SeedColor color)
    {
        if (blockRow == null) return;

        BlockRowSeedSpawner seedSpawner = blockRow.GetComponent<BlockRowSeedSpawner>();
        if (seedSpawner == null)
        {
            seedSpawner = blockRow.AddComponent<BlockRowSeedSpawner>();
        }

        if (seedPrefab == null)
        {
            ;
            return;
        }

        seedSpawner.InitializeFromSpawner(seedPrefab, color, true, true);
    }

    void MoveBlockRowsAlongSpline()
    {
        foreach (var blockRowData in movingBlockRows)
        {
            if (blockRowData.blockRow == null) continue;

            float totalDistance = globalDistance - blockRowData.spawnDistance;
            float t = (totalDistance % splineLength) / splineLength;

            if (t < 0) t += 1f; // Handle negative values

            Vector3 splinePosition = spline.EvaluatePosition(t);
            Vector3 forward = math.normalize(spline.EvaluateTangent(t));

            blockRowData.blockRow.transform.position = splinePosition;
            blockRowData.blockRow.transform.rotation = Quaternion.LookRotation(forward);
        }
    }

    GameObject CreateDefaultBlockRow(Vector3 position, Quaternion rotation)
    {
        GameObject blockRow = new GameObject();
        blockRow.transform.position = position;
        blockRow.transform.rotation = rotation;
        blockRow.transform.SetParent(transform);
        return blockRow;
    }

    void OnSpawningComplete()
    {
        ;
        
        // CÃ³ thá»ƒ thÃªm event hoáº·c callback á»Ÿ Ä‘Ã¢y
        List<GameObject> allBlockRows = new List<GameObject>();
        foreach (var data in movingBlockRows)
        {
            if (data.blockRow != null)
                allBlockRows.Add(data.blockRow);
        }
        OnAllBlockRowsSpawned?.Invoke(allBlockRows);
    }

    // Events
    public System.Action<List<GameObject>> OnAllBlockRowsSpawned;

    // Public methods
    public void SpawnAll()
    {
        // Public method Ä‘á»ƒ spawn táº¥t cáº£ tá»« bÃªn ngoÃ i
        if (blockRowsSpawned < totalBlockRowsToSpawn)
        {
            SpawnAllBlockRows();
        }
        else
        {
            ;
        }
    }

    public void ClearAllBlockRows()
    {
        foreach (var blockRowData in movingBlockRows)
        {
            if (blockRowData.blockRow != null)
            {
                if (Application.isPlaying)
                    Destroy(blockRowData.blockRow);
                else
                    DestroyImmediate(blockRowData.blockRow);
            }
        }
        
        movingBlockRows.Clear();
        blockRowsSpawned = 0;
        globalDistance = 0f;
        
        ;
    }

    public void SetTotalBlocks(int newTotalBlocks)
    {
        totalBlocks = Mathf.Min(newTotalBlocks, maxTotalBlocks);
        if (useColorBatches)
        {
            BuildRowColorPlanFromConfigs();
        }
        else
        {
            totalBlockRowsToSpawn = totalBlocks / Mathf.Max(1, blocksPerRow);
            totalBlocks = totalBlockRowsToSpawn * Mathf.Max(1, blocksPerRow);

            rowColorPlan.Clear();
            for (int i = 0; i < totalBlockRowsToSpawn; i++)
            {
                rowColorPlan.Add(defaultColor);
            }
        }
        ;
    }

    public void SetColorBatches(int blueCount, int redCount, int greenCount, int yellowCount)
    {
        if (colorSeedConfigs == null)
        {
            colorSeedConfigs = new List<ColorSeedConfig>();
        }

        colorSeedConfigs.Clear();
        colorSeedConfigs.Add(new ColorSeedConfig { color = SeedColor.Blue, seedCount = Mathf.Max(0, blueCount) });
        colorSeedConfigs.Add(new ColorSeedConfig { color = SeedColor.Red, seedCount = Mathf.Max(0, redCount) });
        colorSeedConfigs.Add(new ColorSeedConfig { color = SeedColor.Green, seedCount = Mathf.Max(0, greenCount) });
        colorSeedConfigs.Add(new ColorSeedConfig { color = SeedColor.Yellow, seedCount = Mathf.Max(0, yellowCount) });

        BuildRowColorPlanFromConfigs();
        CalculateEvenSpacing();

        ;
        ;
        ;
    }

    public void SetColorSeedConfigs(List<ColorSeedConfig> newConfigs)
    {
        if (newConfigs == null)
        {
            return;
        }

        colorSeedConfigs = new List<ColorSeedConfig>(newConfigs);

        BuildRowColorPlanFromConfigs();
        CalculateEvenSpacing();

        ;
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    public void SetBlockRowLength(float newLength)
    {
        blockRowLength = newLength;
        ;
    }

    public float GetBlockRowLength()
    {
        return blockRowLength;
    }

    public void SetSpawnAllAtStart(bool spawnAll)
    {
        spawnAllAtStart = spawnAll;
        ;
    }

    public bool GetSpawnAllAtStart()
    {
        return spawnAllAtStart;
    }

    public void SetDistributeEvenly(bool distributeEvenly)
    {
        distributeEvenlyOnSpline = distributeEvenly;
        CalculateEvenSpacing();
        ;
    }

    public bool GetDistributeEvenly()
    {
        return distributeEvenlyOnSpline;
    }

    public float GetCalculatedSpacing()
    {
        return calculatedSpacing;
    }

    public void RestartSpawning()
    {
        // Reset vÃ  spawn láº¡i náº¿u cáº§n
        ClearAllBlockRows();
        CalculateEvenSpacing(); // Recalculate spacing
        if (spawnAllAtStart)
        {
            SpawnAllBlockRows();
        }
    }

    public bool IsSpawningComplete()
    {
        return blockRowsSpawned >= totalBlockRowsToSpawn;
    }

    public float GetSpawnProgress()
    {
        if (totalBlockRowsToSpawn == 0) return 0f;
        return (float)blockRowsSpawned / totalBlockRowsToSpawn;
    }

    public List<GameObject> GetActiveBlockRows()
    {
        List<GameObject> activeBlockRows = new List<GameObject>();
        foreach (var data in movingBlockRows)
        {
            if (data.blockRow != null)
                activeBlockRows.Add(data.blockRow);
        }
        return activeBlockRows;
    }

    public int GetActiveBlockRowCount()
    {
        return movingBlockRows.Count;
    }

    // Gizmos Ä‘á»ƒ visualize spawn positions
    void OnDrawGizmosSelected()
    {
        if (spline == null) return;

        Gizmos.color = Color.yellow;
        Vector3 prevPos = spline.EvaluatePosition(0f);
        
        for (int i = 1; i <= 50; i++)
        {
            float t = i / 50f;
            Vector3 currentPos = spline.EvaluatePosition(t);
            Gizmos.DrawLine(prevPos, currentPos);
            prevPos = currentPos;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spline.EvaluatePosition(0f), 0.2f);
    }
}

