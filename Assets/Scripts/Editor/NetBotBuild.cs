using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Banda.EditorTools
{
    /// <summary>Збірка Windows-плеєра для мережевих ботів (tools/net_test.sh): Builds/NetBot/Banda.exe.</summary>
    public static class NetBotBuild
    {
        public const string OutputDir = "Builds/NetBot";

        [MenuItem("Banda/Мережа/Зібрати бота для тестів")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputDir);
            string[] scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, "Banda.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("NetBotBuild: " + report.summary.result + ", " + report.summary.totalErrors + " помилок, " + report.summary.totalTime);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
