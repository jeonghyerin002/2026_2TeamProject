using UnityEditor;

/// <summary>SkillData CSV Import 메뉴를 제공한다.</summary>
public static class SkillDataCsvImporter
{
    // 선택한 CSV를 SkillData SO로 변환
    [MenuItem("Tools/Data/Import SkillData")]
    private static void Import()
    {
        CsvSoImporter.Import<SkillData>(
            "Assets/Data/Skills",
            "Skill",
            "skillid",
            ignoreHeaders: new[] { "memo" });

    }
}