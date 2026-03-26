using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Gắn lên mỗi ShopPack button prefab.
    /// Tự lấy giá từ store và xử lý mua — không cần assign gì trên IAPUIManager.
    /// Chỉ cần set đúng itemType trong Inspector.
    /// </summary>
    public class ShopPackUI : MonoBehaviour
    {
        [Header("Config")]
        public IAPItemType itemType;

        [Header("Price Display (tự tìm nếu để trống)")]
        public Text priceText;
        public TextMeshProUGUI priceTMP;

        private void Start()
        {
            if (priceText == null) priceText = GetComponentInChildren<Text>();
            if (priceTMP  == null) priceTMP  = GetComponentInChildren<TextMeshProUGUI>();

            StartCoroutine(LoadPrice());
        }

        private IEnumerator LoadPrice()
        {
            yield return new WaitUntil(() => IAPManager.Instance != null && IAPManager.Instance.IsInitialized);
            IAPManager.Instance.GetPrice(itemType, SetPrice);
        }

        private void SetPrice(string price)
        {
            if (priceTMP  != null) priceTMP.text  = price;
            if (priceText != null) priceText.text = price;
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
