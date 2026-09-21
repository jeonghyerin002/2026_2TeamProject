using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>선택한 CSV를 읽어 SkillData SO를 생성하거나 갱신한다.</summary>
public static class SkillDataCsvImporter
{
    private const string OutputFolder = "Assets/Data/Skills";

    // 선택한 CSV를 SkillData SO로 변환
    [MenuItem("Tools/Data/Import SkillData")]
    private static void Import()
    {
        if (Selection.activeObject is not TextAsset csv)
        {
            Debug.LogError("Project 창에서 SkillData.csv를 선택하세요.");
            return;
        }

        string[] lines = csv.text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
        {
            Debug.LogError("CSV에 Skill 데이터가 없습니다.");
            return;
        }

        string[] headers = lines[0].Split(',');
        Dictionary<string, int> columns = new();

        for (int i = 0; i < headers.Length; i++)
            columns[headers[i].Trim()] = i;

        string[] required =
        {
            "id",
            "name",
            "power",
            "accuracy",
            "maxPP",
            "priority",
            "type",
            "category",
            "effectType"
        };

        foreach (string header in required)
        {
            if (columns.ContainsKey(header))
                continue;

            Debug.LogError($"CSV에 필요한 열이 없습니다: {header}");
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        HashSet<int> importedIds = new();
        int count = 0;

        // CSV를 한 줄씩 SkillData로 변환
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');

            if (cells.Length < headers.Length)
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄의 데이터가 부족합니다.");
                continue;
            }

            if (!int.TryParse(cells[columns["id"]].Trim(), out int id) ||
                !int.TryParse(cells[columns["power"]].Trim(), out int power) ||
                !int.TryParse(cells[columns["accuracy"]].Trim(), out int accuracy) ||
                !int.TryParse(cells[columns["maxPP"]].Trim(), out int maxPP) ||
                !int.TryParse(cells[columns["priority"]].Trim(), out int priority))
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄의 숫자 데이터가 잘못되었습니다.");
                continue;
            }

            if (!importedIds.Add(id))
            {
                Debug.LogWarning($"CSV에 중복된 Skill ID가 있습니다: {id}");
                continue;
            }

            if (!Enum.TryParse(cells[columns["type"]].Trim(), true, out ElementType type))
            {
                Debug.LogWarning($"Skill {id}의 ElementType이 잘못되었습니다.");
                continue;
            }

            if (!Enum.TryParse(cells[columns["category"]].Trim(), true, out SkillCategory category))
            {
                Debug.LogWarning($"Skill {id}의 SkillCategory가 잘못되었습니다.");
                continue;
            }

            if (!Enum.TryParse(cells[columns["effectType"]].Trim(), true, out SkillEffectType effectType))
            {
                Debug.LogWarning($"Skill {id}의 SkillEffectType이 잘못되었습니다.");
                continue;
            }

            string path = $"{OutputFolder}/Skill_{id}.asset";

            SkillData data = AssetDatabase.LoadAssetAtPath<SkillData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<SkillData>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject so = new(data);

            SerializedProperty idProperty = so.FindProperty("id");
            SerializedProperty nameProperty = so.FindProperty("name");
            SerializedProperty powerProperty = so.FindProperty("power");
            SerializedProperty accuracyProperty = so.FindProperty("accuracy");
            SerializedProperty maxPpProperty = so.FindProperty("maxPP");
            SerializedProperty priorityProperty = so.FindProperty("priority");
            SerializedProperty typeProperty = so.FindProperty("type");
            SerializedProperty categoryProperty = so.FindProperty("category");
            SerializedProperty effectTypeProperty = so.FindProperty("effectType");

            if (idProperty == null ||
                nameProperty == null ||
                powerProperty == null ||
                accuracyProperty == null ||
                maxPpProperty == null ||
                priorityProperty == null ||
                typeProperty == null ||
                categoryProperty == null ||
                effectTypeProperty == null)
            {
                Debug.LogError(
                    $"Skill_{id}.asset 변환 실패: SkillData의 필드 이름을 확인하세요.");
                continue;
            }

            idProperty.intValue = id;
            nameProperty.stringValue = cells[columns["name"]].Trim();
            powerProperty.intValue = power;
            accuracyProperty.intValue = accuracy;
            maxPpProperty.intValue = maxPP;
            priorityProperty.intValue = priority;
            typeProperty.enumValueIndex = (int)type;
            categoryProperty.enumValueIndex = (int)category;
            effectTypeProperty.enumValueIndex = (int)effectType;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"SkillData Import 완료: {count}개");
    }
}
