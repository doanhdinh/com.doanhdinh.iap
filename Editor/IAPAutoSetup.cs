using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoanhDinh.IAP.Editor
{
    /// <summary>
    /// Headless, CI-callable IAP setup. Generates the 9 SKUs for a bundleId, creates/updates
    /// the IapConfigInfo asset + Unity IAP product catalog, makes sure an IAPManager exists in
    /// the first Build Settings scene wired to that config, and (optionally) exports a plain
    /// text SKU list + a machine-readable status JSON (for the CI step to report back to the
    /// Store Manager's /iap-setup-status endpoint).
    ///
    /// Idempotent — every step compares against current content and skips writes/scene saves
    /// when nothing changed, so this is safe to run on every single build.
    ///
    /// CI invocation:
    ///   Unity.exe -batchmode -quit -projectPath <path> \
    ///     -executeMethod DoanhDinh.IAP.Editor.IAPAutoSetup.Run \
    ///     -iapBundleId com.doanhdinh.foo \
    ///     -iapOutputTxt D:\BuildOutput\Foo\Foo_iap_skus.txt \
    ///     -iapStatusJson D:\BuildOutput\Foo\iap-setup-status.json
    ///
    /// Manual (in-editor) invocation: DoanhDinh → IAP → Run Auto-Setup Now
    /// (uses PlayerSettings.applicationIdentifier as the bundleId, no txt/json export).
    /// </summary>
    public static class IAPAutoSetup
    {
        private const string ConfigAssetPath = "Assets/Resources/DoanhDinhIAPConfig.asset";

        [Serializable]
        private class SetupResult
        {
            public string bundleId;
            public bool success;
            public bool configChanged;
            public bool catalogChanged;
            public bool managerChanged;
            public string[] skus;
            public string error;
        }

        [MenuItem("DoanhDinh/IAP/Run Auto-Setup Now")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            string bundleId = GetArg("-iapBundleId");
            string outputTxt = GetArg("-iapOutputTxt");
            string statusJsonPath = GetArg("-iapStatusJson");

            if (string.IsNullOrEmpty(bundleId))
                bundleId = PlayerSettings.applicationIdentifier;
            bundleId = (bundleId ?? "").Trim().ToLowerInvariant();

            var result = new SetupResult { bundleId = bundleId, skus = new string[0] };

            if (string.IsNullOrEmpty(bundleId))
            {
                result.error = "No bundleId available (-iapBundleId not passed and " +
                    "PlayerSettings.applicationIdentifier is empty).";
                Debug.LogError($"[IAPAutoSetup] {result.error}");
                WriteStatusJson(statusJsonPath, result);
                return;
            }

            try
            {
                Debug.Log($"[IAPAutoSetup] Setting up IAP for bundleId: {bundleId}");

                result.configChanged = EnsureConfigAsset(bundleId, out var config);
                result.catalogChanged = EnsureProductCatalog(bundleId);
                result.managerChanged = EnsureShopUiInScene(config);
                result.skus = BuildSkuList(bundleId);
                result.success = true;

                if (!string.IsNullOrEmpty(outputTxt))
                    ExportSkuTxt(bundleId, outputTxt);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[IAPAutoSetup] Done.");
            }
            catch (Exception e)
            {
                result.success = false;
                result.error = e.Message;
                Debug.LogError($"[IAPAutoSetup] Failed: {e}");
            }

            WriteStatusJson(statusJsonPath, result);
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static string[] BuildSkuList(string bundleId)
        {
            var suffixes = IAPProductTiers.Suffixes;
            var list = new string[suffixes.Length];
            for (int i = 0; i < suffixes.Length; i++)
                list[i] = $"{bundleId}.{suffixes[i]}";
            return list;
        }

        private static void WriteStatusJson(string path, SetupResult result)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(result));
                Debug.Log($"[IAPAutoSetup] Status JSON written: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPAutoSetup] Failed to write status json: {e.Message}");
            }
        }

        // ── Config asset ──────────────────────────────────────────────────────

        private static bool EnsureConfigAsset(string bundleId, out IapConfigInfo config)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));

            config = AssetDatabase.LoadAssetAtPath<IapConfigInfo>(ConfigAssetPath);
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

            return changed;
        }

        // ── Product catalog (read by Unity IAP at runtime) ───────────────────

        private static bool EnsureProductCatalog(string bundleId)
        {
            string catalogPath = Path.Combine(Application.dataPath, "Resources", "IAPProductCatalog.json");
            string expected = IAPProductTiers.BuildCatalogJson(bundleId);

            if (File.Exists(catalogPath) && File.ReadAllText(catalogPath) == expected)
            {
                Debug.Log("[IAPAutoSetup] Product catalog already up to date, skipping.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));
            File.WriteAllText(catalogPath, expected);
            Debug.Log("[IAPAutoSetup] Product catalog written: Assets/Resources/IAPProductCatalog.json");
            return true;
        }

        // ── Scene wiring ──────────────────────────────────────────────────────

        private const string ShopUiSampleDisplayName = "Shop UI";
        private const string ShopUiPrefabFileName = "ShopUI_Canvas.prefab";

        /// <summary>
        /// Ensures the package's "Shop UI" sample (ShopUI_Canvas prefab: Canvas + shop
        /// panel + buy buttons, with IAPManager already attached) is imported and placed
        /// in the first Build Settings scene, wired to <paramref name="config"/>. Without
        /// this there is no in-game way to actually call Purchase() - it visibly shows a
        /// shop popup by default (root GameObjects in the prefab are active), same as the
        /// sample is designed to be used ("drag onto scene and it works").
        /// </summary>
        private static bool EnsureShopUiInScene(IapConfigInfo config)
        {
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes == null || buildScenes.Length == 0)
            {
                Debug.LogWarning("[IAPAutoSetup] No scenes in Build Settings, cannot place Shop UI. Skipping.");
                return false;
            }

            string scenePath = buildScenes[0].path;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Already placed in this scene? Re-link config if needed, otherwise no-op.
            var existingRoot = scene.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "ShopUI_Canvas");
            if (existingRoot != null)
            {
                var existingManager = existingRoot.GetComponentInChildren<IAPManager>(true);
                if (existingManager == null)
                {
                    Debug.LogWarning("[IAPAutoSetup] Found a ShopUI_Canvas in scene but no IAPManager under it - leaving as is.");
                    return false;
                }
                return RelinkConfigIfNeeded(existingManager, config, scene, scenePath);
            }

            string prefabPath = FindShopUiPrefabPath();
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[IAPAutoSetup] Could not locate/import the 'Shop UI' sample "
                    + "(ShopUI_Canvas.prefab). No purchase UI was added to the scene.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[IAPAutoSetup] Prefab failed to load at: {prefabPath}");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var manager = instance.GetComponentInChildren<IAPManager>(true);
            if (manager == null)
            {
                Debug.LogWarning("[IAPAutoSetup] Instantiated ShopUI_Canvas has no IAPManager component under it.");
            }
            else
            {
                var so = new SerializedObject(manager);
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[IAPAutoSetup] Instantiated Shop UI ({prefabPath}) into scene: {scenePath}");
            return true;
        }

        private static bool RelinkConfigIfNeeded(IAPManager manager, IapConfigInfo config, UnityEngine.SceneManagement.Scene scene, string scenePath)
        {
            var so = new SerializedObject(manager);
            var cfgProp = so.FindProperty("config");
            if (cfgProp.objectReferenceValue == config)
            {
                Debug.Log("[IAPAutoSetup] Shop UI already present in scene and correctly configured, skipping.");
                return false;
            }

            cfgProp.objectReferenceValue = config;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[IAPAutoSetup] Re-linked existing Shop UI's config in scene: {scenePath}");
            return true;
        }

        /// <summary>Imports the "Shop UI" sample if needed and returns the imported ShopUI_Canvas.prefab path.</summary>
        private static string FindShopUiPrefabPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(IAPManager).Assembly);
            string packageName = packageInfo != null ? packageInfo.name : "com.doanhdinh.iap";
            string packageVersion = packageInfo != null ? packageInfo.version : null;
            string displayName = packageInfo != null ? packageInfo.displayName : "DoanhDinh IAP Manager";
            string version = packageInfo != null ? packageInfo.version : "1.0.0";

            // The standard UPM sample import destination. shopSample.importPath is not
            // reliably populated right after Import() in batch mode, so this fixed
            // convention is what's actually used to locate the file below.
            string importPath = Path.Combine("Assets", "Samples", displayName, version, ShopUiSampleDisplayName);
            string prefabPath = Path.Combine(importPath, ShopUiPrefabFileName).Replace("\\", "/");

            if (File.Exists(prefabPath))
                return prefabPath;

            var samples = Sample.FindByPackage(packageName, packageVersion).ToList();
            var shopSample = samples.FirstOrDefault(s => s.displayName == ShopUiSampleDisplayName);
            if (shopSample.displayName != ShopUiSampleDisplayName)
            {
                Debug.LogWarning($"[IAPAutoSetup] Sample '{ShopUiSampleDisplayName}' not found in package {packageName}.");
                return null;
            }

            if (!shopSample.isImported && !shopSample.Import(Sample.ImportOptions.OverridePreviousImports))
            {
                Debug.LogWarning("[IAPAutoSetup] Failed to import the 'Shop UI' sample.");
                return null;
            }

            // Import() can return before the file copy is fully flushed to disk in batch
            // mode - poll briefly instead of failing on that race.
            for (int i = 0; i < 20 && !File.Exists(prefabPath); i++)
            {
                System.Threading.Thread.Sleep(250);
                AssetDatabase.Refresh();
            }

            return File.Exists(prefabPath) ? prefabPath : null;
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
