using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Railrush.Editor
{
    public static class BuildRailrushWeb
    {
        [MenuItem("RAILRUSH/Build mobile WebGL")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Unity Web Build Support for this editor version.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("The NightShift scene must be enabled in Build Profiles.");

            string root = Path.GetDirectoryName(Application.dataPath);
            string output = Path.Combine(root, "Builds", "RailrushWeb");
            string[] args = Environment.GetCommandLineArgs();
            string outputArgument = Array.IndexOf(args, "-railrushOutput") >= 0
                ? "-railrushOutput"
                : "-customBuildPath";
            int overrideIndex = Array.IndexOf(args, outputArgument);
            if (overrideIndex >= 0)
            {
                if (overrideIndex + 1 >= args.Length || args[overrideIndex + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException(outputArgument + " requires a directory.");
                output = Path.GetFullPath(args[overrideIndex + 1]);
            }
            Directory.CreateDirectory(output);

            string previousTemplate = PlayerSettings.WebGL.template;
            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            try
            {
                PlayerSettings.WebGL.template = "PROJECT:RailrushMobile";
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = BuildTarget.WebGL,
                    locationPathName = output,
                    options = BuildOptions.None,
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Unity WebGL build failed: " + report.summary.result);
                Debug.Log("RAILRUSH mobile WebGL written to build directory " + output);
            }
            finally
            {
                PlayerSettings.WebGL.template = previousTemplate;
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
            }
        }
    }
}
