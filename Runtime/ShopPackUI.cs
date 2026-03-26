using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Gắn lên mỗi ShopPack button prefab.
    /// Tự tìm text theo cấu trúc: Text ngoài = coin amount, Text trong Button = giá tiền.
    /// Chỉ cần set đúng itemType trong Inspector.
    /// </summary>
    public class ShopPackUI : MonoBehaviour
    {
        [Header("Config")]
        public IAPItemType itemType;

        [Header("Price Display (tự tìm nếu để trống)")]
        public Text priceText;
        public TextMeshProUGUI priceTMP;

        [Header("Coin Amount Display (tự tìm nếu để trống)")]
        public Text coinText;
        public TextMeshProUGUI coinTMP;

        private void Start()
        {
            AutoFindReferences();
            StartCoroutine(LoadData());
        }

        private void AutoFindReferences()
        {
            // Price text nằm bên trong Button child
            var btn = GetComponentInChildren<Button>();
            if (btn != null)
            {
                if (priceText == null) priceText = btn.GetComponentInChildren<Text>();
                if (priceTMP  == null) priceTMP  = btn.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Coin text là các Text còn lại (không phải trong Button)
            foreach (var t in GetComponentsInChildren<Text>())
            {
                if (t == priceText) continue;
                if (coinText == null) coinText = t;
            }
            foreach (var t in GetComponentsInChildren<TextMeshProUGUI>())
            {
                if (t == priceTMP) continue;
                if (coinTMP == null) coinTMP = t;
            }
        }

        private IEnumerator LoadData()
        {
            yield return new WaitUntil(() => IAPManager.Instance != null && IAPManager.Instance.IsInitialized);

            // Coin amount lấy ngay từ config (không cần chờ store)
            int coinAmount = IAPManager.Instance.GetCoinAmount(itemType);
            SetCoinAmount(coinAmount);

            // Giá lấy từ store (localizedPriceString)
            IAPManager.Instance.GetPrice(itemType, SetPrice);
        }

        private void SetPrice(string price)
        {
            if (priceTMP  != null) priceTMP.text  = price;
            if (priceText != null) priceText.text = price;
        }

        private void SetCoinAmount(int amount)
        {
            string display = amount.ToString("N0");
            if (coinTMP  != null) coinTMP.text  = display;
            if (coinText != null) coinText.text = display;
        }

        /// <summary>Gọi từ Button.OnClick()</summary>
        public void OnBuyClicked()
        {
            if (IAPManager.Instance == null) return;
            IAPManager.Instance.Purchase(itemType, success =>
            {
                if (success)
                    Debug.Log($"[ShopPackUI] Mua {itemType} thành công!");
            });
        }
    }
}
