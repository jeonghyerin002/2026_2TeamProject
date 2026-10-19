#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 이번 작업의 지정 씬과 대화 에셋만 편집하는 임시 적용 도구
public static class GuardSceneSetup
{
    // 다른 씬과 기존 대사를 변경하지 않도록 고정한 대상 경로
    private const string ScenePath = "Assets/Scenes/PalletTown.unity";
    private const string DialogueFolder = "Assets/Data/Dialogs/DialogSO/";
    private const string ToolPath = "Assets/Scripts/Editor/GuardSceneSetup.cs";

    // 열린 원본 씬을 보존한 뒤 경비·출구·공용 DB만 연결하고 저장
    [MenuItem("Tools/Guard/Configure PalletTown")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlaying && scene.path == ScenePath, "PalletTown 편집 모드에서만 적용할 수 있습니다.");
        var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var guard = components.Single(item => item.name.Trim() == "Guard").gameObject;
        var exit = components.Select(item => item.GetComponent<Door>()).Single(item => item != null && item.sceneName == "Raute_1");
        var manager = components.Select(item => item.GetComponent<DialogManager>()).Single(item => item != null);
        var database = (DialogDatabaseSO)new SerializedObject(manager).FindProperty("dialogDatabase").objectReferenceValue;
        var sensor = guard.GetComponent<BoxCollider2D>();
        var exitCollider = exit.GetComponent<BoxCollider2D>();
        Require(guard.GetComponent<WeaponGate>() == null && sensor != null && sensor.isTrigger && exitCollider != null,
            "기존 Guard Trigger 또는 출구 구성을 확인하세요. 중복 적용은 허용하지 않습니다.");
        Require(database != null, "공용 대화 DB가 필요합니다.");
        var names = new[] { "DialogSO_Guard_First_1", "DialogSO_Guard_First_2", "DialogSO_Guard_First_3", "DialogSO_Guard_Repeat" };
        foreach (var name in names)
        {
            Require(!File.Exists(DialogueFolder + name + ".asset"), "같은 이름의 대화 에셋이 이미 있습니다: " + name);
        }
        // 기존 디스크 변경과 미저장 씬 상태를 모두 보존하는 독립 백업
        var backup = Path.Combine(Path.GetTempPath(), "PalletTown-before-guard-" + Guid.NewGuid().ToString("N") + ".unity");
        Require(EditorSceneManager.SaveScene(scene, backup, true), "적용 전 씬 복사본 저장 실패");

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("경비 무기 장착 통행 조건 설정");
        // 사용 중인 모든 대사 중 가장 큰 ID 다음부터 새 ID 할당
        var nextId = AssetDatabase.FindAssets("t:DialogSO", new[] { "Assets/Data/Dialogs" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<DialogSO>)
            .Where(item => item != null).Select(item => item.id).DefaultIfEmpty(0).Max() + 1;
        var lines = new[]
        {
            "잠깐! 1번도로로 가려는 거야?",
            "이 앞에서는 지나가는 사람끼리 승부를 겨루는 일이 흔해. 무기도 없이 나갔다가 누군가 싸움을 걸면 곤란하잖아?",
            "아직 무기가 없다면 먼저 마을 신사에 들러 봐. 무녀님께서 네게 맞는 무기를 하나 주실 거야",
            "1번도로에서는 언제 배틀을 신청받을지 몰라. 먼저 신사에서 무기를 받아 와. 그전까지는 지나가게 할 수 없어"
        };
        var dialogues = new DialogSO[lines.Length];
        for (var i = 0; i < dialogues.Length; i++)
        {
            var dialog = ScriptableObject.CreateInstance<DialogSO>();
            dialog.id = nextId + i;
            dialog.characterName = "경비";
            dialog.text = lines[i];
            dialog.nextId = i < 2 ? nextId + i + 1 : 0;
            AssetDatabase.CreateAsset(dialog, DialogueFolder + names[i] + ".asset");
            dialogues[i] = dialog;
        }
        Undo.RecordObject(database, "경비 대사 등록");
        database.dialogs.AddRange(dialogues);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssetIfDirty(database);

        // 출구 폭 전체를 덮는 별도 물리 장벽, 기존 경비 외형과 위치는 유지
        var blockerObject = new GameObject("WeaponPassageBlocker");
        Undo.RegisterCreatedObjectUndo(blockerObject, "경비 길막 장벽 생성");
        Undo.SetTransformParent(blockerObject.transform, guard.transform, "경비 장벽 연결");
        blockerObject.layer = guard.layer;
        blockerObject.transform.localRotation = Quaternion.identity;
        blockerObject.transform.localScale = Vector3.one;
        blockerObject.transform.position = new Vector3(exitCollider.bounds.center.x, guard.transform.position.y, guard.transform.position.z);
        var blocker = Undo.AddComponent<BoxCollider2D>(blockerObject);
        var width = exitCollider.bounds.size.x + 0.4f;
        blocker.size = new Vector2(width / Mathf.Abs(blocker.transform.lossyScale.x), 0.25f / Mathf.Abs(blocker.transform.lossyScale.y));
        blocker.isTrigger = false;
        Undo.RecordObject(sensor, "경비 접근 범위 설정");
        sensor.size = new Vector2(width / Mathf.Abs(guard.transform.lossyScale.x), 1.8f / Mathf.Abs(guard.transform.lossyScale.y));
        sensor.offset = guard.transform.InverseTransformPoint(new Vector3(exitCollider.bounds.center.x, guard.transform.position.y - 0.6f, guard.transform.position.z));
        var gate = Undo.AddComponent<WeaponGate>(guard);
        var fields = new SerializedObject(gate);
        fields.FindProperty("passageBlocker").objectReferenceValue = blocker;
        fields.FindProperty("dialogManager").objectReferenceValue = manager;
        fields.FindProperty("firstDialogueId").intValue = dialogues[0].id;
        fields.FindProperty("repeatDialogueId").intValue = dialogues[3].id;
        // 기존 NPC의 데이터와 저장 ID를 유지하고 중복 E 입력 처리만 비활성화
        var oldInteraction = guard.GetComponent<NPCInteraction>();
        if (oldInteraction != null)
        {
            fields.FindProperty("equipmentUI").objectReferenceValue = new SerializedObject(oldInteraction).FindProperty("equipmentUI").objectReferenceValue;
            Undo.RecordObject(oldInteraction, "일반 NPC 대화 중복 실행 방지");
            oldInteraction.enabled = false;
        }
        fields.ApplyModifiedProperties();
        var doorFields = new SerializedObject(exit);
        doorFields.FindProperty("requiresEquippedWeapon").boolValue = true;
        doorFields.FindProperty("weaponGate").objectReferenceValue = gate;
        doorFields.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "PalletTown 저장 실패");
        Selection.activeGameObject = guard;
        Debug.Log($"GUARD_SETUP_APPLIED: first={dialogues[0].id}, repeat={dialogues[3].id}, blockerWidth={width}, backup={backup}", gate);
    }

    // 전제조건이 충족되지 않으면 범위를 넓히지 않고 중단
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // 지정 씬의 연결 완료 후 이번 작업이 만든 임시 도구만 Unity API로 정리
    [MenuItem("Tools/Guard/Remove Setup Tool")]
    public static void RemoveTool()
    {
        Require(!EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "PalletTown 편집 모드가 필요합니다.");
        Require(UnityEngine.Object.FindFirstObjectByType<WeaponGate>() != null, "경비 설정을 먼저 적용하세요.");
        AssetDatabase.DeleteAsset(ToolPath);
    }
}
#endif
