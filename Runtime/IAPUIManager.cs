using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Hiển thị UI shop coin. Tự động lấy giá từ store sau khi IAP init xong.
    /// Gắn script này lên UI panel, kéo references vào Inspector.
    /// </summary>
    public class IAPUIManager : MonoBehaviour
    {
        [Header("References")]
        public IAPManager iapManager;

        [Header("Coin Prices (TextMeshPro)")]
        public TextMeshProUGUI tmpCoin150;
        public TextMeshProUGUI tmpCoin500;
        public TextMeshProUGUI tmpCoin1000;
        public TextMeshProUGUI tmpCoin2000;
        public TextMeshProUGUI tmpCoin4000;
        public TextMeshProUGUI tmpCoin8000;

        [Header("Coin Prices (Legacy Text)")]
        public Text txtCoin150;
        public Text txtCoin500;
        public Text txtCoin1000;
        public Text txtCoin2000;
        public Text txtCoin4000;
        public Text txtCoin8000;

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

            StartCoroutine(LoadPrices());
        }

        private void OnDestroy()
        {
            CurrencyManager.OnCoinsChanged -= RefreshBalance;
        }

        private IEnumerator LoadPrices()
        {
            yield return new WaitUntil(() => iapManager != null && iapManager.IsInitialized);

            iapManager.GetPrice(IAPItemType.Coin_150,  p => SetPrice(tmpCoin150,  txtCoin150,  p));
            iapManager.GetPrice(IAPItemType.Coin_500,  p => SetPrice(tmpCoin500,  txtCoin500,  p));
            iapManager.GetPrice(IAPItemType.Coin_1000, p => SetPrice(tmpCoin1000, txtCoin1000, p));
            iapManager.GetPrice(IAPItemType.Coin_2000, p => SetPrice(tmpCoin2000, txtCoin2000, p));
            iapManager.GetPrice(IAPItemType.Coin_4000, p => SetPrice(tmpCoin4000, txtCoin4000, p));
            iapManager.GetPrice(IAPItemType.Coin_8000, p => SetPrice(tmpCoin8000, txtCoin8000, p));
        }

        /// <summary>
        /// Gọi từ nút Buy trên UI.
        /// Truyền int tương ứng: 1=Coin150, 2=Coin500, 3=Coin1000, 4=Coin2000, 5=Coin4000, 6=Coin8000
        /// </summary>
        public void BuyCoin(int itemTypeIndex)
        {
            var itemType = (IAPItemType)itemTypeIndex;
            if (iapManager == null) return;
            iapManager.Purchase(itemType, success =>
            {
                if (success)
                    Debug.Log($"[IAPUIManager] Mua {itemType} thành công!");
            });
        }

        private void RefreshBalance(int coins)
        {
            string display = coins.ToString("N0");
            if (tmpBalance != null) tmpBalance.text = display;
            if (txtBalance  != null) txtBalance.text  = display;
        }

        private void SetPrice(TextMeshProUGUI tmp, Text txt, string price)
        {
            if (tmp != null) tmp.text = price;
            if (txt != null) txt.text = price;
        }
    }
}
