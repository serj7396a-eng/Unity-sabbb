using System.Linq;
using Railrush.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Railrush.Editor
{
    public sealed class RailrushModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/RailrushAssets/Models/"))
                return;
            var importer = (ModelImporter)assetImporter;
            bool courier = assetPath.EndsWith("/Courier.fbx");
            importer.importAnimation = courier;
            if (courier) importer.animationType = ModelImporterAnimationType.Generic;
            importer.importLights = false;
            importer.importCameras = false;
            importer.isReadable = !courier;
        }
    }

    public sealed class ImportedAssetBuildCheck : IPreprocessBuildWithReport
    {
        private static readonly string[] Models =
        {
            "Courier", "TrainFront", "TrainWagon", "Rail", "Hurdle", "Gate", "BuildingA",
            "BuildingB", "BuildingC", "BuildingD", "Lamp", "Sign", "Coin", "Boots", "Magnet",
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (string name in Models)
            {
                var model = Resources.Load<GameObject>(ImportedArt.Root + "Models/" + name);
                if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new BuildFailedException("RAILRUSH missing imported 3D model: " + name);
            }
            var clips = Resources.LoadAll<AnimationClip>(ImportedArt.Root + "Models/Courier");
            if (!clips.Any(clip => clip.name.EndsWith("Man_Run")) ||
                !clips.Any(clip => clip.name.EndsWith("Man_Jump")))
                throw new BuildFailedException("RAILRUSH Courier requires the imported run and jump animation clips.");
            foreach (string texture in new[] { "Platformer", "City", "Boots" })
                if (Resources.Load<Texture2D>(ImportedArt.Root + "Textures/" + texture) == null)
                    throw new BuildFailedException("RAILRUSH missing texture: " + texture);
            foreach (string name in new[] { "music", "coin", "jump", "switch", "boost", "crash", "land" })
            {
                var audio = Resources.Load<AudioClip>(ImportedArt.Root + "Audio/" + name);
                if (audio == null || audio.samples == 0)
                    throw new BuildFailedException("RAILRUSH missing imported audio: " + name);
            }
            if (Resources.Load<Shader>(ImportedArt.Root + "RailrushImportedSurface") == null)
                throw new BuildFailedException("RAILRUSH missing the shader for imported materials.");
            Debug.Log("RAILRUSH verified the imported models, animation clips, textures and audio for " + report.summary.platform);
        }
    }
}
