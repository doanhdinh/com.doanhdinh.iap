using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Quản lý UI tổng của shop: hiển thị số coin hiện có.
    /// Giá từng pack được xử lý bởi ShopPackUI trên mỗi pack button.
    /// </summary>
    public class IAPUIManager : MonoBehaviour
    {
        [Header("References")]
        public IAPManager iapManager;

        [Header("Coin Balance Display")]
        public TextMeshProUGUI tmpBalance;
        public Text txtBalance;

        private void Start()
        {
            if (iapManager == null)
                iapManager = IAPManager.Instance;

            CurrencyManager.OnCoinsChanged += RefreshBalance;
            int coins = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCoins() : 0;
            RefreshBalance(coins);
        }

        private void OnDestroy()
        {
            CurrencyManager.OnCoinsChanged -= RefreshBalance;
        }

        private void RefreshBalance(int coins)
        {
            string display = coins.ToString("N0");
            if (tmpBalance != null) tmpBalance.text = display;
            if (txtBalance  != null) txtBalance.text  = display;
        }
    }
}
