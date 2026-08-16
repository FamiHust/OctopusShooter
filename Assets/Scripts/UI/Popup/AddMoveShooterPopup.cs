using UnityEngine;
using UnityEngine.UI;

public class AddMoveShooterPopup : BasePopUp
{
    [SerializeField] private Button getButton;
    [SerializeField] private Button coinButton;
    [Tooltip("Gia coin tru khi mua booster tu popup nay. Dat -1 de dung gia tu Booster config.")]
    [SerializeField] private int coinCostOverride = -1;

    protected override void Awake()
    {
        base.Awake();
        SetupGetButton();
    }

    public Button GetButton()
    {
        if (getButton != null)
        {
            return getButton;
        }

        getButton = FindGetButton();
        return getButton;
    }

    public Button GetGetButton() => GetButton();

    public Button GetCoinButton()
    {
        if (coinButton != null)
        {
            return coinButton;
        }

        coinButton = FindCoinButton();
        return coinButton;
    }

    public int GetCoinCost(int fallbackCost)
    {
        if (coinCostOverride < 0)
        {
            return Mathf.Max(0, fallbackCost);
        }

        return Mathf.Max(0, coinCostOverride);
    }

    private void SetupGetButton()
    {
        Button btn = GetButton();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnGetButtonClicked);
        }
    }

    private void OnGetButtonClicked()
    {
        AddBoosterReward(1);
        AudioManager.Instance?.PlaySFX(Const.buyBoosterSFX);
        Hide();
    }

    public void AddBoosterReward(int amount = 1)
    {
        int safeAmount = Mathf.Max(1, amount);
        if (BoosterManager.Instance != null)
        {
            BoosterManager.Instance.AddBooster(Const.BOOSTER_UNLOCKSHOOTER, safeAmount);
        }
        else if (PlayerData.Instance != null)
        {
            PlayerData.Instance.AddBooster(Const.BOOSTER_UNLOCKSHOOTER, safeAmount);
        }
    }

    private Button FindGetButton()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null)
            {
                continue;
            }

            string buttonName = candidate.gameObject.name.ToLowerInvariant();
            if (buttonName.Contains("close") || buttonName.Contains("cancel") || buttonName == "x")
            {
                continue;
            }

            if (buttonName.Contains("coin") || buttonName.Contains("buy") || buttonName.Contains("usecoin"))
            {
                continue;
            }

            if (buttonName.Contains("get") || buttonName.Contains("ad") || buttonName.Contains("free") || buttonName.Contains("reward"))
            {
                return candidate;
            }
        }

        return null;
    }

    private Button FindCoinButton()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        Button fallback = null;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null)
            {
                continue;
            }

            string buttonName = candidate.gameObject.name.ToLowerInvariant();
            if (buttonName.Contains("close") || buttonName.Contains("cancel") || buttonName == "x")
            {
                continue;
            }

            if (buttonName.Contains("get") || buttonName.Contains("ad") || buttonName.Contains("free") || buttonName.Contains("reward"))
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (buttonName.Contains("coin") || buttonName.Contains("buy") || buttonName.Contains("usecoin"))
            {
                return candidate;
            }
        }

        return fallback;
    }
}
