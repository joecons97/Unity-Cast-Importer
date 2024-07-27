using CastImporter.Editor.Cast;
using CastImporter.Editor.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using CastAnimation = CastImporter.Editor.Cast.Animation;

namespace CastImporter.Editor.Importers
{
    internal class CastAnimationImporterSettings
    {
        public Transform BaseSkeleton { get; }
        public ModelImporterAnimationType AnimationType { get; }

        public CastAnimationImporterSettings(Transform baseSkeleton, ModelImporterAnimationType animationType)
        {
            BaseSkeleton = baseSkeleton;
            AnimationType = animationType;
        }
    }

    internal static class CastAnimationImporter
    {
        static void SetCurve(AnimationClip clip, string location, int[] keyFrameBuffer, float[] values, string propertyName)
        {
            var animationCurve = new AnimationCurve();
            var index = 0;
            foreach (var frame in keyFrameBuffer)
            {
                var time = frame / clip.frameRate;
                var value = values[index];
                animationCurve.AddKey(new Keyframe(time, value, 0, 0));
                index++;
            }

            clip.SetCurve(location, typeof(Transform), propertyName, animationCurve);
        }

        internal static void ImportAnimation(AssetImportContext ctx, CastAnimation animation,
            CastAnimationImporterSettings settings)
        {
            var fileName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var animationName = string.IsNullOrEmpty(animation.Name()) ? fileName : animation.Name();
            var animationClip = new AnimationClip
            {
                name = animationName,
                frameRate = animation.Framerate(),
                wrapMode = animation.Looping() ? WrapMode.Loop : WrapMode.Default,
                legacy = settings.AnimationType == ModelImporterAnimationType.Legacy
            };

            ctx.AddObjectToAsset(animationName, animationClip);

            if(ctx.mainObject != null)
                ctx.SetMainObject(animationClip);

            var curves = animation.Curves();
            var curvesIndex = 0;
            foreach (var curve in curves)
            {
                var meshesProgress = curvesIndex++ / (float)curves.Count;
                EditorUtility.DisplayProgressBar("Importing cast file...", $"Importing {Path.GetFileName(ctx.assetPath)} animation curves...", meshesProgress);

                var location = curve.NodeName();
                var bone = settings.BaseSkeleton.FindRecursive(location);
                if (bone == null)
                    continue;

                while (bone.parent != null)
                {
                    bone = bone.parent;
                    location = $"{bone.name}/{location}";

                    if (bone.name == CastFileImporter.ARMATURE_PARENT_NAME)
                        break;
                }

                bone = settings.BaseSkeleton.FindRecursive(curve.NodeName());

                var buffer = curve.KeyFrameBuffer().ToArray();
                var mode = curve.Mode();

                var property = curve.KeyPropertyName();
                if (property == "rq")
                {
                    var valueBuffer = curve.KeyValueBuffer<Cast.Vector4>()
                        .Select(x => x.ToUnityQuaternion())
                        .ToArray();

                    if (mode == "relative")
                        valueBuffer = valueBuffer
                            .Select(x => Quaternion.Inverse(bone.localRotation) * x)
                            .ToArray();

                    var xBuffer = valueBuffer.Select(x => x.x).ToArray();
                    var yBuffer = valueBuffer.Select(x => x.y).ToArray();
                    var zBuffer = valueBuffer.Select(x => x.z).ToArray();
                    var wBuffer = valueBuffer.Select(x => x.w).ToArray();
                    SetCurve(animationClip, location, buffer, xBuffer, "localRotation.x");
                    SetCurve(animationClip, location, buffer, yBuffer, "localRotation.y");
                    SetCurve(animationClip, location, buffer, zBuffer, "localRotation.z");
                    SetCurve(animationClip, location, buffer, wBuffer, "localRotation.w");
                }
                else if (property == "tx")
                {
                    var valueBuffer = curve.KeyValueBuffer<float>().ToArray();
                    if (mode == "relative")
                        valueBuffer = valueBuffer.Select(x => x + bone.transform.localPosition.x).ToArray();

                    SetCurve(animationClip, location, buffer, valueBuffer, "localPosition.x");
                }
                else if (property == "ty")
                {
                    var valueBuffer = curve.KeyValueBuffer<float>().ToArray();
                    if (mode == "relative")
                        valueBuffer = valueBuffer.Select(x => x + bone.transform.localPosition.y).ToArray();

                    SetCurve(animationClip, location, buffer, valueBuffer, "localPosition.y");
                }
                else if (property == "tz")
                {
                    var valueBuffer = curve.KeyValueBuffer<float>().ToArray();
                    if (mode == "relative")
                        valueBuffer = valueBuffer.Select(x => x + bone.transform.localPosition.z).ToArray();

                    SetCurve(animationClip, location, buffer, valueBuffer, "localPosition.z");
                }
                else if (property == "sx")
                {
                    var valueBuffer = curve.KeyValueBuffer<float>().ToArray();
                    if (mode == "relative")
                        valueBuffer = valueBuffer.Select(x => x + bone.transform.localScale.x).ToArray();

                    SetCurve(animationClip, location, buffer, valueBuffer, "localScale.x");
                }
                else if (property == "sy")
                {
                    var valueBuffer = curve.KeyValueBuffer<float>().ToArray();
                    if (mode == "relative")
                        valueBuffer = valueBuffer.Select(x => x + bone.transform.localScale.y).ToArray();

                    SetCurve(animationClip, location, buffer, valueBuffer, "localScale.y");
                }
                else if (property == "sz")
                {
                    var valueBuffer = curve.KeyValueBuffer<float>().ToArray();
                    if (mode == "relative")
                        valueBuffer = valueBuffer.Select(x => x + bone.transform.localScale.z).ToArray();

                    SetCurve(animationClip, location, buffer, valueBuffer, "localScale.z");
                }
                else
                {
                    Debug.LogWarning($"Unknown animation property {property}");
                }
            }

            var events = new List<AnimationEvent>();

            foreach (var notification in animation.Notifications())
            {
                var buffer = notification.KeyFrameBuffer();
                foreach (var frameIndex in buffer)
                {
                    var time = frameIndex / animationClip.frameRate;
                    events.Add(new AnimationEvent
                    {
                        functionName = notification.Name(),
                        time = time,
                    });
                }
            }
            AnimationUtility.SetAnimationEvents(animationClip, events.ToArray());
        }
    }
}
