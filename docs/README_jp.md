# MaterialCombiner

## 概要
MaterialCombinerは、オブジェクト上の複数のマテリアルを単一のマテリアルに結合するUnityエディタ拡張です。

## 動作環境
- Unity 2022.3 以上

## インストール
Unity Package Managerを使用してGitHubから直接インストールします：
```
https://github.com/bigdra50/MaterialCombiner.git?path=Assets/bigdra50/MaterialCombiner
```

## 使用方法
1. Unityメニューから「Tools > Material Combiner」を選択
2. 結合したいマテリアルを持つオブジェクトを選択
3. 設定を調整（出力パス、アトラスサイズなど）
4. 「Process Selected Objects」ボタンをクリック

## 動作原理
1. 選択されたオブジェクトから複数のマテリアルとテクスチャを抽出
2. これらのテクスチャを単一のテクスチャアトラスに結合
3. 新しいアトラス内の位置に合わせて元のメッシュのUV座標を再計算
4. 新しいマテリアルを作成し、結合されたテクスチャアトラスを適用
5. オブジェクトのメッシュとマテリアルを新しく作成されたものに置き換え
6. 処理結果（メッシュ、マテリアル、テクスチャ）を指定された出力パスに保存

## ライセンス
MIT License