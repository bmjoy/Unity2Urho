﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using Assets.Urho3DExporter.Scripts.Editor;
using UnityEditor;
using UnityEngine;

namespace Urho3DExporter
{
    public class TextureExporter : IExporter
    {
        private readonly AssetCollection _assets;
        private readonly TextureMetadataCollection _textureMetadata;

        public TextureExporter(AssetCollection assets, TextureMetadataCollection textureMetadata)
        {
            _assets = assets;
            _textureMetadata = textureMetadata;
        }

        public static float GetLuminance(Color32 rgb)
        {
            var r = rgb.r / 255.0f;
            var g = rgb.g / 255.0f;
            var b = rgb.b / 255.0f;
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        public static string GetTextureOutputName(string baseAssetName, TextureReferences reference)
        {
            switch (reference.Semantic)
            {
                case TextureSemantic.PBRMetallicGlossiness:
                    return ReplaceExtension(baseAssetName, ".MetallicRoughness.png");
                case TextureSemantic.PBRSpecularGlossiness:
                    return ReplaceExtension(baseAssetName, ".MetallicRoughness.png");
                case TextureSemantic.PBRDiffuse:
                    return ReplaceExtension(baseAssetName, ".BaseColor.png");
                default: return baseAssetName;
            }
        }

        private static string ReplaceExtension(string assetUrhoAssetName, string newExt)
        {
            var lastDot = assetUrhoAssetName.LastIndexOf('.');
            var lastSlash = assetUrhoAssetName.LastIndexOf('/');
            if (lastDot > lastSlash) return assetUrhoAssetName.Substring(0, lastDot) + newExt;

            return assetUrhoAssetName + newExt;
        }

        public void ExportAsset(AssetContext asset)
        {
            if (!File.Exists(asset.FullPath))
            {
                Debug.LogError("File " + asset.FullPath + " not found");
                return;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture>(asset.AssetPath);
            _assets.AddTexturePath(texture, asset.UrhoAssetName);

            var fullCopy = false;
            foreach (var reference in _textureMetadata.ResolveReferences(texture))
                switch (reference.Semantic)
                {
                    case TextureSemantic.PBRMetallicGlossiness:
                    {
                        TransformMetallicGlossiness(asset, texture, reference);
                        break;
                    }
                    case TextureSemantic.PBRSpecularGlossiness:
                    {
                        TransformSpecularGlossiness(asset, texture, reference);
                        break;
                    }
                    case TextureSemantic.PBRDiffuse:
                    {
                        TransformDiffuse(asset, texture, reference);
                        break;
                    }
                    default:
                    {
                        if (!fullCopy)
                        {
                            asset.DestinationFolder.CopyFile(asset.FullPath, asset.UrhoAssetName);
                            fullCopy = true;
                        }

                        break;
                    }
                }
        }

        private void EnsureReadableTexture(Texture2D texture)
        {
            if (null == texture) return;

            var assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (tImporter != null)
            {
                tImporter.textureType = TextureImporterType.Advanced;
                if (tImporter.isReadable != true)
                {
                    tImporter.isReadable = true;
                    AssetDatabase.ImportAsset(assetPath);
                    AssetDatabase.Refresh();
                }
            }
        }

        private void TransformDiffuse(AssetContext asset, Texture texture, TextureReferences reference)
        {
            var diffuse = texture as Texture2D;
            EnsureReadableTexture(diffuse);
            var specularGlossiness = reference.SmoothnessSource as Texture2D;
            EnsureReadableTexture(specularGlossiness);

            var metallicRoughMapName = GetTextureOutputName(asset.UrhoAssetName, reference);
            using (var fileStream = asset.DestinationFolder.Create(metallicRoughMapName))
            {
                if (fileStream != null)
                {
                    var specWidth = specularGlossiness?.width ?? 1;
                    var specHeight = specularGlossiness?.height ?? 1;
                    var smoothnessColors = specularGlossiness?.GetPixels32(0) ?? new[] {new Color32(0, 0, 0, 255)};
                    var width = Math.Max(diffuse.width, specWidth);
                    var height = Math.Max(diffuse.height, specHeight);
                    var tmpTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tmpTexture.hideFlags = HideFlags.HideAndDontSave;

                    var diffuseColors = diffuse.GetPixels32(0);
                    var pixels = new Color32[width * height];
                    var index = 0;
                    for (var y = 0; y < height; ++y)
                    for (var x = 0; x < width; ++x)
                    {
                        var specular = Get(smoothnessColors, specWidth, specHeight, x, y, width, height);
                        var diffuseColor = Get(diffuseColors, diffuse.width, diffuse.height, x, y, width, height);
                        pixels[index] = new Color(diffuseColor.r + specular.r, diffuseColor.g + specular.g,
                            diffuseColor.b + specular.b, diffuseColor.a);
                        ++index;
                    }

                    tmpTexture.SetPixels32(pixels, 0);
                    var bytes = tmpTexture.EncodeToPNG();
                    fileStream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        private void TransformMetallicGlossiness(AssetContext asset, Texture texture, TextureReferences reference)
        {
            var metallicGloss = texture as Texture2D;
            EnsureReadableTexture(metallicGloss);
            var smoothnessSource = reference.SmoothnessSource as Texture2D;
            EnsureReadableTexture(smoothnessSource);

            var metallicRoughMapName = GetTextureOutputName(asset.UrhoAssetName, reference);
            using (var fileStream = asset.DestinationFolder.Create(metallicRoughMapName))
            {
                if (fileStream != null)
                {
                    var width = Math.Max(metallicGloss.width, smoothnessSource.width);
                    var height = Math.Max(metallicGloss.height, smoothnessSource.height);
                    var tmpTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tmpTexture.hideFlags = HideFlags.HideAndDontSave;

                    var metallicColors = metallicGloss.GetPixels32(0);
                    var smoothnessColors = metallicGloss == smoothnessSource
                        ? metallicColors
                        : smoothnessSource.GetPixels32(0);
                    var pixels = new Color32[width * height];
                    var index = 0;
                    for (var y = 0; y < height; ++y)
                    for (var x = 0; x < width; ++x)
                    {
                        var r = 1.0f - Get(smoothnessColors, smoothnessSource.width, smoothnessSource.height, x, y,
                                    width, height).a;
                        var m = Get(metallicColors, metallicGloss.width, metallicGloss.height, x, y, width, height).r;
                        pixels[index] = new Color(r, m, 0, 1);
                        ++index;
                    }

                    tmpTexture.SetPixels32(pixels, 0);
                    var bytes = tmpTexture.EncodeToPNG();
                    fileStream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        private void TransformSpecularGlossiness(AssetContext asset, Texture texture, TextureReferences reference)
        {
            var specularGloss = texture as Texture2D;
            EnsureReadableTexture(specularGloss);
            var diffuse = reference.SmoothnessSource as Texture2D;
            EnsureReadableTexture(diffuse);
            var smoothnessSource =
                reference.SmoothnessTextureChannel == SmoothnessTextureChannel.MetallicOrSpecularAlpha
                    ? specularGloss
                    : diffuse;

            var metallicRoughMapName = GetTextureOutputName(asset.UrhoAssetName, reference);
            using (var fileStream = asset.DestinationFolder.Create(metallicRoughMapName))
            {
                if (fileStream != null)
                {
                    var width = Math.Max(specularGloss.width, smoothnessSource.width);
                    var height = Math.Max(specularGloss.height, smoothnessSource.height);
                    var tmpTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tmpTexture.hideFlags = HideFlags.HideAndDontSave;

                    var metallicColors = specularGloss.GetPixels32(0);
                    var diffuseColors = diffuse.GetPixels32(0);
                    var smoothnessColors = specularGloss == smoothnessSource
                        ? metallicColors
                        : smoothnessSource.GetPixels32(0);
                    var pixels = new Color32[width * height];
                    var index = 0;
                    for (var y = 0; y < height; ++y)
                    for (var x = 0; x < width; ++x)
                    {
                        var r = 1.0f - Get(smoothnessColors, smoothnessSource.width, smoothnessSource.height, x, y,
                                    width, height).a;
                        var d = GetLuminance(Get(diffuseColors, diffuse.width, diffuse.height, x, y, width, height));
                        var s = GetLuminance(Get(metallicColors, specularGloss.width, specularGloss.height, x, y, width,
                            height));
                        var m = s / (d + s);
                        pixels[index] = new Color(r, m, 0, 1);
                        ++index;
                    }

                    tmpTexture.SetPixels32(pixels, 0);
                    var bytes = tmpTexture.EncodeToPNG();
                    fileStream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color Get(Color32[] texture, int texWidth, int texHeight, int x, int y, int width, int height)
        {
            var xx = x * texWidth / width;
            var yy = y * texHeight / height;
            return texture[xx + yy * texWidth];
        }
    }
}