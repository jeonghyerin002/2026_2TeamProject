using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>선택한 CSV를 읽어 Game.Data.CharacterData SO를 생성하거나 갱신한다.</summary>
public static class CharacterDataCsvImporter
{
    // SO 저장 경로
    private const string OutputFolder = "Assets/Data/Characters";


    // 선택한 CSV를 CharacterData SO로 변환
    // 유니티 상단에서 표시
    [MenuItem("Tools/Data/Import CharacterData")]
    private static void Import()
    {
        // 선택한 CSV 확인
        if (Selection.activeObject is not TextAsset csv)
        {
            Debug.LogError("Project 창에서 CharacterData.csv를 선택하세요.");
            return;
        }

        // CSV 줄 단위로 나눈다
        string[] lines = csv.text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);


        // CSV 첫 줄 확인
        if (lines.Length <= 1)
        {
            Debug.LogError("CSV에 캐릭터 데이터가 없습니다.");
            return;
        }


        // CSV 첫 줄을 읽어 각 헤더의 위치 저장 
        string[] headers = lines[0].Split(',');
        Dictionary<string, int> columns = new();

        for (int i = 0; i < headers.Length; i++)
            columns[headers[i].Trim()] = i;


        // 필요한 열이 모두 존재하는지 확인
        string[] required =
        {
                "id",
                "name",
                "maxHp",
                "attack",
                "defense",
                "specialAttack",
                "specialDefense",
                "speed"
            };


        foreach (string header in required)
        {
            if (!columns.ContainsKey(header))
            {
                Debug.LogError($"CSV에 필요한 열이 없습니다: {header}");
                return;
            }
        }
        // Assets/Data/Characters 폴더가 없으면 생성
        Directory.CreateDirectory(OutputFolder);

        // 파일이나 폴더가 바뀌었으니까 다시 확인해
        AssetDatabase.Refresh();

        int count = 0;

        // CSV 한 줄씩 읽기 / O(n)
        for (int i = 1; i < lines.Length; i++)
        {
            // 한 줄을 각각의 칸으로 나누기
            string[] cells = lines[i].Split(',');

            // 필요한 열까지 데이터가 존재하는지 확인
            if (cells.Length < headers.Length)
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄의 데이터가 부족합니다.");
                continue;
            }

            // 숫자 변환 부분 (TryParse)
            // Trim()은 공백이 있어도 공백을 제거한다
            if (!int.TryParse(cells[columns["id"]].Trim(), out int id) ||
                !int.TryParse(cells[columns["maxHp"]].Trim(), out int maxHp) ||
                !int.TryParse(cells[columns["attack"]].Trim(), out int attack) ||
                !int.TryParse(cells[columns["defense"]].Trim(), out int defense) ||
                !int.TryParse(cells[columns["specialAttack"]].Trim(), out int specialAttack) ||
                !int.TryParse(cells[columns["specialDefense"]].Trim(), out int specialDefense) ||
                !int.TryParse(cells[columns["speed"]].Trim(), out int speed))
            {
                Debug.LogWarning($"CSV {i + 1}번째 줄에 잘못된 숫자가 있습니다.");
                continue;
            }

            // SO 파일 이름 결정
            string path = $"{OutputFolder}/Character_{id}.asset";


            // 기존 SO 이미 있으면 새로 생성하지 않고 유지시킨다
            // 값만 업데이트 됨
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);

            // 기존 SO가 없다면 새로 생성
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject so = new(data);


            // FindProperty()
            // 해당 Data (.cs)와 CSV Importer가 연결되는 핵심 부분
            SerializedProperty idProperty = so.FindProperty("id");
            SerializedProperty nameProperty = so.FindProperty("name");
            SerializedProperty maxHpProperty = so.FindProperty("maxHp");
            SerializedProperty attackProperty = so.FindProperty("attack");
            SerializedProperty defenseProperty = so.FindProperty("defense");
            SerializedProperty specialAttackProperty = so.FindProperty("specialAttack");
            SerializedProperty specialDefenseProperty = so.FindProperty("specialDefense");
            SerializedProperty speedProperty = so.FindProperty("speed");


            // 방어코드: 해당 Data (.cs)에 변수이름과 다를 때 호출
            if (idProperty == null ||
                nameProperty == null ||
                maxHpProperty == null ||
                attackProperty == null ||
                defenseProperty == null ||
                specialAttackProperty == null ||
                specialDefenseProperty == null ||
                speedProperty == null)
            {
                Debug.LogError(
                    $"Character_{id}.asset 변환 실패: Game.Data.CharacterData의 필드 이름을 확인하세요.");
                continue;
            }

            // CSV 값을 SO에 적용
            idProperty.intValue = id;
            nameProperty.stringValue = cells[columns["name"]].Trim();
            maxHpProperty.intValue = maxHp;
            attackProperty.intValue = attack;
            defenseProperty.intValue = defense;
            specialAttackProperty.intValue = specialAttack;
            specialDefenseProperty.intValue = specialDefense;
            speedProperty.intValue = speed;

            // SerializedProperty에 변경한 값을 실제 SO에 적용
            so.ApplyModifiedPropertiesWithoutUndo();

            // Unity에게 SO가 변경됐다는 것을 알림
            EditorUtility.SetDirty(data);

            count++;
        }

        // for문이 끝난 후 모든 SO 저장
        AssetDatabase.SaveAssets();

        // Unity 갱신
        AssetDatabase.Refresh();

        Debug.Log($"CharacterData Import 완료: {count}개");
    }
}