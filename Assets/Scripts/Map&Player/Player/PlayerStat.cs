/// <summary>
/// 영구 성장에 투자할 수 있는 여섯 능력치 구분
/// </summary>
public enum PlayerStat { MaxHp, Attack, SpecialAttack, Defense, SpecialDefense, Speed }

/// <summary>
/// 저장 및 플레이어 데이터에 공통 적용할 누적 스텟 투자 검증
/// </summary>
public static class PlayerStatAllocation
{
    // 여섯 능력치의 고정 저장 순서와 길이
    public const int Count = 6;

    // 구형 저장의 누락 허용 및 누적 투자 한도 검사
    public static bool IsValid(int[] investments, int level)
    {
        // 투자 데이터가 없는 구형 저장의 기본 미투자 상태 허용
        if (investments == null)
        {
            return true;
        }

        if (investments.Length != Count)
        {
            return false;
        }

        // 스텟별 레벨업 횟수 한도와 전체 지급량을 함께 검사
        int limit = LevelProgression.GetGrowthCount(level);
        int total = 0;

        foreach (int amount in investments)
        {
            if (amount < 0 || amount > limit)
            {
                return false;
            }

            total += amount;
        }

        return total <= limit * LevelProgression.StatPointsPerLevel;
    }


    // 총 지급량에서 누적 투자량을 제외한 남은 포인트 조회
    public static int GetRemainingPoints(int[] investments, int level)
    {
        // 잘못된 배열과 한도 초과 데이터의 추가 투자 차단
        if (!IsValid(investments, level))
        {
            return 0;
        }

        int remaining = LevelProgression.GetGrowthCount(level) * LevelProgression.StatPointsPerLevel;

        if (investments != null)
        {
            foreach (int amount in investments)
            {
                remaining -= amount;
            }
        }

        return remaining;
    }


    // 투자 대상과 스텟별 한도 및 잔여 포인트 검사
    public static bool CanInvest(int[] investments, int level, PlayerStat stat)
    {
        // 미초기화 배열과 정의되지 않은 enum 값의 변경 차단
        int index = (int)stat;
        return investments != null && index >= 0 && index < Count &&
            GetRemainingPoints(investments, level) > 0 &&
            investments[index] < LevelProgression.GetGrowthCount(level);
    }


    // 공통 규칙을 통과한 능력치에 포인트 하나 투자
    public static bool TryInvest(int[] investments, int level, PlayerStat stat)
    {
        // 실패 시 원본 배열을 변경하지 않는 투자 처리
        if (!CanInvest(investments, level, stat))
        {
            return false;
        }

        investments[(int)stat]++;
        return true;
    }
}
