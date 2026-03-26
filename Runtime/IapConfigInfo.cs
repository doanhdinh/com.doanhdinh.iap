using System.Collections.Generic;
using UnityEngine;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// ScriptableObject cấu hình IAP.
    /// - products: 9 sản phẩm với product ID cố định (giá lấy từ store)
    /// - templates: 10 bộ coin khác nhau — đổi activeTemplate để A/B test
    /// Tạo bằng: Right Click → Create → DoanhDinh → IAP Config
    /// </summary>
    [CreateAssetMenu(fileName = "IAPConfig", menuName = "DoanhDinh/IAP Config", order = 1)]
    public class IapConfigInfo : ScriptableObject
    {
        // ── Structs ───────────────────────────────────────────────────────────

        [System.Serializable]
        public struct IAPProductInfo
        {
            public IAPItemType itemType;
            [Tooltip("Product ID đăng ký trên Google Play / App Store")]
            public string productId;
        }

        [System.Serializable]
        public class IAPTemplate
        {
            public string templateName;
            [Tooltip("Số coin tương ứng cho 9 gói theo thứ tự: $0.12 → $0.20 → $0.50 → $1 → $1.5 → $2 → $5 → $7 → $9")]
            public int[] coinAmounts = new int[9];
        }

        // ── Products ──────────────────────────────────────────────────────────

        [Header("Products — Product IDs (giá lấy từ store, không đổi)")]
        public IAPProductInfo[] products = new IAPProductInfo[]
        {
            new IAPProductInfo { itemType = IAPItemType.Pack_012, productId = "com.game.iap.pack012" },
            new IAPProductInfo { itemType = IAPItemType.Pack_020, productId = "com.game.iap.pack020" },
            new IAPProductInfo { itemType = IAPItemType.Pack_050, productId = "com.game.iap.pack050" },
            new IAPProductInfo { itemType = IAPItemType.Pack_100, productId = "com.game.iap.pack100" },
            new IAPProductInfo { itemType = IAPItemType.Pack_150, productId = "com.game.iap.pack150" },
            new IAPProductInfo { itemType = IAPItemType.Pack_200, productId = "com.game.iap.pack200" },
            new IAPProductInfo { itemType = IAPItemType.Pack_500, productId = "com.game.iap.pack500" },
            new IAPProductInfo { itemType = IAPItemType.Pack_700, productId = "com.game.iap.pack700" },
            new IAPProductInfo { itemType = IAPItemType.Pack_900, productId = "com.game.iap.pack900" },
        };

        // ── Templates ─────────────────────────────────────────────────────────

        [Header("Templates — 10 bộ coin (đổi activeTemplate để A/B test)")]
        public IAPTemplate[] templates = new IAPTemplate[]
        {
            //                                          $0.12  $0.20  $0.50  $1.00  $1.50  $2.00  $5.00   $7.00   $9.00
            new IAPTemplate { templateName = "Template 0 — Base",    coinAmounts = new[] { 50,   100,   300,   700,   1200,  1800,  5000,  7500,  10000 } },
            new IAPTemplate { templateName = "Template 1 — +20%",    coinAmounts = new[] { 60,   120,   360,   840,   1440,  2160,  6000,  9000,  12000 } },
            new IAPTemplate { templateName = "Template 2 — +40%",    coinAmounts = new[] { 70,   140,   420,   980,   1680,  2520,  7000,  10500, 14000 } },
            new IAPTemplate { templateName = "Template 3 — +60%",    coinAmounts = new[] { 80,   160,   480,   1120,  1920,  2880,  8000,  12000, 16000 } },
            new IAPTemplate { templateName = "Template 4 — +80%",    coinAmounts = new[] { 90,   180,   540,   1260,  2160,  3240,  9000,  13500, 18000 } },
            new IAPTemplate { templateName = "Template 5 — +100%",   coinAmounts = new[] { 100,  200,   600,   1400,  2400,  3600,  10000, 15000, 20000 } },
            new IAPTemplate { templateName = "Template 6 — +120%",   coinAmounts = new[] { 110,  220,   660,   1540,  2640,  3960,  11000, 16500, 22000 } },
            new IAPTemplate { templateName = "Template 7 — +140%",   coinAmounts = new[] { 120,  240,   720,   1680,  2880,  4320,  12000, 18000, 24000 } },
            new IAPTemplate { templateName = "Template 8 — +160%",   coinAmounts = new[] { 130,  260,   780,   1820,  3120,  4680,  13000, 19500, 26000 } },
            new IAPTemplate { templateName = "Template 9 — +180%",   coinAmounts = new[] { 140,  280,   840,   1960,  3360,  5040,  14000, 21000, 28000 } },
        };

        [Header("Template đang dùng (0 - 9)")]
        [Range(0, 9)] public int activeTemplate = 0;

        // ── Cache ─────────────────────────────────────────────────────────────

        private Dictionary<IAPItemType, int> _indexCache;

        private void BuildCache()
        {
            _indexCache = new Dictionary<IAPItemType, int>();
            for (int i = 0; i < products.Length; i++)
                if (!_indexCache.ContainsKey(products[i].itemType))
                    _indexCache[products[i].itemType] = i;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public string GetProductId(IAPItemType type)
        {
            if (_indexCache == null) BuildCache();
            return _indexCache.TryGetValue(type, out int idx) ? products[idx].productId : "";
        }

        /// <summary>Trả về số coin theo template đang active.</summary>
        public int GetCoinAmount(IAPItemType type)
        {
            if (_indexCache == null) BuildCache();
            if (!_indexCache.TryGetValue(type, out int idx)) return 0;
            int t = Mathf.Clamp(activeTemplate, 0, templates.Length - 1);
            return idx < templates[t].coinAmounts.Length ? templates[t].coinAmounts[idx] : 0;
        }

        public IAPItemType GetItemType(string productId)
        {
            foreach (var p in products)
                if (p.productId.Equals(productId))
                    return p.itemType;
            return IAPItemType.NONE;
        }

        /// <summary>Tên template đang active.</summary>
        public string GetActiveTemplateName()
        {
            int t = Mathf.Clamp(activeTemplate, 0, templates.Length - 1);
            return templates[t].templateName;
        }
    }
}
