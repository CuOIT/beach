using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Chess.EditorTools
{
    /// <summary>
    /// Command-line build entry points. Unity only builds desktop targets without a script, so
    /// Android and WebGL need this. Invoke with, for example:
    ///
    ///   unity run . -- -buildTarget Android -executeMethod Chess.EditorTools.ChessBuild.BuildAndroid
    ///
    /// The output path comes from a -chessOutput argument, then the CHESS_BUILD_OUTPUT
    /// environment variable, then a per-target default under Build/.
    /// </summary>
    public static class ChessBuild
    {
        public static void BuildAndroid() => Build(BuildTarget.Android, "Build/Chess3D.apk");

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Build/Windows/Chess3D.exe");

        /// <summary>A development build keeps the profiler and the stack traces that go with it.</summary>
        public static void BuildAndroidDevelopment() =>
            Build(BuildTarget.Android, "Build/Chess3D-dev.apk", BuildOptions.Development);

        private static void Build(BuildTarget target, string defaultOutput, BuildOptions options = BuildOptions.None)
        {
            string output = ResolveOutputPath(defaultOutput);

            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // The shaders are only reachable through Shader.Find at runtime, so without this the
            // player ships without them and every material falls back to the error shader.
            ChessProjectSetup.EnsureRuntimeShadersIncluded();

            var playerOptions = new BuildPlayerOptions
            {
                scenes = new[] { ChessProjectSetup.ScenePath },
                locationPathName = output,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = options
            };

            Debug.Log("[ChessBuild] Building " + target + " to " + output);

            BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
            BuildSummary summary = report.summary;

            Debug.Log(string.Format(
                "[ChessBuild] Result: {0}. Size: {1:N0} bytes. Time: {2}. Errors: {3}. Warnings: {4}.",
                summary.result, summary.totalSize, summary.totalTime, summary.totalErrors, summary.totalWarnings));

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[ChessBuild] Wrote " + output);
                return;
            }

            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        Debug.LogError("[ChessBuild] " + step.name + ": " + message.content);
                }
            }

            // Batch mode otherwise reports success no matter what the build did.
            EditorApplication.Exit(1);
        }

        private static string ResolveOutputPath(string defaultOutput)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-chessOutput") return args[i + 1];
            }

            string fromEnvironment = Environment.GetEnvironmentVariable("CHESS_BUILD_OUTPUT");
            if (!string.IsNullOrEmpty(fromEnvironment)) return fromEnvironment;

            return defaultOutput;
        }
    }
}
