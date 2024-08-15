using CastImporter.Editor.Extensions;
using CastImporter.Editor.Importers;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace CastImporter.Editor.Inspectors
{
    [CustomEditor(typeof(CastFileImporter), true)]
    public class CastImporterInspector : ScriptedImporterEditor
    {
        private CastFileImporter importer;

        protected override void Awake()
        {
            importer = (CastFileImporter)target;
        }

        public override void OnInspectorGUI()
        {
            RenderModelSection();
            RenderAnimationSection();

            ApplyRevertGUI();

            if(GUI.changed)
                EditorUtility.SetDirty(importer);
        }

        void RenderModelSection()
        {
            EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);
            importer.ScaleUnits = (ScaleUnits)EditorGUILayout.EnumPopup("Scale", importer.ScaleUnits);
            importer.ScaleMultiplier = EditorGUILayout.FloatField("Scale Multiplier", importer.ScaleMultiplier);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Mesh Data", EditorStyles.boldLabel);
            importer.RecalculateNormals = EditorGUILayout.Toggle("Recalculate Normals", importer.RecalculateNormals);
            importer.OptimizeMesh = (MeshOptimizationFlags)EditorGUILayout.EnumFlagsField("Optimize Mesh", importer.OptimizeMesh);

            EditorGUILayout.Space();
        }

        void RenderAnimationSection()
        {
            EditorGUILayout.LabelField("Rig", EditorStyles.boldLabel);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
            Transform rigBase = null;
            if (asset != null)
                rigBase = asset.transform.FindRecursive(CastFileImporter.ARMATURE_PARENT_NAME);

            if (asset == null || rigBase == null)
            {
                EditorGUILayout.HelpBox("No rig could be found in this asset. You may use an external rig as a base for any animations.", MessageType.Warning);
                importer.ExistingSkeleton = (Transform)EditorGUILayout.ObjectField("External Rig", importer.ExistingSkeleton, typeof(Transform), false);
            }
            else
            {
                importer.AnimationType = (ModelImporterAnimationType)EditorGUILayout.EnumPopup("Animation Type", importer.AnimationType);
                if(importer.AnimationType == ModelImporterAnimationType.Human)
                {
                    EditorGUILayout.HelpBox("Human rigs are currently not supported! Please choose another option.", MessageType.Error);
                }
            }

            importer.ImportEvents = EditorGUILayout.Toggle("Import Events", importer.ImportEvents);
        }
    }
}