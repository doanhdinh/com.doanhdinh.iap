using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoanhDinh.IAP.Editor
{
    /// <summary>
    /// Headless, CI-callable IAP setup. Generates the 9 SKUs for a bundleId, creates/updates
    /// the IapConfigInfo asset + Unity IAP product catalog, makes sure an IAPManager exists in
    /// the first Build Settings scene wired to that config, and (optionally) exports a plain
    /// text SKU list for manual store product creation.
    ///
    /// Idempotent — every step compares against current content and skips writes/scene saves
    /// when nothing changed, so this is safe to run on every single build.
    ///
    /// CI invocation:
    ///   Unity.exe -batchmode -quit -projectPath <path> \
    ///     -executeMethod DoanhDinh.IAP.Editor.IAPAutoSetup.Run \
    ///     -iapBundleId com.doanhdinh.foo -iapOutputTxt D:\BuildOutput\Foo\Foo_iap_skus.txt
    ///
    /// Manual (in-editor) invocation: DoanhDinh → IAP → Run Auto-Setup Now
    /// (uses PlayerSettings.applicationIdentifier as the bundleId, no txt export).
    /// </summary>
    public static class IAPAutoSetup
    {
        private const string ConfigAssetPath = "Assets/Resources/DoanhDinhIAPConfig.asset";

        [MenuItem("DoanhDinh/IAP/Run Auto-Setup Now")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            string bundleId = GetArg("-iapBundleId");
            string outputTxt = GetArg("-iapOutputTxt");

            if (string.IsNullOrEmpty(bundleId))
                bundleId = PlayerSettings.applicationIdentifier;
            bundleId = (bundleId ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(bundleId))
            {
                Debug.LogError("[IAPAutoSetup] No bundleId available (-iapBundleId not passed and " +
                    "PlayerSettings.applicationIdentifier is empty). Aborting.");
                return;
            }

            Debug.Log($"[IAPAutoSetup] Setting up IAP for bundleId: {bundleId}");

            var config = EnsureConfigAsset(bundleId);
            EnsureProductCatalog(bundleId);
            EnsureManagerInScene(config);

            if (!string.IsNullOrEmpty(outputTxt))
                ExportSkuTxt(bundleId, outputTxt);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IAPAutoSetup] Done.");
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        // ── Config asset ──────────────────────────────────────────────────────

        private static IapConfigInfo EnsureConfigAsset(string bundleId)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));

            var config = AssetDatabase.LoadAssetAtPath<IapConfigInfo>(ConfigAssetPath);
            bool isNew = config == null;
            if (isNew)
            {
                config = ScriptableObject.CreateInstance<IapConfigInfo>();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
            }

            var so = new SerializedObject(config);
            var productsArray = so.FindProperty("products");
            var suffixes = IAPProductTiers.Suffixes;
            bool changed = isNew;

            for (int i = 0; i < productsArray.arraySize && i < suffixes.Length; i++)
            {
                var element = productsArray.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("productId");
                string expected = $"{bundleId}.{suffixes[i]}";
                if (idProp.stringValue != expected)
                {
                    idProp.stringValue = expected;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                Debug.Log($"[IAPAutoSetup] Config asset created/updated: {ConfigAssetPath}");
            }
            else
            {
                Debug.Log("[IAPAutoSetup] Config asset already up to date, skipping.");
            }

            return config;
        }

        // ── Product catalog (read by Unity IAP at runtime) ───────────────────

        private static void EnsureProductCatalog(string bundleId)
        {
            string catalogPath = Path.Combine(Application.dataPath, "Resources", "IAPProductCatalog.json");
            string expected = IAPProductTiers.BuildCatalogJson(bundleId);

            if (File.Exists(catalogPath) && File.ReadAllText(catalogPath) == expected)
            {
                Debug.Log("[IAPAutoSetup] Product catalog already up to date, skipping.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));
            File.WriteAllText(catalogPath, expected);
            Debug.Log("[IAPAutoSetup] Product catalog written: Assets/Resources/IAPProductCatalog.json");
        }

        // ── Scene wiring ──────────────────────────────────────────────────────

        private static void EnsureManagerInScene(IapConfigInfo config)
        {
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes == null || buildScenes.Length == 0)
            {
                Debug.LogWarning("[IAPAutoSetup] No scenes in Build Settings, cannot place IAPManager. Skipping.");
                return;
            }

            string scenePath = buildScenes[0].path;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // A lean scene GameObject with just the manager component - not the visual
            // "Shop UI" sample prefab, so this never adds unexpected UI on top of a game's
            // own screens. Games that want the sample shop UI can still import/place it
            // manually; this only guarantees IAPManager.Instance.Purchase(...) works.
            var existing = UnityEngine.Object.FindObjectOfType<IAPManager>();
            if (existing != null)
            {
                var existingSo = new SerializedObject(existing);
                var cfgProp = existingSo.FindProperty("config");
                if (cfgProp.objectReferenceValue != config)
                {
                    cfgProp.objectReferenceValue = config;
                    existingSo.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[IAPAutoSetup] Re-linked existing IAPManager's config in scene: {scenePath}");
                }
                else
                {
                    Debug.Log("[IAPAutoSetup] IAPManager already present in scene and correctly configured, skipping.");
                }
                return;
            }

            var go = new GameObject("IAPManager (Auto-Setup)");
            var manager = go.AddComponent<IAPManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[IAPAutoSetup] Added IAPManager to scene: {scenePath}");
        }

        // ── SKU export (for manual Play Console / App Store Connect product creation) ─

        private static void ExportSkuTxt(string bundleId, string outputPath)
        {
            var suffixes = IAPProductTiers.Suffixes;
            var prices = IAPProductTiers.Prices;
            var titles = IAPProductTiers.Titles;

            var sb = new StringBuilder();
            sb.AppendLine($"# IAP Product IDs — {PlayerSettings.productName}");
            sb.AppendLine($"# Bundle ID: {bundleId}");
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine();

            for (int i = 0; i < suffixes.Length; i++)
            {
                string productId = $"{bundleId}.{suffixes[i]}";
                sb.AppendLine($"--- Pack {i + 1} ---");
                sb.AppendLine($"Product ID *:          {productId}");
                sb.AppendLine($"Name *:                {PlayerSettings.productName} {titles[i]}");
                sb.AppendLine($"Description *:         {titles[i]} for {PlayerSettings.productName}");
                sb.AppendLine($"Price (USD):           ${prices[i]:0.00}");
                sb.AppendLine($"Purchase option ID *:  {suffixes[i]}-buy");
                sb.AppendLine();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[IAPAutoSetup] SKU list exported: {outputPath}");
        }
    }
}
