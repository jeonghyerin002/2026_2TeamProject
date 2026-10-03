using System;
using UnityEngine;

/// <summary>
/// 플레이어의 전투 원본 데이터와 전투 결과를 관리한다
/// 데이터 책임:  Character/ 현재 장착 Aether / 보유 Aether /  Weapon / Level / 누적 Experience
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBattleData : MonoBehaviour
{
    [SerializeField] private CharacterData character;
    [SerializeField] private AetherData equippedAether;
    [SerializeField] private AetherData[] ownedAethers = Array.Empty<AetherData>();
    [SerializeField] private WeaponData weapon;
    [SerializeField, Range(1, 100)] private int level = 1;
    [SerializeField, Min(0)] private int experience;

    public CharacterData Character => character;
    public AetherData EquippedAether => equippedAether;
    public AetherData[] OwnedAethers => ownedAethers;
    public WeaponData Weapon => weapon;
    public int Level => level;
    public int Experience => experience;
    public bool IsValid => character != null && equippedAether != null;

    // 전투에서 획득한 경험치와 최종 장착 에테르를 반영한다
    public void ApplyBattleResult(BattleState state)
    {
        if (state == null)
            return;

        equippedAether = state.Aether;
        experience += Mathf.Max(0, state.Experience);
    }
}