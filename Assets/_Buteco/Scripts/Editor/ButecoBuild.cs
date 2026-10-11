using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Buteco.EditorTools
{
    /// <summary>
    /// Build nativo para Linux (x86_64). Pelo menu "Buteco → Compilar para Linux" ou na linha de comando:
    /// Unity -batchmode -quit -projectPath . -executeMethod Buteco.EditorTools.ButecoBuild.Linux [-buildPath pasta]
    /// </summary>
    public static class ButecoBuild
    {
        const string Scene = "Assets/_Buteco/Scenes/Buteco.unity";
        const string DefaultDir = "Builds/Linux";
        const string ExeName = "ButecoDigital.x86_64";

        [MenuItem("Buteco/Compilar para Linux")]
        static void LinuxMenu()
        {
            var report = BuildLinux(DefaultDir, false);
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(DefaultDir, ExeName));
        }

        /// <summary>Ponto de entrada do -executeMethod (sai com código 1 se falhar).</summary>
        public static void Linux()
        {
            string dir = DefaultDir;
            bool dev = false;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-buildPath" && i + 1 < args.Length) dir = args[i + 1];
                if (args[i] == "-dev") dev = true;
            }
            var report = BuildLinux(dir, dev);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport BuildLinux(string dir, bool development)
        {
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Buteco] Build Linux: {s.result} — {s.totalSize / (1024f * 1024f):0.0} MB em {s.totalTime.TotalSeconds:0}s → {opts.locationPathName}");
            return report;
        }
    }
}
