#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 모드에 필요한 입력만 표시하며 숨긴 필드의 직렬화 값은 보존
[CustomEditor(typeof(NPCInteraction))]
[CanEditMultipleObjects]
public sealed class NPCInteractionEditor : Editor
{
    // SerializedProperty로 편집하여 Undo, 다중 선택과 프리팹 오버라이드 지원
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        // 선택한 NPC들의 모드와 최상위 직렬화 필드 순회 상태
        var interactionType = serializedObject.FindProperty("interactionType");
        var property = serializedObject.GetIterator();
        var enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!IsVisible(property.name, (NPCInteraction.InteractionType)interactionType.intValue,
                interactionType.hasMultipleDifferentValues))
            {
                continue;
            }

            using (new EditorGUI.DisabledScope(property.name == "m_Script"))
            {
                EditorGUILayout.PropertyField(property, true);
            }

            if ((property.name == "dialogueOnlyId" || property.name == "beforeRewardDialogueId") &&
                !property.hasMultipleDifferentValues && property.intValue == 0)
            {
                // 기존 설정을 참조하는 0의 의미를 알려 숨긴 필드에 대한 의존성 명시
                var legacyId = serializedObject.FindProperty("beforeBattleDialogueId");
                if (legacyId.hasMultipleDifferentValues)
                {
                    EditorGUILayout.HelpBox("0이면 각 NPC의 기존 시작 대화 ID를 사용합니다. 별도 ID를 입력하면 해당 모드에서만 사용합니다.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"0이면 기존 시작 대화 ID {legacyId.intValue}을 사용합니다. 이 모드의 ID를 별도로 지정하려면 양수를 입력하세요.", MessageType.Info);
                }
            }
        }

        if (interactionType.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("선택한 NPC의 Interaction Type이 서로 다릅니다. 같은 모드끼리 선택하면 해당 대화 ID와 보상을 편집할 수 있습니다.", MessageType.Info);
        }
        serializedObject.ApplyModifiedProperties();
    }

    // 혼합 모드의 다중 선택에서는 다른 모드의 값을 실수로 덮어쓰지 않도록 전용 필드 숨김
    private static bool IsVisible(string propertyName, NPCInteraction.InteractionType type, bool hasMixedTypes)
    {
        switch (propertyName)
        {
            case "beforeBattleDialogueId":
            case "afterVictoryDialogueId":
            case "battleData":
            case "battleSceneName":
            case "equipmentUI":
                return !hasMixedTypes && type == NPCInteraction.InteractionType.Battle;
            case "dialogueOnlyId":
                return !hasMixedTypes && type == NPCInteraction.InteractionType.DialogueOnly;
            case "rewardItemId":
            case "additionalRewardItemIds":
            case "beforeRewardDialogueId":
            case "afterRewardDialogueId":
                return !hasMixedTypes && type == NPCInteraction.InteractionType.GiveItem;
            default:
                return true;
        }
    }
}
#endif
