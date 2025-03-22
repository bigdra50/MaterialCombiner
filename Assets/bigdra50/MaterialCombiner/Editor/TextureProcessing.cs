#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MaterialCombiner.Editor
{
    /// <summary>
    /// テクスチャ処理に関連する機能を提供するクラス
    /// </summary>
    public static class TextureProcessing
    {
        /// <summary>
        /// マテリアルからテクスチャを取得する関数
        /// </summary>
        public static Func<Material, bool, string, Texture2D> GetTextureFromMaterial =>
            (material, useMainTextureByDefault, customPropertyName) =>
            {
                if (material == null) return null;

                Texture2D texture = null;

                if (useMainTextureByDefault)
                {
                    // デフォルトでmainTextureを使用
                }
                else
                {
                    // 指定されたカスタムプロパティを使用
                    if (material.HasProperty(customPropertyName))
                    {
                        texture = material.GetTexture(customPropertyName) as Texture2D;
                        if (texture != null)
                        {
                            Debug.Log($"マテリアル {material.name} でテクスチャプロパティ {customPropertyName} を使用");
                        }
                    }

                    // バックアップとしてmainTextureもチェック
                    if (texture != null || material.mainTexture == null) return texture;
                }

                texture = material.mainTexture as Texture2D;
                if (texture != null)
                {
                    Debug.Log($"マテリアル {material.name} でmainTextureを使用");
                }

                return texture;
            };

        /// <summary>
        /// マテリアルからテクスチャを抽出する関数を生成
        /// </summary>
        public static Func<Material, Texture2D> CreateTextureExtractor(MaterialCombinerConfig config) =>
            material => GetTextureFromMaterial(material, config.UseMainTextureByDefault, config.CustomTexturePropertyName);

        /// <summary>
        /// 読み取り不可能なテクスチャをコピーして読み取り可能にする
        /// </summary>
        public static Texture2D MakeTextureReadable(Texture2D source)
        {
            if (source == null) return null;
            if (source.isReadable) return source;

            try
            {
                var rt = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear
                );

                Graphics.Blit(source, rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;

                var readableCopy = new Texture2D(source.width, source.height);
                readableCopy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readableCopy.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                return readableCopy;
            }
            catch (Exception e)
            {
                Debug.LogError($"テクスチャのコピーに失敗しました: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// デフォルトテクスチャ（白テクスチャ）を作成
        /// </summary>
        public static Texture2D CreateDefaultTexture(int width, int height)
        {
            var defaultTex = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (var p = 0; p < pixels.Length; p++)
            {
                pixels[p] = Color.white;
            }

            defaultTex.SetPixels(pixels);
            defaultTex.Apply();
            return defaultTex;
        }

        /// <summary>
        /// マテリアルとテクスチャのマッピング結果を表す構造体
        /// </summary>
        public readonly struct MaterialTextureMapResult
        {
            public readonly List<Texture2D> Textures;
            public readonly Dictionary<int, int> MaterialToTextureIndex;

            public MaterialTextureMapResult(List<Texture2D> textures, Dictionary<int, int> materialToTextureIndex)
            {
                Textures = textures;
                MaterialToTextureIndex = materialToTextureIndex;
            }
        }

        /// <summary>
        /// マテリアルからテクスチャを処理
        /// </summary>
        public static MaterialTextureMapResult ProcessMaterialTextures(
            Material[] materials,
            Func<Material, Texture2D> getTexture)
        {
            var textures = new List<Texture2D>();
            var materialToTextureIndex = new Dictionary<int, int>();

            for (var i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                var texture = getTexture(mat);

                if (texture != null)
                {
                    // 読み取り専用テクスチャの場合、コピーを作成
                    if (texture.isReadable == false)
                    {
                        Debug.LogWarning($"テクスチャ {texture.name} は読み取り専用です。コピーを作成します。");
                        var readableTexture = MakeTextureReadable(texture);
                        if (readableTexture != null)
                        {
                            texture = readableTexture;
                        }
                    }
                }
                else
                {
                    // テクスチャがない場合は白テクスチャを作成
                    texture = CreateDefaultTexture(64, 64);
                    Debug.LogWarning($"マテリアル {i} にテクスチャがありません。デフォルトテクスチャを使用します。");
                }

                textures.Add(texture);
                materialToTextureIndex[i] = textures.Count - 1;
            }

            return new MaterialTextureMapResult(textures, materialToTextureIndex);
        }

        /// <summary>
        /// テクスチャアトラス作成結果を表す構造体
        /// </summary>
        public readonly struct TextureAtlasResult
        {
            public readonly Texture2D Atlas;
            public readonly Rect[] UVRects;

            public TextureAtlasResult(Texture2D atlas, Rect[] uvRects)
            {
                Atlas = atlas;
                UVRects = uvRects;
            }
        }

        /// <summary>
        /// テクスチャアトラスを作成
        /// </summary>
        public static TextureAtlasResult CreateTextureAtlas(
            List<Texture2D> textures,
            int atlasSize,
            int padding,
            string meshName,
            string outputPath)
        {
            // テクスチャアトラスを作成
            var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
            var uvRects = atlas.PackTextures(textures.ToArray(), padding, atlasSize);

            // テクスチャを保存
            var safeMeshName = System.Text.RegularExpressions.Regex.Replace(meshName, @"[^\w\-]", "_");
            var atlasPath = $"{outputPath}/{safeMeshName}_Atlas.png";
            var pngData = atlas.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + atlasPath), pngData);
            AssetDatabase.Refresh();

            // テクスチャインポート設定を調整
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            // 保存したテクスチャをロード
            var savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            return new TextureAtlasResult(savedAtlas, uvRects);
        }
    }
}
#endif