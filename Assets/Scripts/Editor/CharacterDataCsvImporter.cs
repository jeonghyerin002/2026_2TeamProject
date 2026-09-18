using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using CharacterDataSO = Game.Data.CharacterData;

namespace Game.Editor.DataImport
{
    /// <summary>선택한 CSV를 읽어 Game.Data.CharacterData SO를 생성하거나 갱신한다.</summary>
    public static class CharacterDataCsvImporter
    {
        private const string OutputFolder = "Assets/Data/Characters";

        // 선택한 CSV를 CharacterData SO로 변환
        [MenuItem("Tools/Data/Import CharacterData")]
        private static void Import()
        {
            if (Selection.activeObject is not TextAsset csv)
            {
                Debug.LogError("Project 창에서 CharacterData.csv를 선택하세요.");
                return;
            }

            string[] lines = csv.text.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1)
            {
                Debug.LogError("CSV에 캐릭터 데이터가 없습니다.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();

            int count = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                string[] cells = lines[i].Split(',');

                if (cells.Length != 8)
                {
                    Debug.LogWarning(
                        $"CSV {i + 1}번째 줄의 데이터 개수가 잘못되었습니다. 현재: {cells.Length}, 필요: 8");
                    continue;
                }

                if (!int.TryParse(cells[0].Trim(), out int id) ||
                    !int.TryParse(cells[2].Trim(), out int maxHp) ||
                    !int.TryParse(cells[3].Trim(), out int attack) ||
                    !int.TryParse(cells[4].Trim(), out int defense) ||
                    !int.TryParse(cells[5].Trim(), out int specialAttack) ||
                    !int.TryParse(cells[6].Trim(), out int specialDefense) ||
                    !int.TryParse(cells[7].Trim(), out int speed))
                {
                    Debug.LogWarning($"CSV {i + 1}번째 줄에 잘못된 숫자가 있습니다.");
                    continue;
                }

                string path = $"{OutputFolder}/Character_{id}.asset";

                CharacterDataSO data =
                    AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);

                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CharacterDataSO>();
                    AssetDatabase.CreateAsset(data, path);
                }

                SerializedObject so = new(data);

                SerializedProperty idProperty = so.FindProperty("id");
                SerializedProperty nameProperty = so.FindProperty("displayName");
                SerializedProperty maxHpProperty = so.FindProperty("maxHp");
                SerializedProperty attackProperty = so.FindProperty("attack");
                SerializedProperty defenseProperty = so.FindProperty("defense");
                SerializedProperty specialAttackProperty = so.FindProperty("specialAttack");
                SerializedProperty specialDefenseProperty = so.FindProperty("specialDefense");
                SerializedProperty speedProperty = so.FindProperty("speed");

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

                // CSV 값을 CharacterData SO에 적용
                idProperty.intValue = id;
                nameProperty.stringValue = cells[1].Trim();
                maxHpProperty.intValue = maxHp;
                attackProperty.intValue = attack;
                defenseProperty.intValue = defense;
                specialAttackProperty.intValue = specialAttack;
                specialDefenseProperty.intValue = specialDefense;
                speedProperty.intValue = speed;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);

                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"CharacterData Import 완료: {count}개");
        }
    }
}