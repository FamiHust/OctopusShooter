using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    private const int maxInactiveDefault = 120;
    private const int maxInactiveSeed = 260;
    private const int maxInactiveBlockRow = 140;
    private const int maxInactiveBullet = 180;
    private const int maxInactiveCoin = 220;
    private const int maxInactiveParticle = 220;
    private const int maxInactiveShooter = 80;
    private static readonly int seedColorValueCount = System.Enum.GetValues(typeof(SeedColor)).Length;
    private static readonly int[] seedColorCounts = new int[seedColorValueCount];
    private static readonly int[] seedColorKeepCounts = new int[seedColorValueCount];
    private static readonly Dictionary<string, PoolObjectInfo> objectPoolLookup =
        new Dictionary<string, PoolObjectInfo>(System.StringComparer.Ordinal);
    private static bool poolLookupInitialized;

    public static List<PoolObjectInfo> objectPools = new List<PoolObjectInfo>();

    public static PoolType poolType;
    private static GameObject _objectPoolEmptyHolder;
    private static GameObject seedPool;
    private static GameObject blockRowPool;
    private static GameObject bulletPool;
    private static GameObject coinPool;
    private static GameObject ShooterPool;
    private static GameObject particle;
    public enum PoolType
    {
        Seed,
        BlockRow,
        Bullet,
        Coin,
        Shooter,
        Particle,
        None,
    }
    private void Awake()
    {
        SetUpEmpty();

    }

    private void SetUpEmpty()
    {
        if (_objectPoolEmptyHolder != null)
        {
            if (seedPool == null)
            {
                seedPool = EnsureChildPool(_objectPoolEmptyHolder.transform, "seedPool");
            }

            if (blockRowPool == null)
            {
                blockRowPool = EnsureChildPool(_objectPoolEmptyHolder.transform, "blockRowPool");
            }

            if (bulletPool == null)
            {
                bulletPool = EnsureChildPool(_objectPoolEmptyHolder.transform, "bulletPool");
            }

            if (coinPool == null)
            {
                coinPool = EnsureChildPool(_objectPoolEmptyHolder.transform, "coinPool");
            }

            if (ShooterPool == null)
            {
                ShooterPool = EnsureChildPool(_objectPoolEmptyHolder.transform, "shooterPool");
            }

            if (particle == null)
            {
                particle = EnsureChildPool(_objectPoolEmptyHolder.transform, "ParticlePool");
            }

            return;
        }

        _objectPoolEmptyHolder = new GameObject("PoolObjects");
        DontDestroyOnLoad(_objectPoolEmptyHolder);

        seedPool = new GameObject("seedPool");
        seedPool.transform.SetParent(_objectPoolEmptyHolder.transform);

        blockRowPool = new GameObject("blockRowPool");
        blockRowPool.transform.SetParent(_objectPoolEmptyHolder.transform);

        bulletPool = new GameObject("bulletPool");
        bulletPool.transform.SetParent(_objectPoolEmptyHolder.transform);

        coinPool = new GameObject("coinPool");
        coinPool.transform.SetParent(_objectPoolEmptyHolder.transform);

        ShooterPool = new GameObject("shooterPool");
        ShooterPool.transform.SetParent(_objectPoolEmptyHolder.transform);



        particle = new GameObject("ParticlePool");
        particle.transform.SetParent(_objectPoolEmptyHolder.transform);


    }

    private static GameObject EnsureChildPool(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new GameObject(childName);
        created.transform.SetParent(parent);
        return created;
    }


    public static GameObject SpawnObject(GameObject gameObject, Vector3 spawnPos, Quaternion rotation, GameObject parent = null, PoolType poolType = PoolType.None)
    {
        PoolObjectInfo pool = GetOrCreatePool(gameObject.name, poolType);
        GameObject obj = TakeAvailableObject(pool);
        GameObject defaultParent = SetParentGameObject(pool.poolType);
        if (parent == null)
        {
            parent = defaultParent;
        }

        if (obj == null)
        {
            obj = Instantiate(gameObject, spawnPos, rotation);
            if (parent != null)
            {
                obj.transform.SetParent(parent.transform, true);
            }
        }
        else
        {
            obj.transform.position = spawnPos;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            if (parent != null)
            {
                obj.transform.SetParent(parent.transform, true);
            }
        }
        return obj;
    }
    public static GameObject SpawnObject(GameObject gameObject, Vector3 spawnPos, Quaternion rotation, PoolType poolType = PoolType.None)
    {
        PoolObjectInfo pool = GetOrCreatePool(gameObject.name, poolType);
        GameObject obj = TakeAvailableObject(pool);
        if (obj == null)
        {

            obj = Instantiate(gameObject, spawnPos, rotation);
        }
        else
        {
            obj.transform.position = spawnPos;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
        }
        GameObject parent = SetParentGameObject(pool.poolType);
        if (parent != null)
        {
            obj.transform.SetParent(parent.transform, true);

        }
        return obj;
    }
    public static GameObject SpawnObject(GameObject gameObject, Transform parent, PoolType poolType = PoolType.None)
    {
        PoolObjectInfo pool = GetOrCreatePool(gameObject.name, poolType);
        GameObject obj = TakeAvailableObject(pool);
        Transform resolvedParent = parent;
        if (resolvedParent == null)
        {
            GameObject defaultParent = SetParentGameObject(pool.poolType);
            resolvedParent = defaultParent != null ? defaultParent.transform : null;
        }

        if (obj == null)
        {

            if (resolvedParent != null)
            {
                obj = Instantiate(gameObject, resolvedParent);
            }
            else
            {
                obj = Instantiate(gameObject);
            }

        }
        else
        {

            obj.SetActive(true);
            obj.transform.SetParent(resolvedParent, true);
        }
        return obj;
    }
    public static void ReturnObject(GameObject gameObject)
    {
        ReturnObject(gameObject, PoolType.None);
    }

    public static void ReturnObject(GameObject gameObject, PoolType forcedPoolType)
    {
        if (gameObject == null)
        {
            return;
        }

        string poolName;
        if (gameObject.name.EndsWith("(Clone)"))
        {

            poolName = gameObject.name.Substring(0, gameObject.name.Length - 7);
        }
        else
        {

            poolName = gameObject.name;
        }

        EnsurePoolLookupInitialized();
        objectPoolLookup.TryGetValue(poolName, out PoolObjectInfo pool);

        if (pool == null && forcedPoolType != PoolType.None)
        {
            pool = GetOrCreatePool(poolName, forcedPoolType);
        }
        else if (pool != null && forcedPoolType != PoolType.None)
        {
            pool.poolType = forcedPoolType;
        }

        if (pool != null)
        {
            for (int i = pool.poolObjects.Count - 1; i >= 0; i--)
            {
                if (pool.poolObjects[i] == null)
                {
                    pool.poolObjects.RemoveAt(i);
                }
            }

            int maxInactiveCount = GetMaxInactiveCount(pool.poolType);
            if (pool.poolObjects.Count >= maxInactiveCount)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);

            GameObject poolParent = SetParentGameObject(pool.poolType);
            if (poolParent != null)
            {
                Transform targetParent = poolParent.transform;
                Transform currentParent = gameObject.transform.parent;
                if (targetParent != currentParent)
                {
                    try
                    {
                        gameObject.transform.SetParent(targetParent, true);
                    }
                    catch (System.Exception)
                    {
                        // Ignore reparent failures during activation/deactivation lifecycle transitions.
                    }
                }
            }

            if (!pool.poolObjects.Contains(gameObject))
            {
                pool.poolObjects.Add(gameObject);
            }
        }
        else
        {
            ;
            Destroy(gameObject);
        }
    }

    private static PoolObjectInfo GetOrCreatePool(string poolName, PoolType requestedType = PoolType.None)
    {
        EnsurePoolLookupInitialized();

        if (string.IsNullOrEmpty(poolName))
        {
            poolName = "__UnnamedPool__";
        }

        objectPoolLookup.TryGetValue(poolName, out PoolObjectInfo pool);
        if (pool != null)
        {
            if (pool.poolType == PoolType.None && requestedType != PoolType.None)
            {
                pool.poolType = requestedType;
            }
            return pool;
        }

        pool = new PoolObjectInfo
        {
            poolName = poolName,
            poolType = requestedType
        };
        objectPools.Add(pool);
        objectPoolLookup[poolName] = pool;
        return pool;
    }

    private static void EnsurePoolLookupInitialized()
    {
        if (poolLookupInitialized)
        {
            return;
        }

        if (objectPools == null)
        {
            objectPools = new List<PoolObjectInfo>();
        }

        objectPoolLookup.Clear();
        for (int i = 0; i < objectPools.Count; i++)
        {
            PoolObjectInfo pool = objectPools[i];
            if (pool == null || string.IsNullOrEmpty(pool.poolName))
            {
                continue;
            }

            if (!objectPoolLookup.ContainsKey(pool.poolName))
            {
                objectPoolLookup.Add(pool.poolName, pool);
            }
        }

        poolLookupInitialized = true;
    }

    private static GameObject TakeAvailableObject(PoolObjectInfo pool)
    {
        if (pool == null || pool.poolObjects == null || pool.poolObjects.Count == 0)
        {
            return null;
        }

        for (int i = pool.poolObjects.Count - 1; i >= 0; i--)
        {
            GameObject pooledObject = pool.poolObjects[i];
            if (pooledObject == null)
            {
                pool.poolObjects.RemoveAt(i);
                continue;
            }

            // Invalid state: object still active but inside pool list.
            if (pooledObject.activeInHierarchy)
            {
                pool.poolObjects.RemoveAt(i);
                continue;
            }

            pool.poolObjects.RemoveAt(i);
            return pooledObject;
        }

        return null;
    }
    public static GameObject SetParentGameObject(PoolType type)
    {
        switch (type)
        {
            case PoolType.Seed:
                return seedPool;

            case PoolType.BlockRow:
                return blockRowPool;

            //projectile
            case PoolType.Bullet:
                return bulletPool;

            case PoolType.Coin:
                return coinPool;

            case PoolType.Shooter:
                return ShooterPool;
            //other
            case PoolType.Particle:
                return particle;

            case PoolType.None:
                return null;

            default:
                return null;

        }
    }

    private static int GetMaxInactiveCount(PoolType type)
    {
        switch (type)
        {
            case PoolType.Seed:
                return maxInactiveSeed;

            case PoolType.BlockRow:
                return maxInactiveBlockRow;

            case PoolType.Bullet:
                return maxInactiveBullet;

            case PoolType.Coin:
                return maxInactiveCoin;

            case PoolType.Particle:
                return maxInactiveParticle;

            case PoolType.Shooter:
                return maxInactiveShooter;

            default:
                return maxInactiveDefault;
        }
    }

    public static void TrimPoolsForLongSession(
        int maxInactiveParticle = 90,
        int maxInactiveCoin = 80,
        int maxInactiveBullet = 120,
        int maxInactiveBlockRow = 120)
    {
        if (objectPools == null || objectPools.Count == 0)
        {
            return;
        }

        for (int i = objectPools.Count - 1; i >= 0; i--)
        {
            PoolObjectInfo pool = objectPools[i];
            if (pool == null)
            {
                objectPools.RemoveAt(i);
                continue;
            }

            if (pool.poolObjects == null)
            {
                pool.poolObjects = new List<GameObject>();
                continue;
            }

            int targetCap;
            switch (pool.poolType)
            {
                case PoolType.Particle:
                    targetCap = Mathf.Clamp(maxInactiveParticle, 0, GetMaxInactiveCount(pool.poolType));
                    break;

                case PoolType.Coin:
                    targetCap = Mathf.Clamp(maxInactiveCoin, 0, GetMaxInactiveCount(pool.poolType));
                    break;

                case PoolType.Bullet:
                    targetCap = Mathf.Clamp(maxInactiveBullet, 0, GetMaxInactiveCount(pool.poolType));
                    break;

                case PoolType.BlockRow:
                    targetCap = Mathf.Clamp(maxInactiveBlockRow, 0, GetMaxInactiveCount(pool.poolType));
                    break;

                default:
                    // Keep default behavior for Seed/Shooter/None pools.
                    continue;
            }

            TrimPoolToMaxInactive(pool, targetCap);
        }
    }

    private static void TrimPoolToMaxInactive(PoolObjectInfo pool, int targetMaxInactive)
    {
        if (pool == null || pool.poolObjects == null)
        {
            return;
        }

        for (int i = pool.poolObjects.Count - 1; i >= 0; i--)
        {
            GameObject pooledObject = pool.poolObjects[i];
            if (pooledObject == null || pooledObject.activeInHierarchy)
            {
                pool.poolObjects.RemoveAt(i);
            }
        }

        int overflow = pool.poolObjects.Count - Mathf.Max(0, targetMaxInactive);
        if (overflow <= 0)
        {
            return;
        }

        for (int i = 0; i < overflow && pool.poolObjects.Count > 0; i++)
        {
            int lastIndex = pool.poolObjects.Count - 1;
            GameObject pooledObject = pool.poolObjects[lastIndex];
            pool.poolObjects.RemoveAt(lastIndex);

            if (pooledObject == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(pooledObject);
            }
            else
            {
                DestroyImmediate(pooledObject);
            }
        }
    }

    public static void NormalizeInactiveSeedPoolByColor(int groupSize = 5)
    {
        int normalizedGroupSize = Mathf.Max(1, groupSize);
        if (objectPools == null || objectPools.Count == 0)
        {
            return;
        }

        for (int i = 0; i < objectPools.Count; i++)
        {
            PoolObjectInfo pool = objectPools[i];
            if (pool == null || pool.poolType != PoolType.Seed)
            {
                continue;
            }

            NormalizeSeedPoolByColor(pool, normalizedGroupSize);
        }
    }

    private static void NormalizeSeedPoolByColor(PoolObjectInfo pool, int groupSize)
    {
        if (pool == null || pool.poolObjects == null || pool.poolObjects.Count == 0)
        {
            return;
        }

        System.Array.Clear(seedColorCounts, 0, seedColorValueCount);
        System.Array.Clear(seedColorKeepCounts, 0, seedColorValueCount);

        for (int i = pool.poolObjects.Count - 1; i >= 0; i--)
        {
            GameObject pooledObject = pool.poolObjects[i];
            if (pooledObject == null)
            {
                pool.poolObjects.RemoveAt(i);
                continue;
            }

            if (pooledObject.activeInHierarchy)
            {
                pool.poolObjects.RemoveAt(i);
                continue;
            }

            SeedInfo seedInfo = pooledObject.GetComponent<SeedInfo>();
            if (seedInfo == null)
            {
                continue;
            }

            int colorIndex = (int)seedInfo.GetSeedColor();
            if (colorIndex < 0 || colorIndex >= seedColorValueCount)
            {
                continue;
            }

            seedColorCounts[colorIndex]++;
        }

        bool hasRemainder = false;
        for (int i = 0; i < seedColorValueCount; i++)
        {
            int count = seedColorCounts[i];
            int keepCount = count - (count % groupSize);
            seedColorKeepCounts[i] = keepCount;
            if (keepCount != count)
            {
                hasRemainder = true;
            }
        }

        if (!hasRemainder)
        {
            return;
        }

        for (int i = pool.poolObjects.Count - 1; i >= 0; i--)
        {
            GameObject pooledObject = pool.poolObjects[i];
            if (pooledObject == null)
            {
                pool.poolObjects.RemoveAt(i);
                continue;
            }

            if (pooledObject.activeInHierarchy)
            {
                pool.poolObjects.RemoveAt(i);
                continue;
            }

            SeedInfo seedInfo = pooledObject.GetComponent<SeedInfo>();
            if (seedInfo == null)
            {
                continue;
            }

            int colorIndex = (int)seedInfo.GetSeedColor();
            if (colorIndex < 0 || colorIndex >= seedColorValueCount)
            {
                continue;
            }

            if (seedColorKeepCounts[colorIndex] > 0)
            {
                seedColorKeepCounts[colorIndex]--;
                continue;
            }

            pool.poolObjects.RemoveAt(i);
            if (Application.isPlaying)
            {
                Destroy(pooledObject);
            }
            else
            {
                DestroyImmediate(pooledObject);
            }
        }
    }
}
public class PoolObjectInfo
{
    public string poolName;
    public ObjectPoolManager.PoolType poolType = ObjectPoolManager.PoolType.None;
    public List<GameObject> poolObjects = new List<GameObject>();
}


