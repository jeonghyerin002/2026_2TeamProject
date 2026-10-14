using System;
using UnityEngine;

/// <summary>세이브 파일에는 에셋 참조 대신 고정 ID와 게임 진행 상태만 기록한다.</summary>
[Serializable]
public sealed class GameSaveData
{
    // 저장 형식의 호환 버전과 UTC 저장 시각.
    public int version = 1;
    public string savedAtUtc;
    // 재접속할 맵, 월드 좌표와 플레이어 방향.
    public string sceneName;
    public Vector3 position;
    public bool facingRight;
    // 캐릭터 원본 ID와 플레이어 성장치.
    public int characterId;
    public int level;
    public int experience;
    // 보유 순서, 빈 슬롯을 포함한 장착 순서와 현재 선택 에테르. 빈 항목은 0이다.
    public int[] ownedAetherIds;
    public int[] equippedAetherIds;
    public int activeAetherId;
    // 보유 무기와 현재 장착 무기. 미장착은 0이다.
    public int[] ownedWeaponIds;
    public int equippedWeaponId;
    // NPC 승리, 일회성 컷신 완료와 컷신이 변경한 월드 대상 위치.
    public string[] defeatedNpcIds;
    public string[] completedCutsceneIds;
    public SavedWorldActor[] worldActors;
}

/// <summary>컷신으로 이동한 대상의 Scene별 고유 ID와 최종 월드 좌표.</summary>
[Serializable]
public sealed class SavedWorldActor
{
    // 에디터에서 부여한 고유 ID 및 해당 대상이 속한 Scene 이름.
    public string id;
    public string sceneName;
    // 이동 후 좌표. 회전과 크기는 현재 컷신에서 변경하지 않는다.
    public Vector3 position;
}
