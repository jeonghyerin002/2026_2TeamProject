using System;
using UnityEditor;
using UnityEngine;

namespace TeamProject.ItemManagement.Editor
{
    // 에디터가 변환된 JSON을 임포트하면 씬의 데이터와 구독 UI를 갱신합니다.
    public sealed class GameItemDatabaseAutoReload : AssetPostprocessor
    {
        private const string JsonPath = "Assets/Resources/ItemDatabase/items.json";
        private const string AssetPath = ItemDatabaseScriptableObjectBuilder.AssetPath;
        private static bool pending;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!ContainsDatabasePath(imported) && !ContainsDatabasePath(deleted) &&
                !ContainsDatabasePath(moved) && !ContainsDatabasePath(movedFrom)) return;
            if (pending) return;
            pending = true;
            EditorApplication.delayCall += ReloadSceneDatabases;
        }

        private static bool ContainsDatabasePath(string[] paths)
        {
            return Array.IndexOf(paths, JsonPath) >= 0 || Array.IndexOf(paths, AssetPath) >= 0;
        }

        private static void ReloadSceneDatabases()
        {
            pending = false;
            foreach (GameItemDatabase database in Resources.FindObjectsOfTypeAll<GameItemDatabase>())
            {
                if (database != null && database.gameObject.scene.IsValid()) database.Reload();
            }
        }
    }
}
