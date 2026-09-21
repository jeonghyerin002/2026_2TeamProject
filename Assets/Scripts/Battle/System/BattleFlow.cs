using UnityEngine;

/// <summary>플레이어와 적의 스킬 선택부터 한 턴의 행동 실행까지 전투 흐름을 진행한다.</summary>
public class BattleFlow : MonoBehaviour
{
    [SerializeField] private BattleSystem battleSystem;

    // 전투 시작
    private void Start()
    {
        if (battleSystem == null)
        {
            Debug.LogError("BattleFlow: BattleSystem을 연결하세요.");
            return;
        }

        battleSystem.StartBattle();
    }

    // 플레이어가 선택한 스킬로 한 턴 진행
    public void SelectPlayerSkill(int skillIndex)
    {
        if (!battleSystem.SelectPlayerSkill(skillIndex))
            return;

        int enemySkillIndex = GetEnemySkillIndex();

        if (enemySkillIndex < 0)
        {
            Debug.Log("적이 사용할 수 있는 스킬이 없습니다.");
            return;
        }

        if (!battleSystem.SelectEnemySkill(enemySkillIndex))
            return;

        ResolveTurn();
    }

    // 턴에 등록된 행동을 순서대로 실행
    private void ResolveTurn()
    {
        while (battleSystem.Phase == TurnPhase.Resolving)
        {
            if (!BattleResolver.TryResolveNext(
                battleSystem,
                out BattleState actor,
                out BattleResolveResult result))
                break;

            if (!result.Hit)
                Debug.Log("공격 실패");
            else
                Debug.Log($"{actor.Character.Charactername}이(가) {result.Damage} 데미지를 입혔습니다.");
        }

        if (battleSystem.Phase == TurnPhase.Ended)
            Debug.Log("전투 종료");
    }

    // 적이 사용할 수 있는 첫 번째 스킬 선택
    private int GetEnemySkillIndex()
    {
        BattleState enemy = battleSystem.EnemyState;
        SkillData[] skills = enemy.Aether.Skills;

        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i] != null &&
                enemy.GetCurrentPp(skills[i]) > 0)
                return i;
        }

        return -1;
    }
}