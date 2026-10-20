using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>단일 슬롯 JSON 저장, 검증과 Scene 로딩 이전/이후 복원 순서를 관리한다.</summary>
public static class GameSaveSystem
{
    // 현재 프로젝트 원본 카탈로그와 Scene 로딩을 기다리는 검증된 저장 상태.
    private static GameSaveCatalog catalog;
    private static GameSaveData pending;
    private static bool isBusy;
    public static bool IsBusy => isBusy;

#if UNITY_EDITOR
    // 에디터 검증은 실제 사용자 세이브 대신 고유 임시 파일을 사용한다.
    public static string EditorTestSavePath { get; set; }
#endif

    // 배포 환경별 Unity 영구 데이터 폴더에 저장한다.
    public static string SavePath
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestSavePath))
            {
                return EditorTestSavePath;
            }
#endif
            return Path.Combine(Application.persistentDataPath, "save.json");
        }
    }

    // Domain Reload가 꺼진 Play 모드에서도 이벤트 중복과 이전 진행도를 방지한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        SceneManager.sceneLoaded -= RestoreLoadedScene;
        catalog = null;
        pending = null;
        isBusy = false;
        WorldSaveProgress.Restore(Array.Empty<string>(), Array.Empty<SavedWorldActor>());
#if UNITY_EDITOR
        EditorTestSavePath = null;
#endif
    }

    // StartMenu나 다른 맵에서 직접 시작해도 공통 UI와 복원 이벤트를 준비한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        catalog = Resources.Load<GameSaveCatalog>("GameSaveCatalog");
        SceneManager.sceneLoaded += RestoreLoadedScene;
        // 공통 UI 프리팹은 이 저장/불러오기 기능을 위한 단일 시작 리소스다.
        var menu = Resources.Load<GameObject>("GameMenu");
        if (menu != null)
        {
            UnityEngine.Object.Instantiate(menu);
        }
    }

    // 전투 및 대화/컷신의 중간 상태를 저장하거나 교체하지 않는다.
    public static bool CanChangeSession(out string message)
    {
        message = "";
        if (isBusy || BattleSession.IsActive || SceneFadeIn.IsTransitioning)
        {
            message = "전투 또는 Scene 이동 중에는 저장/불러오기를 사용할 수 없습니다.";
            return false;
        }
        foreach (var cutscene in UnityEngine.Object.FindObjectsByType<TriggerCutscene>(FindObjectsSortMode.None))
        {
            if (cutscene.IsPlaying)
            {
                message = "컷신이 끝난 뒤 이용하세요.";
                return false;
            }
        }
        foreach (var dialogue in UnityEngine.Object.FindObjectsByType<DialogManager>(FindObjectsSortMode.None))
        {
            if (dialogue.IsOpen)
            {
                message = "대화가 끝난 뒤 이용하세요.";
                return false;
            }
        }
        return true;
    }

    // 현재 플레이어의 스냅숏을 만들고 임시 파일의 기록 완료 후 기존 파일을 교체한다.
    public static bool TrySave(out string message)
    {
        if (!CanChangeSession(out message))
        {
            return false;
        }
        var player = PlayerController.Instance;
        if (catalog == null || player == null || !player.TryGetComponent<PlayerBattleData>(out var data))
        {
            message = "저장할 플레이어 또는 저장 카탈로그가 없습니다.";
            return false;
        }
        // 저장 슬롯에는 순서와 빈 에테르 슬롯을 포함한다.
        var save = new GameSaveData
        {
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            sceneName = SceneManager.GetActiveScene().name,
            position = player.transform.position,
            facingRight = player.IsFacingRight,
            characterId = data.Character != null ? data.Character.CharacterId : 0,
            level = data.Level,
            experience = data.Experience,
            ownedAetherIds = data.OwnedAethers.Select(item => item.itemId).ToArray(),
            equippedAetherIds = Enumerable.Range(0, PlayerBattleData.EquipmentCapacity)
                .Select(index => data.GetEquippedAether(index) != null ? data.GetEquippedAether(index).itemId : 0).ToArray(),
            activeAetherId = data.EquippedAether != null ? data.EquippedAether.itemId : 0,
            ownedWeaponIds = data.OwnedWeapons.Select(item => item.ItemId).ToArray(),
            equippedWeaponId = data.Weapon != null ? data.Weapon.ItemId : 0,
            defeatedNpcIds = NpcBattleProgress.CaptureSave(),
            completedCutsceneIds = WorldSaveProgress.CompletedIds,
            worldActors = WorldSaveProgress.Actors
        };
        if (!Validate(save, catalog, out message))
        {
            return false;
        }
        try
        {
            // 같은 디렉터리에 기록해 교체 단계에서 다른 볼륨으로 이동하지 않는다.
            var path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporaryPath = path + ".tmp";
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, path + ".bak");
            }
            else
            {
                File.Move(temporaryPath, path);
            }
            message = "게임을 저장했습니다.";
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            message = "저장하지 못했습니다. 저장 폴더의 공간과 쓰기 권한을 확인하세요.";
            return false;
        }
    }

    // 패널 미리보기와 실제 불러오기 모두 파일을 읽고 같은 규칙으로 검증한다.
    public static bool TryRead(out GameSaveData save, out string message)
    {
        save = null;
        message = "저장된 게임이 없습니다";
        try
        {
            if (!File.Exists(SavePath))
            {
                return false;
            }
            if (new FileInfo(SavePath).Length > 1024 * 1024)
            {
                message = "저장 파일의 크기가 올바르지 않습니다.";
                return false;
            }
            save = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath, Encoding.UTF8));
            if (!Validate(save, catalog, out message))
            {
                save = null;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            save = null;
            message = "저장 파일을 읽을 수 없습니다. 파일이 손상되었거나 접근할 수 없습니다.";
            return false;
        }
    }

    // 손상된 저장 파일도 삭제할 수 있도록 내용 검증과 파일 존재 여부를 분리한다.
    public static bool HasSaveFiles => File.Exists(SavePath) || File.Exists(SavePath + ".bak") || File.Exists(SavePath + ".tmp");

    // 슬롯 삭제는 백업과 임시 파일도 정리하며 실행 중인 게임의 진행도는 유지한다.
    public static bool TryDeleteSave(out string message)
    {
        if (isBusy)
        {
            message = "Scene 이동 중에는 저장 슬롯을 삭제할 수 없습니다.";
            return false;
        }
        try
        {
            var path = SavePath;
            // 정리 실패 시 본 저장 파일을 보존하도록 부속 파일부터 삭제한다.
            File.Delete(path + ".tmp");
            File.Delete(path + ".bak");
            File.Delete(path);
            message = "저장 슬롯을 삭제했습니다.";
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            message = "저장 슬롯을 삭제하지 못했습니다. 파일 접근 권한을 확인하세요.";
            return false;
        }
    }

    // 손상/다른 버전/존재하지 않는 ID를 실행 중 상태에 적용하기 전에 차단한다.
    public static bool Validate(GameSaveData save, GameSaveCatalog assets, out string message)
    {
        message = "저장 파일의 정보가 올바르지 않거나 현재 게임과 호환되지 않습니다.";
        if (save == null || assets == null || save.version != 1 ||
            !DateTimeOffset.TryParse(save.savedAtUtc, out _) ||
            !assets.mapScenes.Contains(save.sceneName) || !Application.CanStreamedLevelBeLoaded(save.sceneName) ||
            !Finite(save.position) || assets.Character(save.characterId) == null ||
            save.level < 1 || save.level > 100 || save.experience < 0 ||
            save.ownedAetherIds == null || save.ownedWeaponIds == null || save.equippedAetherIds == null ||
            save.equippedAetherIds.Length != PlayerBattleData.EquipmentCapacity ||
            save.ownedAetherIds.Length > PlayerBattleData.StorageCapacity + PlayerBattleData.EquipmentCapacity ||
            save.ownedWeaponIds.Length > assets.weapons.Length ||
            save.defeatedNpcIds == null || save.completedCutsceneIds == null || save.worldActors == null)
        {
            return false;
        }
        // 보유 중복, 미보유 장착, 슬롯 중복 및 선택 에테르 불일치를 검사한다.
        var equipped = save.equippedAetherIds.Where(id => id != 0).ToArray();
        if (save.ownedAetherIds.Distinct().Count() != save.ownedAetherIds.Length ||
            save.ownedWeaponIds.Distinct().Count() != save.ownedWeaponIds.Length ||
            save.ownedAetherIds.Any(id => id <= 0 || assets.Aether(id) == null) ||
            save.ownedWeaponIds.Any(id => id <= 0 || assets.Weapon(id) == null) ||
            equipped.Distinct().Count() != equipped.Length || equipped.Any(id => !save.ownedAetherIds.Contains(id)) ||
            (equipped.Length == 0 ? save.activeAetherId != 0 : !equipped.Contains(save.activeAetherId)) ||
            (save.equippedWeaponId != 0 && !save.ownedWeaponIds.Contains(save.equippedWeaponId)) ||
            !ValidIds(save.defeatedNpcIds, assets.npcIds) || !ValidIds(save.completedCutsceneIds, assets.cutsceneIds) ||
            save.worldActors.Any(actor => actor == null || !Finite(actor.position) ||
                !assets.actorDefinitions.Any(definition => definition.id == actor.id && definition.sceneName == actor.sceneName)) ||
            save.worldActors.Select(actor => actor.id).Distinct().Count() != save.worldActors.Length)
        {
            return false;
        }
        message = "";
        return true;
    }

    // 진행도 ID 목록은 프로젝트에 등록된 고유 값만 허용한다.
    private static bool ValidIds(string[] ids, string[] allowed) =>
        ids.Length <= allowed.Length && ids.Distinct().Count() == ids.Length &&
        ids.All(id => !string.IsNullOrWhiteSpace(id) && allowed.Contains(id));

    // JSON의 비정상 좌표를 Transform 및 물리 엔진에 전달하지 않는다.
    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    // 읽기/검증 성공 후에만 현재 세션을 교체하고 저장된 맵을 연다.
    public static bool TryLoad(out string message)
    {
        if (!CanChangeSession(out message) || !TryRead(out var save, out message))
        {
            return false;
        }
        if (catalog.playerPrefab == null)
        {
            message = "불러오기에 필요한 플레이어 프리팹이 없습니다.";
            return false;
        }
        // 현재 화면의 진행 상태는 암전이 완료된 뒤 저장 데이터로 교체
        if (!SceneFadeIn.LoadScene(save.sceneName, () =>
        {
            pending = save;
            isBusy = true;
            BattleSession.Clear();
            SceneSpawnManager.nextSpawnID = null;
            NpcBattleProgress.RestoreSave(save.defeatedNpcIds);
            WorldSaveProgress.Restore(save.completedCutsceneIds, save.worldActors);
        }))
        {
            message = "Scene 전환을 시작할 수 없습니다. 씬 등록과 전환 상태를 확인하세요.";
            return false;
        }
        message = "게임을 불러오는 중입니다.";
        return true;
    }

    // Awake 이후, 맵의 Start보다 먼저 플레이어를 준비하고 장비/위치를 복원한다.
    private static void RestoreLoadedScene(Scene scene, LoadSceneMode mode)
    {
        if (pending == null || scene.name != pending.sceneName)
        {
            return;
        }
        var player = PlayerController.Instance;
        if (player == null)
        {
            player = UnityEngine.Object.Instantiate(catalog.playerPrefab);
        }
        player.GetComponent<PlayerBattleData>().RestoreSave(pending, catalog);
        player.RestoreSaveTransform(pending.position, pending.facingRight);
        pending = null;
        isBusy = false;
    }

    // 새 게임에서는 파일은 보존하고 메모리의 진행도와 임시 전환 상태를 초기화한다.
    public static void PrepareNewGame()
    {
        pending = null;
        BattleSession.Clear();
        SceneSpawnManager.nextSpawnID = null;
        NpcBattleProgress.RestoreSave(Array.Empty<string>());
        WorldSaveProgress.Restore(Array.Empty<string>(), Array.Empty<SavedWorldActor>());
    }
}
