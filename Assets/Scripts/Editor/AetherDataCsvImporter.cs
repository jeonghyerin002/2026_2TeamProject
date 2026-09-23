using UnityEditor;

/// <summary>AetherData CSV Import 메뉴를 제공한다.</summary>
public static class AetherDataCsvImporter
{
    // 선택한 CSV를 AetherData SO로 변환
    [MenuItem("Tools/Data/Import AetherData")]
    private static void Import()
    {
        CsvSoImporter.Import<AetherData>(
            "Assets/Data/Items",
            "Aether",
            "itemid",
            refArray: new CsvSoImporter.RefArray<SkillData>(
                "skills",
                "skillid",
                 3,
                "Assets/Data/Skills",
                "Skill",
                 2));
    }
}