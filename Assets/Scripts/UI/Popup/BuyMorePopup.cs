using UnityEngine;
using UnityEngine.UI;

public class BuyMorePopup : BasePopUp
{
	private enum BuyPopupMode
	{
		AutoByPopupName,
		Gold,
		Lives
	}

	[Header("Buttons")]
	[SerializeField] private Button getBtn;
	[SerializeField] private Button heartBtn;

	[Header("Popup Mode")]
	[SerializeField] private BuyPopupMode popupMode = BuyPopupMode.AutoByPopupName;

	[Header("Configurable Rewards")]
	[SerializeField] private int getCoinRewardAmount = 200;
	[SerializeField] private int getHeartRewardAmount = 1;

	[Header("Heart Package")]
	[SerializeField] private int heartButtonGrantAmount = 5;
	[SerializeField] private int heartButtonCoinCost = 900;

	private void Start()
	{
		if (getBtn != null)
		{
			getBtn.onClick.AddListener(OnGetButtonClicked);
		}

		if (heartBtn != null)
		{
			heartBtn.onClick.AddListener(OnHeartButtonClicked);
		}
	}

	private void OnDestroy()
	{
		if (getBtn != null)
		{
			getBtn.onClick.RemoveListener(OnGetButtonClicked);
		}

		if (heartBtn != null)
		{
			heartBtn.onClick.RemoveListener(OnHeartButtonClicked);
		}
	}

	private BuyPopupMode ResolvePopupMode()
	{
		if (popupMode != BuyPopupMode.AutoByPopupName)
		{
			return popupMode;
		}

		if (string.Equals(gameObject.name, Const.buyMoreGoldPopUp, System.StringComparison.OrdinalIgnoreCase))
		{
			return BuyPopupMode.Gold;
		}

		if (string.Equals(gameObject.name, Const.buyMoreLivesPopUp, System.StringComparison.OrdinalIgnoreCase))
		{
			return BuyPopupMode.Lives;
		}

		return BuyPopupMode.Lives;
	}

	private void OnGetButtonClicked()
	{
		AudioManager.Instance?.PlaySFX(Const.popUISFX);

		BuyPopupMode resolvedMode = ResolvePopupMode();
		if (resolvedMode == BuyPopupMode.Gold)
		{
			AddCoins(getCoinRewardAmount);
		}
		else
		{
			AddHearts(getHeartRewardAmount);
		}

		RefreshMenuUI();
	}

	private void OnHeartButtonClicked()
	{
		AudioManager.Instance?.PlaySFX(Const.popUISFX);

		int safeCost = Mathf.Max(0, heartButtonCoinCost);
		if (!TrySpendCoins(safeCost))
		{
			return;
		}

		AddHearts(heartButtonGrantAmount);
		RefreshMenuUI();
	}

	private void AddCoins(int amount)
	{
		int safeAmount = Mathf.Max(0, amount);
		if (safeAmount <= 0)
		{
			return;
		}

		int currentCoins = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_coins_key, 0));
		int nextCoins = currentCoins + safeAmount;
		PlayerPrefs.SetInt(Const.player_coins_key, nextCoins);
		SyncCoinBalanceToPlayerData(nextCoins);
		PlayerPrefs.Save();
	}

	private bool TrySpendCoins(int amount)
	{
		int safeAmount = Mathf.Max(0, amount);
		if (safeAmount <= 0)
		{
			return true;
		}

		int currentCoins = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_coins_key, 0));
		if (currentCoins < safeAmount)
		{
			return false;
		}

		int nextCoins = currentCoins - safeAmount;
		PlayerPrefs.SetInt(Const.player_coins_key, nextCoins);
		SyncCoinBalanceToPlayerData(nextCoins);
		PlayerPrefs.Save();
		return true;
	}

	private void AddHearts(int amount)
	{
		int safeAmount = Mathf.Max(0, amount);
		if (safeAmount <= 0)
		{
			return;
		}

		int maxHearts = Mathf.Max(1, Const.player_default_hearts);
		int currentHearts = Mathf.Max(0, PlayerPrefs.GetInt(Const.player_hearts_key, maxHearts));
		int nextHearts = Mathf.Clamp(currentHearts + safeAmount, 0, maxHearts);
		PlayerPrefs.SetInt(Const.player_hearts_key, nextHearts);

		int currentUnix = (int)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		if (nextHearts >= maxHearts)
		{
			PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, 0);
		}
		else if (PlayerPrefs.GetInt(Const.player_next_heart_regen_unix_key, 0) <= 0)
		{
			PlayerPrefs.SetInt(Const.player_next_heart_regen_unix_key, currentUnix + 60);
		}

		PlayerPrefs.Save();
	}

	private void SyncCoinBalanceToPlayerData(int targetCoin)
	{
		if (PlayerData.Instance == null)
		{
			return;
		}

		int safeCoin = Mathf.Max(0, targetCoin);
		int currentDataCoin = PlayerData.Instance.GetCoinBalance();

		if (currentDataCoin < safeCoin)
		{
			PlayerData.Instance.AddCoins(safeCoin - currentDataCoin);
		}
		else if (currentDataCoin > safeCoin)
		{
			PlayerData.Instance.SpendCoins(currentDataCoin - safeCoin);
		}
	}

	private void RefreshMenuUI()
	{
		MenuUIManager menuUI = MenuUIManager.Instance;
		menuUI?.UpdateCoinsAndHearts();
	}
}
