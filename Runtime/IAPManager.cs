using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Purchasing;

namespace DoanhDinh.IAP
{
    // ─── Enums & Data ────────────────────────────────────────────────────────

    public enum IAPItemType
    {
        NONE      = 0,
        Coin_150  = 1,
        Coin_500  = 2,
        Coin_1000 = 3,
        Coin_2000 = 4,
        Coin_4000 = 5,
        Coin_8000 = 6
    }

    public enum IAPState
    {
        NONE            = 0,
        IS_WAITING_INIT = 1,
        IS_FAILED       = 2,
        IS_INITED       = 3
    }

    [Serializable]
    public class IAPConfigInfo
    {
        public IAPItemType itemType;
        public string productId;
        public int amount; // số coin tương ứng

        public static string GetProductId(IAPItemType itemType, IAPConfigInfo[] configs)
        {
            foreach (var c in configs)
                if (c.itemType == itemType) return c.productId;
            return "";
        }

        public static IAPItemType GetItemType(string productId, IAPConfigInfo[] configs)
        {
            foreach (var c in configs)
                if (c.productId.Equals(productId)) return c.itemType;
            return IAPItemType.NONE;
        }

        public static int GetAmount(IAPItemType itemType, IAPConfigInfo[] configs)
        {
            foreach (var c in configs)
                if (c.itemType == itemType) return c.amount;
            return 0;
        }
    }

    // ─── IAPManager ──────────────────────────────────────────────────────────

    public class IAPManager : MonoBehaviour, IStoreListener
    {
        public static IAPManager Instance { get; private set; }

        // ── Config ───────────────────────────────────────────────────────────

        /// <summary>
        /// Cấu hình product ID theo từng game.
        /// Nếu để trống, dùng DefaultConfigs với product ID mặc định.
        /// </summary>
        [SerializeField] private IAPConfigInfo[] configInfos;

        private static readonly IAPConfigInfo[] DefaultConfigs = new IAPConfigInfo[]
        {
            new IAPConfigInfo { itemType = IAPItemType.Coin_150,  productId = "com.game.iap.coin150",  amount = 150  },
            new IAPConfigInfo { itemType = IAPItemType.Coin_500,  productId = "com.game.iap.coin500",  amount = 500  },
            new IAPConfigInfo { itemType = IAPItemType.Coin_1000, productId = "com.game.iap.coin1000", amount = 1000 },
            new IAPConfigInfo { itemType = IAPItemType.Coin_2000, productId = "com.game.iap.coin2000", amount = 2000 },
            new IAPConfigInfo { itemType = IAPItemType.Coin_4000, productId = "com.game.iap.coin4000", amount = 4000 },
            new IAPConfigInfo { itemType = IAPItemType.Coin_8000, productId = "com.game.iap.coin8000", amount = 8000 },
        };

        private IAPConfigInfo[] ActiveConfigs =>
            (configInfos != null && configInfos.Length > 0) ? configInfos : DefaultConfigs;

        // ── State ─────────────────────────────────────────────────────────────

        public IAPState State { get; private set; }
        public bool IsInitialized => m_Controller != null && m_StoreExtensionProvider != null;
        public bool IsPurchaseInProgress { get; private set; }

        private IStoreController m_Controller;
        private static IExtensionProvider m_StoreExtensionProvider;

        private bool m_IsWaitingInit;
        private int m_CountInitFail;
        private const int MAX_INIT_FAIL = 5;

        private Action<bool> m_PendingPurchaseCallback;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (CurrencyManager.Instance == null)
                gameObject.AddComponent<CurrencyManager>();
        }

        private void Start()
        {
            InitIAP();
        }

        // ── Init ──────────────────────────────────────────────────────────────

        public void InitIAP(bool forceInit = false)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("[IAPManager] Không có mạng, bỏ qua init IAP.");
                return;
            }
            if (IsInitialized) return;
            if (m_IsWaitingInit) return;
            if (!forceInit && m_CountInitFail >= MAX_INIT_FAIL)
            {
                Debug.LogWarning("[IAPManager] Đã vượt quá số lần thử init.");
                return;
            }

            m_IsWaitingInit = true;
            State = IAPState.IS_WAITING_INIT;

            var module = StandardPurchasingModule.Instance();
            module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
            var builder = ConfigurationBuilder.Instance(module);

            var catalog = ProductCatalog.LoadDefaultCatalog();
            foreach (var product in catalog.allValidProducts)
            {
                if (product.allStoreIDs.Count > 0)
                {
                    var ids = new IDs();
                    foreach (var storeID in product.allStoreIDs)
                        ids.Add(storeID.id, storeID.store);
                    builder.AddProduct(product.id, product.type, ids);
                }
                else
                {
                    builder.AddProduct(product.id, product.type);
                }
            }

            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            m_IsWaitingInit = false;
            State = IAPState.IS_INITED;
            m_Controller = controller;
            m_StoreExtensionProvider = extensions;
            Debug.Log("[IAPManager] Khởi tạo IAP thành công.");
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            m_IsWaitingInit = false;
            State = IAPState.IS_FAILED;
            m_CountInitFail++;
            Debug.LogWarning($"[IAPManager] Init thất bại ({m_CountInitFail}/{MAX_INIT_FAIL}): {error}");

            if (m_CountInitFail < MAX_INIT_FAIL)
                InitIAP();
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[IAPManager] Init thất bại: {error} - {message}");
            OnInitializeFailed(error);
        }

        // ── Purchase ──────────────────────────────────────────────────────────

        /// <summary>
        /// Mua coin. Sau khi thành công coin tự động được cộng vào CurrencyManager.
        /// </summary>
        public void Purchase(IAPItemType itemType, Action<bool> onComplete)
        {
            if (IsPurchaseInProgress)
            {
                Debug.LogWarning("[IAPManager] Đang có giao dịch khác đang xử lý.");
                return;
            }
            if (!IsInitialized)
            {
                Debug.LogError("[IAPManager] Chưa khởi tạo xong.");
                onComplete?.Invoke(false);
                return;
            }

            string productId = IAPConfigInfo.GetProductId(itemType, ActiveConfigs);
            var product = m_Controller.products.WithID(productId);
            if (product == null)
            {
                Debug.LogError($"[IAPManager] Không tìm thấy sản phẩm: {productId}");
                onComplete?.Invoke(false);
                return;
            }

            IsPurchaseInProgress = true;
            m_PendingPurchaseCallback = onComplete;
            m_Controller.InitiatePurchase(product);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            var product = e.purchasedProduct;
            var itemType = IAPConfigInfo.GetItemType(product.definition.id, ActiveConfigs);
            int coinAmount = IAPConfigInfo.GetAmount(itemType, ActiveConfigs);

            if (coinAmount > 0 && CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddCoins(coinAmount);

            Debug.Log($"[IAPManager] Mua thành công: {product.definition.id} → +{coinAmount} coins");

            IsPurchaseInProgress = false;
            m_PendingPurchaseCallback?.Invoke(true);
            m_PendingPurchaseCallback = null;

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product item, PurchaseFailureReason reason)
        {
            Debug.LogWarning($"[IAPManager] Mua thất bại: {item.definition.id} — {reason}");
            IsPurchaseInProgress = false;
            m_PendingPurchaseCallback?.Invoke(false);
            m_PendingPurchaseCallback = null;
        }

        // ── Price Helpers ─────────────────────────────────────────────────────

        /// <summary>Lấy giá string (vd: "$1.99"). Dùng callback vì phải chờ init.</summary>
        public void GetPrice(IAPItemType itemType, UnityAction<string> callback)
        {
            StartCoroutine(FetchPrice(itemType, callback));
        }

        private IEnumerator FetchPrice(IAPItemType itemType, UnityAction<string> callback)
        {
            yield return new WaitUntil(() => IsInitialized);
            string productId = IAPConfigInfo.GetProductId(itemType, ActiveConfigs);
            var product = m_Controller.products.WithID(productId);
            callback?.Invoke(product?.metadata?.localizedPriceString ?? "---");
        }

        public float GetPriceNumber(IAPItemType itemType)
        {
            if (!IsInitialized) return -1f;
            string productId = IAPConfigInfo.GetProductId(itemType, ActiveConfigs);
            var product = m_Controller.products.WithID(productId);
            return product != null ? (float)product.metadata.localizedPrice : -1f;
        }

        // ── Restore (iOS) ─────────────────────────────────────────────────────

        public void RestorePurchases(Action onComplete)
        {
            if (!IsInitialized) return;
            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
                apple.RestoreTransactions(result =>
                {
                    Debug.Log($"[IAPManager] Restore: {result}");
                    onComplete?.Invoke();
                });
            }
        }

        // ── Utils ─────────────────────────────────────────────────────────────

        public string GetProductId(IAPItemType itemType) =>
            IAPConfigInfo.GetProductId(itemType, ActiveConfigs);

        public int GetCoinAmount(IAPItemType itemType) =>
            IAPConfigInfo.GetAmount(itemType, ActiveConfigs);
    }
}
