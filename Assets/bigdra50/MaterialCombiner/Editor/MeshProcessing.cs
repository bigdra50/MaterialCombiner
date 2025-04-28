#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MaterialCombiner.Editor
{
    /// <summary>
    /// メッシュ処理に関連する機能を提供するクラス
    /// </summary>
    public static class MeshProcessing
    {
        /// <summary>
        /// 子のMeshRendererをカウント（複数のマテリアルを持つもののみ）
        /// </summary>
        public static int CountMeshRenderersInChildren(GameObject obj)
        {
            if (obj == null) return 0;

            var renderers = obj.GetComponentsInChildren<MeshRenderer>(true);

            return renderers.Count(renderer => renderer.gameObject != obj && renderer.sharedMaterials.Length > 1);
        }

        /// <summary>
        /// 処理対象のオブジェクトリストを取得
        /// </summary>
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

        /// <summary>
        /// UVを再調整したメッシュを作成
        /// </summary>
        public static Mesh CreateAdjustedMesh(
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

        /// <summary>
        /// 結合されたマテリアルを作成
        /// </summary>
        public static Material CreateCombinedMaterial(
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
        /// <summary>
        /// マテリアルのプロパティをコピー
        /// </summary>
public static void CopyMaterialProperties(Material source, Material destination)
{
    if (source == null || destination == null)
        return;

    Debug.Log($"マテリアルプロパティをコピー: {source.name} -> {destination.name}");
    Debug.Log($"元のシェーダー: {source.shader.name}, 対象のシェーダー: {destination.shader.name}");

    // 色関連のプロパティを先にコピー（特に重要なプロパティ）
    if (source.HasProperty("_Color") && destination.HasProperty("_Color"))
    {
        Color sourceColor = source.GetColor("_Color");
        destination.SetColor("_Color", sourceColor);
        Debug.Log($"_Color プロパティをコピー: {sourceColor}");
    }

    // シェーダーのプロパティ数を取得
    var propertyCount = ShaderUtil.GetPropertyCount(source.shader);
    Debug.Log($"シェーダープロパティ数: {propertyCount}");

    // 各プロパティをチェック
    int copiedCount = 0;
    for (var i = 0; i < propertyCount; i++)
    {
        var propertyName = ShaderUtil.GetPropertyName(source.shader, i);
        var propertyType = ShaderUtil.GetPropertyType(source.shader, i);

        // 対象のマテリアルに同じプロパティがあるか確認
        if (!destination.HasProperty(propertyName))
        {
            Debug.LogWarning($"プロパティ '{propertyName}' は対象マテリアルにありません");
            continue;
        }

        // プロパティタイプに応じて値をコピー
        try
        {
            switch (propertyType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    Color color = source.GetColor(propertyName);
                    destination.SetColor(propertyName, color);
                    Debug.Log($"カラープロパティをコピー: {propertyName} = {color}");
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    float floatValue = source.GetFloat(propertyName);
                    destination.SetFloat(propertyName, floatValue);
                    Debug.Log($"数値プロパティをコピー: {propertyName} = {floatValue}");
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    Vector4 vector = source.GetVector(propertyName);
                    destination.SetVector(propertyName, vector);
                    Debug.Log($"ベクトルプロパティをコピー: {propertyName} = {vector}");
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    // テクスチャは個別に処理するのでスキップ
                    Debug.Log($"テクスチャプロパティはスキップ: {propertyName}");
                    break;
                default:
                    Debug.LogWarning($"未対応のプロパティタイプ: {propertyType} for {propertyName}");
                    break;
            }
            copiedCount++;
        }
        catch (Exception e)
        {
            Debug.LogError($"プロパティ '{propertyName}' のコピー中にエラー: {e.Message}");
        }
    }

    // レンダリングモードとカラースペース関連の設定をコピー
    if (source.HasProperty("_Mode") && destination.HasProperty("_Mode"))
    {
        destination.SetFloat("_Mode", source.GetFloat("_Mode"));
        Debug.Log($"レンダリングモードをコピー: {source.GetFloat("_Mode")}");
    }

    // シェーダーキーワードもコピー
    string[] keywords = source.shaderKeywords;
    Debug.Log($"シェーダーキーワード数: {keywords.Length}");
    foreach (var keyword in keywords)
    {
        if (!destination.IsKeywordEnabled(keyword))
        {
            destination.EnableKeyword(keyword);
            Debug.Log($"シェーダーキーワードを有効化: {keyword}");
        }
    }

    Debug.Log($"マテリアルプロパティコピー完了: {copiedCount}個のプロパティをコピー");
    
    // レンダリング設定のコピー
    destination.renderQueue = source.renderQueue;
    Debug.Log($"レンダーキューをコピー: {source.renderQueue}");
}

        /// <summary>
        /// シェーダーを取得
        /// </summary>
        public static Shader GetShaderForCombinedMaterial(Material sourceMaterial, bool keepOriginalShader,
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
    }
}
#endif