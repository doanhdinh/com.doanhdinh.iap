using System.Globalization;
using System.Text;

namespace DoanhDinh.IAP
{
    /// <summary>
    /// Single source of truth for the 9 fixed price tiers used by every game
    /// (mirrors IAPItemType). Shared by IapConfigInfoEditor (manual button) and
    /// IAPAutoSetup (headless CI setup) so both generate identical SKUs/catalogs.
    /// </summary>
    public static class IAPProductTiers
    {
        public static readonly string[] Suffixes =
        {
            "pack012", "pack020", "pack050", "pack100",
            "pack150", "pack200", "pack500", "pack700", "pack900"
        };

        public static readonly float[] Prices =
        {
            0.12f, 0.20f, 0.50f, 1.00f,
            1.50f, 2.00f, 5.00f, 7.00f, 9.00f
        };

        public static readonly string[] Titles =
        {
            "Starter Pack", "Small Pack", "Basic Pack", "Standard Pack",
            "Value Pack", "Popular Pack", "Big Pack", "Mega Pack", "Ultimate Pack"
        };

        /// <summary>Builds the Unity IAP "IAPProductCatalog.json" contents for a bundleId.</summary>
        public static string BuildCatalogJson(string bundleId)
        {
            var sb = new StringBuilder();
            sb.Append("{\"appleSKU\":\"\",\"appleTeamID\":\"\",");
            sb.Append("\"enableCodelessAutoInitialization\":true,");
            sb.Append("\"enableUnityGamingServicesAutoInitialization\":false,");
            sb.Append("\"products\":[");

            for (int i = 0; i < Suffixes.Length; i++)
            {
                if (i > 0) sb.Append(",");
                float price = Prices[i];
                int cents = UnityEngine.Mathf.RoundToInt(price * 100);
                string priceStr = price.ToString("0.00", CultureInfo.InvariantCulture);

                sb.Append($"{{\"id\":\"{bundleId}.{Suffixes[i]}\",\"type\":0,\"storeIDs\":[],");
                sb.Append($"\"defaultDescription\":{{\"googleLocale\":4,\"title\":\"{Titles[i]}\",\"description\":\"Buy coins\"}},");
                sb.Append("\"screenshotPath\":\"\",\"applePriceTier\":0,");
                sb.Append($"\"googlePrice\":{{\"data\":[{cents},0,0,131072],\"num\":{priceStr}}},");
                sb.Append($"\"pricingTemplateID\":\"{priceStr}\",\"descriptions\":[],");
                sb.Append("\"udpPrice\":{\"data\":[0,0,0,0],\"num\":0.0},\"payouts\":[]}");
            }

            sb.Append("]}");
            return sb.ToString();
        }
    }
}
