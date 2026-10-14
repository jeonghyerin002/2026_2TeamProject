using UnityEngine;

/// <summary>컷신으로 이동하는 Scene 오브젝트의 위치를 안정적인 ID로 복원한다.</summary>
[DisallowMultipleComponent]
public sealed class PersistentWorldActor : MonoBehaviour
{
    // 이름이나 Hierarchy 위치를 변경해도 유지되는 자동 생성 ID.
    [SerializeField] private string persistentId;
    // 에디터 자동 등록에서 복제 오브젝트를 식별하는 소유 정보.
    [SerializeField, HideInInspector] private string saveIdentityOwner;
    public string PersistentId => persistentId;

#if UNITY_EDITOR
    // 신규 이동 대상의 ID만 생성하고 기존 세이브의 ID는 바꾸지 않는다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = System.Guid.NewGuid().ToString("N");
        }
    }
#endif

    // Scene의 Start와 물리 시뮬레이션 전에 저장한 위치를 적용한다.
    private void Awake()
    {
        if (WorldSaveProgress.TryGetActor(persistentId, out var state) && state.sceneName == gameObject.scene.name)
        {
            transform.position = state.position;
            // 물리 대상도 같은 월드 좌표로 동기화한다.
            if (TryGetComponent<Rigidbody2D>(out var body))
            {
                body.position = state.position;
            }
        }
    }

    // 컷신 완료 시 현재 위치를 진행도에 기록한다.
    public void RecordPosition()
    {
        if (!string.IsNullOrEmpty(persistentId))
        {
            WorldSaveProgress.Record(new SavedWorldActor
            {
                id = persistentId, sceneName = gameObject.scene.name, position = transform.position
            });
        }
    }
}
