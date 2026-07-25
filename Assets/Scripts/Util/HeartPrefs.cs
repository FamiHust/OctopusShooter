using UnityEngine;
using System;

public static class HeartPrefs
{
    private const int HeartRegenSeconds = 60;

    public static int GetCurrentHearts()
    {
        return ProcessRealtimeHeartRegen();
    }

    public static int DecreaseHeart(int amount = 1)
    {
        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int current = ProcessRealtimeHeartRegen();
        int safeAmount = Mathf.Max(1, amount);
        int next = Mathf.Clamp(current - safeAmount, 0, maxHearts);
        bool changed = false;

        if (next != current)
        {
            PlayerPrefs.SetInt(Const.player_hearts_key, next);
            changed = true;
        }

        if (next >= maxHearts)
        {
            if (PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0) != 0)
            {
                PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, 0);
                changed = true;
            }
        }
        else
        {
            int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
            if (nextRegenUnix <= 0)
            {
                PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, GetCurrentUnixTime() + HeartRegenSeconds);
                changed = true;
            }
        }

        if (changed)
        {
            PlayerPrefs.Save();
        }

        return next;
    }

    private static int ProcessRealtimeHeartRegen()
    {
        int maxHearts = Mathf.Max(1, Const.player_default_hearts);
        int rawHearts = PlayerPrefs.GetInt(Const.player_hearts_key, maxHearts);
        int hearts = Mathf.Clamp(rawHearts, 0, maxHearts);
        int now = GetCurrentUnixTime();
        bool changed = false;

        if (hearts != rawHearts)
        {
            PlayerPrefs.SetInt(Const.player_hearts_key, hearts);
            changed = true;
        }

        if (hearts >= maxHearts)
        {
            if (PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0) != 0)
            {
                PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, 0);
                changed = true;
            }

            if (changed)
            {
                PlayerPrefs.Save();
            }

            return hearts;
        }

        int nextRegenUnix = PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0);
        if (nextRegenUnix <= 0)
        {
            nextRegenUnix = now + HeartRegenSeconds;
            PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, nextRegenUnix);
            changed = true;
        }

        while (hearts < maxHearts && now >= nextRegenUnix)
        {
            hearts++;
            nextRegenUnix += HeartRegenSeconds;
            changed = true;
        }

        if (changed)
        {
            PlayerPrefs.SetInt(Const.player_hearts_key, hearts);
            PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, hearts >= maxHearts ? 0 : nextRegenUnix);
            PlayerPrefs.Save();
        }

        return hearts;
    }

    private static int GetCurrentUnixTime()
    {
        return (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
