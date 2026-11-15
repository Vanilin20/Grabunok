using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Banda.EditorTools
{
    /// <summary>
    /// Збірка APK для тестів на телефонах (tools/build_android.sh). IL2CPP + ARM64 (Burst потрібен для UnityTransport),
    /// підпис тимчасовим debug-ключем Unity. Результат: Builds/Android/Banda.apk.
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Android/Banda.apk";
        // Ідентифікатор застосунку: у Unity за замовчуванням «com.DefaultCompany.2D-URP» недійсний для Android (дефіс, цифра на початку сегмента).
        public const string ApplicationId = "com.banda.heist";

        [MenuItem("Banda/Збірка/Android APK")]
        public static void Build()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.useCustomKeystore = false; // debug-ключ Unity: для тестових збірок достатньо
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            string[] scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("AndroidBuild: " + report.summary.result + ", " + report.summary.totalErrors + " помилок, " + report.summary.totalSize / (1024 * 1024) + " МБ, " + report.summary.totalTime);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
