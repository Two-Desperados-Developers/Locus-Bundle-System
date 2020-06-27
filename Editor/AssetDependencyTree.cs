﻿using System.Collections.Generic;
using UnityEditor;

namespace BundleSystem
{
    /// <summary>
    /// this class finds out duplicated assets
    /// and make them into one single shared bundle one by one(to reduce bundle rebuild)
    /// so that there would be no asset duplicated
    /// </summary>
    public static class AssetDependencyTree
    {
        public static void AppendSharedBundles(List<AssetBundleBuild> definedBundles)
        {
            var context = new Context();
            foreach(var bundle in definedBundles)
            {
                foreach(var asset in bundle.assetNames)
                {
                    var rootNode = new RootNode(asset, bundle.assetBundleName, false);
                    context.RootNodes.Add(asset, rootNode);
                    rootNode.CollectChildNodes(context);
                }
            }

            foreach(var sharedRootNode in context.ResultSharedNodes)
            {
                var assetNames = new string[] { sharedRootNode.Path };
                var bundleDefinition = new AssetBundleBuild()
                {
                    assetBundleName = sharedRootNode.BundleName,
                    assetNames = assetNames,
                    addressableNames = assetNames
                };
                definedBundles.Add(bundleDefinition);
            }
        }

        //actual node tree context
        public class Context
        {
            public Dictionary<string, RootNode> RootNodes = new Dictionary<string, RootNode>();
            public Dictionary<string, Node> IndirectNodes = new Dictionary<string, Node>();
            public List<RootNode> ResultSharedNodes = new List<RootNode>();
        }

        public class RootNode : Node
        {
            public string BundleName { get; private set; }
            public bool IsShared { get; private set; }
            public HashSet<string> ReferencedBundleNames = new HashSet<string>();

            public RootNode(string path, string bundleName, bool isShared) : base(path, null)
            {
                IsShared = isShared;
                BundleName = bundleName;
                Root = this;
            }
        }

        public class Node
        {
            public RootNode Root { get; protected set; }
            public string Path { get; private set; }
            public Dictionary<string, Node> Children = new Dictionary<string, Node>();
            public bool IsRoot => Root == this;
            public bool HasChild => Children.Count > 0;

            public Node(string path, RootNode root)
            {
                Root = root;
                Path = path;
            }

            public void RemoveFromTree(Context context)
            {
                context.IndirectNodes.Remove(Path);
                foreach (var kv in Children) kv.Value.RemoveFromTree(context);
            }

            public void CollectChildNodes(Context context)
            {
                var childDeps = AssetDatabase.GetDependencies(Path, false);
                foreach (var child in childDeps)
                {
                    //is not bundled file
                    if (!AssetbundleBuildSettings.IsAssetCanBundled(child)) continue;

                    //already root node, wont be included multiple times
                    if (context.RootNodes.TryGetValue(child, out var rootNode))
                    {
                        rootNode.ReferencedBundleNames.Add(child);
                        continue;
                    }

                    //check if it's already indirect node (skip if it's same bundle)
                    //circular dependency will be blocked by indirect check
                    if (context.IndirectNodes.TryGetValue(child, out var node))
                    {
                        if (node.Root.BundleName != Root.BundleName)
                        {
                            node.RemoveFromTree(context);
                            var newName = $"Shared_{AssetDatabase.AssetPathToGUID(child)}";
                            var newRoot = new RootNode(child, newName, true);

                            //add deps
                            newRoot.ReferencedBundleNames.Add(node.Root.BundleName);
                            newRoot.ReferencedBundleNames.Add(Root.BundleName);

                            context.RootNodes.Add(child, newRoot);
                            context.ResultSharedNodes.Add(newRoot);
                            //is it okay to do here?
                            newRoot.CollectChildNodes(context);
                        }
                        continue;
                    }

                    //if not, add to indirect node
                    var childNode = new Node(child, Root);
                    context.IndirectNodes.Add(child, childNode);
                    childNode.CollectChildNodes(context);
                }
            }
        }
    }
}