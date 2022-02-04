using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using UnityEditor.Build.Pipeline.Utilities;
using System.IO;

namespace BundleSystem
{
    [DisallowMultipleComponent]
    [CustomEditor(typeof(BundleSettingObject))]
    public class BundleSettingObjectInspector : Editor
    {
        SerializedProperty m_BundleSetting;

        private void OnEnable()
        {
            m_BundleSetting = serializedObject.FindProperty("bundleSetting");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var setting = target as BundleSettingObject;

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_BundleSetting, true);
            GUILayout.EndHorizontal();

            string bundleName = setting.bundleSetting.BundleName;
            bundleName = bundleName.Remove(bundleName.LastIndexOf("/"));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build Remote"))
            {
                AssetbundleBuilder.BuildAssetBundles(AssetbundleBuildSettings.EditorInstance, BuildType.Remote, bundleName);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Build Local"))
            {
                AssetbundleBuilder.BuildAssetBundles(AssetbundleBuildSettings.EditorInstance, BuildType.Local, bundleName);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}