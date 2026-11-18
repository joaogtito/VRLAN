using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace VRLAN.BuildTools
{
    // Gera os dois APKs do projeto (fora do pacote do plugin — é ferramenta deste repositório):
    //   Quest  → Build Profile "Android™ 1" (lista global: QuestLobby primeiro) + OpenXR LIGADO
    //   Tablet → Build Profile "Android™"   (TabletScene Teste primeiro)         + OpenXR DESLIGADO
    // O estado do loader OpenXR do Android é restaurado no fim.
    //
    // Menu: VRLAN → Build APKs (Quest + Tablet)
    // Batch: -executeMethod VRLAN.BuildTools.VRLANApkBuilder.BuildBoth [-vrlanApkSuffix 1811] [-vrlanOnly quest|tablet]
    public static class VRLANApkBuilder
    {
        const string QuestProfile = "Assets/Settings/Build Profiles/Android™ 1.asset";
        const string TabletProfile = "Assets/Settings/Build Profiles/Android™.asset";
        const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        [MenuItem("VRLAN/Build APKs (Quest + Tablet)")]
        static void Menu() => BuildBoth();

        public static void BuildBoth()
        {
            var suffix = Arg("-vrlanApkSuffix") ?? DateTime.Now.ToString("ddMM");
            var only = Arg("-vrlanOnly");
            Directory.CreateDirectory("Builds");

            bool hadOpenXR = HasOpenXR();
            bool ok = true;
            try
            {
                if (only == null || only == "quest")
                    ok &= Build(QuestProfile, $"Builds/Build{suffix}_Quest.apk", openXR: true);
                if (only == null || only == "tablet")
                    ok &= Build(TabletProfile, $"Builds/Build{suffix}_Tablet.apk", openXR: false);
            }
            finally
            {
                SetOpenXR(hadOpenXR);
                AssetDatabase.SaveAssets();
            }

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string profilePath, string apkPath, bool openXR)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null) { Debug.LogError($"[VRLANApkBuilder] perfil não encontrado: {profilePath}"); return false; }

            SetOpenXR(openXR);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VRLANApkBuilder] build {apkPath} (perfil '{profile.name}', OpenXR={(openXR ? "ON" : "OFF")})");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = apkPath,
                options = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[VRLANApkBuilder] {apkPath}: {s.result} · {s.totalSize / (1024f * 1024f):0.0} MB · " +
                      $"{s.totalTime:mm\\:ss} · erros={s.totalErrors}");
            return s.result == BuildResult.Succeeded;
        }

        static XRManagerSettings AndroidManager()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            return general != null ? general.Manager : null;
        }

        static bool HasOpenXR()
        {
            var m = AndroidManager();
            if (m == null) return false;
            foreach (var l in m.activeLoaders)
                if (l != null && l.GetType().FullName == OpenXRLoader) return true;
            return false;
        }

        static void SetOpenXR(bool on)
        {
            var m = AndroidManager();
            if (m == null) { Debug.LogWarning("[VRLANApkBuilder] sem XR Management settings pro Android."); return; }
            if (on == HasOpenXR()) return;
            bool done = on
                ? XRPackageMetadataStore.AssignLoader(m, OpenXRLoader, BuildTargetGroup.Android)
                : XRPackageMetadataStore.RemoveLoader(m, OpenXRLoader, BuildTargetGroup.Android);
            EditorUtility.SetDirty(m);
            Debug.Log($"[VRLANApkBuilder] OpenXR (Android) → {(on ? "ON" : "OFF")} ({(done ? "ok" : "falhou")})");
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
