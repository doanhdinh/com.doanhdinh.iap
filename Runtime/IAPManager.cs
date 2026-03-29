using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Purchasing;

namespace DoanhDinh.IAP
{
    // ─── Enums ───────────────────────────────────────────────────────────────

    public enum IAPItemType
    {
        NONE     = 0,
        Pack_012 = 1,  // $0.12
        Pack_020 = 2,  // $0.20
        Pack_050 = 3,  // $0.50
        Pack_100 = 4,  // $1.00
        Pack_150 = 5,  // $1.50
        Pack_200 = 6,  // $2.00
        Pack_500 = 7,  // $5.00
        Pack_700 = 8,  // $7.00
        Pack_900 = 9   // $9.00
    }

    public enum IAPState
    {
        NONE            = 0,
        IS_WAITING_INIT = 1,
        IS_FAILED       = 2,
        IS_INITED       = 3
    }

    // ─── IAPManager ──────────────────────────────────────────────────────────

    public class IAPManager : MonoBehaviour, IStoreListener
    {
        public static IAPManager Instance { get; private set; }

        // ── Config ───────────────────────────────────────────────────────────

        /// <summary>
        /// ScriptableObject chứa product IDs và 10 templates coin.
        /// Tạo bằng: Right Click → Create → DoanhDinh → IAP Config
        /// </summary>
        [SerializeField] private IapConfigInfo config;

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
                    var ids = new StoreSpecificIds();
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
        /// Mua pack. Coin tự động cộng theo template đang chọn trong IapConfigInfo.
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

            string productId = GetProductId(itemType);
            var product = m_Controller.products.WithID(productId);
            if (product == null)
            {
                Debug.LogError($"[IAPManager] Không tìm thấy sản phẩm: {productId}");
                onComplete?.Invoke(false);
                return;
            }
            if (!product.availableToPurchase)
            {
                Debug.LogWarning($"[IAPManager] Sản phẩm chưa available trên store: {productId}\n" +
                    "→ Kiểm tra: sản phẩm đã active trên Play Console, tài khoản đã là tester của app chưa?");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log($"[IAPManager] Bắt đầu mua: {productId}");
            IsPurchaseInProgress = true;
            m_PendingPurchaseCallback = onComplete;
            m_Controller.InitiatePurchase(product);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            var product = e.purchasedProduct;
            var itemType = GetItemType(product.definition.id);
            int coinAmount = GetCoinAmount(itemType);

            if (coinAmount > 0 && CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddCoins(coinAmount);

            int templateIndex = config != null ? config.activeTemplate : -1;
            Debug.Log($"[IAPManager] Mua thành công: {product.definition.id} | Template {templateIndex} → +{coinAmount} coins");

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

        /// <summary>Lấy giá string từ store (vd: "$0.99"). Dùng callback vì phải chờ init.</summary>
        public void GetPrice(IAPItemType itemType, UnityAction<string> callback)
        {
            StartCoroutine(FetchPrice(itemType, callback));
        }

        private IEnumerator FetchPrice(IAPItemType itemType, UnityAction<string> callback)
        {
            yield return new WaitUntil(() => IsInitialized);
            string productId = GetProductId(itemType);
            var product = m_Controller.products.WithID(productId);
            callback?.Invoke(product?.metadata?.localizedPriceString ?? "---");
        }

        public float GetPriceNumber(IAPItemType itemType)
        {
            if (!IsInitialized) return -1f;
            var product = m_Controller.products.WithID(GetProductId(itemType));
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
                apple.RestoreTransactions((result, error) =>
                {
                    Debug.Log($"[IAPManager] Restore: {result}" + (error != null ? $" — {error}" : ""));
                    onComplete?.Invoke();
                });
            }
        }

        // ── Utils ─────────────────────────────────────────────────────────────

        public string GetProductId(IAPItemType itemType) =>
            config != null ? config.GetProductId(itemType) : "";

        public IAPItemType GetItemType(string productId) =>
            config != null ? config.GetItemType(productId) : IAPItemType.NONE;

        public int GetCoinAmount(IAPItemType itemType) =>
            config != null ? config.GetCoinAmount(itemType) : 0;

        /// <summary>Đổi template đang dùng lúc runtime (0-9).</summary>
        public void SetTemplate(int index)
        {
            if (config == null) return;
            config.activeTemplate = Mathf.Clamp(index, 0, config.templates.Length - 1);
            Debug.Log($"[IAPManager] Đổi sang Template {config.activeTemplate}: {config.templates[config.activeTemplate].templateName}");
        }

        public int GetActiveTemplate() => config != null ? config.activeTemplate : -1;
    }
}
