#if UNITY_EDITOR
using System.Collections;
using System.IO;
using MaterialCombiner.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MaterialCombiner.Tests.Editor
{
    public class MaterialCombinerTests
    {
        private GameObject _testObject;
        private Material[] _testMaterials;
        private Texture2D[] _testTextures;
        private const string TestOutputPath = "Assets/Test/Combined";

        [SetUp]
        public void Setup()
        {
            // テスト用のテクスチャを作成
            _testTextures = new Texture2D[3];
            for (int i = 0; i < _testTextures.Length; i++)
            {
                _testTextures[i] = new Texture2D(64, 64);
                Color[] pixels = new Color[64 * 64];
                for (int p = 0; p < pixels.Length; p++)
                {
                    pixels[p] = new Color(i / 2.0f, 0.5f, 1 - i / 2.0f);
                }

                _testTextures[i].SetPixels(pixels);
                _testTextures[i].Apply();
            }

            // テスト用のマテリアルを作成
            _testMaterials = new Material[3];
            for (int i = 0; i < _testMaterials.Length; i++)
            {
                _testMaterials[i] = new Material(Shader.Find("Standard"));
                _testMaterials[i].mainTexture = _testTextures[i];
                _testMaterials[i].name = $"TestMaterial_{i}";
            }

            // テスト用のゲームオブジェクトを作成
            _testObject = new GameObject("TestObject");
            var meshFilter = _testObject.AddComponent<MeshFilter>();
            var meshRenderer = _testObject.AddComponent<MeshRenderer>();

            // 基本的なメッシュを作成 (正方形)
            Mesh mesh = new Mesh();
            mesh.name = "TestMesh";

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-1, -1, 0),
                new Vector3(1, -1, 0),
                new Vector3(1, 1, 0),
                new Vector3(-1, 1, 0)
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,
                0, 3, 2
            };

            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;

            // サブメッシュを追加
            mesh.subMeshCount = 3;
            mesh.SetTriangles(new int[] { 0, 2, 1 }, 0);
            mesh.SetTriangles(new int[] { 0, 3, 2 }, 1);
            mesh.SetTriangles(new int[] { 0, 3, 2 }, 2); // 同じトライアングルを別のサブメッシュに

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterials = _testMaterials;

            // テスト用の出力ディレクトリを作成
            if (!AssetDatabase.IsValidFolder("Assets/Test"))
            {
                AssetDatabase.CreateFolder("Assets", "Test");
            }

            if (AssetDatabase.IsValidFolder(TestOutputPath))
            {
                AssetDatabase.DeleteAsset(TestOutputPath);
            }

            AssetDatabase.CreateFolder("Assets/Test", "Combined");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            // テスト用オブジェクトを削除
            GameObject.DestroyImmediate(_testObject);

            // テスト用マテリアルとテクスチャを削除
            foreach (var material in _testMaterials)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            foreach (var texture in _testTextures)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            // テスト用出力ディレクトリを削除
            if (AssetDatabase.IsValidFolder(TestOutputPath))
            {
                AssetDatabase.DeleteAsset(TestOutputPath);
            }

            AssetDatabase.Refresh();
        }

        // 純粋関数のテスト

        [Test]
        public void 子オブジェクトのMeshRendererを正しくカウントする()
        {
            // Arrange
            var parent = new GameObject("Parent");
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");

            child1.transform.parent = parent.transform;
            child2.transform.parent = parent.transform;

            var rendererParent = parent.AddComponent<MeshRenderer>();
            rendererParent.sharedMaterials = new Material[] { new Material(Shader.Find("Standard")) };

            var rendererChild1 = child1.AddComponent<MeshRenderer>();
            rendererChild1.sharedMaterials = new Material[]
            {
                new Material(Shader.Find("Standard")),
                new Material(Shader.Find("Standard"))
            };

            var rendererChild2 = child2.AddComponent<MeshRenderer>();
            rendererChild2.sharedMaterials = new Material[] { new Material(Shader.Find("Standard")) };

            // Act
            int count = MaterialCombiner.Editor.MaterialCombiner.CountMeshRenderersInChildren(parent);

            // Assert
            Assert.AreEqual(1, count, "複数のマテリアルを持つ子MeshRendererの数が正しくありません");

            // Cleanup
            GameObject.DestroyImmediate(parent);
        }

        // デバッグ用のヘルパーメソッド
        private void LogMeshNameForDebug(Mesh mesh)
        {
            Debug.Log($"メッシュ名: '{mesh.name}'");
        }

        [Test]
        public void 再帰オプションで処理対象オブジェクトが正しく抽出される()
        {
            // Arrange
            var parent1 = new GameObject("Parent1");
            var parent2 = new GameObject("Parent2");
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");

            child1.transform.parent = parent1.transform;
            child2.transform.parent = parent1.transform;

            var rendererParent1 = parent1.AddComponent<MeshRenderer>();
            rendererParent1.sharedMaterials = new Material[]
            {
                new Material(Shader.Find("Standard")),
                new Material(Shader.Find("Standard"))
            };

            var rendererParent2 = parent2.AddComponent<MeshRenderer>();
            rendererParent2.sharedMaterials = new Material[] { new Material(Shader.Find("Standard")) };

            var rendererChild1 = child1.AddComponent<MeshRenderer>();
            rendererChild1.sharedMaterials = new Material[]
            {
                new Material(Shader.Find("Standard")),
                new Material(Shader.Find("Standard"))
            };

            var rendererChild2 = child2.AddComponent<MeshRenderer>();
            rendererChild2.sharedMaterials = new Material[] { new Material(Shader.Find("Standard")) };

            // Act
            var selectedObjects = new GameObject[] { parent1, parent2 };
            var result1 = MaterialCombiner.Editor.MaterialCombiner.GetObjectsToProcess(selectedObjects, false);
            var result2 = MaterialCombiner.Editor.MaterialCombiner.GetObjectsToProcess(selectedObjects, true);

            // Assert
            Assert.AreEqual(2, result1.Count, "再帰的でない場合、選択されたオブジェクトだけを処理する必要があります");
            Assert.AreEqual(3, result2.Count, "再帰的な場合、選択されたオブジェクトと適格な子オブジェクトを処理する必要があります");

            // 再帰的でない場合は親オブジェクトのみ
            Assert.IsTrue(result1.Contains(parent1));
            Assert.IsTrue(result1.Contains(parent2));

            // 再帰的な場合は親と複数マテリアルを持つ子を含む
            Assert.IsTrue(result2.Contains(parent1));
            Assert.IsTrue(result2.Contains(parent2));
            Assert.IsTrue(result2.Contains(child1));
            Assert.IsFalse(result2.Contains(child2));

            // Cleanup
            GameObject.DestroyImmediate(parent1);
            GameObject.DestroyImmediate(parent2);
        }

        [Test]
        public void タイムスタンプオプションで正しい出力パスが生成される()
        {
            // Arrange
            string basePath = "Assets/Test";
            string meshName = "Test Mesh With Spaces!";

            // Act
            string path1 = PathUtility.GenerateOutputPath(basePath, meshName, true, false);
            string path2 = PathUtility.GenerateOutputPath(basePath, meshName, false, false);

            // Assert
            StringAssert.Contains("Test_Mesh_With_Spaces_", path1, "安全なメッシュ名の変換と、タイムスタンプの付加が正しく行われていません");
            Assert.AreEqual("Assets/Test/Test_Mesh_With_Spaces_",
                path1.Substring(0, "Assets/Test/Test_Mesh_With_Spaces_".Length));

            Assert.AreEqual("Assets/Test/Test_Mesh_With_Spaces_", path2, "タイムスタンプなしの場合、単にメッシュ名を変換するだけです");
        }

        [Test]
        public void 上書き防止オプションでフォルダ名にカウンターが追加される()
        {
            // Arrange
            string basePath = "Assets/Test";
            string meshName = "TestMesh";

            // Test用のフォルダを作成してテスト
            if (!AssetDatabase.IsValidFolder("Assets/Test/TestMesh"))
            {
                AssetDatabase.CreateFolder("Assets/Test", "TestMesh");
                AssetDatabase.Refresh();
            }

            // Act
            string path = PathUtility.GenerateOutputPath(basePath, meshName, false, true);

            // Assert
            Assert.AreEqual("Assets/Test/TestMesh_1", path, "上書き防止が有効な場合、フォルダ名に数字を追加する必要があります");

            // Cleanup
            AssetDatabase.DeleteAsset("Assets/Test/TestMesh");
            AssetDatabase.Refresh();
        }

        [Test]
        public void テクスチャを読み取り可能な状態に変換できる()
        {
            // Arrange
            Texture2D texture = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.red;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Act
            Texture2D result = TextureProcessing.MakeTextureReadable(texture);

            // Assert
            Assert.IsNotNull(result, "読み取り可能なテクスチャを返す必要があります");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(result);
        }

        [Test]
        public void メインテクスチャが正しく取得される()
        {
            // Arrange
            Texture2D texture = new Texture2D(32, 32);
            Material material = new Material(Shader.Find("Standard"));
            material.mainTexture = texture;

            // Act
            var getTextureFunc = TextureProcessing.GetTextureFromMaterial;
            Texture2D result = getTextureFunc(material, true, "_MainTex");

            // Assert
            Assert.AreEqual(texture, result, "マテリアルのmainTextureが返される必要があります");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void カスタムプロパティのテクスチャが正しく取得される()
        {
            // Arrange
            Texture2D mainTexture = new Texture2D(32, 32);
            Texture2D emissionTexture = new Texture2D(32, 32);
            Material material = new Material(Shader.Find("Standard"));
            material.mainTexture = mainTexture;
            material.SetTexture("_EmissionMap", emissionTexture);

            // Act
            var getTextureFunc = TextureProcessing.GetTextureFromMaterial;
            Texture2D result = getTextureFunc(material, false, "_EmissionMap");

            // Assert
            Assert.AreEqual(emissionTexture, result, "指定されたカスタムプロパティのテクスチャが返される必要があります");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(mainTexture);
            UnityEngine.Object.DestroyImmediate(emissionTexture);
        }

        [Test]
        public void テクスチャ抽出関数が正しく生成される()
        {
            // Arrange
            MaterialCombinerConfig config = new MaterialCombinerConfig(
                UseMainTextureByDefault: true,
                CustomTexturePropertyName: "_MainTex"
            );

            Texture2D texture = new Texture2D(32, 32);
            Material material = new Material(Shader.Find("Standard"));
            material.mainTexture = texture;

            // Act
            var extractorFunc = TextureProcessing.CreateTextureExtractor(config);
            Texture2D result = extractorFunc(material);

            // Assert
            Assert.AreEqual(texture, result, "抽出関数は正しいテクスチャを返す必要があります");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        // 統合テスト - ProcessSingleObjectの機能テスト

        [UnityTest]
        public IEnumerator オブジェクト処理で正しい出力ファイルが生成される()
        {
            // Arrange
            MaterialCombinerConfig config = new MaterialCombinerConfig(
                OutputBasePath: TestOutputPath,
                AtlasSize: 512,
                Padding: 2,
                UseTimestampFolder: false,
                KeepOriginalShader: true,
                UseMainTextureByDefault: true,
                PreventOverwrite: true
            );

            // Act
            var result = MaterialCombiner.Editor.MaterialCombiner.ProcessSingleObject(_testObject, config);

            // ファイルが書き込まれるまで1フレーム待機
            yield return null;
            AssetDatabase.Refresh();

            // Assert
            Assert.IsTrue(result.IsSuccess, $"処理は成功する必要があります。エラー: {result.Message}");

            string meshOutputPath = $"{TestOutputPath}/TestMesh";
            Assert.IsTrue(AssetDatabase.IsValidFolder(meshOutputPath), "出力フォルダが作成される必要があります");

            string atlasPath = $"{meshOutputPath}/TestMesh_Atlas.png";
            string materialPath = $"{meshOutputPath}/TestMesh_Material.mat";
            string meshPath = $"{meshOutputPath}/TestMesh_Mesh.asset";

            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "../" + atlasPath)), "アトラステクスチャが作成される必要があります");
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "../" + materialPath)), "マテリアルが作成される必要があります");
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "../" + meshPath)), "メッシュが作成される必要があります");

            // レンダラーにマテリアルが設定されているか確認
            MeshRenderer renderer = _testObject.GetComponent<MeshRenderer>();
            Assert.AreEqual(1, renderer.sharedMaterials.Length, "マテリアルは1つに統合される必要があります");

            // メッシュのUVが更新されているか確認
            MeshFilter filter = _testObject.GetComponent<MeshFilter>();
            Mesh newMesh = filter.sharedMesh;

            // デバッグのためにメッシュ名をログ出力
            LogMeshNameForDebug(newMesh);

            Assert.AreNotEqual("TestMesh", newMesh.name, "新しいメッシュが設定される必要があります");
            Assert.IsTrue(newMesh.name.StartsWith("Combined_"), "新しいメッシュ名は 'Combined_' で始まる必要があります");
        }
    }
}
#endif