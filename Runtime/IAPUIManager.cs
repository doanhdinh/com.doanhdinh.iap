using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Quản lý UI shop: tự spawn đủ 9 pack button và hiển thị số coin.
    /// Kéo ShopUI prefab vào scene là chạy ngay — không cần setup thêm.
    /// ShopPack Single prefab được load tự động từ Resources.
    /// </summary>
    public class IAPUIManager : MonoBehaviour
    {
        private const string PACK_PREFAB_PATH = "ShopPack Single";

        [Header("References")]
        public IAPManager iapManager;

        [Header("Pack Container")]
        [Tooltip("Layout Group chứa các pack (kéo Content / Bg vào)")]
        public Transform packContainer;

        [Header("Coin Balance Display")]
        public TextMeshProUGUI tmpBalance;
        public Text txtBalance;

        private static readonly IAPItemType[] AllPacks =
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
            if (packContainer == null)
            {
                Debug.LogWarning("[IAPUIManager] Pack Container chưa được assign.");
                return;
            }

            var packPrefab = Resources.Load<ShopPackUI>(PACK_PREFAB_PATH);
            if (packPrefab == null)
            {
                Debug.LogError($"[IAPUIManager] Không tìm thấy prefab '{PACK_PREFAB_PATH}' trong Resources.");
                return;
            }

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
