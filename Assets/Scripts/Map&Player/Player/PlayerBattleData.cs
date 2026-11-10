using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 전투 원본 데이터와 장비 목록 및 전투 결과 관리
/// 데이터 책임: Character / 현재 사용 Aether / 보유 및 장착 목록 / Weapon / Level / 누적 Experience
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBattleData : MonoBehaviour
{
    // 장착 및 보관 용량 제한
    public const int EquipmentCapacity = 5;  // 장착 가능한 에테르 슬롯 수
    public const int StorageCapacity = 1000; // 미장착 에테르 보관 한도

    // 전투 캐릭터 원본
    [SerializeField] private CharacterData character; // 플레이어의 전투 캐릭터 원본

    // 에테르 보유 및 장착 상태
    [SerializeField] private AetherData equippedAether;                                        // 현재 전투에 사용할 활성 에테르
    [SerializeField] private AetherData[] ownedAethers = Array.Empty<AetherData>();            // 중복 ID를 제외한 보유 에테르 목록
    [SerializeField] private AetherData[] equippedAethers = new AetherData[EquipmentCapacity]; // 슬롯 순서에 따른 장착 에테르 목록

    // 무기 보유 및 장착 상태
    [SerializeField] private WeaponData weapon;                                     // 현재 장착한 보유 무기
    [SerializeField] private WeaponData[] ownedWeapons = Array.Empty<WeaponData>(); // 중복 ID를 제외한 보유 무기 목록

    // 누적 경험치와 레벨 상태
    [Range(1, LevelProgression.MaxLevel)]
    [SerializeField] private int level = 1;  // 누적 경험치에 따른 현재 레벨
    [Min(0)]
    [SerializeField] private int experience; // 레벨업 시 차감하지 않는 누적 경험치

    // 전투 및 인벤토리 상태 조회
    public CharacterData Character => character;
    public AetherData EquippedAether
    {
        get
        {
            // 파괴된 Unity 에셋을 제외하고 기존 활성 항목으로 대체
            AetherData firstAether = GetEquippedAether(0);
            return firstAether != null ? firstAether : equippedAether;
        }
    }

    public AetherData[] OwnedAethers => ownedAethers != null ? (AetherData[])ownedAethers.Clone() : Array.Empty<AetherData>();
    public int AetherSlotCount => equippedAethers?.Length ?? 0;
    public int EquippedAetherCount
    {
        get
        {
            // 빈 슬롯을 제외한 장착 수 집계
            int count = 0;

            for (int i = 0; i < AetherSlotCount; i++)
            {
                if (equippedAethers[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public AetherData[] StoredAethers
    {
        get
        {
            // 보유 목록에서 장착 중인 ID를 제외한 독립 배열 구성
            List<AetherData> stored = new();

            foreach (AetherData aether in ownedAethers)
            {
                if (aether != null && FindEquipped(aether.itemId) == null)
                {
                    stored.Add(aether);
                }
            }

            return stored.ToArray();
        }
    }

    public int StorageCount => StoredAethers.Length;
    public bool CanEditEquipment => !BattleSession.IsActive;
    public WeaponData Weapon => weapon;
    public WeaponData[] OwnedWeapons => (WeaponData[])ownedWeapons.Clone();
    public int Level => level;
    public int Experience => experience;
    public bool IsMaxLevel => level >= LevelProgression.MaxLevel;
    public int ExperienceToNextLevel => IsMaxLevel ? 0 : LevelProgression.GetRequiredExperience(level + 1) - experience;
    public bool IsValid => character != null && equippedAether != null && FindEquipped(equippedAether.itemId) != null && FindOwned(equippedAether.itemId) != null;

    // 장비 및 보유 목록 변경 통지
    public event Action EquipmentChanged;    // 장착 항목 변경 알림
    public event Action OwnedAethersChanged; // 보유 에테르 목록 변경 알림
    public event Action OwnedWeaponsChanged; // 보유 무기 목록 변경 알림

    // 항목 ID를 기준으로 무기 보유 여부 조회
    public bool OwnsWeapon(WeaponData item)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        return item != null && FindOwnedWeapon(item.ItemId) != null;
    }


    // 보유 무기 목록에서 ID에 대응하는 원본 에셋 조회
    private WeaponData FindOwnedWeapon(int id)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        foreach (WeaponData item in ownedWeapons)
        {
            if (item != null && item.ItemId == id)
            {
                return item;
            }
        }

        return null;
    }


    // 중복 보유를 제외한 무기 추가 및 목록 변경 알림
    public bool TryAddWeapon(WeaponData item)
    {
        // 전투 중 변경과 null 입력 및 중복 보유 무기 추가 차단
        if (!CanEditEquipment || item == null || OwnsWeapon(item))
        {
            return false;
        }

        int count = ownedWeapons.Length;
        Array.Resize(ref ownedWeapons, count + 1);
        ownedWeapons[count] = item;
        OwnedWeaponsChanged?.Invoke();
        return true;
    }


    // 보유 무기 장착 및 장비 변경 알림
    public bool TryEquipWeapon(WeaponData item)
    {
        // 전투 중 변경과 null 입력 차단
        if (!CanEditEquipment || item == null)
        {
            return false;
        }

        WeaponData owned = FindOwnedWeapon(item.ItemId);

        if (owned == null)
        {
            return false;
        }

        weapon = owned;
        EquipmentChanged?.Invoke();
        return true;
    }


    // 현재 무기 장착 해제 및 장비 변경 알림
    public bool TryUnequipWeapon()
    {
        // 전투 중 변경과 미장착 상태의 해제 차단
        if (!CanEditEquipment || weapon == null)
        {
            return false;
        }

        weapon = null;
        EquipmentChanged?.Invoke();
        return true;
    }


    // 항목 ID를 기준으로 에테르 보유 여부 조회
    public bool OwnsAether(AetherData aether)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        return aether != null && FindOwned(aether.itemId) != null;
    }


    // 신규 에테르 보유 목록 추가 및 빈 슬롯 우선 장착
    public bool TryAddAether(AetherData aether)
    {
        // 전투 중 변경과 null 입력 및 중복 보유 에테르 추가 차단
        if (!CanEditEquipment || aether == null || OwnsAether(aether))
        {
            return false;
        }

        // 빈 장착 슬롯 우선 사용 및 가득 찬 경우 보관 용량 검사
        int emptySlot = Array.FindIndex(equippedAethers, item => item == null);

        if (emptySlot < 0 && StorageCount >= StorageCapacity)
        {
            return false;
        }

        int count = ownedAethers?.Length ?? 0;
        Array.Resize(ref ownedAethers, count + 1);
        ownedAethers[count] = aether;

        if (emptySlot >= 0)
        {
            equippedAethers[emptySlot] = aether;
        }

        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        OwnedAethersChanged?.Invoke();
        return true;
    }


    private void Awake()
    {
        // 저장된 레벨과 경험치 정규화
        LevelProgression.Normalize(ref level, ref experience);

        // 보유 무기의 중복 ID 제거 및 장착 무기를 보유 원본으로 정규화
        List<WeaponData> uniqueWeapons = new();
        HashSet<int> weaponIds = new();

        foreach (WeaponData item in ownedWeapons ?? Array.Empty<WeaponData>())
        {
            if (item != null && weaponIds.Add(item.ItemId))
            {
                uniqueWeapons.Add(item);
            }
        }

        ownedWeapons = uniqueWeapons.ToArray();
        weapon = weapon != null ? FindOwnedWeapon(weapon.ItemId) : null;
        ownedAethers ??= Array.Empty<AetherData>();

        // 기존 씬의 보유 에테르 중복 제거 및 초기 보유 순서 유지
        List<AetherData> unique = new();
        HashSet<int> ids = new();

        foreach (AetherData aether in ownedAethers)
        {
            if (aether != null && ids.Add(aether.itemId))
            {
                unique.Add(aether);
            }
        }

        ownedAethers = unique.ToArray();

        // 장착 배열 크기 보정 및 보유하지 않은 항목과 중복 슬롯 제거
        equippedAethers ??= new AetherData[EquipmentCapacity];
        Array.Resize(ref equippedAethers, EquipmentCapacity);

        for (int i = 0; i < AetherSlotCount; i++)
        {
            AetherData aether = equippedAethers[i];
            equippedAethers[i] = aether != null ? FindOwned(aether.itemId) : null;

            for (int j = 0; j < i && equippedAethers[i] != null; j++)
            {
                if (equippedAethers[j] != null && equippedAethers[j].itemId == equippedAethers[i].itemId)
                {
                    equippedAethers[i] = null;
                }
            }
        }

        // 남은 빈 슬롯에 초기 보유 순서로 자동 장착
        foreach (AetherData aether in ownedAethers)
        {
            if (FindEquipped(aether.itemId) != null)
            {
                continue;
            }

            int emptySlot = Array.FindIndex(equippedAethers, item => item == null);

            if (emptySlot < 0)
            {
                break;
            }

            equippedAethers[emptySlot] = aether;
        }

        RefreshActiveAether();
    }


    // 유효한 슬롯의 장착 항목 조회
    public AetherData GetEquippedAether(int slotIndex)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        return slotIndex >= 0 && slotIndex < AetherSlotCount ? equippedAethers[slotIndex] : null;
    }


    // 다른 슬롯의 중복 장착을 제한하며 보유 에테르 장착
    public bool TryEquipAether(int slotIndex, AetherData aether)
    {
        // 전투 중 변경과 슬롯 범위 초과 및 null 입력 차단
        if (!CanEditEquipment || slotIndex < 0 || slotIndex >= AetherSlotCount || aether == null)
        {
            return false;
        }

        AetherData owned = FindOwned(aether.itemId);

        if (owned == null)
        {
            return false;
        }

        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (i != slotIndex && equippedAethers[i] != null && equippedAethers[i].itemId == owned.itemId)
            {
                return false;
            }
        }

        if (equippedAethers[slotIndex] == owned)
        {
            return true;
        }

        equippedAethers[slotIndex] = owned;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }


    // 에테르 ID에 대응하는 장착 슬롯 조회 및 미장착 시 -1 반환
    public int GetEquippedSlot(AetherData aether)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        if (aether == null)
        {
            return -1;
        }

        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] != null && equippedAethers[i].itemId == aether.itemId)
            {
                return i;
            }
        }

        return -1;
    }


    // 보유 항목의 드래그 허용 여부 조회
    public bool CanDragAether(AetherData aether)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        return CanEditEquipment && OwnsAether(aether);
    }


    // 보유 항목을 대상 슬롯으로 이동하고 기존 장착 항목 교환
    public bool TryMoveAetherToSlot(AetherData aether, int targetSlot)
    {
        // 전투 중 변경과 대상 슬롯 범위 초과 및 null 입력 차단
        if (!CanEditEquipment || aether == null || targetSlot < 0 || targetSlot >= AetherSlotCount)
        {
            return false;
        }

        AetherData owned = FindOwned(aether.itemId);

        if (owned == null)
        {
            return false;
        }

        // 장착 출발 위치 확인 및 같은 슬롯 요청의 중복 변경 방지
        int sourceSlot = GetEquippedSlot(owned);

        if (sourceSlot == targetSlot)
        {
            return true;
        }

        if (sourceSlot >= 0)
        {
            equippedAethers[sourceSlot] = equippedAethers[targetSlot];
        }

        equippedAethers[targetSlot] = owned;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }


    // 마지막 장착 항목과 보관함 용량을 보호하며 장착 해제
    public bool TryUnequipAether(int slotIndex)
    {
        // 빈 슬롯과 마지막 장착 항목 및 보관함 포화 상태의 해제 차단
        if (!CanEditEquipment || slotIndex < 0 || slotIndex >= AetherSlotCount ||
            equippedAethers[slotIndex] == null || EquippedAetherCount <= 1 || StorageCount >= StorageCapacity)
        {
            return false;
        }

        equippedAethers[slotIndex] = null;
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }


    // 빈 슬롯을 제외한 전투용 목록의 독립 배열 반환
    public AetherData[] GetBattleAethers()
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        List<AetherData> aethers = new();

        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] != null)
            {
                aethers.Add(equippedAethers[i]);
            }
        }

        return aethers.ToArray();
    }


    // 보유 에테르 목록에서 ID에 대응하는 원본 에셋 조회
    private AetherData FindOwned(int id)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        if (ownedAethers == null)
        {
            return null;
        }

        foreach (AetherData aether in ownedAethers)
        {
            if (aether != null && aether.itemId == id)
            {
                return aether;
            }
        }

        return null;
    }


    // 장착 슬롯에서 ID에 대응하는 에테르 조회
    private AetherData FindEquipped(int id)
    {
        // 현재 보유 및 장착 데이터에 따른 결과 조회
        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] != null && equippedAethers[i].itemId == id)
            {
                return equippedAethers[i];
            }
        }

        return null;
    }


    // 1번 슬롯 우선 적용 및 빈 슬롯일 때 기존 활성 항목 유지
    private void RefreshActiveAether()
    {
        // 전투 시작용 1번 슬롯 우선 선택
        if (GetEquippedAether(0) != null)
        {
            equippedAether = GetEquippedAether(0);
            return;
        }

        // 기존 활성 항목이 남아 있으면 유지하고 없으면 첫 유효 장착 항목으로 대체
        equippedAether = equippedAether != null ? FindEquipped(equippedAether.itemId) : null;

        if (equippedAether != null)
        {
            return;
        }

        for (int i = 0; i < AetherSlotCount; i++)
        {
            if (equippedAethers[i] == null)
            {
                continue;
            }

            equippedAether = equippedAethers[i];
            return;
        }
    }


    // 두 개 이상 장착 시 2~5번 슬롯에서 이동 및 교환 허용
    public bool TrySwapAethers(int sourceSlot, int targetSlot)
    {
        // 편집 상태와 장착 수 및 출발·대상 슬롯 제한 검사
        if (!CanEditEquipment || EquippedAetherCount < 2 || sourceSlot <= 0 || targetSlot < 0 ||
            sourceSlot >= AetherSlotCount || targetSlot >= AetherSlotCount || sourceSlot == targetSlot ||
            equippedAethers[sourceSlot] == null)
        {
            return false;
        }

        // 대상과 출발 항목 교환 후 활성 에테르 갱신 및 변경 통지
        (equippedAethers[targetSlot], equippedAethers[sourceSlot]) = (equippedAethers[sourceSlot], equippedAethers[targetSlot]);
        RefreshActiveAether();
        EquipmentChanged?.Invoke();
        return true;
    }


    // 검증된 저장 데이터의 ID를 원본 에셋 참조로 복원
    public void RestoreSave(GameSaveData save, GameSaveCatalog catalog)
    {
        // 호출부에서 검증된 저장 ID를 카탈로그의 원본 에셋으로 변환
        character = catalog.Character(save.characterId);
        level = save.level;
        experience = save.experience;
        LevelProgression.Normalize(ref level, ref experience);
        ownedAethers = Array.ConvertAll(save.ownedAetherIds, catalog.Aether);
        equippedAethers = Array.ConvertAll(save.equippedAetherIds, catalog.Aether);
        equippedAether = catalog.Aether(save.activeAetherId);
        RefreshActiveAether();
        ownedWeapons = Array.ConvertAll(save.ownedWeaponIds, catalog.Weapon);
        weapon = catalog.Weapon(save.equippedWeaponId);

        // 복원 완료 후 보유 목록과 장비 상태를 UI 구독자에게 통지
        OwnedAethersChanged?.Invoke();
        OwnedWeaponsChanged?.Invoke();
        EquipmentChanged?.Invoke();
    }


    // 전투 결과의 장착 에테르와 획득 경험치 반영
    public void ApplyBattleResult(BattleState state)
    {
        // 전투 결과가 없는 호출 제외
        if (state == null)
        {
            return;
        }

        AetherData aether = state.Aether != null ? FindEquipped(state.Aether.itemId) : null;

        if (aether != null)
        {
            equippedAether = aether;
        }

        RefreshActiveAether();

        // 전투 보상 경험치 적용 및 최종 장비 상태 통지
        GainExperience(state.Experience);
        EquipmentChanged?.Invoke();
    }


    // 경험치 반영 및 상승한 레벨 수 반환
    public int GainExperience(int amount)
    {
        // 레벨 규칙에 누적 경험치 전달
        return LevelProgression.GainExperience(ref level, ref experience, amount);
    }
}
