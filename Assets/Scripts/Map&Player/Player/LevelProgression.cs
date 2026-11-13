using System;

/// <summary>
/// 누적 경험치에 따른 레벨 판정과 최대 레벨 및 경험치 상한을 관리한다.
/// </summary>
public static class LevelProgression
{
    public const int MaxLevel = 100; // 도달할 수 있는 최고 레벨이다.
    public const int MaxExperience = 1000000; // 최고 레벨에 필요한 누적 경험치이자 저장 상한이다.

    // 레벨업별 자동 성장 및 포인트 지급 기준
    public const int MaxHpPerLevel = 2;
    public const int StatPerLevel = 1;
    public const int StatPointsPerLevel = 3;

    // 레벨 1을 기준으로 누적 레벨업 횟수 조회
    public static int GetGrowthCount(int level)
    {
        // 초기 설정과 저장 복원의 레벨 범위를 동일한 기준으로 제한
        return Math.Max(1, Math.Min(MaxLevel, level)) - 1;
    }


    // 해당 레벨에 도달하기 위한 누적 경험치를 반환한다.
    public static int GetRequiredExperience(int level)
    {
        level = Math.Max(1, Math.Min(MaxLevel, level));
        return level == 1 ? 0 : level * level * level;
    }

    // 기존 레벨을 보존하면서 씬 설정과 이전 세이브의 경험치를 새 기준에 맞춘다.
    public static void Normalize(ref int level, ref int experience)
    {
        level = Math.Max(1, Math.Min(MaxLevel, level));
        experience = Math.Max(GetRequiredExperience(level), Math.Min(MaxExperience, experience));
        while (level < MaxLevel && experience >= GetRequiredExperience(level + 1))
            level++;
    }

    // 음수 보상을 무시하고 누적 경험치로 연속 레벨업한 횟수를 반환한다.
    public static int GainExperience(ref int level, ref int experience, int amount)
    {
        Normalize(ref level, ref experience);
        int previousLevel = level;
        // 더하기 전에 남은 용량을 제한해 큰 보상에서도 정수 오버플로를 방지한다.
        experience += Math.Min(MaxExperience - experience, Math.Max(0, amount));
        Normalize(ref level, ref experience);
        return level - previousLevel;
    }
}
