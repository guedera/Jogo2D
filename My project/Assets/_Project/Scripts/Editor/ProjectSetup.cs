using System.IO;
using UnityEditor;
using UnityEngine;

namespace TypeDuel.EditorTools
{
    /// <summary>
    /// Setup e builds do projeto. Menu: Tools > TypeDuel.
    /// Também usado em linha de comando: Unity -batchmode -executeMethod TypeDuel.EditorTools.ProjectSetup.BuildMac
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/TypeDuel/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro"))
            {
                Debug.Log("[TypeDuel] TMP Essentials ja importado.");
                return;
            }

            // A importacao e assincrona: em batchmode so sai depois do callback (nao usar -quit).
            AssetDatabase.importPackageCompleted += _ =>
            {
                Debug.Log("[TypeDuel] TMP Essentials importado.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError("[TypeDuel] Falha ao importar TMP: " + error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            };

            string path = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(path, false);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Insper";
            PlayerSettings.productName = "TypeDuel";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.macOS.buildNumber = "1";

            // D3D12 (padrao do Unity 6) falha em GPUs/drivers antigos e VMs; para um jogo 2D, D3D11 roda em tudo.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });

            // O itch.io nao envia os cabecalhos de compressao que o Unity espera: sem compressao, o build Web abre em qualquer servidor.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
        }

        static bool Build(BuildTarget target, string relativeOutput)
        {
            ApplyPlayerSettings();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = target,
                locationPathName = Path.Combine("Builds", relativeOutput),
                options = BuildOptions.None,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName));
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[TypeDuel] Build {target}: {report.summary.result} -> {options.locationPathName}");

            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            // So encerra o Unity em linha de comando; com o editor aberto, apenas registra o erro.
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
            if (!ok) Debug.LogError($"[TypeDuel] Build {target} falhou. Veja os erros acima no Console.");
            return ok;
        }

        [MenuItem("Tools/TypeDuel/Build macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/TypeDuel.app");

        [MenuItem("Tools/TypeDuel/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows/TypeDuel.exe");

        [MenuItem("Tools/TypeDuel/Build Web (itch.io)")]
        public static void BuildWeb() => Build(BuildTarget.WebGL, "WebGL");

        /// <summary>Gera macOS, Windows e Web em sequencia (Web por ultimo, porque troca a plataforma e reimporta os assets).</summary>
        [MenuItem("Tools/TypeDuel/Build All (macOS, Windows, Web)")]
        public static void BuildAll()
        {
            bool mac = Build(BuildTarget.StandaloneOSX, "macOS/TypeDuel.app");
            bool win = Build(BuildTarget.StandaloneWindows64, "Windows/TypeDuel.exe");
            bool web = Build(BuildTarget.WebGL, "WebGL");
            Debug.Log($"[TypeDuel] Build All: macOS={(mac ? "ok" : "FALHOU")}  Windows={(win ? "ok" : "FALHOU")}  Web={(web ? "ok" : "FALHOU")}");
        }
    }
}
