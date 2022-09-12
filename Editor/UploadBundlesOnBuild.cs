using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System;

namespace BundleSystem
{
    public class BuildHelper
    {
        public static void BuildAndUploadBundles()
        {
            string vers = PlayerSettings.bundleVersion;
            var settings = AssetbundleBuildSettings.EditorInstance;
            settings.RemoteURL = settings.RemoteUrlBase + vers + "/";

            string bundleUrl = BundleSystem.Utility.CombinePath(settings.BunnyApiUrl, vers) + "/";
            if (EditorUtil.GetRequest(bundleUrl, out string response, new (string, string)[] { ("AccessKey", settings.AccessKey) }))
            {

                if (response.Length > 5)
                    throw new Exception("Version already exists!");
 
                var local = BundleSystem.Utility.CombinePath(settings.LocalOutputPath, GetPlatform());
                var remote = BundleSystem.Utility.CombinePath(settings.RemoteOutputPath, GetPlatform());
                
                
                if (!Directory.Exists(local))
                    Directory.CreateDirectory(local);
                
                
                if (!Directory.Exists(remote))
                    Directory.CreateDirectory(remote);
                
                
                var dirLocalInfo = new DirectoryInfo(local);
                var dirRemoteInfo = new DirectoryInfo(remote);

                foreach (var file in dirRemoteInfo.GetFiles())
                {
                    file.Delete();
                }
                foreach (var file in dirLocalInfo.GetFiles())
                {
                    file.Delete();
                }

                AssetbundleBuilder.BuildAssetBundles(settings, BuildType.Local, false);

                AssetbundleBuilder.BuildAssetBundles(settings, BuildType.Remote, false);

                foreach (var file in dirRemoteInfo.GetFiles())
                {
                    if (EditorUtil.PutRequest(BundleSystem.Utility.CombinePath(bundleUrl, GetPlatform(), file.Name), File.ReadAllBytes(file.FullName), out string response2, new (string, string)[] { ("AccessKey", settings.AccessKey) }))
                    {
                        UnityEngine.Debug.Log("Uploaded: " + file.FullName);
                    }
                }
            }
        }
        private static string GetPlatform()
        {
#if UNITY_ANDROID && !STORE_AMAZON && !XIAOMI
        return "Android";
#elif UNITY_IPHONE
        return "iOS";
#elif UNITY_ANDROID && STORE_AMAZON && !XIAOMI
        return "Amazon";
#elif UNITY_WEBGL
        return "WebGL";
#else
            throw new Exception("Could not determine store to build!");
#endif
        }
    }
}
