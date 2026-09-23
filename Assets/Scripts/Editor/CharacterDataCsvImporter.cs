using UnityEditor;

/// <summary>CharacterData CSV Import 메뉴를 제공한다.</summary>
public static class CharacterDataCsvImporter
{
    // 선택한 CSV를 CharacterData SO로 변환
    [MenuItem("Tools/Data/Import CharacterData")]
    private static void Import()
    {
        CsvSoImporter.Import<CharacterData>(
            "Assets/Data/Characters",
            "Character",
            "characterid");
    }
}