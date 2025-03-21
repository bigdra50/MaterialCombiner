#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MaterialCombiner.Editor
{
    public static class MaterialCombinerFunctions
    {
        // 複数オブジェクトの処理を実行し、結果を集約
        public static (int, List<string>) ProcessMultipleObjects(
            GameObject[] objects,
            MaterialCombinerConfig config)
        {
            if (objects == null || objects.Length == 0)
            {
                return (0, new List<string> { "処理するオブジェクトが選択されていません" });
            }

            var successCount = 0;
            var errorMessages = new List<string>();

            // 処理対象のオブジェクトリストを取得
            var objectsToProcess = GetObjectsToProcess(objects, config.ProcessChildrenRecursively);

            // プログレスバー表示
            var totalObjects = objectsToProcess.Count;
            for (var i = 0; i < totalObjects; i++)
            {
                var obj = objectsToProcess[i];

                // プログレスバーの更新
                EditorUtility.DisplayProgressBar(
                    "マテリアル結合処理中",
                    $"処理中: {obj.name} ({i + 1}/{totalObjects})",
                    (float)(i + 1) / totalObjects);

                var result = ProcessSingleObject(obj, config);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    errorMessages.Add($"{obj.name}: {result.Message}");
                }
            }

            EditorUtility.ClearProgressBar();

            return (successCount, errorMessages);
        }

        // 子のMeshRendererをカウント
        public static int CountMeshRenderersInChildren(GameObject obj)
        {
            if (obj == null) return 0;

            var renderers = obj.GetComponentsInChildren<MeshRenderer>(true);

            return renderers.Count(renderer => renderer.gameObject != obj && renderer.sharedMaterials.Length > 1);
        }

        // 処理対象のオブジェクトリストを取得
        public static List<GameObject> GetObjectsToProcess(GameObject[] selectedObjects,
            bool processChildrenRecursively)
        {
            if (selectedObjects == null || selectedObjects.Length == 0)
                return new List<GameObject>();

            var objectsToProcess = new List<GameObject>(selectedObjects);

            if (!processChildrenRecursively) return objectsToProcess.Distinct().ToList();

            foreach (var obj in selectedObjects)
            {
                var childRenderers = obj.GetComponentsInChildren<MeshRenderer>(true);

                objectsToProcess.AddRange(
                    childRenderers
                        .Where(renderer =>
                            renderer.gameObject != obj &&
                            renderer.sharedMaterials.Length > 1)
                        .Select(renderer => renderer.gameObject)
                );
            }

            // 重複を排除
            return objectsToProcess.Distinct().ToList();
        }

        // 出力パスを生成
        public static string GenerateOutputPath(
            string baseOutputPath,
            string meshName,
            bool useTimestampFolder,
            bool preventOverwrite)
        {
            // 無効なファイル名文字を除去
            var safeMeshName = Regex.Replace(meshName, @"[^\w\-]", "_");

            if (useTimestampFolder)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                return $"{baseOutputPath}/{safeMeshName}_{timestamp}";
            }
            else
            {
                var outputPath = $"{baseOutputPath}/{safeMeshName}";

                // 上書き防止
                if (preventOverwrite && AssetDatabase.IsValidFolder(outputPath))
                {
                    var counter = 1;
                    while (AssetDatabase.IsValidFolder($"{outputPath}_{counter}"))
                    {
                        counter++;
                    }

                    return $"{outputPath}_{counter}";
                }

                return outputPath;
            }
        }

        // OutputPathからDirectoryを作成
        public static void EnsureDirectoryExists(string outputPath)
        {
            var baseOutputPath = outputPath[..outputPath.LastIndexOf('/')];

            if (!AssetDatabase.IsValidFolder(baseOutputPath))
            {
                var folders = baseOutputPath.Split('/');
                var currentPath = folders[0];

                for (var i = 1; i < folders.Length; i++)
                {
                    var nextPath = $"{currentPath}/{folders[i]}";
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }

                    currentPath = nextPath;
                }
            }

            if (AssetDatabase.IsValidFolder(outputPath)) return;

            var parentFolder = outputPath[..outputPath.LastIndexOf('/')];
            var newFolder = outputPath[(outputPath.LastIndexOf('/') + 1)..];
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }

        // テクスチャを取得
        public static Func<Material, bool, string, Texture2D> GetTextureFromMaterial =>
            (material, useMainTextureByDefault, customPropertyName) =>
            {
                if (material == null) return null;

                Texture2D texture = null;

                if (useMainTextureByDefault)
                {
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

        public static Func<Material, Texture2D> CreateTextureExtractor(MaterialCombinerConfig config) =>
            material =>
                GetTextureFromMaterial(material, config.UseMainTextureByDefault, config.CustomTexturePropertyName);

        // 読み取り不可能なテクスチャをコピー
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

        // マテリアルのプロパティをコピー
        public static void CopyMaterialProperties(Material source, Material destination)
        {
            if (source == null || destination == null)
                return;

            // シェーダーのプロパティ数を取得
            var propertyCount = ShaderUtil.GetPropertyCount(source.shader);

            // 各プロパティをチェック
            for (var i = 0; i < propertyCount; i++)
            {
                var propertyName = ShaderUtil.GetPropertyName(source.shader, i);
                var propertyType = ShaderUtil.GetPropertyType(source.shader, i);

                // 対象のマテリアルに同じプロパティがあるか確認
                if (!destination.HasProperty(propertyName))
                    continue;

                // プロパティタイプに応じて値をコピー
                switch (propertyType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        destination.SetColor(propertyName, source.GetColor(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        destination.SetFloat(propertyName, source.GetFloat(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        destination.SetVector(propertyName, source.GetVector(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        // テクスチャは別途処理するのでスキップ
                        break;
                }
            }

            // シェーダーキーワードもコピー
            foreach (var keyword in source.shaderKeywords)
            {
                if (!destination.IsKeywordEnabled(keyword))
                {
                    destination.EnableKeyword(keyword);
                }
            }
        }

        // デフォルトテクスチャを作成
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

        // シングルオブジェクト処理
        public static ProcessResult ProcessSingleObject(GameObject obj, MaterialCombinerConfig config)
        {
            try
            {
                // 対象のレンダラーを取得
                var renderer = obj.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    return ProcessResult.Failure("MeshRendererがありません");
                }

                var materials = renderer.sharedMaterials;
                if (materials.Length <= 1)
                {
                    return ProcessResult.Failure("マテリアルは既に1つのみです");
                }

                // 元のメッシュ名を取得してフォルダ名に使用
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    return ProcessResult.Failure("MeshFilterまたはメッシュがありません");
                }

                var meshName = meshFilter.sharedMesh.name;

                // 出力パス生成
                var outputPath = GenerateOutputPath(
                    config.OutputBasePath,
                    meshName,
                    config.UseTimestampFolder,
                    config.PreventOverwrite
                );

                EnsureDirectoryExists(outputPath);

                // 元のマテリアルのシェーダーを保持
                var targetShader =
                    GetShaderForCombinedMaterial(materials[0], config.KeepOriginalShader, config.DefaultShaderName);

                var materialTextureMap = ProcessMaterialTextures(materials, GetTextureFrom);

                if (materialTextureMap.Textures.Count == 0)
                {
                    return ProcessResult.Failure("テクスチャがありません");
                }

                // アトラス作成
                var atlasResult = CreateTextureAtlas(
                    materialTextureMap.Textures,
                    config.AtlasSize,
                    config.Padding,
                    meshName,
                    outputPath
                );

                // マテリアル作成
                var combinedMaterial = CreateCombinedMaterial(
                    targetShader,
                    atlasResult.Atlas,
                    materials[0],
                    config.UseMainTextureByDefault,
                    config.CustomTexturePropertyName,
                    $"{outputPath}/{Regex.Replace(meshName, @"[^\w\-]", "_")}_Material.mat"
                );

                // UVの再調整とメッシュの作成・保存
                var newMesh = CreateAdjustedMesh(
                    meshFilter.sharedMesh,
                    materialTextureMap.MaterialToTextureIndex,
                    atlasResult.UVRects,
                    $"{outputPath}/{Regex.Replace(meshName, @"[^\w\-]", "_")}_Mesh.asset"
                );

                // 新しいメッシュとマテリアルを適用
                meshFilter.sharedMesh = newMesh;
                renderer.sharedMaterials = new[] { combinedMaterial };

                Debug.Log($"マテリアル統合が完了しました。出力先: {outputPath}");
                return ProcessResult.Success();

                // テクスチャの抽出と処理
                Texture2D GetTextureFrom(Material material) => GetTextureFromMaterial(material,
                    config.UseMainTextureByDefault, config.CustomTexturePropertyName);
            }
            catch (Exception e)
            {
                return ProcessResult.Failure(e.Message);
            }
        }

        // シェーダー取得
        private static Shader GetShaderForCombinedMaterial(Material sourceMaterial, bool keepOriginalShader,
            string defaultShaderName)
        {
            if (keepOriginalShader)
            {
                return sourceMaterial.shader;
            }

            var shader = Shader.Find(defaultShaderName);
            if (shader != null) return shader;

            Debug.LogWarning($"シェーダー '{defaultShaderName}' が見つかりませんでした。デフォルトシェーダーを使用します。");
            shader = Shader.Find("Unlit/Texture");

            return shader;
        }

        // マテリアルとテクスチャのマッピング結果を表す構造体
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

        // マテリアルからテクスチャを処理
        private static MaterialTextureMapResult ProcessMaterialTextures(
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

        // テクスチャアトラス作成結果を表す構造体
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

        // テクスチャアトラスを作成
        private static TextureAtlasResult CreateTextureAtlas(
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
            var safeMeshName = Regex.Replace(meshName, @"[^\w\-]", "_");
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

        // 結合されたマテリアルを作成
        private static Material CreateCombinedMaterial(
            Shader shader,
            Texture2D atlas,
            Material sourceMaterial,
            bool useMainTextureByDefault,
            string customTexturePropertyName,
            string savePath)
        {
            var combinedMaterial = new Material(shader);

            // デフォルトでmainTextureを使用しない場合のみ、指定されたプロパティにアトラスを設定
            if (!useMainTextureByDefault && combinedMaterial.HasProperty(customTexturePropertyName))
            {
                combinedMaterial.SetTexture(customTexturePropertyName, atlas);
                Debug.Log($"{customTexturePropertyName} にテクスチャを設定しました");
            }

            // いずれの場合もmainTextureは設定
            combinedMaterial.mainTexture = atlas;

            // 元のマテリアルからその他のプロパティをコピー
            CopyMaterialProperties(sourceMaterial, combinedMaterial);

            AssetDatabase.CreateAsset(combinedMaterial, savePath);

            return combinedMaterial;
        }

        // UVを再調整したメッシュを作成
        private static Mesh CreateAdjustedMesh(
            Mesh originalMesh,
            Dictionary<int, int> materialToTextureIndex,
            Rect[] uvRects,
            string savePath)
        {
            var newMesh = new Mesh
            {
                name = $"Combined_{originalMesh.name}",
                // 頂点データをコピー
                vertices = originalMesh.vertices,
                normals = originalMesh.normals
            };

            if (originalMesh.tangents != null && originalMesh.tangents.Length > 0)
            {
                newMesh.tangents = originalMesh.tangents;
            }

            var originalUVs = originalMesh.uv;
            var newUVs = new Vector2[originalUVs.Length];

            // 各サブメッシュごとにトライアングルとUVを処理
            var allTriangles = new List<int>();

            for (var subMesh = 0; subMesh < originalMesh.subMeshCount; subMesh++)
            {
                if (subMesh >= materialToTextureIndex.Count)
                {
                    Debug.LogWarning($"サブメッシュ {subMesh} に対応するマテリアルがありません");
                    continue;
                }

                var triangles = originalMesh.GetTriangles(subMesh);

                // このマテリアルに対応するテクスチャのインデックスを取得
                if (!materialToTextureIndex.TryGetValue(subMesh, out var textureIndex))
                {
                    Debug.LogWarning($"マテリアル {subMesh} のテクスチャインデックスが見つかりません");
                    continue;
                }

                // uvRectsの範囲チェック
                if (textureIndex >= uvRects.Length)
                {
                    Debug.LogError($"テクスチャインデックス {textureIndex} が uvRects の範囲外です（長さ: {uvRects.Length}）");
                    continue;
                }

                var uvRect = uvRects[textureIndex];

                foreach (var index in triangles)
                {
                    if (index < 0 || index >= originalUVs.Length)
                    {
                        Debug.LogError($"UV インデックス {index} が範囲外です（長さ: {originalUVs.Length}）");
                        continue;
                    }

                    var originalUV = originalUVs[index];
                    newUVs[index] = new Vector2(
                        uvRect.x + originalUV.x * uvRect.width,
                        uvRect.y + originalUV.y * uvRect.height
                    );
                }

                allTriangles.AddRange(triangles);
            }

            newMesh.uv = newUVs;
            newMesh.triangles = allTriangles.ToArray();
            newMesh.RecalculateBounds();

            AssetDatabase.CreateAsset(newMesh, savePath);

            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savePath);
            if (savedMesh != null && !savedMesh.name.StartsWith("Combined_"))
            {
                savedMesh.name = $"Combined_{originalMesh.name}";
            }

            return newMesh;
        }
    }
}
#endif
