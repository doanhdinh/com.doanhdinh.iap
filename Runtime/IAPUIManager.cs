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

        [Header("Coin Prices — Pack $0.12")]
        public TextMeshProUGUI tmpPack012;
        public Text txtPack012;

        [Header("Coin Prices — Pack $0.20")]
        public TextMeshProUGUI tmpPack020;
        public Text txtPack020;

        [Header("Coin Prices — Pack $0.50")]
        public TextMeshProUGUI tmpPack050;
        public Text txtPack050;

        [Header("Coin Prices — Pack $1.00")]
        public TextMeshProUGUI tmpPack100;
        public Text txtPack100;

        [Header("Coin Prices — Pack $1.50")]
        public TextMeshProUGUI tmpPack150;
        public Text txtPack150;

        [Header("Coin Prices — Pack $2.00")]
        public TextMeshProUGUI tmpPack200;
        public Text txtPack200;

        [Header("Coin Prices — Pack $5.00")]
        public TextMeshProUGUI tmpPack500;
        public Text txtPack500;

        [Header("Coin Prices — Pack $7.00")]
        public TextMeshProUGUI tmpPack700;
        public Text txtPack700;

        [Header("Coin Prices — Pack $9.00")]
        public TextMeshProUGUI tmpPack900;
        public Text txtPack900;

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

            iapManager.GetPrice(IAPItemType.Pack_012, p => SetPrice(tmpPack012, txtPack012, p));
            iapManager.GetPrice(IAPItemType.Pack_020, p => SetPrice(tmpPack020, txtPack020, p));
            iapManager.GetPrice(IAPItemType.Pack_050, p => SetPrice(tmpPack050, txtPack050, p));
            iapManager.GetPrice(IAPItemType.Pack_100, p => SetPrice(tmpPack100, txtPack100, p));
            iapManager.GetPrice(IAPItemType.Pack_150, p => SetPrice(tmpPack150, txtPack150, p));
            iapManager.GetPrice(IAPItemType.Pack_200, p => SetPrice(tmpPack200, txtPack200, p));
            iapManager.GetPrice(IAPItemType.Pack_500, p => SetPrice(tmpPack500, txtPack500, p));
            iapManager.GetPrice(IAPItemType.Pack_700, p => SetPrice(tmpPack700, txtPack700, p));
            iapManager.GetPrice(IAPItemType.Pack_900, p => SetPrice(tmpPack900, txtPack900, p));
        }

        /// <summary>
        /// Gọi từ nút Buy trên UI.
        /// itemTypeIndex tương ứng với IAPItemType: 1=Pack_012 ... 9=Pack_900
        /// </summary>
        public void BuyCoin(int itemTypeIndex)
        {
            if (iapManager == null) return;
            var itemType = (IAPItemType)itemTypeIndex;
            iapManager.Purchase(itemType, success =>
            {
                if (success)
                    Debug.Log($"[IAPUIManager] Mua {itemType} thành công! Template {iapManager.GetActiveTemplate()}");
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
