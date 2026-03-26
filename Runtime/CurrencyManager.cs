using UnityEngine;
using System;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Quản lý ví coin của người chơi, lưu bằng PlayerPrefs.
    /// Game project chỉ cần gọi SpendCoins() / GetCoins().
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        private const string COIN_KEY = "doanhdinh_iap_coins";

        public static CurrencyManager Instance { get; private set; }

        /// <summary>Sự kiện khi số coin thay đổi. Tham số là số coin mới.</summary>
        public static event Action<int> OnCoinsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        /// <summary>Lấy số coin hiện tại.</summary>
        public int GetCoins()
        {
            return PlayerPrefs.GetInt(COIN_KEY, 0);
        }

        /// <summary>Cộng coin (gọi tự động sau khi mua IAP thành công).</summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            int newTotal = GetCoins() + amount;
            PlayerPrefs.SetInt(COIN_KEY, newTotal);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(newTotal);
            Debug.Log($"[CurrencyManager] +{amount} coins → total: {newTotal}");
        }

        /// <summary>
        /// Trừ coin. Trả về true nếu đủ coin và trừ thành công, false nếu không đủ.
        /// </summary>
        public bool SpendCoins(int amount)
        {
            if (amount <= 0) return true;
            int current = GetCoins();
            if (current < amount)
            {
                Debug.LogWarning($"[CurrencyManager] Không đủ coin. Cần: {amount}, Hiện có: {current}");
                return false;
            }
            int newTotal = current - amount;
            PlayerPrefs.SetInt(COIN_KEY, newTotal);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(newTotal);
            Debug.Log($"[CurrencyManager] -{amount} coins → total: {newTotal}");
            return true;
        }

        /// <summary>Kiểm tra có đủ coin không mà không trừ.</summary>
        public bool HasEnoughCoins(int amount)
        {
            return GetCoins() >= amount;
        }
    }
}
