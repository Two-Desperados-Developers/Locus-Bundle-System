# Two Desperados Bundle System

[Two Desperados Bundle System Git Repository](https://github.com/Two-Desperados/Locus-Bundle-System.git)

## Locus Bundle System

Two Desperados Bundle System is forked from Locus Bundle System.
[Original Locus Bundle System Git Repository](https://github.com/locus84/Locus-Bundle-System)

# How it works

![Flow](https://viola-data.s3.us-west-2.amazonaws.com/BundleSystemDocumentation/Flow.png)

# Installation

### Install via Git URL

Add to **Packages / Manifest.json**
> For newest release version
```json
"com.locus.bundlesystem": "https://github.com/Two-Desperados/Locus-Bundle-System.git"
```
		
> For using specific release version
```json
"com.locus.bundlesystem": "https://github.com/Two-Desperados/Locus-Bundle-System.git#1.1.13"
```

### Existing Install

Find active settings:
![FindActiveSetting](https://user-images.githubusercontent.com/6591432/73616927-a5c04c00-465c-11ea-9689-3b8e5cdd4970.png)\

Set active settings option:
![ActiveSetting](https://user-images.githubusercontent.com/6591432/73616924-a527b580-465c-11ea-8cff-a4bfa60faf0a.png)
Note: visible only if this settings is not already set to active.

### Fresh Install

Create AssetBundleSettings ScriptableObject using Context Menu.
This object can be anywhere under Assets folder (recommended - *Assets/Bundles/AssetbundleBuildSettings.asset*)

### Updating Bundle System

- Commit changes to repository
- Create a new release
- Change release version in Unity project **Packages > Manifest.json**

![UpdateRelease](https://viola-data.s3.us-west-2.amazonaws.com/BundleSystemDocumentation/UpdateRelease.png)

# Asset Bundle Settings

![BundleSettingInspector](https://viola-data.s3.us-west-2.amazonaws.com/BundleSystemDocumentation/BundleBuildSettings.png)

1. *Shared Bundles Options*

| Option | Description |
| ------ | ------ |
| **Auto Create Shared Bundles** | On building assets, dependencies between multiple asset bundles will be packed into a shared bundle file. This means there will be **no** duplicated assets. ***Note: if a list goes beyond 255, the game crashes on start.*** |
| **Get Expected Sharedbundle List** | On click, it creates a file ExpectedSharedBundles.txt with all expected shared assets. |
| **Pack Into Shared Bundles** | On click, moves all shared assets from their current directory to SharedLocal or SharedRemote folders (which are used as separate bundles). Uses AssetDatabase. This is a fix so we don't have a large number of individual shared assets, which slows down loading. Therefore, we use SharedLocal and SharedRemote bundles to have 2 bundles instead of more than 200. ***Note: use wisely and only when needed, preferably on a clean commit.*** |

ExpectedSharedBundles.txt example of a shared asset:
```
Shared_545626214bd4044bf9ca820ad839e18b - Assets/Images/Events/RosesGlow.png is referenced by
    - EASTERSkins
    - NORMALSkins
```

2. *Bundle List*

| Option | Description |
| ------ | ------ |
| **BundleName** | Assetbundle's name which you should provide when loading object from AssetBundles. |
| **Included In Player** | If ***true***, this bundle will be shipped with player(also can be updated). |
| **Folder** | Drag or select folder, assets under that folder will be packed into this bundle. |
| **Include Subfolder** | If ***true***, will search assets from subfolders recurviely, your asset name when loading will be [SubFolderPath]/[AssetName] |
| **Compress Bundle** | If ***true***, it will use LMZA compression. otherwise LZ4 is used. Shipped local bundles will be always LZ4 by default. |
   
3. *Output Folder and URL*

| Option | Description |
| ------ | ------ |
| **Remote Output Folder** | Remote bundle build output path. |
| **Local Output Folder** | Local bundle build output path. |
| **Remote URL** | Remote URL for remote content delivery. |

4. *Editor Functionalities*

| Option | Description |
| ------ | ------ |
| **Emulate In Editor** | Use and Update actual assetbundles like you do in built player. ***Note: Emulating in Editor causes shaders to not load properly.*** |
| **Emulate Without Remote URL** | If ***true***, remote bundle will be loaded from remote output path, useful when testing without uploading files to CDN (or the CDN is not ready yet). |
| **Clean Cache In Editor** | If ***true***, clean up cache when initializing. |
| **Force Rebuild** | Disables BuildCache (When Scriptable Build Pipline ignores your modification, turn it on. It barely happens though) |
   
5. *Other Utilities*

| Option | Description |
| ------ | ------ |
| **Cache Server**| Cache server setting for faster bundle build (you need seperate Cache server along with asset cache server). |
| **Ftp** | if you have FTP information, upload your remote bundle with a single click. |

6. *Building Utilities.*

| Option | Description |
| ------ | ------ |
| **Use All Local**| If ***true***, all bundles will be packed as local and included in app build. |
| **Build Remote** | On click, build all bundles to remote bundle folder. ***Note: Local bundles are included in the app build, but both Local & Remote can be updated via Bundle System.*** |
| **Build Local** | On click, build only local bundles to local bundle folder. |

# Usage Examples

## BundleManager.cs

### Initialize

Initialize is required for accessing assets that are packed in bundles.
`public static BundleAsyncOperation Initialize(bool autoReloadBundle = true)`

| Parameter | Description |
| ------ | ------ |
| **autoReloadBundle**| If ***true***, bundles will be automatically unloaded when no longer needed. Since we use bundles to get content that needs to be accessible throughout the game, **we are using false**, because we don’t want a skin to be unloaded mid gameplay. |

## BundleController.cs
### Download

`public IEnumerator DownloadAssets(List<string> required, Action<float> onUpdate, bool useCachedManifest = false, bool unsub = false)`

| Parameter | Description |
| ------ | ------ |
| **List<string> required**| List of asset bundle names that are required to be downloaded. TODO: difference between hard and soft list. |
| **Action<float> onUpdate**| Returns value from 0f to 1f, based on download progress. |
| **bool useCachedManifest**| Force use only already cached manifest (via PlayerPrefs “CachedManifest” key) without checking for manifest updates. Stops download coroutine if there is no cached manifest. Set true for soft update. |
| **bool unsub**| Remove scene change after download finishes. Set true for soft update. |

### Load

`public static T Load<T>(string bundleName, string assetName) where T : UnityEngine.Object`

| Parameter | Description |
| ------ | ------ |
| **bundleName** | Specify name of the bundle where to look for an asset. |
| **assetName** | Specify name of the asset in the bundle of given type T, returns asset of that type. Null if not found. |

### LoadWithSubAssets

`public static T[] LoadWithSubAssets<T>(string bundleName, string assetName) where T : UnityEngine.Object`

| Parameter | Description |
| ------ | ------ |
| **bundleName** | Specify name of the bundle where to look for assets. |
| **assetName** | Specify name of the asset in the bundle of given type T, returns all subassets from that asset. Null if not found. *For example, get all sprites from a specific spliced texture asset.* |

### LoadFromSubDirectory

`public static IEnumerable<T> LoadFromSubDirectory<T>(string bundleName, string directoryName) where T : UnityEngine.Object`

| Parameter | Description |
| ------ | ------ |
| **bundleName** | Specify name of the bundle where to look for assets. |
| **directoryName** | Specify directory name in the bundle of given type T, returns all assets that share the start of path string. Null if not found. *For example, get all AudioClips in 'Sounds' folder from bundle 'FX'.* |

### LoadScene

`public static void LoadScene(string bundleName, string sceneName, LoadSceneMode mode)`

| Parameter | Description |
| ------ | ------ |
| **bundleName** | Specify name of the bundle where to look for scenes. ***Note: bundle cannot contain a mix of scenes and other types of assets.*** |
| **sceneName** | Specify name of the scene to be loaded. |
| **mode** | Unity [modes](https://docs.unity3d.com/ScriptReference/SceneManagement.LoadSceneMode.html) of scene loading. |

### IsAssetExist

`public static bool IsAssetExist(string bundleName, string assetName)`

| Parameter | Description |
| ------ | ------ |
| **bundleName** | Specify name of the bundle where to look for an asset. |
| **assetName** | Specify name of the asset in the bundle of given type T, returns true if it exists. |

## BundleSystemUtility.cs ???

### IsAssetExist

`public static bool IsAssetExist(string bundleName, string assetName)`

| Function | Description |
| ------ | ------ |
| **BundleExportSprites** | Get all dependencies from an asset and export all textures to a folder. |
| **BundleExportLocalization** | Get all Texts from WSkin and export them to a CSV file. (VIOLA SPECIFIC???) |

### Other notable uses

| Function | Description |
| ------ | ------ |
| **LoadAsync** | Same as Load, but async. |
| **LoadSceneAsync** | Same as LoadScene, but async. |
| **Instantiate** | Better memory management for instantiating objects from bundles. |

> For more examples, check Locus Bundle System git repo readme.

# Required Bundle List

Required Bundle List of bundle names should be implemented specifically for each game.

| Game | Description |
| ------ | ------ |
| **Viola's Quest** | Uses DynamoDB. |

# CDN / Server

Content for all games should all be on the same server. All games should use same Pull Zones, but different Storage folders.

| Game | Server |
| ------ | ------ |
| **Viola's Quest** | Bunny.net |

# Versioning

Example of CDN folder structure for Viola's Quest.

![Versioning](https://viola-data.s3.us-west-2.amazonaws.com/BundleSystemDocumentation/VersionFolders.png)
