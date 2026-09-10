using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [Header("Characters")]
    public CharacterData player;
    public CharacterData enemy;

    [Header("UI")]
    public GameObject attackPanel;

    private AttackData selectedPlayerAttack;
    private bool isAttackSelected = false;

    private void Start()
    {
        // 1. 초기화 및 턴 시작
        player.Init();
        enemy.Init();
        StartCoroutine(TurnLoop());
    }

    private IEnumerator TurnLoop()
    {
        while (true)
        {
            // 2. HP 및 플레이어 공격 횟수 확인
            if (CheckBattleEnd())
            {
                Debug.Log("전투 종료 조건 만족!");
                yield break;
            }

            // 3. AttackPanel 활성화 및 플레이어 공격 선택 대기
            attackPanel.SetActive(true);
            isAttackSelected = false;

            yield return new WaitUntil(() => isAttackSelected);

            attackPanel.SetActive(false);

            // 적 스킬 랜덤 선택 (테스트용)
            AttackData enemyAttack = GetRandomEnemyAttack();

            // 4. 스피드 비교 후 선제 공격 진행
            yield return StartCoroutine(ExecuteCombat(selectedPlayerAttack, enemyAttack));

            // 6. Loop를 통해 2번 단계로 돌아감
        }
    }

    // UI 버튼 클릭 이벤트에 연결할 메서드 (0~3번 인덱스)
    public void OnSelectAttackButton(int attackIndex)
    {
        if (attackIndex < player.attackList.Count)
        {
            AttackData attack = player.attackList[attackIndex];
            if (attack.currentCount > 0)
            {
                selectedPlayerAttack = attack;
                isAttackSelected = true;
            }
            else
            {
                Debug.Log("해당 기술의 남은 횟수가 없습니다.");
            }
        }
    }

    private IEnumerator ExecuteCombat(AttackData pAttack, AttackData eAttack)
    {
        bool playerFirst = player.speed >= enemy.speed;

        CharacterData firstAttacker = playerFirst ? player : enemy;
        CharacterData secondAttacker = playerFirst ? enemy : player;
        AttackData firstSkill = playerFirst ? pAttack : eAttack;
        AttackData secondSkill = playerFirst ? eAttack : pAttack;

        // --- 첫 번째 공격자 행동 ---
        PerformAttack(firstAttacker, secondAttacker, firstSkill);

        // 5. 공격받은 대상의 HP 확인
        if (secondAttacker.currentHP <= 0)
        {
            Debug.Log($"{secondAttacker.characterName} 쓰러짐! 턴을 종료합니다.");
            yield break; // 턴 즉시 종료 후 다음 Loop로 진입하여 전투 종료 처리
        }

        yield return new WaitForSeconds(1.0f); // 연출 대기 시간

        // --- 두 번째 공격자 행동 ---
        PerformAttack(secondAttacker, firstAttacker, secondSkill);

        // 5. 공격받은 대상의 HP 확인
        if (firstAttacker.currentHP <= 0)
        {
            Debug.Log($"{firstAttacker.characterName} 쓰러짐! 턴을 종료합니다.");
            yield break;
        }

        yield return new WaitForSeconds(1.0f);
    }

    private void PerformAttack(CharacterData attacker, CharacterData target, AttackData skill)
    {
        if (skill == null) return;

        skill.currentCount--;

        // 데미지 계산 예시 (기본 데미지 10 * 상성 계수)
        float damageMultiplier = skill.typeData != null ? skill.typeData.damageMultiplier : 1.0f;
        int damage = Mathf.RoundToInt(10 * damageMultiplier);

        target.currentHP = Mathf.Max(0, target.currentHP - damage);

        Debug.Log($"{attacker.characterName}의 {skill.attackName}! " +
                  $"{target.characterName}에게 {damage} 데미지 (남은 HP: {target.currentHP})");
    }

    private bool CheckBattleEnd()
    {
        // 둘 중 HP가 0인 캐릭터가 있거나, 플레이어의 사용 가능한 공격 횟수가 없는 경우
        if (player.currentHP <= 0 || enemy.currentHP <= 0) return true;
        if (!player.HasRemainingAttacks()) return true;

        return false;
    }

    private AttackData GetRandomEnemyAttack()
    {
        var validAttacks = enemy.attackList.FindAll(a => a != null && a.currentCount > 0);
        if (validAttacks.Count > 0)
            return validAttacks[Random.Range(0, validAttacks.Count)];
        return null;
    }
}