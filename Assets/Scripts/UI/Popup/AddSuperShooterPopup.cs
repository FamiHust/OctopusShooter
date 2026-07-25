using UnityEngine;
using UnityEngine.UI;

public class AddSuperShooterPopup : BasePopUp
{
    [SerializeField] private Button coinButton;
    [Tooltip("Gia coin tru khi mua booster tu popup nay. Dat -1 de dung gia tu Booster config.")]
    [SerializeField] private int coinCostOverride = -1;

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
