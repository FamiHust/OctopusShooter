using UnityEditor;
using UnityEngine;

public class PlayerPrefsCurrencyToolWindow : EditorWindow
{
    private const string BoosterCountKeyPrefix = "BoosterCount_";

    private int coinValue;
    private int heartValue;
    private int magicStoneValue;
    private int levelValue;

    private int coinDelta = 100;
    private int heartDelta = 1;
    private int magicStoneDelta = 1;
    private int levelDelta = 1;

    private string boosterId = Const.BOOSTER_HERO;
    private int boosterDelta = 1;

    [MenuItem("Tools/PlayerPrefs/Currency Tool")]
    public static void ShowWindow()
    {
        PlayerPrefsCurrencyToolWindow window = GetWindow<PlayerPrefsCurrencyToolWindow>("Currency Tool");
        window.minSize = new Vector2(380f, 320f);
        window.RefreshFromPrefs();
    }

    private void OnEnable()
    {
        RefreshFromPrefs();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PlayerPrefs Currency + Level Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        DrawCurrentKeysInfo();
        EditorGUILayout.Space(10f);

        EditorGUILayout.LabelField("Direct Set", EditorStyles.boldLabel);
        coinValue = EditorGUILayout.IntField("Coin", coinValue);
        heartValue = EditorGUILayout.IntField("Heart", heartValue);
        magicStoneValue = EditorGUILayout.IntField("MagicStone", magicStoneValue);
        levelValue = EditorGUILayout.IntField("Level", levelValue);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Values", GUILayout.Height(28f)))
        {
            SaveValues();
        }

        if (GUILayout.Button("Refresh", GUILayout.Height(28f)))
        {
            RefreshFromPrefs();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Quick Adjust", EditorStyles.boldLabel);

        coinDelta = EditorGUILayout.IntField("Coin Delta", coinDelta);
        heartDelta = EditorGUILayout.IntField("Heart Delta", heartDelta);
        magicStoneDelta = EditorGUILayout.IntField("MagicStone Delta", magicStoneDelta);
        levelDelta = EditorGUILayout.IntField("Level Delta", levelDelta);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Coin"))
        {
            coinValue += Mathf.Max(0, coinDelta);
            SaveValues();
        }
        if (GUILayout.Button("- Coin"))
        {
            coinValue = Mathf.Max(0, coinValue - Mathf.Max(0, coinDelta));
            SaveValues();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Heart"))
        {
            heartValue += Mathf.Max(0, heartDelta);
            SaveValues();
        }
        if (GUILayout.Button("- Heart"))
        {
            heartValue = Mathf.Max(0, heartValue - Mathf.Max(0, heartDelta));
            SaveValues();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ MagicStone"))
        {
            magicStoneValue += Mathf.Max(0, magicStoneDelta);
            SaveValues();
        }
        if (GUILayout.Button("- MagicStone"))
        {
            magicStoneValue = Mathf.Max(0, magicStoneValue - Mathf.Max(0, magicStoneDelta));
            SaveValues();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Level"))
        {
            levelValue += Mathf.Max(1, levelDelta);
            SaveValues();
        }
        if (GUILayout.Button("- Level"))
        {
            levelValue = Mathf.Max(1, levelValue - Mathf.Max(1, levelDelta));
            SaveValues();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        DrawBoosterSection();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Reset Coin/Heart/MagicStone/Level", GUILayout.Height(24f)))
        {
            coinValue = 0;
            heartValue = Const.player_default_hearts;
            magicStoneValue = 0;
            levelValue = 1;
            SaveValues();
        }
    }

    private void DrawCurrentKeysInfo()
    {
        EditorGUILayout.LabelField("Keys In Use", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Coin key: " + Const.player_coins_key + "\n" +
            "Heart key: " + Const.player_hearts_key + "\n" +
            "MagicStone key: " + Const.player_magicstone_key + "\n" +
            "Level key: " + Const.player_level_key + "\n" +
            "Booster count key: " + BoosterCountKeyPrefix + "<BoosterId>\n" +
            "Also sync coin to PlayerData_CoinBalance.",
            MessageType.Info
        );
    }

    private void DrawBoosterSection()
    {
        EditorGUILayout.LabelField("Booster Adjust", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Booster Id");
        if (GUILayout.Button(Const.BOOSTER_HERO)) boosterId = Const.BOOSTER_HERO;
        if (GUILayout.Button(Const.BOOSTER_ADDSLOT)) boosterId = Const.BOOSTER_ADDSLOT;
        if (GUILayout.Button(Const.BOOSTER_UNLOCKSHOOTER)) boosterId = Const.BOOSTER_UNLOCKSHOOTER;
        EditorGUILayout.EndHorizontal();

        boosterId = EditorGUILayout.TextField("Custom Booster Id", boosterId);
        boosterDelta = EditorGUILayout.IntField("Booster Delta", boosterDelta);

        int currentBoosterCount = GetBoosterCount(boosterId);
        EditorGUILayout.LabelField("Current Booster Count", currentBoosterCount.ToString());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Booster"))
        {
            AdjustBoosterCount(boosterId, Mathf.Max(0, boosterDelta));
        }

        if (GUILayout.Button("- Booster"))
        {
            AdjustBoosterCount(boosterId, -Mathf.Max(0, boosterDelta));
        }
        EditorGUILayout.EndHorizontal();
    }

    private void RefreshFromPrefs()
    {
        coinValue = PlayerPrefs.GetInt(Const.player_coins_key, 0);
        heartValue = PlayerPrefs.GetInt(Const.player_hearts_key, 0);
        magicStoneValue = PlayerPrefs.GetInt(Const.player_magicstone_key, 0);
        levelValue = PlayerPrefs.GetInt(Const.player_level_key, 1);
        Repaint();
    }

    private int GetBoosterCount(string targetBoosterId)
    {
        if (string.IsNullOrWhiteSpace(targetBoosterId))
        {
            return 0;
        }

        string countKey = BoosterCountKeyPrefix + targetBoosterId.Trim();
        return Mathf.Max(0, PlayerPrefs.GetInt(countKey, 0));
    }

    private void AdjustBoosterCount(string targetBoosterId, int delta)
    {
        if (string.IsNullOrWhiteSpace(targetBoosterId) || delta == 0)
        {
            return;
        }

        string safeBoosterId = targetBoosterId.Trim();
        int currentCount = GetBoosterCount(safeBoosterId);
        int nextCount = Mathf.Max(0, currentCount + delta);

        string countKey = BoosterCountKeyPrefix + safeBoosterId;
        PlayerPrefs.SetInt(countKey, nextCount);
        PlayerPrefs.Save();

        if (Application.isPlaying && BoosterManager.Instance != null)
        {
            int runtimeCount = BoosterManager.Instance.GetBoosterCount(safeBoosterId);
            if (runtimeCount < nextCount)
            {
                BoosterManager.Instance.AddBooster(safeBoosterId, nextCount - runtimeCount);
            }
            else if (runtimeCount > nextCount)
            {
                BoosterManager.Instance.TryConsumeBooster(safeBoosterId, runtimeCount - nextCount);
            }
        }

        Repaint();
    }

    private void SaveValues()
    {
        coinValue = Mathf.Max(0, coinValue);
        heartValue = Mathf.Max(0, heartValue);
        magicStoneValue = Mathf.Max(0, magicStoneValue);
        levelValue = Mathf.Max(1, levelValue);

        PlayerPrefs.SetInt(Const.player_coins_key, coinValue);
        PlayerPrefs.SetInt(Const.player_hearts_key, heartValue);
        PlayerPrefs.SetInt(Const.player_magicstone_key, magicStoneValue);
        PlayerPrefs.SetInt(Const.player_level_key, levelValue);

        // Keep runtime player data coin in sync if project uses this key.
        PlayerPrefs.SetInt("PlayerData_CoinBalance", coinValue);

        PlayerPrefs.Save();

        if (Application.isPlaying && PlayerData.Instance != null)
        {
            PlayerData.Instance.coinBalance = coinValue;
            PlayerData.Instance.Save();
        }

    }
}
