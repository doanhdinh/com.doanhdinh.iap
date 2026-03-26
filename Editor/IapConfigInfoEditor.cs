using System.IO;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace DoanhDinh.IAP.Editor
{
    [CustomEditor(typeof(IapConfigInfo))]
    public class IapConfigInfoEditor : UnityEditor.Editor
    {
        private static readonly string[] Suffixes =
        {
            "pack012", "pack020", "pack050", "pack100",
            "pack150", "pack200", "pack500", "pack700", "pack900"
        };

        private static readonly float[] Prices =
        {
            0.12f, 0.20f, 0.50f, 1.00f,
            1.50f, 2.00f, 5.00f, 7.00f, 9.00f
        };

        private static readonly string[] Titles =
        {
            "Starter Pack", "Small Pack", "Basic Pack", "Standard Pack",
            "Value Pack", "Popular Pack", "Big Pack", "Mega Pack", "Ultimate Pack"
        };

        private string _bundlePrefix = "com.company.gamename";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);

            var style = new GUIStyle(EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Auto Generate Product IDs", style);

            _bundlePrefix = EditorGUILayout.TextField("Bundle Prefix", _bundlePrefix);

            EditorGUILayout.HelpBox(
                "Ví dụ: com.doanhdinh.smashdunk\n→ com.doanhdinh.smashdunk.pack012\n→ com.doanhdinh.smashdunk.pack020\n→ ...",
                MessageType.Info);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Update Product IDs", GUILayout.Height(36)))
            {
                ApplyBundlePrefix(_bundlePrefix.TrimEnd('.'));
            }
            GUI.backgroundColor = Color.white;
        }

        private void ApplyBundlePrefix(string prefix)
        {
            // 1. Cập nhật IapConfigInfo asset
            var so = new SerializedObject(target);
            var productsArray = so.FindProperty("products");

            for (int i = 0; i < productsArray.arraySize && i < Suffixes.Length; i++)
            {
                var element = productsArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("productId").stringValue = $"{prefix}.{Suffixes[i]}";
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            // 2. Cập nhật IAPProductCatalog.json
            UpdateCatalog(prefix);

            Debug.Log($"[IapConfigInfo] Updated {Suffixes.Length} product IDs with prefix: <b>{prefix}</b>");
        }

        private static void UpdateCatalog(string prefix)
        {
            string catalogPath = Path.Combine(Application.dataPath, "Resources", "IAPProductCatalog.json");

            if (!File.Exists(catalogPath))
            {
                // Tạo mới nếu chưa có
                Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));
                Debug.Log("[IapConfigInfo] Tạo mới IAPProductCatalog.json tại Assets/Resources/");
            }

            var sb = new StringBuilder();
            sb.Append("{\"appleSKU\":\"\",\"appleTeamID\":\"\",");
            sb.Append("\"enableCodelessAutoInitialization\":true,");
            sb.Append("\"enableUnityGamingServicesAutoInitialization\":false,");
            sb.Append("\"products\":[");

            for (int i = 0; i < Suffixes.Length; i++)
            {
                if (i > 0) sb.Append(",");

                string id    = $"{prefix}.{Suffixes[i]}";
                float  price = Prices[i];
                int    cents = Mathf.RoundToInt(price * 100);
                string priceStr = price.ToString("0.00", CultureInfo.InvariantCulture);

                sb.Append("{");
                sb.Append($"\"id\":\"{id}\",\"type\":0,\"storeIDs\":[],");
                sb.Append($"\"defaultDescription\":{{\"googleLocale\":4,\"title\":\"{Titles[i]}\",\"description\":\"Buy coins\"}},");
                sb.Append($"\"screenshotPath\":\"\",\"applePriceTier\":0,");
                sb.Append($"\"googlePrice\":{{\"data\":[{cents},0,0,131072],\"num\":{priceStr}}},");
                sb.Append($"\"pricingTemplateID\":\"{priceStr}\",\"descriptions\":[],");
                sb.Append($"\"udpPrice\":{{\"data\":[0,0,0,0],\"num\":0.0}},\"payouts\":[]");
                sb.Append("}");
            }

            sb.Append("]}");

            File.WriteAllText(catalogPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[IapConfigInfo] IAPProductCatalog.json updated → {prefix}.pack012 ... pack900");
        }
    }
}
