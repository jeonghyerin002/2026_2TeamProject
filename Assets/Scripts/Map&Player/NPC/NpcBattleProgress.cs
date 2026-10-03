using System.Collections.Generic;
using UnityEngine;

/// <summary>현재 실행 중 승리한 NPC의 고유 ID를 관리한다</summary>
public static class NpcBattleProgress
{
    private static readonly HashSet<string> defeatedNpcIds = new();

    // 새로운 게임 실행 시 런타임 진행도를 초기화한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        defeatedNpcIds.Clear();
    }

    // 해당 NPC에게 이미 승리했는지 확인한다
    public static bool IsDefeated(string npcId)
    {
        return !string.IsNullOrEmpty(npcId) && defeatedNpcIds.Contains(npcId);
    }

    // 승리한 NPC를 중복 없이 등록한다
    public static void MarkDefeated(string npcId)
    {
        if (!string.IsNullOrEmpty(npcId))
            defeatedNpcIds.Add(npcId);
    }
}