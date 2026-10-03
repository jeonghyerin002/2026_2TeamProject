using UnityEngine;

/// <summary>현재 NPC 전투와 전투 종료 후 복귀 정보를 임시로 전달한다</summary>
public static class BattleSession
{
    public static string NpcId { get; private set; }
    public static NpcBattleData Enemy { get; private set; }
    public static string ReturnScene { get; private set; }
    public static Vector3 ReturnPosition { get; private set; }
    public static bool IsActive { get; private set; }

    // NPC 전투와 복귀 정보를 등록한다
    public static bool Begin(string npcId, NpcBattleData enemy, string returnScene, Vector3 returnPosition)
    {
        if (string.IsNullOrEmpty(npcId) || enemy == null || !enemy.IsValid || string.IsNullOrEmpty(returnScene))
            return false;

        NpcId = npcId;
        Enemy = enemy;
        ReturnScene = returnScene;
        ReturnPosition = returnPosition;
        IsActive = true;
        return true;
    }

    // 현재 전투의 임시 정보를 초기화한다
    public static void Clear()
    {
        NpcId = null;
        Enemy = null;
        ReturnScene = null;
        ReturnPosition = default;
        IsActive = false;
    }
}