#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MaterialCombiner.Editor
{
    /// <summary>
    /// パス操作とディレクトリ作成に関するユーティリティ
    /// </summary>
    public static class PathUtility
    {
        /// <summary>
        /// 出力パスを生成
        /// </summary>
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

        /// <summary>
        /// OutputPathからDirectoryを作成
        /// </summary>
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
    }
}
#endif
