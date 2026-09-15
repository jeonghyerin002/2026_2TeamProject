using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject.ItemManagement
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameItemDatabase : MonoBehaviour
    {
        public const string ResourcePath = "ItemDatabase/items";
        public const string AssetResourcePath = "ItemDatabase/GameItemDatabase";
        [Tooltip("ScriptableObject에서 읽은 내용입니다. 원본 수정은 엑셀에서 합니다.")]
        [SerializeField] private GameItemDataFile loadedData;
        private Dictionary<string, GameItemCategory> categoriesById = new Dictionary<string, GameItemCategory>();
        private Dictionary<string, GameItemDefinition> itemsById = new Dictionary<string, GameItemDefinition>();

        public event Action DatabaseChanged;
        public string LastError { get; private set; }
        public int CategoryCount => loadedData?.categories?.Length ?? 0;
        public bool IsLoaded => loadedData != null;

        private void Awake() { Reload(); }

        [ContextMenu("Reload Item Database")]
        public void Reload()
        {
            GameItemDatabaseAsset asset = Resources.Load<GameItemDatabaseAsset>(AssetResourcePath);
            if (asset != null && asset.Data != null)
            {
                if (LoadData(asset.Data)) LogLoaded("ScriptableObject");
                return;
            }

            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            if (source == null)
            {
                Fail("GameItemDatabase ScriptableObject와 items.json을 모두 찾을 수 없습니다.");
                return;
            }
            if (LoadFromJson(source.text)) LogLoaded("JSON 대체 데이터");
        }

        private void LogLoaded(string source)
        {
            var names = new List<string>();
            foreach (GameItemCategory category in loadedData.categories) names.Add(category.displayName);
            Debug.Log($"아이템 데이터 로드 완료 ({source}): 분류 {CategoryCount}개 / 아이템 {itemsById.Count}개 / {string.Join(", ", names)}", this);
        }

        public bool LoadFromJson(string json)
        {
            return LoadData(JsonUtility.FromJson<GameItemDataFile>(json));
        }

        public bool LoadData(GameItemDataFile parsed)
        {
            try
            {
                if (parsed == null || parsed.schemaVersion != 1 || parsed.categories == null)
                    throw new ArgumentException("schemaVersion 1과 categories 배열이 필요합니다.");
                var nextCategories = new Dictionary<string, GameItemCategory>(StringComparer.Ordinal);
                var nextItems = new Dictionary<string, GameItemDefinition>(StringComparer.Ordinal);
                foreach (GameItemCategory category in parsed.categories)
                {
                    if (category == null || string.IsNullOrWhiteSpace(category.id) ||
                        string.IsNullOrWhiteSpace(category.displayName) || category.items == null)
                        throw new ArgumentException("분류 ID, 이름, items 배열을 확인하세요.");
                    if (nextCategories.ContainsKey(category.id))
                        throw new ArgumentException("중복 분류 ID: " + category.id);
                    nextCategories.Add(category.id, category);
                    foreach (GameItemDefinition item in category.items)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.displayName) ||
                            item.initialQuantity < 0 || item.maxStack < 1)
                            throw new ArgumentException(category.id + ": 아이템 ID, 이름, 수량을 확인하세요.");
                        if (nextItems.ContainsKey(item.id)) throw new ArgumentException("중복 아이템 ID: " + item.id);
                        nextItems.Add(item.id, item);
                    }
                }
                loadedData = parsed;
                categoriesById = nextCategories;
                itemsById = nextItems;
                LastError = null;
                DatabaseChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return false;
            }
        }

        public GameItemCategory GetCategory(int index)
        {
            return index >= 0 && index < CategoryCount ? loadedData.categories[index] : null;
        }

        public GameItemCategory FindCategory(string id)
        {
            return id != null && categoriesById.TryGetValue(id, out GameItemCategory category) ? category : null;
        }

        public GameItemDefinition FindItem(string id)
        {
            return id != null && itemsById.TryGetValue(id, out GameItemDefinition item) ? item : null;
        }


        private void Fail(string message)
        {
            LastError = message;
            Debug.LogError("아이템 데이터 로드 실패: " + message + " 기존 정상 데이터는 유지됩니다.", this);
        }
    }
}
