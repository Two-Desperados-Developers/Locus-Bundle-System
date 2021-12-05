﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BundleSystem
{
    public static partial class BundleManager
    {
#if UNITY_EDITOR
        private static AssetbundleBuildSettings s_EditorBuildSettings;
        private static EditorAssetMap s_EditorAssetMap;

        static void SetupAssetdatabaseUsage()
        {

            s_EditorBuildSettings = AssetbundleBuildSettings.EditorInstance;
            if (s_EditorBuildSettings == null || !s_EditorBuildSettings.IsValid()) throw new System.Exception("AssetbundleBuildSetting is not valid");

            if (s_EditorBuildSettings.CleanCacheInEditor)
            {
                Caching.ClearCache();
            }

            UseAssetDatabase = !s_EditorBuildSettings.EmulateInEditor;

            //create editor asset map
            if(UseAssetDatabase)
            {
                s_EditorAssetMap = new EditorAssetMap(s_EditorBuildSettings);
                //set initialied so it does not need explit call initialzed when using aassetdatabase
                Initialized = true;
            }
        }

        public static void SetupApiTestSettings(AssetbundleBuildSettings settings = null)
        {
            if(Application.isPlaying) throw new System.Exception("This funcion cannot be called while playing!");
            if(settings == null) settings = AssetbundleBuildSettings.EditorInstance;
            if(settings == null || !settings.IsValid()) throw new System.Exception("AssetbundleBuildSetting is not valid");
            UseAssetDatabase = true;
            //create editor asset map only for testing
            s_EditorAssetMap = new EditorAssetMap(settings);
        }

        private static void EnsureAssetDatabase()
        {
            if(!Application.isPlaying && s_EditorAssetMap == null) throw new System.Exception("EditorAssetMap is null, try call SetupApiTestSettings before calling actual api in non-play mode");
        }
#endif

        public static T[] LoadAll<T>(string bundleName) where T : Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase)
            {
                EnsureAssetDatabase();
                var assets = s_EditorAssetMap.GetAssetPaths(bundleName);
                if (assets.Count == 0) return new T[0];

                var typeExpected = typeof(T);
                var foundList = new List<T>(assets.Count);

                for (int i = 0; i < assets.Count; i++)
                {
                    var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assets[i]);
                    if (loaded == null) continue;
                    foundList.Add(loaded);
                }
                return foundList.ToArray();
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return new T[0];
            var loadedAssets = foundBundle.Bundle.LoadAllAssets<T>();
            TrackObjectsInternal(loadedAssets, foundBundle);
            return loadedAssets;
        }

        /// <summary>
        /// Load asset from any loaded bundles.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public static T Load<T>(string assetName) where  T: UnityEngine.Object
        {
            foreach(var bundle in s_AssetBundles)
            {
                if (!bundle.Value.Bundle.isStreamedSceneAssetBundle)
                {
                    var loadedAsset = Load<T>(bundle.Key, assetName);

                    if (loadedAsset != null)
                    {
                        return loadedAsset;
                    }
                }
            }
            return null;
        }

        public static T Load<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorAssetMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return null; //asset not exist
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return null;
            var loadedAsset = foundBundle.Bundle.LoadAsset<T>(assetName);
            if(loadedAsset != null) TrackObjectInternal(loadedAsset, foundBundle);
            return loadedAsset;
        }

        
        public static T[] LoadWithSubAssets<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorAssetMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return new T[0];
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                return assets.Select(a => a as T).Where(a => a != null).ToArray();
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return new T[0];
            var loadedAssets = foundBundle.Bundle.LoadAssetWithSubAssets<T>(assetName);
            TrackObjectsInternal(loadedAssets, foundBundle);
            return loadedAssets;
        }


        public static BundleRequest<T> LoadAsync<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                var assetPath = s_EditorAssetMap.GetAssetPath<T>(bundleName, assetName);
                if(string.IsNullOrEmpty(assetPath)) return new BundleRequest<T>((T)null); //asset not exist
                return new BundleRequest<T>(UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return new BundleRequest<T>((T)null); //asset not exist
            var request = foundBundle.Bundle.LoadAssetAsync<T>(assetName);
            //need to keep bundle while loading, so we retain before load, release after load
            RetainBundleInternal(foundBundle, 1);
            request.completed += op => AsyncAssetLoaded(request, foundBundle);
            return new BundleRequest<T>(request);
        }

        private static void AsyncAssetLoaded(AssetBundleRequest request, LoadedBundle loadedBundle)
        {
            if(request.asset != null)
            {
                TrackObjectInternal(request.asset, loadedBundle);
            }
            
            //because we did increase ref count in loadasync function, it need to be released
            ReleaseBundleInternal(loadedBundle, 1);
        }

        public static void LoadScene(BundledAssetPath path, LoadSceneMode mode)
        {
            LoadScene(path.BundleName, path.AssetName, mode);
        }

        public static void LoadScene(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) throw new System.Exception("This function does not support non-playing mode!");
            if (UseAssetDatabase)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorAssetMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return; // scene does not exist
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(mode));
                return;
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            SceneManager.LoadScene(Path.GetFileName(sceneName), mode);
        }
        
        public static AsyncOperation LoadSceneAsync(BundledAssetPath path, LoadSceneMode mode)
        {
            return LoadSceneAsync(path.BundleName, path.AssetName, mode);
        }

        public static AsyncOperation LoadSceneAsync(string bundleName, string sceneName, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) throw new System.Exception("This function does not support non-playing mode!");
            if (UseAssetDatabase)
            {
                EnsureAssetDatabase();
                var scenePath = s_EditorAssetMap.GetScenePath(bundleName, sceneName);
                if(string.IsNullOrEmpty(scenePath)) return null; // scene does not exist
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(mode));
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");

            //like default scene load functionality, we return null if something went wrong
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) 
            {
                Debug.LogError("Bundle you requested could not be found");
                return null;
            }

            //need to keep bundle while loading, so we retain before load, release after load
            var aop = SceneManager.LoadSceneAsync(Path.GetFileName(sceneName), mode);
            if(aop != null)
            {
                RetainBundleInternal(foundBundle, 1);
                aop.completed += op => ReleaseBundleInternal(foundBundle, 1);
            }

            return aop;
        }

        public static bool IsAssetExist(string bundleName, string assetName)
        {
#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return s_EditorAssetMap.IsAssetExist(bundleName, assetName);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            if (!s_AssetBundles.TryGetValue(bundleName, out var foundBundle)) return false;
            return foundBundle.Bundle.Contains(assetName);
        }
        
        public static GameObject Instantiate(GameObject original)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");

#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Transform parent)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");
            if(parent == null) throw new System.Exception("The parent transform is null");

#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, parent);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, parent);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Transform parent, bool instantiateInWorldSpace)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");
            if(parent == null) throw new System.Exception("The parent transform is null");

#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, parent, instantiateInWorldSpace);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, parent, instantiateInWorldSpace);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");

#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, position, rotation);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, position, rotation);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }

        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            if(original == null) throw new System.Exception("The gameobject you want instantiate is null");
            if(parent == null) throw new System.Exception("The parent transform is null");

#if UNITY_EDITOR
            if (UseAssetDatabase) 
            {
                EnsureAssetDatabase();
                return GameObject.Instantiate(original, position, rotation, parent);
            }
#endif
            if(!Initialized) throw new System.Exception("BundleManager not initialized, try initialize first!");
            var id = original.GetInstanceID();
            if (original.scene.name != null || !s_TrackingObjects.TryGetValue(id, out var tracking)) throw new System.Exception("Object must be valid bundle object");
            var instantiated = GameObject.Instantiate(original, position, rotation, parent);
            var tupleKey = new TupleObjectKey(instantiated, original);
            s_TrackingOwners.Add(tupleKey, new TrackingOwner(instantiated, original));
            tracking.RefCount++; //increase refCount
            s_TrackingObjects[id] = tracking;
            return instantiated;
        }
    }

    /// <summary>
    /// this class is for simulating assetbundle request in editor.
    /// using this class we can provide unified structure.
    /// </summary>
    public class BundleRequest<T> : CustomYieldInstruction, System.IDisposable where T : Object
    {
        AssetBundleRequest mRequest;
        T mLoadedAsset;

        /// <summary>
        /// actual assetbundle request warpper
        /// </summary>
        /// <param name="request"></param>
        public BundleRequest(AssetBundleRequest request)
        {
            mRequest = request;
        }

        /// <summary>
        /// create already ended bundle request for editor use
        /// </summary>
        /// <param name="loadedAsset"></param>
        public BundleRequest(T loadedAsset)
        {
            mLoadedAsset = loadedAsset;
        }

        //provide similar apis
        public override bool keepWaiting => mRequest == null ? false : !mRequest.isDone;
        public bool IsDone => mRequest == null ? true : mRequest.isDone;
        public T Asset => mRequest == null ? mLoadedAsset : mRequest.asset as T;
        public float Progress => mRequest == null ? 1f : mRequest.progress;

        public void Dispose()
        {
            if(mRequest != null)
            {
                if(mRequest.isDone)
                {
                    if (mRequest.asset != null) BundleManager.ReleaseObject(mRequest.asset);
                }
                else
                {
                    mRequest.completed += op =>
                    {
                        if(mRequest.asset != null) BundleManager.ReleaseObject(mRequest.asset);
                    };
                }
            }
        }
    }


    /// <summary>
    /// assetbundle update
    /// </summary>
    public class BundleAsyncOperation<T> : BundleAsyncOperation
    {
        public T Result;
    }

    public class BundleAsyncOperation : CustomYieldInstruction
    {
        public bool IsDone => ErrorCode != BundleErrorCode.NotFinished;
        public bool Succeeded => ErrorCode == BundleErrorCode.Success;
        public BundleErrorCode ErrorCode { get; private set; } = BundleErrorCode.NotFinished;
        public int TotalCount { get; private set; } = 0;
        public int CurrentCount { get; private set; } = -1;
        public float Progress { get; private set; } = 0f;
        public bool CurrentlyLoadingFromCache { get; private set; } = false;
        public bool IsCancelled => ErrorCode == BundleErrorCode.Cancelled;
        public string CurrentBundleName { get; private set; } = "";

        internal void SetCachedBundle(bool cached)
        {
            CurrentlyLoadingFromCache = cached;
        }

        internal void SetIndexLength(int total)
        {
            TotalCount = total;
        }

        internal void SetCurrentIndex(int current)
        {
            CurrentCount = current;
        }

        internal void SetProgress(float progress)
        {
            Progress = progress;
        }

        internal void SetBundleName(string name)
        {
            CurrentBundleName = name;
        }

        internal void Done(BundleErrorCode code)
        {
            if (code == BundleErrorCode.Success)
            {
                CurrentCount = TotalCount;
                Progress = 1f;
            }
            ErrorCode = code;
        }

        public void Cancel()
        {
            if(IsDone) throw new System.Exception("Operation has been dont. can't be cancelled");
            ErrorCode = BundleErrorCode.Cancelled;
        }

        public override bool keepWaiting => !IsDone;
    }

    public enum BundleErrorCode
    {
        NotFinished = -1,
        Success = 0,
        NotInitialized = 1,
        NetworkError = 2,
        ManifestParseError = 3,
        Cancelled = 4,
    }
}