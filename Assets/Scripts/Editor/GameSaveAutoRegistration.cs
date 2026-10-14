using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장 리소스와 콘텐츠 카탈로그를 자동으로 준비하고 기존 저장 ID를 보존한다.</summary>
[InitializeOnLoad]
public static class GameSaveAutoRegistration
{
    // 미리보기 Scene의 Hierarchy 이벤트가 다시 전체 스캔을 유발하지 않도록 구분한다.
    private static bool queued;
    private static bool hierarchyQueued;
    private static bool refreshing;
    private static string hierarchySignature;

    static GameSaveAutoRegistration()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorBuildSettings.sceneListChanged += QueueRefresh;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Detach;
        QueueRefresh();
    }

    // 에디터 도메인 교체 전 등록한 콜백을 해제한다.
    private static void Detach()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorBuildSettings.sceneListChanged -= QueueRefresh;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall -= RefreshQueued;
        EditorApplication.delayCall -= RefreshLoadedMetadata;
        AssemblyReloadEvents.beforeAssemblyReload -= Detach;
    }

    // 변경 이벤트를 한 번의 지연 작업으로 묶는다.
    public static void QueueRefresh()
    {
        if (queued || refreshing || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true;
        EditorApplication.delayCall += RefreshQueued;
    }

    private static void RefreshQueued()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueRefresh();
            return;
        }
        try
        {
            PrepareLoadedScenes();
            RefreshCatalog();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!EditorSceneManager.IsPreviewScene(scene)) QueueRefresh();
    }

    private static void OnSceneSaved(Scene scene) => QueueRefresh();

    private static void OnHierarchyChanged()
    {
        if (hierarchyQueued || refreshing || EditorApplication.isPlayingOrWillChangePlaymode) return;
        hierarchyQueued = true;
        EditorApplication.delayCall += RefreshLoadedMetadata;
    }

    // 로드된 실제 Scene의 저장 대상이 달라졌을 때만 전체 카탈로그를 갱신한다.
    private static void RefreshLoadedMetadata()
    {
        hierarchyQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || refreshing) return;
        PrepareLoadedScenes();
        var signature = string.Join("|", LoadedScenes().SelectMany(scene => Components<MonoBehaviour>(scene))
            .Where(item => item is NPCInteraction || item is TriggerCutscene || item is PersistentWorldActor)
            .Select(item => item.GetInstanceID() + ":" + EditorJsonUtility.ToJson(item)).OrderBy(value => value));
        if (signature == hierarchySignature) return;
        hierarchySignature = signature;
        QueueRefresh();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            try
            {
                PrepareLoadedScenes();
                RefreshCatalog();
            }
            catch (Exception exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogException(exception);
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode) QueueRefresh();
    }

    // 저장 대상의 메타데이터만 변경하고 사용자 Scene을 임의로 저장하지 않는다.
    public static void PrepareLoadedScenes()
    {
        foreach (var scene in LoadedScenes()) PrepareScene(scene, true);
    }

    private static IEnumerable<Scene> LoadedScenes()
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene)) yield return scene;
        }
    }

    private static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    // 신규 NPC/컷신과 컷신 대상은 저장 ID를 얻고, 복제된 대상은 별도의 ID를 얻는다.
    public static void PrepareScene(Scene scene, bool undo)
    {
        var seenNpc = new HashSet<string>();
        foreach (var npc in Components<NPCInteraction>(scene)) EnsureIdentity(npc, "npcId", "npc", seenNpc, undo);
        var seenCutscene = new HashSet<string>();
        foreach (var cutscene in Components<TriggerCutscene>(scene))
        {
            EnsureIdentity(cutscene, "persistentId", "cutscene", seenCutscene, undo);
            var steps = new SerializedObject(cutscene).FindProperty("steps");
            for (var index = 0; index < steps.arraySize; index++)
            {
                var target = steps.GetArrayElementAtIndex(index).FindPropertyRelative("target").objectReferenceValue as GameObject;
                if (target == null || target.scene != scene || target.GetComponent<PlayerController>() != null ||
                    target.GetComponent<PersistentWorldActor>() != null) continue;
                if (undo)
                {
                    Undo.AddComponent<PersistentWorldActor>(target);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                else target.AddComponent<PersistentWorldActor>();
            }
        }
        var seenActor = new HashSet<string>();
        foreach (var actor in Components<PersistentWorldActor>(scene)) EnsureIdentity(actor, "persistentId", "actor", seenActor, undo);
    }

    // 기존 ID는 유지하고 오브젝트 소유 정보가 복제된 경우에만 새 ID를 만든다.
    private static void EnsureIdentity(Component item, string field, string category, HashSet<string> seen, bool undo)
    {
        var state = new SerializedObject(item);
        var id = state.FindProperty(field);
        var owner = state.FindProperty("saveIdentityOwner");
        var globalId = GlobalObjectId.GetGlobalObjectIdSlow(item);
        var key = globalId.targetObjectId != 0 && globalId.assetGUID.ToString() != "00000000000000000000000000000000"
            ? globalId.ToString() : "";
        var value = id.stringValue;
        if (string.IsNullOrWhiteSpace(value) || seen.Contains(value) ||
            (!string.IsNullOrEmpty(owner.stringValue) && !string.IsNullOrEmpty(key) && owner.stringValue != key))
        {
            value = string.IsNullOrEmpty(key) ? Guid.NewGuid().ToString("N") : category + ":" + key;
        }
        if (!seen.Add(value)) throw new InvalidOperationException("중복 저장 ID: " + value);
        var ownerValue = string.IsNullOrEmpty(key) ? owner.stringValue : key;
        if (value == id.stringValue && ownerValue == owner.stringValue) return;
        if (undo) Undo.RecordObject(item, "Register save identity");
        id.stringValue = value;
        owner.stringValue = ownerValue;
        state.ApplyModifiedPropertiesWithoutUndo();
        if (undo)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
        }
    }

    // 현재 저장 에셋을 찾아 Resources 하위에 배치하며 GUID와 프리팹 참조를 유지한다.
    private static GameSaveCatalog PrepareResources()
    {
        var paths = AssetDatabase.FindAssets("t:GameSaveCatalog", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        if (paths.Length != 1) throw new InvalidOperationException("GameSaveCatalog 에셋이 하나 필요합니다.");
        var catalogPath = EnsureResource(paths[0], "GameSaveCatalog.asset");
        if (Resources.Load<GameObject>("GameMenu") == null)
        {
            var menus = AssetDatabase.FindAssets("GameMenu t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => Path.GetFileNameWithoutExtension(path) == "GameMenu").ToArray();
            if (menus.Length != 1) throw new InvalidOperationException("GameMenu 프리팹이 하나 필요합니다.");
            EnsureResource(menus[0], "GameMenu.prefab");
        }
        return AssetDatabase.LoadAssetAtPath<GameSaveCatalog>(catalogPath);
    }

    private static string EnsureResource(string path, string fileName)
    {
        if (path.Contains("/Resources/") && Path.GetFileName(path) == fileName) return path;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var folder = parent + "/Resources";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, "Resources");
        var destination = folder + "/" + fileName;
        var error = AssetDatabase.MoveAsset(path, destination);
        if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        return destination;
    }

    // 유효한 Build Scene과 모든 원본 데이터 에셋을 수집해 변경된 경우에만 카탈로그에 기록한다.
    public static void RefreshCatalog()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            var catalog = PrepareResources();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled &&
                Path.GetFileNameWithoutExtension(scene.path) != "StartMenu" && Path.GetFileNameWithoutExtension(scene.path) != "Battle").ToArray();
            var maps = scenes.Select(scene => Path.GetFileNameWithoutExtension(scene.path)).ToArray();
            if (maps.Distinct().Count() != maps.Length) throw new InvalidOperationException("Build Scene 이름이 중복됩니다.");
            var npcs = new List<string>();
            var cutscenes = new List<string>();
            var actors = new List<SavedWorldActor>();
            foreach (var source in scenes)
            {
                var scene = SceneManager.GetSceneByPath(source.path);
                var preview = !scene.IsValid() || !scene.isLoaded;
                if (preview) scene = EditorSceneManager.OpenPreviewScene(source.path);
                try
                {
                    PrepareScene(scene, !preview);
                    npcs.AddRange(Components<NPCInteraction>(scene).Select(item => new SerializedObject(item).FindProperty("npcId").stringValue));
                    cutscenes.AddRange(Components<TriggerCutscene>(scene).Select(item => new SerializedObject(item).FindProperty("persistentId").stringValue));
                    actors.AddRange(Components<PersistentWorldActor>(scene).Select(item => new SavedWorldActor
                    {
                        id = item.PersistentId, sceneName = scene.name, position = item.transform.position
                    }));
                }
                finally
                {
                    if (preview) EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            if (npcs.Distinct().Count() != npcs.Count || cutscenes.Distinct().Count() != cutscenes.Count ||
                actors.Select(actor => actor.id).Distinct().Count() != actors.Count)
                throw new InvalidOperationException("Scene 사이에 저장 ID가 중복됩니다. 복제한 Scene의 대상을 확인하세요.");
            var before = EditorJsonUtility.ToJson(catalog);
            catalog.characters = Collect(catalog.characters, (CharacterData item) => item.CharacterId);
            catalog.aethers = Collect(catalog.aethers, (AetherData item) => item.itemId);
            catalog.weapons = Collect(catalog.weapons, (WeaponData item) => item.ItemId);
            catalog.mapScenes = maps;
            catalog.npcIds = npcs.OrderBy(id => id).ToArray();
            catalog.cutsceneIds = cutscenes.OrderBy(id => id).ToArray();
            catalog.actorDefinitions = actors.OrderBy(actor => actor.id).ToArray();
            if (before != EditorJsonUtility.ToJson(catalog))
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
        }
        finally
        {
            refreshing = false;
        }
    }

    // 같은 논리 ID의 전투용 복사본보다 이미 등록된 원본을 우선한다.
    private static T[] Collect<T>(T[] previous, Func<T, int> id) where T : ScriptableObject =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path.Contains("Battle") ? 1 : 0).ThenBy(path => path)
            .Select(AssetDatabase.LoadAssetAtPath<T>).Where(item => item != null).GroupBy(id).OrderBy(group => group.Key)
            .Select(group => SelectOriginal(group, previous)).ToArray();

    // Unity 오브젝트의 생존 여부를 확인한 뒤 원본을 선택한다.
    private static T SelectOriginal<T>(IGrouping<int, T> group, T[] previous) where T : ScriptableObject
    {
        var original = group.FirstOrDefault(item => previous.Contains(item));
        return original != null ? original : group.First();
    }
}

// 에셋 추가/삭제/이동과 일반 Scene 저장도 자동 등록 경로에 연결한다.
public sealed class GameSaveAssetWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path =>
            (path.EndsWith(".asset") || path.EndsWith(".unity") || path.EndsWith(".prefab")) &&
            !path.EndsWith("GameSaveCatalog.asset"))) GameSaveAutoRegistration.QueueRefresh();
    }
}

public sealed class GameSaveBeforeSceneSave : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (paths.Any(path => path.EndsWith(".unity")) && !EditorApplication.isPlaying)
        {
            GameSaveAutoRegistration.PrepareLoadedScenes();
            GameSaveAutoRegistration.QueueRefresh();
        }
        return paths;
    }
}

// 플레이 및 빌드에 전달하는 Scene에도 카탈로그와 동일한 저장 ID를 준비한다.
public sealed class GameSaveSceneProcessor : IProcessSceneWithReport, IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        GameSaveAutoRegistration.PrepareLoadedScenes();
        GameSaveAutoRegistration.RefreshCatalog();
    }

    public void OnProcessScene(Scene scene, BuildReport report) => GameSaveAutoRegistration.PrepareScene(scene, false);
}
