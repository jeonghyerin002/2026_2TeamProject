using System.Collections;
using System.Collections.Generic;
using UnityEditor.U2D.Animation;
using UnityEngine;

public class TurnManager : MonoBehaviour //매니저 스크립트 엎을 필요가 있음 굳이 코루틴을 사용하지 않아도 되는 부분 다수 존재
{
    [Header("Characters")]
    public CharacterData player;
    public CharacterData enemy; // 적 가지수는 나중에 늘릴 예정

    [Header("Managers")]
    public BattleUIManager uiManager;

    AttackData selectedPlayerAttack;
    bool isAttackSelected = false;

    private void OnEnable()
    {
        if (uiManager != null)
        {
            uiManager.OnSkillButtonClicked += HandleSkillButtonClicked;
        }
    }
    private void OnDisable()
    {
        if (uiManager != null)
        {
            uiManager.OnSkillButtonClicked -= HandleSkillButtonClicked;
        }
    }
    void Start()
    {
        player.Init(); //기능 구현 이후에는 삭제
        enemy.Init(); //기능 구현 이후에는 삭제
        StartCoroutine(TurnLoop());
    }

    IEnumerator TurnLoop()
    {
        while (true) //상태enum으로 확인하고
        {
            //HP 및 플레이어 공격 횟수 확인
            if (CheckBattleEnd())
            {
                Debug.Log("전투 종료 조건 만족");
                yield break;
            }

            //UI 갱신 및 선택 대기
            uiManager.RefreshSkillButtons(player.attackList);
            uiManager.ToggleAttackPanel(true);

            isAttackSelected = false;
            yield return new WaitUntil(() => isAttackSelected);

            uiManager.ToggleAttackPanel(false);

            //전투 진행
            AttackData enemyAttack = GetRandomEnemyAttack();
            yield return StartCoroutine(ExecuteCombat(selectedPlayerAttack, enemyAttack));
        }
    }

    void HandleSkillButtonClicked(int attackIndex)
    {
        if (attackIndex < player.attackList.Count)
        {
            AttackData attack = player.attackList[attackIndex];
            if (attack.currentCount > 0)
            {
                selectedPlayerAttack = attack;
                isAttackSelected = true;
            }
        }
    }
    IEnumerator ExecuteCombat(AttackData playerAttack, AttackData enemyAttack)
    {
        bool playerFirst = player.speed >= enemy.speed; // bool 값 사용 말고 직접 데이터로 비교

        CharacterData firstAttacker = playerFirst ? player : enemy;
        CharacterData secondAttacker = playerFirst ? enemy : player;
        AttackData firstSkill = playerFirst ? playerAttack : enemyAttack;
        AttackData secondSkill = playerFirst ? enemyAttack : playerAttack;

        bool isFirstPlayer = playerFirst;

        //첫 번째 공격자 행동
        PerformAttack(firstAttacker, secondAttacker, firstSkill);
        if (secondAttacker.currentHP <= 0)
            yield break;

        yield return new WaitForSeconds(1.0f); // 연출 대기 시간

        //두 번째 공격자 행동
        PerformAttack(secondAttacker, firstAttacker, secondSkill);
        if (firstAttacker.currentHP <= 0)
            yield break;

        yield return new WaitForSeconds(1.0f);
    }

    //얘는 완전히 뜯어내야함 
    void PerformAttack(CharacterData attacker, CharacterData target, AttackData skill)
    {
        if (skill != null)
        {
            skill.currentCount--;

            //데미지 계산 대충 (기본 데미지 10 * 상성 계수)
            float damageMultiplier = skill.typeData != null ? skill.typeData.damageMultiplier : 1.0f;
            int damage = Mathf.RoundToInt(10 * damageMultiplier);

            target.currentHP = Mathf.Max(0, target.currentHP - damage);

            Debug.Log($"{attacker.characterName}의 {skill.attackName} " +
                $"{target.characterName}에게 {damage} 데미지 (남은 HP : {target.currentHP}");

        }
    }
    bool CheckBattleEnd()
    {
        //둘 중 HP가 0인 캐릭터가 있거나, 플레이어의 사용 가능한 공격 횟수가 없는 경우
        if (player.currentHP <= 0 || enemy.currentHP <= 0) return true;
        if (!player.HasRemainingAttacks()) return true;

        return false;
    }

    AttackData GetRandomEnemyAttack()
    {
        var validAttacks = enemy.attackList.FindAll(a => a != null && a.currentCount > 0);
        return validAttacks.Count > 0 ? validAttacks[Random.Range(0, validAttacks.Count)] : null;
    }
}