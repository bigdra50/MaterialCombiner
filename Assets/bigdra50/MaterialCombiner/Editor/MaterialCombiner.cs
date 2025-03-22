#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MaterialCombiner.Editor
{
    /// <summary>
    /// マテリアル結合の主要な処理を行うクラス
    /// </summary>
    public static class MaterialCombiner
    {
        /// <summary>
        /// 複数オブジェクトの処理を実行し、結果を集約
        /// </summary>
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
            var objectsToProcess = MeshProcessing.GetObjectsToProcess(objects, config.ProcessChildrenRecursively);

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

        /// <summary>
        /// 子のMeshRendererをカウント
        /// </summary>
        public static int CountMeshRenderersInChildren(GameObject obj)
        {
            return MeshProcessing.CountMeshRenderersInChildren(obj);
        }

        /// <summary>
        /// 処理対象のオブジェクトリストを取得
        /// </summary>
        public static List<GameObject> GetObjectsToProcess(GameObject[] selectedObjects,
            bool processChildrenRecursively)
        {
            return MeshProcessing.GetObjectsToProcess(selectedObjects, processChildrenRecursively);
        }

        /// <summary>
        /// 出力パスを生成
        /// </summary>
        public static string GenerateOutputPath(
            string baseOutputPath,
            string meshName,
            bool useTimestampFolder,
            bool preventOverwrite)
        {
            return PathUtility.GenerateOutputPath(baseOutputPath, meshName, useTimestampFolder, preventOverwrite);
        }

        /// <summary>
        /// OutputPathからDirectoryを作成
        /// </summary>
        public static void EnsureDirectoryExists(string outputPath)
        {
            PathUtility.EnsureDirectoryExists(outputPath);
        }

        /// <summary>
        /// テクスチャを取得
        /// </summary>
        public static Func<Material, bool, string, Texture2D> GetTextureFromMaterial =>
            TextureProcessing.GetTextureFromMaterial;

        /// <summary>
        /// シングルオブジェクト処理
        /// </summary>
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
                    MeshProcessing.GetShaderForCombinedMaterial(materials[0], config.KeepOriginalShader,
                        config.DefaultShaderName);

                Func<Material, Texture2D> getTextureFrom =
                    material => GetTextureFromMaterial(material, config.UseMainTextureByDefault,
                        config.CustomTexturePropertyName);

                var materialTextureMap = TextureProcessing.ProcessMaterialTextures(materials, getTextureFrom);

                if (materialTextureMap.Textures.Count == 0)
                {
                    return ProcessResult.Failure("テクスチャがありません");
                }

                // アトラス作成
                var atlasResult = TextureProcessing.CreateTextureAtlas(
                    materialTextureMap.Textures,
                    config.AtlasSize,
                    config.Padding,
                    meshName,
                    outputPath
                );

                // マテリアル作成
                var combinedMaterial = MeshProcessing.CreateCombinedMaterial(
                    targetShader,
                    atlasResult.Atlas,
                    materials[0],
                    config.UseMainTextureByDefault,
                    config.CustomTexturePropertyName,
                    $"{outputPath}/{Regex.Replace(meshName, @"[^\w\-]", "_")}_Material.mat"
                );

                // UVの再調整とメッシュの作成・保存
                var newMesh = MeshProcessing.CreateAdjustedMesh(
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
            }
            catch (Exception e)
            {
                return ProcessResult.Failure(e.Message);
            }
        }
    }
}
#endif
