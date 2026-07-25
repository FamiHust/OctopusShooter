using Solo.MOST_IN_ONE;
using UnityEngine;

public static class HapticManager
{
    private const float DefaultShootHapticCooldown = 0.12f;
    private const float MinShootHapticCooldown = 0.08f;
    private const float LoseHapticCooldown = 0.2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SyncHapticToggleOnLoad()
    {
        bool enabled = ReadEnabledFromPlayerPrefs();
        ApplyEnabledState(enabled, savePlayerPrefs: false);
    }

    public static bool IsEnabled()
    {
        return ReadEnabledFromPlayerPrefs();
    }

    public static void SetEnabled(bool enabled)
    {
        ApplyEnabledState(enabled, savePlayerPrefs: true);
    }

    public static void PlayShootHaptic(float requestedCooldownSeconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        float cooldown = requestedCooldownSeconds > 0f ? requestedCooldownSeconds : DefaultShootHapticCooldown;
        cooldown = Mathf.Max(MinShootHapticCooldown, cooldown);
        MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.LightImpact, cooldown);
    }

    public static void PlayLoseHaptic()
    {
        if (!IsEnabled())
        {
            return;
        }

        MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Failure, LoseHapticCooldown);
    }

    private static bool ReadEnabledFromPlayerPrefs()
    {
        int defaultValue = MOST_HapticFeedback.HapticsEnabled ? 1 : 0;
        return PlayerPrefs.GetInt(Const.player_vibration_key, defaultValue) == 1;
    }

    private static void ApplyEnabledState(bool enabled, bool savePlayerPrefs)
    {
        PlayerPrefs.SetInt(Const.player_vibration_key, enabled ? 1 : 0);
        MOST_HapticFeedback.HapticsEnabled = enabled;

        if (savePlayerPrefs)
        {
            PlayerPrefs.Save();
        }
    }
}

public static class FlowBlastHaptics
{
    public static bool IsEnabled()
    {
        return HapticManager.IsEnabled();
    }

    public static void SetEnabled(bool enabled)
    {
        HapticManager.SetEnabled(enabled);
    }

    public static void PlayShootHaptic(float requestedCooldownSeconds)
    {
        HapticManager.PlayShootHaptic(requestedCooldownSeconds);
    }

    public static void PlayLoseHaptic()
    {
        HapticManager.PlayLoseHaptic();
    }
}