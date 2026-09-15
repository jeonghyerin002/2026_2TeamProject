using UnityEditor;
using UnityEngine;

namespace TeamProject.ItemManagement.Editor
{
    public static class ItemDatabaseScriptableObjectBuilder
    {
        public const string AssetPath = "Assets/Resources/ItemDatabase/GameItemDatabase.asset";

        // 기존 에셋을 유지하면서 내용만 바꾼다. 다른 컴포넌트의 참조가 끊기지 않는다.
        public static GameItemDatabaseAsset CreateOrUpdate(GameItemDataFile data)
        {
            GameItemDatabaseAsset asset = AssetDatabase.LoadAssetAtPath<GameItemDatabaseAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GameItemDatabaseAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            asset.SetData(data);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            return asset;
        }
    }
}
