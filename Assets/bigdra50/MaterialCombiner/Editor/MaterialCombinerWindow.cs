#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MaterialCombiner.Editor
{
    public class MaterialCombinerWindow : EditorWindow
    {
        private MaterialCombinerConfig _config;
        private GameObject[] _targetObjects;

        // 選択可能なアトラスサイズオプション
        private readonly string[] _atlasSizeOptions = { "512", "1024", "2048", "4096", "8192" };

        [MenuItem("Tools/Material Combiner")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialCombinerWindow>("Material Combiner");
            window.minSize = new Vector2(450, 600);
        }

        private void OnEnable()
        {
            _config = new MaterialCombinerConfig();
        }

        private void CreateGUI()
        {
            // ターゲットオブジェクトを取得
            _targetObjects = Selection.gameObjects;

            // UXMLテンプレートを相対パスで読み込み
            // スクリプトパスからディレクトリ構造を判断
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var editorDirectory = System.IO.Path.GetDirectoryName(scriptPath);
            var parentDirectory = System.IO.Path.GetDirectoryName(editorDirectory);
            var uiDirectory = System.IO.Path.Combine(parentDirectory, "UI");
            var uxmlPath = System.IO.Path.Combine(uiDirectory, "MaterialCombinerWindow.uxml");

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"UXMLテンプレートが見つかりません: {uxmlPath}");
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            // スタイルシートを相対パスで読み込み
            var ussPath = System.IO.Path.Combine(uiDirectory, "MaterialCombinerWindow.uss");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet == null)
            {
                Debug.LogWarning($"スタイルシートが見つかりません: {ussPath}");
            }
            else
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            // UI要素の初期化とイベントの設定
            // _configはInitializeUI内で初期化される
            InitializeUI();
        }


        private void InitializeUI()
        {
            // 主要なUI要素の取得
            var recursiveToggle = rootVisualElement.Q<Toggle>("recursive-toggle");
            var childCountLabel = rootVisualElement.Q<Label>("child-count");
            var outputPathField = rootVisualElement.Q<TextField>("output-path");
            var timestampToggle = rootVisualElement.Q<Toggle>("timestamp-toggle");
            var overwriteToggle = rootVisualElement.Q<Toggle>("overwrite-toggle");
            var atlasSizeField = rootVisualElement.Q<PopupField<string>>("atlas-size");
            var paddingSlider = rootVisualElement.Q<SliderInt>("padding-slider");
            var shaderToggle = rootVisualElement.Q<Toggle>("shader-toggle");
            var shaderSelector = rootVisualElement.Q<VisualElement>("shader-selector");
            var shaderButton = rootVisualElement.Q<Button>("shader-button");
            var mainTexToggle = rootVisualElement.Q<Toggle>("main-tex-toggle");
            var customPropertyContainer = rootVisualElement.Q<VisualElement>("custom-property-container");
            var customPropertyField = rootVisualElement.Q<TextField>("custom-property-field");
            var processButton = rootVisualElement.Q<Button>("process-button");

            recursiveToggle?.SetValueWithoutNotify(_config.ProcessChildrenRecursively);

            if (childCountLabel != null)
            {
                childCountLabel.style.display =
                    _config.ProcessChildrenRecursively ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateChildCount(childCountLabel);
            }

            outputPathField?.SetValueWithoutNotify(_config.OutputBasePath);

            timestampToggle?.SetValueWithoutNotify(_config.UseTimestampFolder);

            overwriteToggle?.SetValueWithoutNotify(_config.PreventOverwrite);

            // アトラスサイズ設定
            if (atlasSizeField != null)
            {
                // SetValueWithoutNotifyがPopupFieldでは使えないので代わりにindexを設定
                atlasSizeField.index = GetAtlasSizeIndex(_config.AtlasSize);
            }
            else
            {
                // PopupFieldが取得できない場合は手動で作成
                var container = rootVisualElement.Q("atlas-size-container");
                if (container != null)
                {
                    atlasSizeField = new PopupField<string>("アトラスサイズ", _atlasSizeOptions.ToList(),
                        GetAtlasSizeIndex(_config.AtlasSize));
                    atlasSizeField.name = "atlas-size";
                    container.Add(atlasSizeField);
                }
            }

            paddingSlider?.SetValueWithoutNotify(_config.Padding);

            // シェーダー設定
            shaderToggle?.SetValueWithoutNotify(_config.KeepOriginalShader);

            if (shaderSelector != null)
            {
                shaderSelector.style.display = _config.KeepOriginalShader ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (shaderButton != null)
            {
                shaderButton.text = string.IsNullOrEmpty(_config.DefaultShaderName)
                    ? "シェーダーを選択"
                    : _config.DefaultShaderName;
            }

            // テクスチャ設定
            mainTexToggle?.SetValueWithoutNotify(_config.UseMainTextureByDefault);

            if (customPropertyContainer != null)
            {
                customPropertyContainer.style.display =
                    _config.UseMainTextureByDefault ? DisplayStyle.None : DisplayStyle.Flex;
            }

            customPropertyField?.SetValueWithoutNotify(_config.CustomTexturePropertyName);

            processButton?.SetEnabled(Selection.gameObjects.Length > 0);

            // イベントハンドラの設定
            SetupEventHandlers(
                recursiveToggle, childCountLabel, outputPathField, timestampToggle, overwriteToggle,
                atlasSizeField, paddingSlider, shaderToggle, shaderSelector, shaderButton,
                mainTexToggle, customPropertyContainer, customPropertyField, processButton
            );
        }

        private void SetupEventHandlers(
            Toggle recursiveToggle,
            Label childCountLabel,
            TextField outputPathField,
            Toggle timestampToggle,
            Toggle overwriteToggle,
            PopupField<string> atlasSizeField,
            SliderInt paddingSlider,
            Toggle shaderToggle,
            VisualElement shaderSelector,
            Button shaderButton,
            Toggle mainTexToggle,
            VisualElement customPropertyContainer,
            TextField customPropertyField,
            Button processButton)
        {
            // Selection変更イベントの登録
            Selection.selectionChanged += () =>
            {
                _targetObjects = Selection.gameObjects;
                var selectionInfo = rootVisualElement.Q<Label>("selection-info");
                if (selectionInfo != null)
                {
                    selectionInfo.text = Selection.gameObjects.Length > 0
                        ? $"{Selection.gameObjects.Length} オブジェクト選択中"
                        : "オブジェクトが選択されていません";
                }

                UpdateChildCount(childCountLabel);

                processButton?.SetEnabled(Selection.gameObjects.Length > 0);
            };
            if (processButton != null) processButton.clicked += ProcessSelectedObjects;

            // 再帰処理オプション
            if (recursiveToggle != null)
            {
                recursiveToggle.RegisterValueChangedCallback(evt =>
                {
                    _config = _config with { ProcessChildrenRecursively = evt.newValue };
                    if (childCountLabel == null) return;
                    childCountLabel.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                    UpdateChildCount(childCountLabel);
                });

                // 初期状態を設定(再帰処理の表示/非表示)
                if (childCountLabel != null)
                {
                    childCountLabel.style.display = recursiveToggle.value ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            // 出力パス
            outputPathField?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { OutputBasePath = evt.newValue };
            });

            // タイムスタンプオプション
            timestampToggle?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { UseTimestampFolder = evt.newValue };
            });

            // 上書き防止オプション
            overwriteToggle?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { PreventOverwrite = evt.newValue };
            });

            // アトラスサイズ
            atlasSizeField?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { AtlasSize = int.Parse(evt.newValue) };
            });

            // パディング
            paddingSlider?.RegisterValueChangedCallback(evt => { _config = _config with { Padding = evt.newValue }; });

            // シェーダーオプション
            shaderToggle?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { KeepOriginalShader = evt.newValue };
                if (shaderSelector != null)
                {
                    shaderSelector.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                }
            });

            // メインテクスチャオプション
            mainTexToggle?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { UseMainTextureByDefault = evt.newValue };
                if (customPropertyContainer != null)
                {
                    customPropertyContainer.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                }
            });

            // カスタムプロパティ名
            customPropertyField?.RegisterValueChangedCallback(evt =>
            {
                _config = _config with { CustomTexturePropertyName = evt.newValue };
            });
        }

        private void UpdateChildCount(Label childCountLabel)
        {
            if (childCountLabel == null) return;

            var childCount = _targetObjects.Sum(MaterialCombiner.CountMeshRenderersInChildren);
            childCountLabel.text = $"処理対象の子MeshRendererオブジェクト: {childCount}個";
        }

        private int GetAtlasSizeIndex(int atlasSize)
        {
            for (var i = 0; i < _atlasSizeOptions.Length; i++)
            {
                if (int.Parse(_atlasSizeOptions[i]) == atlasSize)
                {
                    return i;
                }
            }

            // デフォルトは2048 (インデックス2)
            return 2;
        }

        private void ShowShaderSelectionMenu(Button shaderButton)
        {
            var menu = new GenericDropdownMenu();

            // プロジェクト内のすべてのシェーダーを検索
            var shaderGuids = AssetDatabase.FindAssets("t:Shader");
            foreach (var guid in shaderGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                var shaderName = shader.name;

                menu.AddItem(shaderName, _config.DefaultShaderName == shaderName,
                    () => { SetDefaultShader(shaderName, shaderButton); });
            }

            menu.DropDown(shaderButton.worldBound, shaderButton);
        }

        private void SetDefaultShader(string shaderName, Button shaderButton)
        {
            _config = _config with { DefaultShaderName = shaderName };
            shaderButton.text = shaderName;
        }

        private void ProcessSelectedObjects()
        {
            var result = MaterialCombiner.ProcessMultipleObjects(_targetObjects, _config);

            var successCount = result.Item1;
            var errorMessages = result.Item2;

            var message = $"{successCount}個のオブジェクトを正常に処理しました。";
            if (errorMessages.Count > 0)
            {
                message += $"\n\n{errorMessages.Count}個のエラー:";
                message = errorMessages.Aggregate(message, (current, error) => current + $"\n- {error}");
            }

            EditorUtility.DisplayDialog("処理完了", message, "OK");
        }
    }
}
#endif
