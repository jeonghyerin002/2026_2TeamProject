using System.Collections.Generic;
using System.Linq;

/// <summary>Scene 이동 후에도 유지되는 컷신 완료와 대상 위치를 관리한다.</summary>
public static class WorldSaveProgress
{
    // 런타임 진행도는 새 게임 시작 및 불러오기에서 명시적으로 교체한다.
    private static readonly HashSet<string> completed = new();
    private static readonly Dictionary<string, SavedWorldActor> actors = new();

    // 저장용 독립 목록을 반환한다.
    public static string[] CompletedIds => completed.OrderBy(id => id).ToArray();
    public static SavedWorldActor[] Actors => actors.Values.Select(value => new SavedWorldActor
    {
        id = value.id, sceneName = value.sceneName, position = value.position
    }).ToArray();

    // 컷신 재진입과 완료 기록을 처리한다.
    public static bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && completed.Contains(id);
    public static void Complete(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            completed.Add(id);
        }
    }

    // 이동 대상의 최신 위치를 등록하고 재진입 시 조회한다.
    public static void Record(SavedWorldActor actor) => actors[actor.id] = actor;
    public static bool TryGetActor(string id, out SavedWorldActor actor) => actors.TryGetValue(id, out actor);

    // 검증된 저장 상태로 런타임 진행도를 교체한다. 빈 배열은 새 게임을 뜻한다.
    public static void Restore(string[] ids, SavedWorldActor[] savedActors)
    {
        completed.Clear();
        actors.Clear();
        foreach (var id in ids)
        {
            completed.Add(id);
        }
        foreach (var actor in savedActors)
        {
            Record(actor);
        }
    }
}
