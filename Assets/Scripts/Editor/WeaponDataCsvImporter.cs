using UnityEditor;
using UnityEngine;

public class WeaponDataCsvImporter : MonoBehaviour
{
    // 선택한 CSV를 WeaponData SO로 변환
    [MenuItem("Tools/Data/Import WeaponData")]
    private static void Import()
    {
        CsvSoImporter.Import<WeaponData>(
            "Assets/Data/Items",
            "Item",
            "itemid",
            refArray: new CsvSoImporter.RefArray<SkillData>(
                "skills",
                "skillId",
                1,
                "Assets/Data/Skills",
                "Skill",
                1));
    }
}
