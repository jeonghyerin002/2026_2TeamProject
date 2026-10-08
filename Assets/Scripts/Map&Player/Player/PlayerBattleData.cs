using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 전투 원본 데이터와 전투 결과를 관리한다
/// 데이터 책임: Character / 현재 사용 Aether / 보유 및 장착 목록 / Weapon / Level / 누적 Experience
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBattleData : MonoBehaviour
{
    [SerializeField] private CharacterData character;
    [SerializeField] private AetherData equippedAether;
    [SerializeField] private AetherData[] ownedAethers = Array.Empty<AetherData>();
    [SerializeField] private AetherData[] equippedAethers = new AetherData[5];
    [SerializeField] private WeaponData weapon;
    [SerializeField, Range(1, 100)] private int level = 1;
    [SerializeField, Min(0)] private int experience;

    public CharacterData Character => character;
    public AetherData EquippedAether => equippedAether;
    public AetherData[] OwnedAethers => ownedAethers != null ? (AetherData[])ownedAethers.Clone() : Array.Empty<AetherData>();
    public int AetherSlotCount => equippedAethers?.Length ?? 0;
    public bool CanEditEquipment => !BattleSession.IsActive;
    public WeaponData Weapon => weapon;
    public int Level => level;
    public int Experience => experience;
    public bool IsValid => character != null && equippedAether != null && FindEquipped(equippedAether.itemId) != null && FindOwned(equippedAether.itemId) != null;

    public event Action EquipmentChanged;
    public event Action OwnedAethersChanged;

    public bool OwnsAether(AetherData aether)
    {
        return aether != null && FindOwned(aether.itemId) != null;
    }

    public bool TryAddAether(AetherData aether)
    {
        if (!CanEditEquipment || aether == null || OwnsAether(aether))
            return false;
        int count = ownedAethers?.Length ?? 0;
        Array.Resize(ref ownedAethers, count + 1);
        ownedAethers[count] = aether;
        OwnedAethersChanged?.Invoke();
        return true;
    }

    private void Awake()
    {
        for (int i = 0; i < AetherSlotCount; i++)
        {
            AetherData aether = equippedAethers[i];
            equippedAethers[i] = aether != null ? FindOwned(aether.itemId) : null;
            for (int j = 0; j < i && equippedAethers[i] != null; j++)
            {
                if (equippedAethers[j] != null && equippedAethers[j].itemId == equippedAethers[i].itemId)
                    equippedAethers[i] = null;
            }
        }
        RefreshActiveAether();
    }

    public AetherData GetEquippedAether(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < AetherSlotCount ? equippedAethers[slotIndex] : null;
    }

    public bool TryEquipAether(int slotIndex, AetherData aether)
    {
        if (!CanEditEquipment || slotIndex < 0 || slotIndex >= AetherSlotCount || aether == null)
            return false;
        AetherData owned = FindOwned(aether.itemId);
        if (owned == null)
            return false;
        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (i != slotIndex && equippedAethers[i] != null && equippedAethers[i].itemId == owned.itemId)
                return false;
        }
        if (equippedAethers[slotIndex] == owned)
            return true;

        equippedAethers[slotIndex] = owned;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool TryUnequipAether(int slotIndex)
    {
        if (!CanEditEquipment || slotIndex < 0 || slotIndex >= AetherSlotCount || equippedAethers[slotIndex] == null)
            return false;
        equippedAethers[slotIndex] = null;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }

    // 빈 슬롯을 제외한 전투용 목록을 독립 배열로 반환함
    public AetherData[] GetBattleAethers()
    {
        List<AetherData> aethers = new();
        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] != null)
                aethers.Add(equippedAethers[i]);
        }
        return aethers.ToArray();
    }

    private AetherData FindOwned(int id)
    {
        if (ownedAethers == null)
            return null;
        foreach (AetherData aether in ownedAethers)
        {
            if (aether != null && aether.itemId == id)
                return aether;
        }
        return null;
    }

    private AetherData FindEquipped(int id)
    {
        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] != null && equippedAethers[i].itemId == id)
                return equippedAethers[i];
        }
        return null;
    }

    private void RefreshActiveAether()
    {
        equippedAether = equippedAether != null ? FindEquipped(equippedAether.itemId) : null;
        if (equippedAether != null)
            return;
        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] == null)
                continue;
            equippedAether = equippedAethers[i];
            return;
        }
    }

    // 전투에서 획득한 경험치와 최종 장착 에테르를 반영한다
    public void ApplyBattleResult(BattleState state)
    {
        if (state == null)
            return;

        AetherData aether = state.Aether != null ? FindEquipped(state.Aether.itemId) : null;
        if (aether != null)
            equippedAether = aether;
        experience += Mathf.Max(0, state.Experience);
        EquipmentChanged?.Invoke();
    }
}
