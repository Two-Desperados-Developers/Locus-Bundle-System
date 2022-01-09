﻿using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEngine;
using System;

namespace BundleSystem
{
    public enum BuildType
    {
        Remote,
        Local
    }

    /// <summary>
    /// class that contains actual build functionalities
    /// </summary>
    public static class AssetbundleBuilder
    {
        const string LogFileName = "BundleBuildLog.txt";
        const string LogExpectedSharedBundleFileName = "ExpectedSharedBundles.txt";

        class CustomBuildParameters : BundleBuildParameters
        {
            public AssetbundleBuildSettings CurrentSettings;
            public BuildType CurrentBuildType;
            public Dictionary<string, HashSet<string>> DependencyDic;
            public string SingleBundle;

            public CustomBuildParameters(AssetbundleBuildSettings settings,
                BuildTarget target,
                BuildTargetGroup group,
                string outputFolder,
                Dictionary<string, HashSet<string>> deps,
                BuildType  buildType,
                string singleBundle = null) : base(target, group, outputFolder)
            {
                CurrentSettings = settings;
                CurrentBuildType = buildType;
                DependencyDic = deps;
                SingleBundle = singleBundle;
            }

            // Override the GetCompressionForIdentifier method with new logic
            public override BuildCompression GetCompressionForIdentifier(string identifier)
            {
                //local bundles are always lz4 for faster initializing
                if (CurrentBuildType == BuildType.Local) return BuildCompression.LZ4;

                //find user set compression method
                var found = CurrentSettings.BundleSettings.FirstOrDefault(setting => setting.BundleName == identifier);
                return found == null || !found.CompressBundle ? BuildCompression.LZ4 : BuildCompression.LZMA;
            }
        }

        public static void BuildAssetBundles(BuildType buildType)
        {
            var editorInstance = AssetbundleBuildSettings.EditorInstance;
            BuildAssetBundles(editorInstance, buildType);
        }

        public static void WriteExpectedSharedBundles(AssetbundleBuildSettings settings)
        {
            if(!Application.isBatchMode)
            {
                //have to ask save current scene
                var saved = UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

                if(!saved)
                {
                    Debug.LogError("Failed! User Canceled");
                    return;
                }
            }

            var tempPrevSceneKey = "WriteExpectedSharedBundlesPrevScene";
            var prevScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            EditorPrefs.SetString(tempPrevSceneKey, prevScene.path);

            var bundleList = GetAssetBundlesList(settings);
            int sharedIndex = bundleList.FindIndex((AssetBundleBuild abb) => abb.assetBundleName == "AutoSharedBundle");
            if (sharedIndex >= 0)
            {
                bundleList.RemoveAt(sharedIndex);
            }
            var treeResult = AssetDependencyTree.ProcessDependencyTree(bundleList);

            BundleSetting autoBundle = settings.BundleSettings.Find((BundleSetting bs) => bs.BundleName == "AutoSharedBundle");
            if (autoBundle == null)
            {
                autoBundle = new BundleSetting();
                settings.BundleSettings.Add(autoBundle);
            }

            autoBundle.BundleName = "AutoSharedBundle";
            autoBundle.CompressBundle = true;
            autoBundle.assetNames = new List<string>();
            autoBundle.addressableNames = new List<string>();

            foreach (var bundle in treeResult.SharedBundles)
            {
                foreach(var name in bundle.assetNames)
                {
                    autoBundle.assetNames.Add(name);
                }

                foreach(var name in bundle.addressableNames)
                {
                    autoBundle.addressableNames.Add(name);
                }
            }

            WriteSharedBundleLog($"{Application.dataPath}/../", treeResult);
            if(!Application.isBatchMode)
            {
                Debug.Log($"Succeeded! Check {LogExpectedSharedBundleFileName} in your project root directory!");
            }

            //domain reloaded, we need to restore previous scene path
            var prevScenePath = EditorPrefs.GetString(tempPrevSceneKey, string.Empty);
            //back to previous scene as all processed scene's prefabs are unpacked.
            if(string.IsNullOrEmpty(prevScenePath))
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(prevScenePath);
            }
        }

        public static List<AssetBundleBuild> GetAssetBundlesList(AssetbundleBuildSettings settings)
        {
            var bundleList = new List<AssetBundleBuild>();

            foreach (var setting in settings.BundleSettings)
            {
                bundleList.Add(MakeAssetBundleBuild(setting));
            }

            if (settings.IncludeBundleSettingObjects)
            {
                string[] bundleSettingGuids = AssetDatabase.FindAssets("t:BundleSettingObject");
                if (bundleSettingGuids.Length > 0)
                {
                    for (int i = 0; i < bundleSettingGuids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(bundleSettingGuids[i]);
                        bundleList.Add(MakeAssetBundleBuild(AssetDatabase.LoadAssetAtPath<BundleSettingObject>(path).bundleSetting));
                    }
                }
            }

            return bundleList;
        }

        public static AssetBundleBuild MakeAssetBundleBuild(BundleSetting setting)
        {
            //collect assets
            var assetPathes = new List<string>();
            var loadPathes = new List<string>();

            //find folder
            if (setting.Folder.guid != "")
            {
                var folderPath = AssetDatabase.GUIDToAssetPath(setting.Folder.guid);
                if (!AssetDatabase.IsValidFolder(folderPath)) throw new Exception($"Could not found Path {folderPath} for {setting.BundleName}");

                Utility.GetFilesInDirectory(string.Empty, assetPathes, loadPathes, folderPath, setting.IncludeSubfolder);
            }
            else
            {
                assetPathes = setting.assetNames;
                loadPathes = setting.addressableNames;
            }
            if (assetPathes.Count == 0) Debug.LogWarning($"Could not found Any Assets for {setting.BundleName}");

            //make assetbundlebuild
            var newBundle = new AssetBundleBuild();
            newBundle.assetBundleName = setting.BundleName;
            newBundle.assetNames = assetPathes.ToArray();
            newBundle.addressableNames = loadPathes.ToArray();
            return newBundle;
        }

        public static void BuildAssetBundles(AssetbundleBuildSettings settings, BuildType buildType, BuildTarget buildTarget, string singleBundle = null)
        {
            if (!Application.isBatchMode)
            {
                //have to ask save current scene
                var saved = UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

                if (!saved)
                {
                    Debug.LogError("Build Failed! User Canceled");
                    return;
                }
            }

            var bundleList = GetAssetBundlesList(settings);

            var groupTarget = BuildPipeline.GetBuildTargetGroup(buildTarget);

            var outputPath = Utility.CombinePath(buildType == BuildType.Local ? settings.LocalOutputPath : settings.RemoteOutputPath, buildTarget.ToString());


            //generate sharedBundle if needed, and pre generate dependency
            var treeResult = AssetDependencyTree.ProcessDependencyTree(bundleList);

            if (settings.AutoCreateSharedBundles)
            {
                bundleList.AddRange(treeResult.SharedBundles);
            }

            var buildParams = new CustomBuildParameters(settings, buildTarget, groupTarget, outputPath, treeResult.BundleDependencies, buildType, singleBundle);

            buildParams.UseCache = !settings.ForceRebuild;

            if (buildParams.UseCache && settings.UseCacheServer)
            {
                buildParams.CacheServerHost = settings.CacheServerHost;
                buildParams.CacheServerPort = settings.CacheServerPort;
            }

            ContentPipeline.BuildCallbacks.PostPackingCallback += PostPackingForSelectiveBuild;
            AssetBundleBuild[] bundleArray = bundleList.ToArray();

            var returnCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(bundleArray), out var results);
            ContentPipeline.BuildCallbacks.PostPackingCallback -= PostPackingForSelectiveBuild;


            if (returnCode == ReturnCode.Success)
            {
                //only remote bundle build generates link.xml
                switch (buildType)
                {
                    case BuildType.Local:
                        WriteManifestFile(outputPath, results, buildTarget, settings.RemoteURL, singleBundle);
                        WriteLogFile(outputPath, results);
                        Debug.Log("Local bundle build succeeded!");
                        break;
                    case BuildType.Remote:
                        WriteManifestFile(outputPath, results, buildTarget, settings.RemoteURL, singleBundle);
                        WriteLogFile(outputPath, results);
                        var linkPath = TypeLinkerGenerator.Generate(settings, results);
                        Debug.Log($"Remote bundle build succeeded, \n {linkPath} updated!");
                        break;
                }
            }
            else
            {
                Debug.LogError($"Build Failed! Bundle build failed, \n Code : {returnCode}");
            }
        }

        public static void BuildAssetBundles(AssetbundleBuildSettings settings, BuildType buildType, string singleBundle = null)
        {
            BuildAssetBundles(settings, buildType, EditorUserBuildSettings.activeBuildTarget, singleBundle);
        }

        private static ReturnCode PostPackingForSelectiveBuild(IBuildParameters buildParams, IDependencyData dependencyData, IWriteData writeData)
        {
            var customBuildParams = buildParams as CustomBuildParameters;
            var depsDic = customBuildParams.DependencyDic;

            List<string> includedBundles;

            if(customBuildParams.CurrentBuildType == BuildType.Local)
            {
                //deps includes every local dependencies recursively
                includedBundles = customBuildParams.CurrentSettings.BundleSettings
                    .Where(setting => setting.IncludedInPlayer)
                    .Select(setting => setting.BundleName)
                    .SelectMany(bundleName => Utility.CollectBundleDependencies(depsDic, bundleName, true))
                    .Distinct()
                    .ToList();
            }
            //if not local build, we include everything
            else
            {
                includedBundles = depsDic.Keys.ToList();
            }

            //quick exit
            if (includedBundles == null || includedBundles.Count == 0)
            {
                Debug.Log("Nothing to build");
                writeData.WriteOperations.Clear();
                return ReturnCode.Success;
            }

            for (int i = writeData.WriteOperations.Count - 1; i >= 0; --i)
            {
                string bundleName;
                switch (writeData.WriteOperations[i])
                {
                    case SceneBundleWriteOperation sceneOperation:
                        bundleName = sceneOperation.Info.bundleName;
                        break;
                    case SceneDataWriteOperation sceneDataOperation:
                        var bundleWriteData = writeData as IBundleWriteData;
                        bundleName = bundleWriteData.FileToBundle[sceneDataOperation.Command.internalName];
                        break;
                    case AssetBundleWriteOperation assetBundleOperation:
                        bundleName = assetBundleOperation.Info.bundleName;
                        break;
                    default:
                        Debug.LogError("Unexpected write operation");
                        return ReturnCode.Error;
                }

                // if we do not want to build that bundle, remove the write operation from the list
                if (!includedBundles.Contains(bundleName) || (customBuildParams.SingleBundle != null && bundleName.StartsWith(customBuildParams.SingleBundle)))
                {
                    writeData.WriteOperations.RemoveAt(i);
                }
            }

            return ReturnCode.Success;
        }

        /// <summary>
        /// write manifest into target path.
        /// </summary>
        static void WriteManifestFile(string path, IBundleBuildResults bundleResults, BuildTarget target, string remoteURL, string singleBundle = null)
        {
            var manifest = new AssetbundleBuildManifest();
            manifest.BuildTarget = target.ToString();

            //we use unity provided dependency result for final check
            var deps = bundleResults.BundleInfos.ToDictionary(kv => kv.Key, kv => kv.Value.Dependencies.ToList());

            foreach (var result in bundleResults.BundleInfos)
            {
                var bundleInfo = new AssetbundleBuildManifest.BundleInfo();
                bundleInfo.BundleName = result.Key;
                bundleInfo.Dependencies = Utility.CollectBundleDependencies(deps, result.Key);
                bundleInfo.Hash = result.Value.Hash;
                bundleInfo.Size = new FileInfo(result.Value.FileName).Length;
                manifest.BundleInfos.Add(bundleInfo);
            }

            //sort by size
            manifest.BundleInfos.Sort((a, b) => b.Size.CompareTo(a.Size));
            var manifestString = JsonUtility.ToJson(manifest);
            manifest.GlobalHash = Hash128.Compute(manifestString);
            manifest.BuildTime = DateTime.UtcNow.Ticks;
            manifest.RemoteURL = remoteURL;
            manifest.Channel = AssetbundleBuildSettings.EditorInstance.Channel;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string manifestFileName = AssetbundleBuildSettings.ManifestFileName;
            if (singleBundle != null)
            {
                manifestFileName = $"{singleBundle}.json";
            }
            File.WriteAllText(Utility.CombinePath(path, manifestFileName), JsonUtility.ToJson(manifest, true));
        }

        static void WriteSharedBundleLog(string path, AssetDependencyTree.ProcessResult treeResult)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Build Time : {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            sb.AppendLine($"Possible shared bundles will be created..");
            sb.AppendLine();

            var sharedBundleDic = treeResult.SharedBundles.ToDictionary(ab => ab.assetBundleName, ab => ab.assetNames[0]);

            //find flatten deps which contains non-shared bundles
            var definedBundles = treeResult.BundleDependencies.Keys.Where(name => !sharedBundleDic.ContainsKey(name)).ToList();
            var depsOnlyDefined = definedBundles.ToDictionary(name => name, name => Utility.CollectBundleDependencies(treeResult.BundleDependencies, name));

            foreach(var kv in sharedBundleDic)
            {
                var bundleName = kv.Key;
                var assetPath = kv.Value;
                var referencedDefinedBundles = depsOnlyDefined.Where(pair => pair.Value.Contains(bundleName)).Select(pair => pair.Key).ToList();

                sb.AppendLine($"Shared_{AssetDatabase.AssetPathToGUID(assetPath)} - { assetPath } is referenced by");
                foreach(var refBundleName in referencedDefinedBundles) sb.AppendLine($"    - {refBundleName}");
                sb.AppendLine();
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            File.WriteAllText(Utility.CombinePath(path, LogExpectedSharedBundleFileName), sb.ToString());
        }


        /// <summary>
        /// write logs into target path.
        /// </summary>
        static void WriteLogFile(string path, IBundleBuildResults bundleResults)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Build Time : {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            sb.AppendLine();

            for (int i = 0; i < bundleResults.BundleInfos.Count; i++)
            {
                var bundleInfo = bundleResults.BundleInfos.ElementAt(i);
                var writeResult = bundleResults.WriteResults.ElementAt(i);
                sb.AppendLine($"----File Path : {bundleInfo.Value.FileName}----");
                var assetDic = new Dictionary<string, ulong>();
                foreach(var file in writeResult.Value.serializedObjects)
                {
                    //skip nonassettype
                    if (file.serializedObject.fileType == UnityEditor.Build.Content.FileType.NonAssetType) continue;

                    //gather size
                    var assetPath = AssetDatabase.GUIDToAssetPath(file.serializedObject.guid.ToString());
                    if (!assetDic.ContainsKey(assetPath))
                    {
                        assetDic.Add(assetPath, file.header.size);
                    }
                    else assetDic[assetPath] += file.header.size;
                }

                //sort by it's size
                var sortedAssets = assetDic.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key);

                foreach(var asset in sortedAssets)
                {
                    sb.AppendLine($"{(asset.Value * 0.000001f).ToString("0.00000").PadLeft(10)} mb - {asset.Key}");
                }

                sb.AppendLine();
            }

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            File.WriteAllText(Utility.CombinePath(path, LogFileName), sb.ToString());
        }
    }
}
