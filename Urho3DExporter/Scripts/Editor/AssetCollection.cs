﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Urho3DExporter
{
    public class AssetCollection : IEnumerable<AssetContext>
    {
        private readonly DestinationFolder _urhoDataPath;
        private readonly List<AssetContext> _assets;

        private readonly Dictionary<string, string> _meshPaths = new Dictionary<string, string>();

        private readonly Dictionary<string, string> _materialPaths = new Dictionary<string, string>();

        private readonly Dictionary<string, string> _texturePaths = new Dictionary<string, string>();

        public AssetCollection(DestinationFolder urhoDataPath, IEnumerable<AssetContext> assets)
        {
            _urhoDataPath = urhoDataPath;
            _assets = assets.ToList();

            foreach (var assetContext in assets.Where(_ => _.Type == typeof(Material)))
                AddMaterialPath(AssetDatabase.LoadAssetAtPath<Material>(assetContext.AssetPath),
                    assetContext.UrhoAssetName);
        }

        public void AddMeshPath(Mesh mesh, string fileName)
        {
            fileName = fileName.FixAssetSeparator();
            TryAdd(_meshPaths, mesh, mesh.name, fileName);
        }

        public bool TryAdd(Dictionary<string, string> values, Object asset, string name, string fileName)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            var id = path + "#" + name;
            if (values.ContainsKey(id))
                //Debug.LogError("Duplicate asset " + id);
                return false;
            values.Add(id, fileName);
            return true;
        }

        public bool TryGetMeshPath(Mesh sharedMesh, out string meshPath)
        {
            meshPath = null;
            if (sharedMesh == null)
                return false;
            var path = AssetDatabase.GetAssetPath(sharedMesh);
            var id = path + "#" + sharedMesh.name;
            return _meshPaths.TryGetValue(id, out meshPath);
        }

        public void AddMaterialPath(Material material, string fileName)
        {
            fileName = fileName.FixAssetSeparator();
            TryAdd(_materialPaths, material, material.name, fileName);
        }

        public bool TryGetMaterialPath(Material sharedMaterial, out string materialPath)
        {
            materialPath = null;
            if (sharedMaterial == null)
                return false;
            var path = AssetDatabase.GetAssetPath(sharedMaterial);
            var id = path + "#" + sharedMaterial.name;
            return _materialPaths.TryGetValue(id, out materialPath);
        }


        public void AddTexturePath(Texture texture, string fileName)
        {
            fileName = fileName.FixAssetSeparator();
            TryAdd(_texturePaths, texture, texture.name, fileName);
        }

        public bool TryGetTexturePath(Texture sharedTexture, out string texturePath)
        {
            texturePath = null;
            if (sharedTexture == null)
                return false;
            var path = AssetDatabase.GetAssetPath(sharedTexture);
            var id = path + "#" + sharedTexture.name;
            return _texturePaths.TryGetValue(id, out texturePath);
        }

        public IEnumerator<AssetContext> GetEnumerator()
        {
            return _assets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _assets).GetEnumerator();
        }
    }
}