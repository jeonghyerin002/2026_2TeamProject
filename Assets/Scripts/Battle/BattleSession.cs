using UnityEngine;

/// <summary>현재 NPC 전투와 전투 종료 후 복귀 정보를 임시로 전달한다</summary>
public static class BattleSession
{
    public static string NpcId { get; private set; }
    public static CharacterData Enemy { get; private set; }
    public static string ReturnScene { get; private set; }
    public static Vector3 ReturnPosition { get; private set; }
    public static bool IsActive { get; private set; }
    public static int VictoryDialogueId { get; private set; } // 이번 전투에서 승리했을 때만 복귀 후 자동 출력할 대화 ID

    private static int afterVictoryDialogueId; // 전투를 시작한 NPC에 설정된 승리 후 대화 ID

    // NPC 전투와 복귀 정보를 등록한다
    public static bool Begin(string npcId, CharacterData enemy, string returnScene, Vector3 returnPosition, int victoryDialogueId = 0)
    {
        if (string.IsNullOrEmpty(npcId) || enemy == null || !enemy.IsValid || string.IsNullOrEmpty(returnScene))
            return false;

        NpcId = npcId;
        Enemy = enemy;
        ReturnScene = returnScene;
        ReturnPosition = returnPosition;
        afterVictoryDialogueId = victoryDialogueId;
        VictoryDialogueId = 0;
        IsActive = true;
        return true;
    }

    // 현재 전투의 플레이어 승리가 확정된 경우에만 자동 대화를 예약한다.
    public static void RecordVictory()
    {
        if (IsActive)
            VictoryDialogueId = afterVictoryDialogueId;
    }

    // Domain Reload가 꺼져 있어도 새 실행의 전투 정보를 초기화함
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Clear();
    }

    // 현재 전투의 임시 정보를 초기화한다
    public static void Clear()
    {
        NpcId = null;
        Enemy = null;
        ReturnScene = null;
        ReturnPosition = default;
        IsActive = false;
        afterVictoryDialogueId = 0;
        VictoryDialogueId = 0;
    }
}
