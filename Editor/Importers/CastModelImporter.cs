using CastImporter.Editor.Cast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;
using CastMesh = CastImporter.Editor.Cast.Mesh;
using CastModel = CastImporter.Editor.Cast.Model;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

namespace CastImporter.Editor.Importers
{
    public enum ScaleUnits
    {
        Meters,
        Inches,
        Centermeters
    }

    internal class CastModelImporterSettings
    {
        public ScaleUnits ScaleUnit { get; }
        public float ScaleMultiplier { get; }
        public bool GenerateLightmapUvs { get; }
        public MeshOptimizationFlags OptimizeMesh { get; }
        public ModelImporterAnimationType AnimationType { get; }
        public bool RecalculateNormals { get; }

        public CastModelImporterSettings(ScaleUnits scaleUnit, float scaleMultiplier, bool generateLightmapUvs, bool recalculateNormals, MeshOptimizationFlags optimizeMesh, ModelImporterAnimationType animationType)
        {
            ScaleUnit = scaleUnit;
            ScaleMultiplier = scaleMultiplier;
            GenerateLightmapUvs = generateLightmapUvs;
            RecalculateNormals = recalculateNormals;
            OptimizeMesh = optimizeMesh;
            AnimationType = animationType;
        }

        public float GetTotalScale()
        {
            var baseScale = ScaleUnit switch
            {
                ScaleUnits.Meters => CastModelImporter.METERS_SCALE,
                ScaleUnits.Inches => CastModelImporter.INCHES_SCALE,
                ScaleUnits.Centermeters => CastModelImporter.CENTERMETERS_SCALE,
                _ => 1
            };

            return baseScale * ScaleMultiplier;
        }
    }

    internal static class CastModelImporter
    {
        public const float METERS_SCALE = 1.0f / 1.0f;
        public const float INCHES_SCALE = 1.0f / 39.3701f;
        public const float CENTERMETERS_SCALE = 1.0f / 100.0f;

        internal static void ImportModel(AssetImportContext ctx, CastModel model, CastModelImporterSettings settings)
        {
            var fileName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var modelName = string.IsNullOrEmpty(model.Name()) ? fileName : model.Name();
            var mainObject = new GameObject(modelName);

            ctx.AddObjectToAsset(modelName, mainObject);
            ctx.SetMainObject(mainObject);

            int index = 0;

            var meshesByHash = new Dictionary<CastMesh, (SkinnedMeshRenderer, Mesh)>();
            var (skeletonObj, poses) = ImportSkeleton(ctx, model.Skeleton(), mainObject, settings);

            var meshes = model.Meshes();
            var meshesIndex = 0;
            foreach (var castMesh in meshes)
            {
                var meshesProgress = meshesIndex++ / (float)meshes.Count;

                var name = castMesh.Name();
                if (string.IsNullOrEmpty(name))
                    name = $"CastMesh {index++}";

                EditorUtility.DisplayProgressBar("Importing cast file...", $"Importing {Path.GetFileName(ctx.assetPath)} meshes... ({name})", meshesProgress);

                var gameObject = new GameObject(name);
                var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                gameObject.transform.SetParent(mainObject.transform, false);
                ctx.AddObjectToAsset($"{modelName}_gameObject_{name}", gameObject);

                var unityMesh = new Mesh
                {
                    name = name,
                    vertices = castMesh.VertexPositionBuffer()
                        .Select(x => x.ToUnityVector() * settings.GetTotalScale())
                        .ToArray(),

                    triangles = castMesh.FaceBuffer()
                        .ToArray(),

                    colors = castMesh.VertexColorBuffer()
                        .Select(x => FromUint(x))
                        .ToArray()
                };

                if (settings.RecalculateNormals == false)
                    unityMesh.normals = castMesh.VertexNormalBuffer()
                        .Select(x => x.ToUnityVector())
                        .ToArray();
                else
                    unityMesh.RecalculateNormals();

                ctx.AddObjectToAsset($"{modelName}_mesh_{name}", unityMesh);
                meshesByHash.Add(castMesh, (renderer, unityMesh));

                for (int i = 0; i < castMesh.UVLayerCount(); i++)
                {
                    var uvBuffer = castMesh.VertexUVLayerBuffer(i)
                        .Select(x => new UnityEngine.Vector2(x.X, x.Y))
                        .ToArray();

                    unityMesh.SetUVs(i, uvBuffer);
                }

                unityMesh.RecalculateTangents();
                unityMesh.RecalculateBounds();

                ImportBlendshapes(model, meshesByHash, settings);

                if (skeletonObj != null)
                {
                    renderer.rootBone = skeletonObj.transform;
                    renderer.bones = poses.Select(x => x.Value.transform).ToArray();
                    unityMesh.bindposes = poses.Select(x => x.Value.transform.worldToLocalMatrix * skeletonObj.transform.localToWorldMatrix).ToArray();
                    ImportWeightsBones(poses, unityMesh, castMesh);
                }

                if(settings.OptimizeMesh == MeshOptimizationFlags.Everything)
                    unityMesh.Optimize();
                else if(settings.OptimizeMesh == MeshOptimizationFlags.PolygonOrder)
                    unityMesh.OptimizeIndexBuffers();
                else if (settings.OptimizeMesh == MeshOptimizationFlags.VertexOrder)
                    unityMesh.OptimizeReorderVertexBuffer();

                renderer.sharedMesh = unityMesh;
                renderer.localBounds = unityMesh.bounds;

                var materialName = castMesh.Material()?.Name() ?? name;
                var newMat = new Material(GetDefaultShader())
                {
                    name = materialName
                };
                ctx.AddObjectToAsset($"{modelName}_mesh_{name}_mat_{newMat.name}", newMat);
                renderer.sharedMaterial = newMat;
            }
        }

        static void ImportBlendshapes(CastModel model, Dictionary<CastMesh, (SkinnedMeshRenderer, Mesh)> meshesByHash, CastModelImporterSettings settings)
        {
            var blendShapesByBaseShape = new Dictionary<CastMesh, List<BlendShape>>();
            foreach (var blendShape in model.BlendShapes())
            {
                var baseShape = blendShape.BaseShape();
                if (blendShapesByBaseShape.ContainsKey(baseShape))
                    blendShapesByBaseShape[baseShape].Add(blendShape);
                else
                    blendShapesByBaseShape.Add(baseShape, new List<BlendShape> { blendShape });
            }

            foreach (var blendShapes in blendShapesByBaseShape.Values)
            {
                var baseShape = meshesByHash[blendShapes.First().BaseShape()];

                foreach (var blendShape in blendShapes)
                {
                    var positions = blendShape.TargetShapeVertexPositions().Select(x => x.ToUnityVector()).ToArray();
                    baseShape.Item2.AddBlendShapeFrame(blendShape.Name(), blendShape.TargetWeightScale(), positions, Array.Empty<UnityEngine.Vector3>(), Array.Empty<UnityEngine.Vector3>());
                }
            }
        }

        static void ImportWeightsBones(Dictionary<string, GameObject> poses, Mesh newMesh, CastMesh mesh)
        {
            var maximumInfluence = mesh.MaximumWeightInfluence();
            if (maximumInfluence > 1)
            {
                var bonesPerVertex = new NativeArray<byte>(newMesh.vertices.Length, Allocator.Temp);
                var weightsList = new List<BoneWeight1>();

                var weightBoneBuffer = mesh.VertexWeightBoneBuffer().ToArray();
                var weightValueBuffer = mesh.VertexWeightValueBuffer().ToArray();
                for (int i = 0; i < newMesh.vertices.Length; i++)
                {
                    var vertexWeights = new List<BoneWeight1>();

                    byte maxCount = 0;
                    for (int j = 0; j < maximumInfluence; j++)
                    {
                        int weightBufferIndex = j + (i * maximumInfluence);
                        var boneIndex = weightBoneBuffer[weightBufferIndex];
                        var boneValue = weightValueBuffer[weightBufferIndex];

                        vertexWeights.Add(new BoneWeight1
                        {
                            boneIndex = boneIndex,
                            weight = boneValue
                        });
                        maxCount++;
                    }
                    weightsList.AddRange(vertexWeights.OrderByDescending(x => x.weight));
                    bonesPerVertex[i] = maxCount;
                }

                var weightsArray = new NativeArray<BoneWeight1>(weightsList.ToArray(), Allocator.Temp);
                newMesh.SetBoneWeights(bonesPerVertex, weightsArray);

                weightsArray.Dispose();
                bonesPerVertex.Dispose();
            }
            else if (maximumInfluence > 0)
            {
                var weights = new List<BoneWeight>();
                var weightBoneBuffer = mesh.VertexWeightBoneBuffer().ToArray();
                for (int i = 0; i < newMesh.vertices.Length; i++)
                {
                    var boneIndex = weightBoneBuffer[i];
                    var weight = new BoneWeight
                    {
                        boneIndex0 = boneIndex,
                        weight0 = 1
                    };
                    weights.Add(weight);
                }
                newMesh.boneWeights = weights.ToArray();
            }
        }

        static (GameObject armature, Dictionary<string, GameObject> poses) ImportSkeleton(AssetImportContext ctx, Skeleton skeleton, GameObject mainObject, CastModelImporterSettings settings)
        {
            var bones = skeleton.Bones();

            var armature = new GameObject(CastFileImporter.ARMATURE_PARENT_NAME);
            armature.transform.SetParent(mainObject.transform, false);
            ctx.AddObjectToAsset($"bone_Joints", armature);

            var handles = new GameObject[bones.Count];
            var poses = new Dictionary<string, GameObject>();

            int index = 0;
            foreach (var bone in bones)
            {
                var newBone = new GameObject(bone.Name());
                ctx.AddObjectToAsset($"bone_{newBone.name}", newBone);

                var translation = bone.LocalPosition().ToUnityVector() * settings.GetTotalScale();
                var rotation = bone.LocalRotation().ToUnityQuaternion();
                var scale = bone.Scale() == null
                    ? UnityEngine.Vector3.one
                    : bone.Scale().ToUnityVector();

                newBone.transform.SetLocalPositionAndRotation(translation, rotation);
                newBone.transform.localScale = scale;

                handles[index] = newBone;
                poses[newBone.name] = newBone;

                index++;
            }

            index = 0;
            foreach (var bone in bones)
            {
                if (bone.ParentIndex() > -1)
                    handles[index].transform.SetParent(handles[bone.ParentIndex()].transform, false);
                else
                    handles[index].transform.SetParent(armature.transform, false);

                index++;
            }

            Avatar avatar = null;

            if (settings.AnimationType == ModelImporterAnimationType.Human)
                ctx.LogImportError("Human rigs are currently not supported! Please choose another option.");
            else if (settings.AnimationType == ModelImporterAnimationType.Generic)
                avatar = AvatarBuilder.BuildGenericAvatar(armature, CastFileImporter.ARMATURE_PARENT_NAME);

            if (avatar != null)
            {
                avatar.name = $"{mainObject.name}_Avatar";
                ctx.AddObjectToAsset($"avatar", avatar);
            }

            return (armature, poses);
        }

        static Color FromUint(uint value)
        {
            var r = (value >> 16) & 0xff;
            var g = (value >> 8) & 0xff;
            var b = value & 0xff;

            return new Color(r, g, b);
        }

        static Shader GetDefaultShader()
        {
            if (GetUsedRenderPipeline() == null)
                return Shader.Find("Standard");
            else
                return GetUsedRenderPipeline().defaultShader;
        }

        static RenderPipelineAsset GetUsedRenderPipeline()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                return GraphicsSettings.currentRenderPipeline;
            else
                return GraphicsSettings.defaultRenderPipeline;
        }
    }
}