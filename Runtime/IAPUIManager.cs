using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Quản lý UI shop: tự spawn đủ 9 pack button và hiển thị số coin.
    /// Kéo ShopUI prefab vào scene là chạy ngay, không cần setup thêm.
    /// </summary>
    public class IAPUIManager : MonoBehaviour
    {
        [Header("References")]
        public IAPManager iapManager;

        [Header("Pack Spawn")]
        [Tooltip("Prefab ShopPack Single — có sẵn ShopPackUI component")]
        public ShopPackUI packPrefab;
        [Tooltip("Container để spawn các pack vào (ScrollView Content hoặc Layout Group)")]
        public Transform packContainer;

        [Header("Coin Balance Display")]
        public TextMeshProUGUI tmpBalance;
        public Text txtBalance;

        private static readonly IAPItemType[] AllPacks = new[]
        {
            IAPItemType.Pack_012,
            IAPItemType.Pack_020,
            IAPItemType.Pack_050,
            IAPItemType.Pack_100,
            IAPItemType.Pack_150,
            IAPItemType.Pack_200,
            IAPItemType.Pack_500,
            IAPItemType.Pack_700,
            IAPItemType.Pack_900,
        };

        private void Start()
        {
            if (iapManager == null)
                iapManager = IAPManager.Instance;

            SpawnPacks();

            CurrencyManager.OnCoinsChanged += RefreshBalance;
            int coins = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCoins() : 0;
            RefreshBalance(coins);
        }

        private void OnDestroy()
        {
            CurrencyManager.OnCoinsChanged -= RefreshBalance;
        }

        private void SpawnPacks()
        {
            if (packPrefab == null || packContainer == null) return;

            foreach (var itemType in AllPacks)
            {
                var pack = Instantiate(packPrefab, packContainer);
                pack.itemType = itemType;
            }
        }

        private void RefreshBalance(int coins)
        {
            string display = coins.ToString("N0");
            if (tmpBalance != null) tmpBalance.text = display;
            if (txtBalance  != null) txtBalance.text  = display;
        }
    }
}
