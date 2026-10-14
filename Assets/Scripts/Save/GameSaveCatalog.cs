using System;
using System.Linq;
using UnityEngine;

/// <summary>빌드에 포함되는 원본 에셋과 저장 가능한 맵을 ID로 해석한다.</summary>
public sealed class GameSaveCatalog : ScriptableObject
{
    // 저장에 사용되는 원본 에셋. 같은 종류에서 ID는 고유해야 한다.
    public CharacterData[] characters = Array.Empty<CharacterData>();
    public AetherData[] aethers = Array.Empty<AetherData>();
    public WeaponData[] weapons = Array.Empty<WeaponData>();
    // 저장 위치로 허용하는 맵과 진행도 ID 목록.
    public string[] mapScenes = Array.Empty<string>();
    public string[] npcIds = Array.Empty<string>();
    public string[] cutsceneIds = Array.Empty<string>();
    public SavedWorldActor[] actorDefinitions = Array.Empty<SavedWorldActor>();
    // 플레이어가 없는 맵으로 불러올 때 사용할 초기 플레이어 프리팹.
    public PlayerController playerPrefab;

    // ID를 영구 프로젝트 에셋으로 변환한다.
    public CharacterData Character(int id) => characters.FirstOrDefault(item => item != null && item.CharacterId == id);
    public AetherData Aether(int id) => aethers.FirstOrDefault(item => item != null && item.itemId == id);
    public WeaponData Weapon(int id) => weapons.FirstOrDefault(item => item != null && item.ItemId == id);
}
