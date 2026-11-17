#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TriggerCutscene.Step))]
public sealed class TriggerCutsceneStepDrawer : PropertyDrawer
{
    private const int TextAreaLineCount = 4;
    private static readonly string[] TargetFields = { "target" };
    private static readonly string[] DialogueFields = { "text" };
    private static readonly string[] MoveFields = { "useScenePath", "npcId", "axis", "coordinate", "speed" };
    private static readonly string[] NpcMoveFields = { "useScenePath", "npcId", "npcFrontOffset", "speed" };
    private static readonly string[] PathFields = { "useScenePath", "npcId", "pathPoints", "speed" };
    private static readonly string[] NpcPathFields = { "useScenePath", "npcId", "npcFrontOffset", "pathPoints", "speed" };
    // 강제 전투 단계의 NPC 식별 설정
    private static readonly string[] BattleFields = { "npcId" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing +
            EditorGUI.GetPropertyHeight(property.FindPropertyRelative("type"), true);
        foreach (string fieldName in GetVisibleFields(property))
        {
            height += EditorGUIUtility.standardVerticalSpacing +
                GetFieldHeight(property.FindPropertyRelative(fieldName));
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int originalIndent = EditorGUI.indentLevel;
        try
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawField(ref row, property.FindPropertyRelative("type"));
            foreach (string fieldName in GetVisibleFields(property))
            {
                DrawField(ref row, property.FindPropertyRelative(fieldName));
            }
        }
        finally
        {
            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }
    }

    private static string[] GetVisibleFields(SerializedProperty property)
    {
        var type = (TriggerCutscene.StepType)property.FindPropertyRelative("type").intValue;
        switch (type)
        {
            case TriggerCutscene.StepType.Dialogue:
                return DialogueFields;
            case TriggerCutscene.StepType.Move:
                if (property.FindPropertyRelative("useScenePath").boolValue)
                {
                    return string.IsNullOrWhiteSpace(property.FindPropertyRelative("npcId").stringValue)
                        ? PathFields : NpcPathFields;
                }

                return string.IsNullOrWhiteSpace(property.FindPropertyRelative("npcId").stringValue)
                    ? MoveFields : NpcMoveFields;
            case TriggerCutscene.StepType.Battle:
                return BattleFields;
            default:
                return TargetFields;
        }
    }

    private static void DrawField(ref Rect row, SerializedProperty property)
    {
        row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
        row.height = GetFieldHeight(property);
        if (property.name == "text")
        {
            DrawTextField(row, property);
            return;
        }
        EditorGUI.PropertyField(row, property, true);
    }

    private static float GetFieldHeight(SerializedProperty property)
    {
        if (property.name == "text")
        {
            return EditorGUIUtility.singleLineHeight * (TextAreaLineCount + 1) +
                EditorGUIUtility.standardVerticalSpacing;
        }
        return EditorGUI.GetPropertyHeight(property, true);
    }

    private static void DrawTextField(Rect position, SerializedProperty property)
    {
        GUIContent label = EditorGUI.BeginProperty(position, new GUIContent(property.displayName, property.tooltip), property);
        try
        {
            var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);
            var textRect = new Rect(position.x,
                labelRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width, EditorGUIUtility.singleLineHeight * TextAreaLineCount);
            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.TextArea(EditorGUI.IndentedRect(textRect), property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = text;
            }
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }
}

/// <summary>컷신의 이동 경로를 Scene 뷰에서 그리기 및 경유점 편집</summary>
[CustomEditor(typeof(TriggerCutscene))]
public sealed class TriggerCutsceneEditor : Editor
{
    // 현재 그리는 이동 단계와 Scene 뷰 표시 크기
    private int drawingStep = -1;
    private const float MarkerSize = 0.15f;


    // 기존 단계 설정과 경로 그리기 도구 표시
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("경로는 Play 종료 후 편집하세요.", MessageType.Info);
            return;
        }

        serializedObject.Update();
        var steps = serializedObject.FindProperty("steps");
        EditorGUILayout.HelpBox("Move 단계의 경로 그리기를 누르고 Scene 뷰를 왼쪽 클릭하세요. Enter/Esc로 종료하며 이동점은 드래그로 수정할 수 있습니다. 각 선은 X축 후 Y축으로 연결됩니다. 실제 캐릭터는 현재 위치에서 시작점까지 먼저 이동합니다.", MessageType.Info);
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i);
            if (step.FindPropertyRelative("type").intValue != (int)TriggerCutscene.StepType.Move)
            {
                continue;
            }

            EditorGUILayout.LabelField($"단계 {i + 1} 이동 경로", EditorStyles.boldLabel);
            if (GUILayout.Button(drawingStep == i ? "경로 그리기 종료" : "경로 그리기 / 이어 그리기"))
            {
                drawingStep = drawingStep == i ? -1 : i;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("경로 비우기"))
            {
                step.FindPropertyRelative("pathPoints").ClearArray();
                step.FindPropertyRelative("useScenePath").boolValue = false;
                drawingStep = -1;
                SceneView.RepaintAll();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }


    // 선택한 컷신의 경로 표시와 마우스 경유점 입력
    private void OnSceneGUI()
    {
        if (Application.isPlaying)
        {
            return;
        }

        serializedObject.Update();
        var steps = serializedObject.FindProperty("steps");
        float z = ((TriggerCutscene)target).transform.position.z;
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i);
            if (step.FindPropertyRelative("type").intValue != (int)TriggerCutscene.StepType.Move)
            {
                continue;
            }

            DrawPath(step, z, i);
        }

        // 그리기 모드에서만 선택 입력을 예약하여 Scene 조작 보존
        if (drawingStep >= 0 && drawingStep < steps.arraySize &&
            steps.GetArrayElementAtIndex(drawingStep).FindPropertyRelative("type").intValue == (int)TriggerCutscene.StepType.Move)
        {
            HandleDrawing(steps.GetArrayElementAtIndex(drawingStep), z);
        }
        else
        {
            drawingStep = -1;
        }

        serializedObject.ApplyModifiedProperties();
    }


    // 저장된 경유점의 핸들과 가로 세로 연결선 표시
    private void DrawPath(SerializedProperty step, float z, int index)
    {
        if (!step.FindPropertyRelative("useScenePath").boolValue)
        {
            return;
        }

        var points = step.FindPropertyRelative("pathPoints");
        Vector3 previous = Vector3.zero;
        Handles.color = Color.cyan;
        for (int i = 0; i < points.arraySize; i++)
        {
            var pointProperty = points.GetArrayElementAtIndex(i);
            Vector2 point = pointProperty.vector2Value;
            if (i > 0)
            {
                point = TriggerCutscene.AlignPathPoint(previous, point);
            }

            Vector3 position = new Vector3(point.x, point.y, z);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                // 직선으로 배치한 이동점의 작은 드래그 오차 정렬
                Vector2 aligned = new Vector2(moved.x, moved.y);
                if (i > 0)
                {
                    aligned = TriggerCutscene.AlignPathPoint(previous, aligned);
                }

                position = new Vector3(aligned.x, aligned.y, z);
                pointProperty.vector2Value = aligned;
            }

            if (i > 0)
            {
                DrawOrthogonalLine(previous, position);
            }

            Handles.Label(position + Vector3.up * MarkerSize, $"{index + 1}-{i + 1}");
            if (i == 0)
            {
                DrawMarker(position, "시작", Color.green);
            }

            previous = position;
        }

        if (points.arraySize == 0)
        {
            return;
        }

        // NPC가 있으면 실제 최종 도착 위치까지 경로 표시
        if (TryGetNpcEnd(step, z, out var end))
        {
            // NPC 앞의 끝점을 드래그하면 원본 NPC 위치 대신 도착 간격 수정
            EditorGUI.BeginChangeCheck();
            Vector3 movedEnd = Handles.PositionHandle(end, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                var offsetProperty = step.FindPropertyRelative("npcFrontOffset");
                offsetProperty.vector2Value += new Vector2(movedEnd.x - end.x, movedEnd.y - end.y);
                end = new Vector3(movedEnd.x, movedEnd.y, z);
            }

            Handles.color = Color.cyan;
            DrawOrthogonalLine(previous, end);
            DrawMarker(end, "끝 (NPC 앞)", Color.red);
        }
        else
        {
            DrawMarker(previous, "끝", Color.red);
        }
    }


    // Scene 평면을 클릭한 월드 좌표를 Undo 가능한 경유점으로 추가
    private void HandleDrawing(SerializedProperty step, float z)
    {
        var sceneEvent = Event.current;
        if (sceneEvent.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (sceneEvent.type == EventType.KeyDown &&
            (sceneEvent.keyCode == KeyCode.Escape || sceneEvent.keyCode == KeyCode.Return))
        {
            drawingStep = -1;
            sceneEvent.Use();
            Repaint();
            return;
        }

        if (sceneEvent.type != EventType.MouseDown || sceneEvent.button != 0 || sceneEvent.alt)
        {
            return;
        }

        var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
        var ray = HandleUtility.GUIPointToWorldRay(sceneEvent.mousePosition);
        if (!plane.Raycast(ray, out float distance))
        {
            return;
        }

        Vector3 point = ray.GetPoint(distance);
        var points = step.FindPropertyRelative("pathPoints");
        int pointIndex = points.arraySize;
        // 직선으로 그린 선에서 미세한 마우스 좌표 차이 제거
        Vector2 alignedPoint = new Vector2(point.x, point.y);
        if (pointIndex > 0)
        {
            alignedPoint = TriggerCutscene.AlignPathPoint(points.GetArrayElementAtIndex(pointIndex - 1).vector2Value, alignedPoint);
        }

        points.arraySize++;
        points.GetArrayElementAtIndex(pointIndex).vector2Value = alignedPoint;
        step.FindPropertyRelative("useScenePath").boolValue = true;
        sceneEvent.Use();
        Repaint();
        SceneView.RepaintAll();
    }


    // 런타임과 같은 X축 우선 연결선 표시
    private static void DrawOrthogonalLine(Vector3 start, Vector3 end)
    {
        var corner = new Vector3(end.x, start.y, start.z);
        Handles.DrawLine(start, corner);
        Handles.DrawLine(corner, end);
    }


    // 시작선과 끝선 및 라벨 표시
    private static void DrawMarker(Vector3 position, string label, Color color)
    {
        Handles.color = color;
        Handles.DrawLine(position - Vector3.right * MarkerSize, position + Vector3.right * MarkerSize);
        Handles.DrawLine(position - Vector3.up * MarkerSize, position + Vector3.up * MarkerSize);
        Handles.Label(position + Vector3.left * MarkerSize, label);
        Handles.color = Color.cyan;
    }


    // 에디터에서 NPC ID에 대응하는 최종 목적지 조회
    private static bool TryGetNpcEnd(SerializedProperty step, float z, out Vector3 end)
    {
        end = Vector3.zero;
        string npcId = step.FindPropertyRelative("npcId").stringValue;
        if (string.IsNullOrWhiteSpace(npcId))
        {
            return false;
        }

        // 미설정 NPC에 대한 반복 Console 로그 없이 현재 씬의 참조 조회
        foreach (var npc in Object.FindObjectsByType<NPCInteraction>(FindObjectsSortMode.None))
        {
            var npcObject = new SerializedObject(npc);
            if (npcObject.FindProperty("npcId").stringValue != npcId)
            {
                continue;
            }

            Vector2 offset = step.FindPropertyRelative("npcFrontOffset").vector2Value;
            end = npc.transform.position + new Vector3(offset.x, offset.y, 0f);
            end.z = z;
            return true;
        }

        return false;
    }
}
#endif
