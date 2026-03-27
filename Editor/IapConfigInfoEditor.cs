using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DoanhDinh.IAP.Editor
{
    [CustomEditor(typeof(IapConfigInfo))]
    public class IapConfigInfoEditor : UnityEditor.Editor
    {
        // ── Constants ─────────────────────────────────────────────────────────

        private const string DefaultCompany = "com.doanhdinh";

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

        // ── State ─────────────────────────────────────────────────────────────

        private string _bundlePrefix;
        private string _serviceAccountPath = "";
        private bool   _isCreatingProducts;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            string productName = PlayerSettings.productName
                .ToLower()
                .Replace(" ", "");
            _bundlePrefix = $"{DefaultCompany}.{productName}";
        }

        // ── Inspector GUI ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // ── Section: Auto Generate IDs ────────────────────────────────────
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Auto Generate Product IDs", EditorStyles.boldLabel);

            _bundlePrefix = EditorGUILayout.TextField("Bundle Prefix", _bundlePrefix);
            EditorGUILayout.HelpBox(
                $"Product Name: \"{PlayerSettings.productName}\"\n→ {_bundlePrefix}.pack012 ... pack900",
                MessageType.Info);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Update Product IDs", GUILayout.Height(32)))
                ApplyBundlePrefix(_bundlePrefix.TrimEnd('.'));
            GUI.backgroundColor = Color.white;

            // ── Section: Google Play ───────────────────────────────────────────
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Google Play Store", EditorStyles.boldLabel);

            // Service account file picker
            EditorGUILayout.BeginHorizontal();
            _serviceAccountPath = EditorGUILayout.TextField("Service Account JSON", _serviceAccountPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("Select Service Account JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                    _serviceAccountPath = path;
            }
            EditorGUILayout.EndHorizontal();

            bool hasFile    = File.Exists(_serviceAccountPath);
            bool hasPrefix  = !string.IsNullOrEmpty(_bundlePrefix);
            bool canCreate  = hasFile && hasPrefix && !_isCreatingProducts;

            if (!hasFile)
                EditorGUILayout.HelpBox("Chọn file Service Account JSON từ Google Play Console.", MessageType.Warning);

            GUI.enabled = canCreate;
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
            if (GUILayout.Button(_isCreatingProducts ? "Đang tạo sản phẩm..." : "Create Products on Google Play", GUILayout.Height(36)))
                _ = CreateProductsAsync();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        // ── Product ID Generation ─────────────────────────────────────────────

        private void ApplyBundlePrefix(string prefix)
        {
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

            UpdateCatalog(prefix);
            Debug.Log($"[IapConfigInfo] Updated {Suffixes.Length} product IDs: {prefix}.pack012 ... pack900");
        }

        private static void UpdateCatalog(string prefix)
        {
            string catalogPath = Path.Combine(Application.dataPath, "Resources", "IAPProductCatalog.json");
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));

            var sb = new StringBuilder();
            sb.Append("{\"appleSKU\":\"\",\"appleTeamID\":\"\",");
            sb.Append("\"enableCodelessAutoInitialization\":true,");
            sb.Append("\"enableUnityGamingServicesAutoInitialization\":false,");
            sb.Append("\"products\":[");

            for (int i = 0; i < Suffixes.Length; i++)
            {
                if (i > 0) sb.Append(",");
                float  price    = Prices[i];
                int    cents    = Mathf.RoundToInt(price * 100);
                string priceStr = price.ToString("0.00", CultureInfo.InvariantCulture);

                sb.Append($"{{\"id\":\"{prefix}.{Suffixes[i]}\",\"type\":0,\"storeIDs\":[],");
                sb.Append($"\"defaultDescription\":{{\"googleLocale\":4,\"title\":\"{Titles[i]}\",\"description\":\"Buy coins\"}},");
                sb.Append($"\"screenshotPath\":\"\",\"applePriceTier\":0,");
                sb.Append($"\"googlePrice\":{{\"data\":[{cents},0,0,131072],\"num\":{priceStr}}},");
                sb.Append($"\"pricingTemplateID\":\"{priceStr}\",\"descriptions\":[],");
                sb.Append($"\"udpPrice\":{{\"data\":[0,0,0,0],\"num\":0.0}},\"payouts\":[]}}");
            }

            sb.Append("]}");
            File.WriteAllText(catalogPath, sb.ToString());
            AssetDatabase.Refresh();
        }

        // ── Google Play API ───────────────────────────────────────────────────

        private async Task CreateProductsAsync()
        {
            _isCreatingProducts = true;
            Repaint();

            try
            {
                var sa = LoadServiceAccount(_serviceAccountPath);
                if (sa == null) return;

                string packageName = PlayerSettings.applicationIdentifier;
                Debug.Log($"[GooglePlay] Package: {packageName}");

                EditorUtility.DisplayProgressBar("Google Play", "Lấy access token...", 0.1f);
                string token = await GetAccessTokenAsync(sa);
                if (string.IsNullOrEmpty(token))
                {
                    Debug.LogError("[GooglePlay] Không lấy được access token.");
                    return;
                }

                int total = Suffixes.Length;
                for (int i = 0; i < total; i++)
                {
                    string productId = $"{_bundlePrefix}.{Suffixes[i]}";
                    string title     = Titles[i];
                    long   micros    = (long)(Prices[i] * 1_000_000);

                    float progress = 0.1f + 0.9f * (i / (float)total);
                    EditorUtility.DisplayProgressBar("Google Play", $"Tạo {productId}...", progress);

                    bool ok = await CreateOrUpdateProductAsync(token, packageName, productId, title, micros);
                    Debug.Log(ok
                        ? $"[GooglePlay] ✓ {productId}"
                        : $"[GooglePlay] ✗ {productId} — xem Console để biết lỗi");
                }

                Debug.Log("[GooglePlay] Hoàn thành tạo sản phẩm!");
            }
            finally
            {
                _isCreatingProducts = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        // ── Service Account ───────────────────────────────────────────────────

        [Serializable]
        private class ServiceAccount
        {
            public string client_email;
            public string private_key;
            public string token_uri;
        }

        private static ServiceAccount LoadServiceAccount(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                var sa = JsonUtility.FromJson<ServiceAccount>(json);
                if (string.IsNullOrEmpty(sa?.client_email) || string.IsNullOrEmpty(sa?.private_key))
                {
                    Debug.LogError("[GooglePlay] File service account không hợp lệ.");
                    return null;
                }
                return sa;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GooglePlay] Lỗi đọc service account: {e.Message}");
                return null;
            }
        }

        // ── JWT / OAuth2 ──────────────────────────────────────────────────────

        private static async Task<string> GetAccessTokenAsync(ServiceAccount sa)
        {
            string jwt = CreateJWT(sa);
            if (string.IsNullOrEmpty(jwt)) return null;

            string body = $"grant_type={Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:jwt-bearer")}&assertion={Uri.EscapeDataString(jwt)}";
            string tokenUri = string.IsNullOrEmpty(sa.token_uri) ? "https://oauth2.googleapis.com/token" : sa.token_uri;

            using var req = new UnityWebRequest(tokenUri, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GooglePlay] Token error: {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            var resp = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
            return resp?.access_token;
        }

        [Serializable]
        private class TokenResponse { public string access_token; }

        private static string CreateJWT(ServiceAccount sa)
        {
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string header  = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
                string payload = Base64Url(Encoding.UTF8.GetBytes(
                    $"{{\"iss\":\"{sa.client_email}\"," +
                    $"\"scope\":\"https://www.googleapis.com/auth/androidpublisher\"," +
                    $"\"aud\":\"{sa.token_uri}\"," +
                    $"\"exp\":{now + 3600}," +
                    $"\"iat\":{now}}}"));

                string message   = $"{header}.{payload}";
                byte[] signature = SignRS256(Encoding.UTF8.GetBytes(message), sa.private_key);
                return $"{message}.{Base64Url(signature)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"[GooglePlay] JWT error: {e.Message}");
                return null;
            }
        }

        private static byte[] SignRS256(byte[] data, string pem)
        {
            bool isPkcs8 = pem.Contains("BEGIN PRIVATE KEY");
            string key = pem
                .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                .Replace("-----END RSA PRIVATE KEY-----", "")
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\n", "").Replace("\r", "").Trim();

            byte[] keyBytes = Convert.FromBase64String(key);
            RSAParameters rsaParams = isPkcs8 ? DecodePkcs8(keyBytes) : DecodePkcs1(keyBytes);

            using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportParameters(rsaParams);
                byte[] hash = System.Security.Cryptography.SHA256.Create().ComputeHash(data);
                return rsa.SignHash(hash, System.Security.Cryptography.CryptoConfig.MapNameToOID("SHA256"));
            }
        }

        // Strip PKCS#8 wrapper and return inner PKCS#1 key parameters
        private static RSAParameters DecodePkcs8(byte[] der)
        {
            using (var ms = new MemoryStream(der))
            {
                ReadAsnTag(ms, 0x30); ReadAsnLength(ms);  // outer SEQUENCE
                ReadAsnInteger(ms);                        // version INTEGER (skip)
                ReadAsnTag(ms, 0x30);                      // algorithm SEQUENCE
                ms.Seek(ReadAsnLength(ms), SeekOrigin.Current); // skip algorithm body
                ReadAsnTag(ms, 0x04);                      // OCTET STRING
                int len = ReadAsnLength(ms);
                byte[] pkcs1 = new byte[len];
                ms.Read(pkcs1, 0, len);
                return DecodePkcs1(pkcs1);
            }
        }

        private static RSAParameters DecodePkcs1(byte[] der)
        {
            using (var ms = new MemoryStream(der))
            {
                ReadAsnTag(ms, 0x30); ReadAsnLength(ms); // SEQUENCE
                ReadAsnInteger(ms);                       // version (skip)
                return new RSAParameters
                {
                    Modulus  = ReadAsnInteger(ms),
                    Exponent = ReadAsnInteger(ms),
                    D        = ReadAsnInteger(ms),
                    P        = ReadAsnInteger(ms),
                    Q        = ReadAsnInteger(ms),
                    DP       = ReadAsnInteger(ms),
                    DQ       = ReadAsnInteger(ms),
                    InverseQ = ReadAsnInteger(ms),
                };
            }
        }

        private static void ReadAsnTag(MemoryStream ms, byte expected)
        {
            int b = ms.ReadByte();
            if (b != expected) throw new Exception($"ASN.1: expected tag 0x{expected:X2}, got 0x{b:X2}");
        }

        private static int ReadAsnLength(MemoryStream ms)
        {
            int b = ms.ReadByte();
            if (b < 0x80) return b;
            int count = b & 0x7f;
            int len = 0;
            for (int i = 0; i < count; i++) len = (len << 8) | ms.ReadByte();
            return len;
        }

        private static byte[] ReadAsnInteger(MemoryStream ms)
        {
            ReadAsnTag(ms, 0x02);
            int len = ReadAsnLength(ms);
            byte[] raw = new byte[len];
            ms.Read(raw, 0, len);
            // Strip leading 0x00 padding byte (DER uses it when high bit is set)
            if (raw.Length > 1 && raw[0] == 0x00)
            {
                byte[] trimmed = new byte[raw.Length - 1];
                Array.Copy(raw, 1, trimmed, 0, trimmed.Length);
                return trimmed;
            }
            return raw;
        }

        private static string Base64Url(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        // ── Create / Update Product ───────────────────────────────────────────

        private static async Task<bool> CreateOrUpdateProductAsync(
            string token, string packageName, string productId, string title, long priceMicros)
        {
            string url  = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/inappproducts";
            string json = BuildProductJson(packageName, productId, title, priceMicros);

            // Thử INSERT trước
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success) return true;

            // 409 = đã tồn tại → PATCH để update
            if (req.responseCode == 409)
            {
                string patchUrl = $"{url}/{productId}";
                using var patch = new UnityWebRequest(patchUrl, "PATCH");
                patch.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                patch.downloadHandler = new DownloadHandlerBuffer();
                patch.SetRequestHeader("Content-Type", "application/json");
                patch.SetRequestHeader("Authorization", $"Bearer {token}");

                var patchOp = patch.SendWebRequest();
                while (!patchOp.isDone) await Task.Yield();

                if (patch.result == UnityWebRequest.Result.Success) return true;
                Debug.LogError($"[GooglePlay] PATCH {productId}: {patch.error}\n{patch.downloadHandler.text}");
                return false;
            }

            Debug.LogError($"[GooglePlay] POST {productId}: {req.error}\n{req.downloadHandler.text}");
            return false;
        }

        private static string BuildProductJson(string packageName, string productId, string title, long priceMicros)
        {
            return $@"{{
  ""packageName"": ""{packageName}"",
  ""sku"": ""{productId}"",
  ""status"": ""active"",
  ""purchaseType"": ""managedUser"",
  ""defaultLanguage"": ""en-US"",
  ""defaultPrice"": {{
    ""currency"": ""USD"",
    ""priceMicros"": ""{priceMicros}""
  }},
  ""listings"": {{
    ""en-US"": {{
      ""title"": ""{title}"",
      ""description"": ""Buy coins""
    }}
  }}
}}";
        }
    }
}
