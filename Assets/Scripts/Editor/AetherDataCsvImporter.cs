using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>선택한 CSV를 읽어 AetherData SO를 생성하거나 갱신한다.</summary>
public static class AetherDataCsvImporter
{
    private const string OutputFolder = "Assets/Data/Aethers";

    // 선택한 CSV를 AetherData SO로 변환
    [MenuItem("Tools/Data/Import AetherData")]
    private static void Import()
    {
        if (Selection.activeObject is not TextAsset csv)
        {
            Debug.LogError("Project 창에서 AetherData.csv를 선택하세요.");
            return;
        }

        string[] lines = csv.text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
        {
            Debug.LogError("CSV에 Aether 데이터가 없습니다.");
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
            "type",
            "skillId1",
            "skillId2",
            "skillId3"
        };

        foreach (string header in required)
        {
            if (columns.ContainsKey(header))
                continue;

            Debug.LogError($"CSV에 필요한 열이 없습니다: {header}");
            return;
        }

        Dictionary<int, SkillData> skillMap = BuildSkillMap();

        if (skillMap == null)
            return;

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        HashSet<int> importedIds = new();
        int count = 0;

        // CSV를 한 줄씩 변환
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');

            if (cells.Length < headers.Length)
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄의 데이터가 부족합니다.");
                continue;
            }

            if (!int.TryParse(cells[columns["id"]].Trim(), out int id))
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄의 ID가 잘못되었습니다.");
                continue;
            }

            if (!importedIds.Add(id))
            {
                Debug.LogWarning($"CSV에 중복된 Aether ID가 있습니다: {id}");
                continue;
            }

            if (!Enum.TryParse(
                    cells[columns["type"]].Trim(),
                    true,
                    out ElementType type))
            {
                Debug.LogWarning($"Aether {id}의 ElementType이 잘못되었습니다.");
                continue;
            }

            List<SkillData> skills = new(3);

            if (!TryAddSkill(cells[columns["skillId1"]], skillMap, skills, id) ||
                !TryAddSkill(cells[columns["skillId2"]], skillMap, skills, id) ||
                !TryAddSkill(cells[columns["skillId3"]], skillMap, skills, id))
                continue;

            if (skills.Count == 0)
            {
                Debug.LogWarning($"Aether {id}에 스킬이 없습니다.");
                continue;
            }

            string path = $"{OutputFolder}/Aether_{id}.asset";

            AetherData data =
                AssetDatabase.LoadAssetAtPath<AetherData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AetherData>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject so = new(data);

            SerializedProperty idProperty = so.FindProperty("id");
            SerializedProperty nameProperty = so.FindProperty("name");
            SerializedProperty typeProperty = so.FindProperty("type");
            SerializedProperty skillsProperty = so.FindProperty("skills");

            if (idProperty == null ||
                nameProperty == null ||
                typeProperty == null ||
                skillsProperty == null)
            {
                Debug.LogError(
                    $"Aether_{id}.asset 변환 실패: AetherData 필드 이름을 확인하세요.");
                continue;
            }

            idProperty.intValue = id;
            nameProperty.stringValue =
                cells[columns["name"]].Trim();
            typeProperty.enumValueIndex = (int)type;

            skillsProperty.arraySize = skills.Count;

            for (int j = 0; j < skills.Count; j++)
                skillsProperty.GetArrayElementAtIndex(j).objectReferenceValue = skills[j];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"AetherData Import 완료: {count}개");
    }

    // 프로젝트의 모든 SkillData를 ID 기준으로 저장
    private static Dictionary<int, SkillData> BuildSkillMap()
    {
        Dictionary<int, SkillData> map = new();

        foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);

            if (skill == null)
                continue;

            if (!map.TryAdd(skill.Id, skill))
            {
                Debug.LogError($"중복된 Skill ID가 있습니다: {skill.Id}");
                return null;
            }
        }

        return map;
    }

    // CSV Skill ID를 SkillData로 찾아 목록에 추가
    private static bool TryAddSkill(
        string value,
        Dictionary<int, SkillData> skillMap,
        List<SkillData> skills,
        int aetherId)
    {
        value = value.Trim();

        if (string.IsNullOrEmpty(value) || value == "0")
            return true;

        if (!int.TryParse(value, out int skillId))
        {
            Debug.LogWarning(
                $"Aether {aetherId}의 Skill ID가 잘못되었습니다: {value}");
            return false;
        }

        if (!skillMap.TryGetValue(skillId, out SkillData skill))
        {
            Debug.LogWarning(
                $"Aether {aetherId}에서 SkillData를 찾지 못했습니다: {skillId}");
            return false;
        }

        if (!skills.Contains(skill))
            skills.Add(skill);

        return true;
    }
}