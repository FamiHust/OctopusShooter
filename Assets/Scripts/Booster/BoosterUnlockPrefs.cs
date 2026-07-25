using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manage booster lock/unlock state in PlayerPrefs.
/// This is independent from PlayerData unlock storage.
/// </summary>
public static class BoosterUnlockPrefs
{
    private const string BoosterUnlockKeyPrefix = "BoosterUnlocked_";

    private static string GetUnlockKey(string boosterId)
        => string.Concat(BoosterUnlockKeyPrefix, boosterId);

    public static bool IsBoosterUnlocked(string boosterId)
    {
        if (string.IsNullOrEmpty(boosterId)) return false;
        return PlayerPrefs.GetInt(GetUnlockKey(boosterId), 0) == 1;
    }

    public static bool UnlockBooster(string boosterId, bool save = true)
    {
        if (string.IsNullOrEmpty(boosterId)) return false;
        string key = GetUnlockKey(boosterId);
        if (PlayerPrefs.GetInt(key, 0) == 1) return false;

        PlayerPrefs.SetInt(key, 1);
        if (save) PlayerPrefs.Save();

        BoosterManager.Instance?.SyncUnlockedBoosterInitialCount();
        return true;
    }

    public static bool LockBooster(string boosterId, bool save = true)
    {
        if (string.IsNullOrEmpty(boosterId)) return false;
        string key = GetUnlockKey(boosterId);
        if (PlayerPrefs.GetInt(key, 0) == 0) return false;

        PlayerPrefs.SetInt(key, 0);
        if (save) PlayerPrefs.Save();
        return true;
    }

    public static void EvaluateUnlockByLevel(IEnumerable<BoosterStrategyConfig> boosterConfigs, int currentLevel, bool save = true)
    {
        if (boosterConfigs == null) return;

        bool hasChanges = false;
        foreach (BoosterStrategyConfig cfg in boosterConfigs)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.boosterName)) continue;

            int requiredLevel = Mathf.Max(1, cfg.unlockAtLevel);
            if (currentLevel < requiredLevel) continue;

            string key = GetUnlockKey(cfg.boosterName);
            if (PlayerPrefs.GetInt(key, 0) == 1) continue;

            PlayerPrefs.SetInt(key, 1);
            hasChanges = true;
        }

        if (save && hasChanges)
        {
            PlayerPrefs.Save();

            BoosterManager.Instance?.SyncUnlockedBoosterInitialCount();
        }
    }
}
