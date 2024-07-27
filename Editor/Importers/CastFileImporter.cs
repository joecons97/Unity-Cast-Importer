using CastImporter.Editor.Cast;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using CastAnimation = CastImporter.Editor.Cast.Animation;
using CastModel = CastImporter.Editor.Cast.Model;

namespace CastImporter.Editor.Importers
{
    [ScriptedImporter(1, "cast")]
    public class CastFileImporter : ScriptedImporter
    {
        internal const string ARMATURE_PARENT_NAME = "Joints";

        public float Scale = 1;
        public bool GenerateLightmapUvs = false;
        public bool RecalculateNormals = false;
        public MeshOptimizationFlags OptimizeMesh = MeshOptimizationFlags.Everything;

        public Transform ExistingSkeleton;
        public ModelImporterAnimationType AnimationType = ModelImporterAnimationType.Generic;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var cast = CastFile.Load(ctx.assetPath);
            EditorUtility.DisplayProgressBar("Importing cast file...", $"Importing {Path.GetFileName(ctx.assetPath)}...", 0.1f);

            int rootNodeIndex = 0;
            foreach (var root in cast.RootNodes)
            {
                var nodesProgress = rootNodeIndex++ / (float)cast.RootNodes.Count;

                EditorUtility.DisplayProgressBar("Importing cast file...", $"Importing {Path.GetFileName(ctx.assetPath)} nodes...", nodesProgress);

                var models = root.ChildrenOfType<CastModel>();
                var modelsIndex = 0;
                foreach (var model in models)
                {
                    var modelsProgress = modelsIndex++ / (float)models.Count;
                    EditorUtility.DisplayProgressBar("Importing cast file...", $"Importing {Path.GetFileName(ctx.assetPath)} models...", modelsProgress);
                    CastModelImporter.ImportModel(ctx, model, new CastModelImporterSettings(Scale, GenerateLightmapUvs, RecalculateNormals, OptimizeMesh, AnimationType));
                }

                var animations = root.ChildrenOfType<CastAnimation>();
                var animationsIndex = 0;
                foreach (var animation in animations)
                {
                    var animationsProgress = animationsIndex++ / (float)animations.Count;
                    EditorUtility.DisplayProgressBar("Importing cast file...", $"Importing {Path.GetFileName(ctx.assetPath)} animations...", animationsProgress);
                    CastAnimationImporter.ImportAnimation(ctx, animation, new CastAnimationImporterSettings(ExistingSkeleton, AnimationType));
                }
            }

            EditorUtility.ClearProgressBar();
        }
    }
}