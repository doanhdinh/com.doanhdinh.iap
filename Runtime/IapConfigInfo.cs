using System.Collections.Generic;
using UnityEngine;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// ScriptableObject cấu hình product ID theo từng game.
    /// Tạo bằng: Right Click → Create → DoanhDinh → IAP Config
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(fileName = "IAPConfig", menuName = "DoanhDinh/IAP Config", order = 1)]
    public class IapConfigInfo : ScriptableObject
    {
        [System.Serializable]
        public struct IAPConfigInfoItem
        {
            public IAPItemType itemType;
            public string productId;
            public int amount;
        }

        public IAPConfigInfoItem[] arrayItemInfo = new IAPConfigInfoItem[]
        {
            new IAPConfigInfoItem { itemType = IAPItemType.Coin_150,  productId = "com.game.iap.coin150",  amount = 150  },
            new IAPConfigInfoItem { itemType = IAPItemType.Coin_500,  productId = "com.game.iap.coin500",  amount = 500  },
            new IAPConfigInfoItem { itemType = IAPItemType.Coin_1000, productId = "com.game.iap.coin1000", amount = 1000 },
            new IAPConfigInfoItem { itemType = IAPItemType.Coin_2000, productId = "com.game.iap.coin2000", amount = 2000 },
            new IAPConfigInfoItem { itemType = IAPItemType.Coin_4000, productId = "com.game.iap.coin4000", amount = 4000 },
            new IAPConfigInfoItem { itemType = IAPItemType.Coin_8000, productId = "com.game.iap.coin8000", amount = 8000 },
        };

        private Dictionary<IAPItemType, IAPConfigInfoItem> _cache;

        private void BuildCache()
        {
            _cache = new Dictionary<IAPItemType, IAPConfigInfoItem>();
            foreach (var item in arrayItemInfo)
                if (!_cache.ContainsKey(item.itemType))
                    _cache[item.itemType] = item;
        }

        public string GetProductId(IAPItemType type)
        {
            if (_cache == null) BuildCache();
            return _cache.TryGetValue(type, out var item) ? item.productId : "";
        }

        public int GetAmount(IAPItemType type)
        {
            if (_cache == null) BuildCache();
            return _cache.TryGetValue(type, out var item) ? item.amount : 0;
        }

        public IAPItemType GetItemType(string productId)
        {
            foreach (var item in arrayItemInfo)
                if (item.productId.Equals(productId))
                    return item.itemType;
            return IAPItemType.NONE;
        }
    }
}
