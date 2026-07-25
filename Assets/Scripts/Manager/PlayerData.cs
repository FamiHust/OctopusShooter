using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using UnityEngine;

/// <summary>
/// Quáº£n lÃ½ dá»¯ liá»‡u player: level, boosters inventory
/// Singleton pattern - persist qua scenes
/// </summary>
public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    [Header("Booster System")]
    public HashSet<string> unlockedBoosters = new HashSet<string>();
    public Dictionary<string, int> boosterInventory = new Dictionary<string, int>();

    [Header("Feature Unlock System")]
    //public HashSet<FeatureID> unlockedFeatures = new HashSet<FeatureID>();


    [Header("Currency System")]
    [Tooltip("Sá»‘ coin hiá»‡n cÃ³")]
    public int coinBalance = 0;

    // Tutorial System: Sá»­ dá»¥ng PlayerPrefs Ä‘á»ƒ store individual tutorial names

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Events
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static event Action<string> OnBoosterUnlocked;
    public static event Action<string, int> OnBoosterCountChanged;
    //public static event Action<FeatureID> OnFeatureUnlocked;
    public static event Action<string> OnTutorialCompleted; // Thay Ä‘á»•i tá»« TutorialType thÃ nh string
    public static event Action<int> OnCoinChanged; // Event khi coin thay Ä‘á»•i

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Save/Load
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void Save()
    {
        // Save unlocked boosters (serialize to JSON)
        string unlockedJson = JsonUtility.ToJson(new StringListWrapper(new List<string>(unlockedBoosters)));
        PlayerPrefs.SetString("PlayerData_UnlockedBoosters", unlockedJson);

        // Save inventory (serialize to JSON)
        List<BoosterInventoryEntry> entries = new List<BoosterInventoryEntry>();
        foreach (var kvp in boosterInventory)
        {
            entries.Add(new BoosterInventoryEntry { boosterId = kvp.Key, count = kvp.Value });
        }
        string inventoryJson = JsonUtility.ToJson(new BoosterInventoryWrapper(entries));
        PlayerPrefs.SetString("PlayerData_BoosterInventory", inventoryJson);

        // âœ… Save unlocked features
        List<int> featureIDs = new List<int>();
        // foreach (var feature in unlockedFeatures)
        // {
        //     featureIDs.Add((int)feature);
        // }
        string featuresJson = JsonUtility.ToJson(new IntListWrapper(featureIDs));
        PlayerPrefs.SetString("PlayerData_UnlockedFeatures", featuresJson);



        // âœ… Save coin balance
        PlayerPrefs.SetInt("PlayerData_CoinBalance", coinBalance);
        PlayerPrefs.SetInt(Const.player_coins_key, coinBalance);

        // âœ… Completed tutorials giá» Ä‘Æ°á»£c save riÃªng láº» dÆ°á»›i cÃ¡c key "Tutorial_<name>"
        // KhÃ´ng cáº§n save chung
        PlayerPrefs.Save();

    }

    public void Load()
    {
        // Load unlocked boosters
        string unlockedJson = PlayerPrefs.GetString("PlayerData_UnlockedBoosters", "");
        if (!string.IsNullOrEmpty(unlockedJson))
        {
            try
            {
                StringListWrapper wrapper = JsonUtility.FromJson<StringListWrapper>(unlockedJson);
                unlockedBoosters = new HashSet<string>(wrapper.list);
            }
            catch
            {
                ;
                unlockedBoosters = new HashSet<string>();
            }
        }

        // Load inventory
        string inventoryJson = PlayerPrefs.GetString("PlayerData_BoosterInventory", "");
        if (!string.IsNullOrEmpty(inventoryJson))
        {
            try
            {
                BoosterInventoryWrapper wrapper = JsonUtility.FromJson<BoosterInventoryWrapper>(inventoryJson);
                boosterInventory = new Dictionary<string, int>();
                foreach (var entry in wrapper.entries)
                {
                    boosterInventory[entry.boosterId] = entry.count;
                }
            }
            catch
            {
                ;
                boosterInventory = new Dictionary<string, int>();
            }
        }

        // âœ… Load unlocked features
        string featuresJson = PlayerPrefs.GetString("PlayerData_UnlockedFeatures", "");
        if (!string.IsNullOrEmpty(featuresJson))
        {
            try
            {
                IntListWrapper wrapper = JsonUtility.FromJson<IntListWrapper>(featuresJson);
                // unlockedFeatures = new HashSet<FeatureID>();
                // foreach (var id in wrapper.list)
                // {
                //     unlockedFeatures.Add((FeatureID)id);
                // }
                //;
            }
            catch (Exception e)
            {
                ;
                //unlockedFeatures = new HashSet<FeatureID>();
            }
        }



        // âœ… Load coin balance (fallback sang key game cu de tranh mat du lieu)
        if (PlayerPrefs.HasKey("PlayerData_CoinBalance"))
        {
            coinBalance = PlayerPrefs.GetInt("PlayerData_CoinBalance", 0);
        }
        else
        {
            coinBalance = PlayerPrefs.GetInt(Const.player_coins_key, 0);
        }

        PlayerPrefs.SetInt(Const.player_coins_key, coinBalance);
        PlayerPrefs.SetInt("PlayerData_CoinBalance", coinBalance);
        ;

        PlayerPrefs.Save();
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Unlock Management
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public bool IsBoosterUnlocked(string boosterId)
    {
        return unlockedBoosters.Contains(boosterId);
    }

    public void UnlockBooster(string boosterId)
    {
        if (!unlockedBoosters.Contains(boosterId))
        {
            unlockedBoosters.Add(boosterId);
            OnBoosterUnlocked?.Invoke(boosterId);
            Save();

            ;
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Inventory Query
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public int GetBoosterCount(string boosterId)
    {
        return boosterInventory.ContainsKey(boosterId) ? boosterInventory[boosterId] : 0;
    }

    public bool HasBooster(string boosterId)
    {
        return GetBoosterCount(boosterId) > 0;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Inventory Modify
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public bool AddBooster(string boosterId, int amount)
    {
        if (amount <= 0) return false;

        if (!boosterInventory.ContainsKey(boosterId))
            boosterInventory[boosterId] = 0;

        boosterInventory[boosterId] += amount;
        OnBoosterCountChanged?.Invoke(boosterId, boosterInventory[boosterId]);

        ;
        return true;
    }

    public bool RemoveBooster(string boosterId, int amount)
    {
        if (amount <= 0) return false;

        if (!boosterInventory.ContainsKey(boosterId))
            return false;

        if (boosterInventory[boosterId] < amount)
            return false;

        boosterInventory[boosterId] -= amount;
        OnBoosterCountChanged?.Invoke(boosterId, boosterInventory[boosterId]);

        ;
        return true;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Feature Unlock Management
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // public bool IsFeatureUnlocked(FeatureID featureID)
    // {
    //     return unlockedFeatures.Contains(featureID);
    // }

    // public void UnlockFeature(FeatureID featureID)
    // {
    //     if (featureID == FeatureID.None) return;

    //     if (!unlockedFeatures.Contains(featureID))
    //     {
    //         unlockedFeatures.Add(featureID);
    //         OnFeatureUnlocked?.Invoke(featureID);
    //         Save();

    //         ;
    //     }
    // }

    // public List<FeatureID> GetUnlockedFeatures()
    // {
    //     return new List<FeatureID>(unlockedFeatures);
    // }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Win Streak Management
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€




    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Coin Management
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Get current coin balance
    /// </summary>
    public int GetCoinBalance()
    {
        return coinBalance;
    }

    /// <summary>
    /// Add coins to balance (from stars + spin reward)
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        coinBalance += amount;
        OnCoinChanged?.Invoke(coinBalance);
        Save();

        ;
    }

    /// <summary>
    /// Spend coins (for buying boosters)
    /// </summary>
    /// <returns>True if successful, false if insufficient balance</returns>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return false;

        if (coinBalance < amount)
        {
            ;
            return false;
        }

        coinBalance -= amount;
        OnCoinChanged?.Invoke(coinBalance);
        Save();

        ;
        return true;
    }

    /// <summary>
    /// Check if player can afford a purchase
    /// </summary>
    public bool CanAfford(int amount)
    {
        return coinBalance >= amount;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Tutorial Completion Management (PlayerPrefs-based)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Tiá»n tá»‘ cho táº¥t cáº£ tutorial keys

    // public bool IsTutorialCompleted(string tutorialName, TutorialType type)
    // {
    //     if (string.IsNullOrEmpty(tutorialName)) return false;

    //     string key = Const.TUTORIAL_PREFIX + $"{type.ToString()}" + tutorialName;
    //     return PlayerPrefs.GetInt(key, 0) == 1; // 1 = completed, 0 = not completed
    // }

    // public void CompleteTutorial(string tutorialName, TutorialType type)
    // {
    //     if (string.IsNullOrEmpty(tutorialName))
    //     {
    //         ;
    //         return;
    //     }

    //     string key = Const.TUTORIAL_PREFIX + $"{type.ToString()}" + tutorialName;

    //     if (!IsTutorialCompleted(tutorialName, type))
    //     {
    //         PlayerPrefs.SetInt(key, 1);
    //         PlayerPrefs.Save();
    //         OnTutorialCompleted?.Invoke(tutorialName);

    //         ;
    //     }
    // }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // [DEBUG] Sample Data
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Khá»Ÿi táº¡o dá»¯ liá»‡u máº«u Ä‘á»ƒ test.
    /// Cho 9999 coin + unlock/cáº¥p 5 lÆ°á»£t má»—i booster trong danh sÃ¡ch truyá»n vÃ o.
    /// Gá»i tá»« Inspector (ContextMenu).
    /// </summary>
    [ContextMenu("Init Sample Data")]
    public void InitSampleData()
    {
        coinBalance = 9999;
        OnCoinChanged?.Invoke(coinBalance);

        UnlockBooster(Const.BOOSTER_ADDSLOT);
        UnlockBooster(Const.BOOSTER_UNLOCKSHOOTER);
        UnlockBooster(Const.BOOSTER_HERO);
        AddBooster(Const.BOOSTER_ADDSLOT, 5);
        AddBooster(Const.BOOSTER_HERO, 5);
        AddBooster(Const.BOOSTER_UNLOCKSHOOTER, 5);
    }
}

// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// Helper classes for JSON serialization
// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[Serializable]
public class StringListWrapper
{
    public List<string> list;

    public StringListWrapper(List<string> list)
    {
        this.list = list;
    }
}

[Serializable]
public class IntListWrapper
{
    public List<int> list;

    public IntListWrapper(List<int> list)
    {
        this.list = list;
    }
}

[Serializable]
public class BoosterInventoryWrapper
{
    public List<BoosterInventoryEntry> entries;

    public BoosterInventoryWrapper(List<BoosterInventoryEntry> entries)
    {
        this.entries = entries;
    }
}

[Serializable]
public class BoosterInventoryEntry
{
    public string boosterId;
    public int count;
}

