#if UNITY_EDITOR
using System.Text.RegularExpressions;
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
// MakeTextureReadable メソッドの修正バージョン - sRGB問題対応
public static Texture2D MakeTextureReadable(Texture2D source)
{
    if (source == null) return null;
    if (source.isReadable) return source;

    try
    {
        // RenderTexture の作成方法を変更
        // sRGB プロパティを直接設定するのではなく、RenderTextureReadWrite 列挙型を使用
        RenderTextureReadWrite colorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear 
            ? RenderTextureReadWrite.Linear 
            : RenderTextureReadWrite.sRGB;
            
        RenderTexture rt = RenderTexture.GetTemporary(
            source.width, 
            source.height, 
            0, 
            RenderTextureFormat.DefaultHDR, 
            colorSpace); // ここでカラースペースを指定
            
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.Create();

        // 元のテクスチャのフィルタリングモードを保存
        FilterMode originalFilterMode = source.filterMode;
        // ポイントフィルタリングに設定して正確なピクセル値を保持
        source.filterMode = FilterMode.Point;
        
        // 単純なブリット処理
        Graphics.Blit(source, rt);
        
        // 元のフィルタリングモードを復元
        source.filterMode = originalFilterMode;
        
        // 現在のアクティブなRenderTextureを保存
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        
        // 同じフォーマットで新しいテクスチャを作成
        Texture2D readableCopy = new Texture2D(source.width, source.height, 
            TextureFormat.RGBA32, source.mipmapCount > 1, QualitySettings.activeColorSpace == ColorSpace.Linear);
        readableCopy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readableCopy.Apply(true); // mipmapを生成
        
        // 元のRenderTextureを復元し、一時的なものを解放
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        
        Debug.Log($"テクスチャをreadableにコピーしました: {source.name} -> {readableCopy.width}x{readableCopy.height}");
        
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
            bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Texture2D defaultTex = new Texture2D(width, height, TextureFormat.RGBA32, false, isLinear);
    
            Color[] pixels = new Color[width * height];
            Color whiteColor = Color.white;
    
            // リニアカラースペースの場合、明示的にガンマから線形に変換
            if (isLinear)
            {
                whiteColor = GammaToLinear(whiteColor);
                Debug.Log($"デフォルトテクスチャにリニアカラー使用: R={whiteColor.r}, G={whiteColor.g}, B={whiteColor.b}");
            }
    
            for (var p = 0; p < pixels.Length; p++)
            {
                pixels[p] = whiteColor;
            }

            defaultTex.SetPixels(pixels);
            defaultTex.Apply();
            return defaultTex;
        }
        
        public static Color LinearToGamma(Color color)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                // 線形からガンマへの変換
                return new Color(
                    Mathf.Pow(color.r, 1.0f / 2.2f),
                    Mathf.Pow(color.g, 1.0f / 2.2f),
                    Mathf.Pow(color.b, 1.0f / 2.2f),
                    color.a
                );
            }
            return color;
        }

        public static Color GammaToLinear(Color color)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                // ガンマから線形への変換
                return new Color(
                    Mathf.Pow(color.r, 2.2f),
                    Mathf.Pow(color.g, 2.2f),
                    Mathf.Pow(color.b, 2.2f),
                    color.a
                );
            }
            return color;
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
            // カラースペース情報をログ出力
            var colorSpace = QualitySettings.activeColorSpace;
            Debug.Log($"現在のカラースペース: {colorSpace}, テクスチャ: {texture.name}");
            
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
    Debug.Log($"アトラス作成: サイズ={atlasSize}, テクスチャ数={textures.Count}");
    
    // リニアカラースペースかを確認
    bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
    Debug.Log($"現在のカラースペース: {QualitySettings.activeColorSpace}");
    
    // アトラステクスチャを作成
    // 第4引数: mipmapsを生成
    // 第5引数: リニアカラースペースならtrue
    Texture2D atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true, isLinear);
    
    // テクスチャの詳細をログに出力
    foreach (var tex in textures)
    {
        if (tex != null)
        {
            Debug.Log($"テクスチャ情報: {tex.name}, {tex.width}x{tex.height}, Format={tex.format}, " +
                      $"Mipmaps={tex.mipmapCount}");
        }
        else
        {
            Debug.LogWarning("nullテクスチャがリストに含まれています");
        }
    }
    
    // テクスチャをアトラスにパック
    Rect[] uvRects = atlas.PackTextures(textures.ToArray(), padding, atlasSize, false);
    Debug.Log($"アトラスパック完了: UVRect数={uvRects.Length}");
    
    // テクスチャを保存
    var safeMeshName = Regex.Replace(meshName, @"[^\w\-]", "_");
    var atlasPath = $"{outputPath}/{safeMeshName}_Atlas.png";
    
    // PNGとして保存する前に、テクスチャの色味をチェック
    // 中央あたりのピクセルをサンプリング
    Color centerColor = atlas.GetPixel(atlas.width / 2, atlas.height / 2);
    Debug.Log($"アトラス中央ピクセル色: R={centerColor.r}, G={centerColor.g}, B={centerColor.b}, A={centerColor.a}");
    
    byte[] pngData = atlas.EncodeToPNG();
    File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + atlasPath), pngData);
    AssetDatabase.Refresh();
    
    // テクスチャインポート設定を詳細に調整
    TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
    if (importer != null)
    {
        // テクスチャインポートの設定を保存
        var prevSettings = new 
        {
            textureType = importer.textureType,
            isReadable = importer.isReadable,
            sRGB = importer.sRGBTexture,
            compression = importer.textureCompression,
            compressionQuality = importer.compressionQuality
        };
        
        // 新しい設定を適用
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.sRGBTexture = !isLinear; // リニアカラースペースならfalse
        importer.textureCompression = TextureImporterCompression.Uncompressed; // 圧縮なし
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        
        // インポート設定の変更をログ出力
        Debug.Log($"テクスチャインポート設定変更: " +
                  $"textureType: {prevSettings.textureType} -> {importer.textureType}, " +
                  $"isReadable: {prevSettings.isReadable} -> {importer.isReadable}, " +
                  $"sRGB: {prevSettings.sRGB} -> {importer.sRGBTexture}, " +
                  $"compression: {prevSettings.compression} -> {importer.textureCompression}, " +
                  $"quality: {prevSettings.compressionQuality} -> {importer.compressionQuality}");
        
        importer.SaveAndReimport();
    }
    else
    {
        Debug.LogError($"テクスチャインポーターの取得に失敗: {atlasPath}");
    }
    
    // 保存したテクスチャをロード
    Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
    if (savedAtlas != null)
    {
        Debug.Log($"アトラスを読み込み: {savedAtlas.name}, {savedAtlas.width}x{savedAtlas.height}, " +
                  $"Format={savedAtlas.format}");
    }
    else
    {
        Debug.LogError("保存したアトラスの読み込みに失敗しました");
    }
    
    return new TextureAtlasResult(savedAtlas, uvRects);
}
    }
}
#endif