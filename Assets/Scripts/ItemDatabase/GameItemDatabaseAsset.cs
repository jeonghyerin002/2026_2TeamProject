using UnityEngine;

namespace TeamProject.ItemManagement
{
    [CreateAssetMenu(fileName = "GameItemDatabase", menuName = "Team Project/Item Database")]
    public sealed class GameItemDatabaseAsset : ScriptableObject
    {
        [SerializeField] private GameItemDataFile data;

        public GameItemDataFile Data => data;

#if UNITY_EDITOR
        // 엑셀 변환기만 이 값을 갱신한다. Inspector에서 직접 수정하지 않는다.
        public void SetData(GameItemDataFile value)
        {
            data = value;
        }
#endif
    }
}
