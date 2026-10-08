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
    public const int EquipmentCapacity = 5;
    public const int StorageCapacity = 1000;
    [SerializeField] private CharacterData character;
    [SerializeField] private AetherData equippedAether;
    [SerializeField] private AetherData[] ownedAethers = Array.Empty<AetherData>();
    [SerializeField] private AetherData[] equippedAethers = new AetherData[5];
    [SerializeField] private WeaponData weapon;
    [SerializeField] private WeaponData[] ownedWeapons = Array.Empty<WeaponData>();
    [SerializeField, Range(1, 100)] private int level = 1;
    [SerializeField, Min(0)] private int experience;

    public CharacterData Character => character;
    public AetherData EquippedAether => equippedAether;
    public AetherData[] OwnedAethers => ownedAethers != null ? (AetherData[])ownedAethers.Clone() : Array.Empty<AetherData>();
    public int AetherSlotCount => equippedAethers?.Length ?? 0;
    public int EquippedAetherCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < AetherSlotCount; i++)
                if (equippedAethers[i] != null)
                    count++;
            return count;
        }
    }
    public AetherData[] StoredAethers
    {
        get
        {
            List<AetherData> stored = new();
            foreach (AetherData aether in ownedAethers)
                if (aether != null && FindEquipped(aether.itemId) == null)
                    stored.Add(aether);
            return stored.ToArray();
        }
    }
    public int StorageCount => StoredAethers.Length;
    public bool CanEditEquipment => !BattleSession.IsActive;
    public WeaponData Weapon => weapon;
    public WeaponData[] OwnedWeapons => (WeaponData[])ownedWeapons.Clone();
    public int Level => level;
    public int Experience => experience;
    public bool IsValid => character != null && equippedAether != null && FindEquipped(equippedAether.itemId) != null && FindOwned(equippedAether.itemId) != null;

    public event Action EquipmentChanged;
    public event Action OwnedAethersChanged;
    public event Action OwnedWeaponsChanged;

    public bool OwnsWeapon(WeaponData item) => item != null && FindOwnedWeapon(item.ItemId) != null;

    private WeaponData FindOwnedWeapon(int id)
    {
        foreach (WeaponData item in ownedWeapons)
            if (item != null && item.ItemId == id) return item;
        return null;
    }

    public bool TryAddWeapon(WeaponData item)
    {
        if (!CanEditEquipment || item == null || OwnsWeapon(item)) return false;
        int count = ownedWeapons.Length;
        Array.Resize(ref ownedWeapons, count + 1);
        ownedWeapons[count] = item;
        OwnedWeaponsChanged?.Invoke();
        return true;
    }

    public bool TryEquipWeapon(WeaponData item)
    {
        if (!CanEditEquipment || item == null) return false;
        WeaponData owned = FindOwnedWeapon(item.ItemId);
        if (owned == null) return false;
        weapon = owned;
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool TryUnequipWeapon()
    {
        if (!CanEditEquipment || weapon == null) return false;
        weapon = null;
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool OwnsAether(AetherData aether)
    {
        return aether != null && FindOwned(aether.itemId) != null;
    }

    public bool TryAddAether(AetherData aether)
    {
        if (!CanEditEquipment || aether == null || OwnsAether(aether))
            return false;
        int emptySlot = Array.FindIndex(equippedAethers, item => item == null);
        if (emptySlot < 0 && StorageCount >= StorageCapacity)
            return false;
        int count = ownedAethers?.Length ?? 0;
        Array.Resize(ref ownedAethers, count + 1);
        ownedAethers[count] = aether;
        if (emptySlot >= 0)
            equippedAethers[emptySlot] = aether;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        OwnedAethersChanged?.Invoke();
        return true;
    }

    private void Awake()
    {
        List<WeaponData> uniqueWeapons = new();
        HashSet<int> weaponIds = new();
        foreach (WeaponData item in ownedWeapons ?? Array.Empty<WeaponData>())
            if (item != null && weaponIds.Add(item.ItemId)) uniqueWeapons.Add(item);
        ownedWeapons = uniqueWeapons.ToArray();
        weapon = weapon != null ? FindOwnedWeapon(weapon.ItemId) : null;
        ownedAethers ??= Array.Empty<AetherData>();
        // 기존 씬의 설정을 정규화한다. 초기 보유 순서를 자동 장착 순서로 사용한다.
        List<AetherData> unique = new();
        HashSet<int> ids = new();
        foreach (AetherData aether in ownedAethers)
            if (aether != null && ids.Add(aether.itemId))
                unique.Add(aether);
        ownedAethers = unique.ToArray();
        equippedAethers ??= new AetherData[EquipmentCapacity];
        Array.Resize(ref equippedAethers, EquipmentCapacity);
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
        foreach (AetherData aether in ownedAethers)
        {
            if (FindEquipped(aether.itemId) != null)
                continue;
            int emptySlot = Array.FindIndex(equippedAethers, item => item == null);
            if (emptySlot < 0)
                break;
            equippedAethers[emptySlot] = aether;
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

    public int GetEquippedSlot(AetherData aether)
    {
        if (aether == null)
            return -1;
        for (int i = 0; i < AetherSlotCount; i++)
            if (equippedAethers[i] != null && equippedAethers[i].itemId == aether.itemId)
                return i;
        return -1;
    }

    // 마지막 장착 항목도 다른 슬롯으로 이동할 수 있다. 보관함 이동 제한은 장착 해제 시 검사한다.
    public bool CanDragAether(AetherData aether)
    {
        return CanEditEquipment && OwnsAether(aether);
    }

    // 보관 항목은 대상 슬롯과 교환하고, 장착 항목은 원래 슬롯과 대상 슬롯을 교환한다.
    public bool TryMoveAetherToSlot(AetherData aether, int targetSlot)
    {
        if (!CanEditEquipment || aether == null || targetSlot < 0 || targetSlot >= AetherSlotCount)
            return false;
        AetherData owned = FindOwned(aether.itemId);
        if (owned == null)
            return false;
        int sourceSlot = GetEquippedSlot(owned);
        if (sourceSlot == targetSlot)
            return true;
        if (sourceSlot >= 0)
            equippedAethers[sourceSlot] = equippedAethers[targetSlot];
        equippedAethers[targetSlot] = owned;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool TryUnequipAether(int slotIndex)
    {
        if (!CanEditEquipment || slotIndex < 0 || slotIndex >= AetherSlotCount ||
            equippedAethers[slotIndex] == null || EquippedAetherCount <= 1 || StorageCount >= StorageCapacity)
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
