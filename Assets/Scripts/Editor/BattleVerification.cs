using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

/// <summary>씬 없이 전투 규칙과 SO 보존을 검증한다</summary>
public static class BattleVerification
{
    private static readonly List<UnityEngine.Object> created = new();
    private static int checks;

    // 메뉴 또는 배치 모드에서 전투 회귀 검증을 실행한다
    [MenuItem("Tools/Battle/Verify Rules")]
    public static void Run()
    {
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        checks = 0;
        try
        {
            VerifyDamage();
            VerifyState();
            VerifyOrder();
            VerifyBattle();
            VerifyFlow();
            Debug.Log($"Battle verification passed: {checks} checks");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            foreach (UnityEngine.Object item in created)
                UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
        }
    }

    // 사진의 기본값과 보정 순서 및 명중 실패를 검증한다
    private static void VerifyDamage()
    {
        Check(BattleResolver.GetBaseDamage(50, 40, 100, 80) == 24, "사진 예시 기본 피해 24");
        SkillData skill = Skill(40);
        BattleState actor = Unit(skill, ElementType.Fire);
        BattleState target = Unit(Skill(1), ElementType.Grass);
        Set(skill, "criticalChance", 1f);
        BattleResolveResult result = BattleResolver.Resolve(actor, target, skill);
        Check(result.Damage == 144 && result.Critical && result.Effectiveness == 2f, "24 → 36 → 72 → 144");
        Check(actor.GetCurrentPp(skill) == 9 && skill.PP == 10, "실행 PP 감소 및 SO 보존");
        Set(skill, "accuracy", 0);
        Set(skill, "effectId", 60013);
        int hp = target.CurrentHp;
        int attack = actor.GetStat(BattleStat.Attack);
        result = BattleResolver.Resolve(actor, target, skill);
        Check(result.Executed && !result.Hit && target.CurrentHp == hp, "빗나감은 피해 없음");
        Check(actor.GetStat(BattleStat.Attack) == attack, "빗나감은 부가효과 없음");
        Set(skill, "accuracy", 100);
        Set(skill, "criticalChance", 0f);
        Set(skill, "effectId", 0);
        Set(skill, "attackType", SkillAttackType.Special);
        Set(actor.Character, "specialAttack", 200);
        Set(target.Character, "specialDefense", 80);
        result = BattleResolver.Resolve(actor, target, skill);
        Check(result.Damage == 138, "특수 공격과 특수 방어 사용");
        Set(skill, "damage", 0);
        Set(skill, "effectId", 60012);
        int defense = actor.GetStat(BattleStat.Defense);
        result = BattleResolver.Resolve(actor, target, skill);
        Check(result.Damage == 0 && actor.GetStat(BattleStat.Defense) == defense * 3 / 2, "위력 0 변화기");
        Set(skill, "healRatio", 0.5f);
        actor.TakeDamage(100);
        BattleResolver.Resolve(actor, target, skill);
        Check(actor.CurrentHp == actor.MaxHp, "회복 기술");
        Check(BattleResolver.GetEffectiveness(ElementType.Water, ElementType.Fire) == 2f, "물 → 불");
        Check(BattleResolver.GetEffectiveness(ElementType.Fire, ElementType.Water) == 0.5f, "불 → 물");
        Check(BattleResolver.GetEffectiveness(ElementType.Fighting, ElementType.Normal) == 2f, "격투 → 일반");
    }

    // 장비와 슬롯, 상태이상, 반동을 검증한다
    private static void VerifyState()
    {
        SkillData skill = Skill(40);
        SkillData weaponSkill = Skill(40);
        WeaponData weapon = Create<WeaponData>();
        Set(weapon, "healthMultiplier", 2f);
        Set(weapon, "attackMultiplier", 1.5f);
        Set(weapon, "skills", new[] { weaponSkill });
        BattleState baseUnit = Unit(skill);
        BattleState actor = new(baseUnit.Character, baseUnit.Aether, weapon, 50);
        Check(actor.MaxHp == 2000 && actor.GetStat(BattleStat.Attack) == 150, "무기 능력치");
        Check(actor.SkillCount == 2 && actor.GetSkill(1) == weaponSkill, "무기 스킬 슬롯");
        actor.TryConsumePp(skill);
        Check(actor.GetCurrentPp(weaponSkill) == 10, "같은 ID를 가진 다른 SO의 PP 분리");
        Check(baseUnit.Character.MaxHp == 1000 && baseUnit.GetCurrentPp(skill) == 10, "상태와 원본 분리");
        actor.ApplyStatus(BattleStatus.Sleep, 1);
        int pp = actor.GetCurrentPp(skill);
        BattleResolveResult result = BattleResolver.Resolve(actor, baseUnit, skill);
        Check(!result.Executed && actor.GetCurrentPp(skill) == pp, "수면은 PP 소모 없음");
        result = BattleResolver.Resolve(actor, baseUnit, skill);
        Check(result.Executed && actor.Status == BattleStatus.None, "자신의 행동 기회로 수면 해제");
        Check(actor.ApplyStatus(BattleStatus.Poison), "독 적용");
        Check(!actor.ApplyStatus(BattleStatus.Burn), "주요 상태 중복 차단");
        Check(actor.ApplyStatus(BattleStatus.Toxic), "독에서 맹독 전환");
        Check(actor.ApplyConfusion(3), "혼란 공존");
        Check(actor.ApplyResidualDamage() == 125 && actor.ApplyResidualDamage() == 250, "맹독 누적 피해");
        actor.FinishBattle();
        Check(!actor.IsConfused && actor.Status == BattleStatus.Toxic, "종료 시 혼란 해제 및 주요 상태 유지");
        BattleState recoilActor = Unit(Skill(40));
        BattleState recoilTarget = Unit(Skill(1));
        SkillData recoilSkill = recoilActor.GetSkill(0);
        Set(recoilSkill, "recoilRatio", 1f);
        recoilTarget.TakeDamage(999);
        result = BattleResolver.Resolve(recoilActor, recoilTarget, recoilSkill);
        Check(result.Damage == 1 && recoilActor.CurrentHp == 999, "실제 HP 감소량 기준 반동");
    }

    // 우선도와 스피드, 동률 랜덤 및 중복 완료를 검증한다
    private static void VerifyOrder()
    {
        BattleTurnSystem turn = new();
        turn.StartBattle();
        turn.SetPlayerAction(new BattleTurnAction(BattleSide.Player, 1, 1));
        turn.SetEnemyAction(new BattleTurnAction(BattleSide.Enemy, 0, 100));
        Check(turn.TryGetNextAction(out BattleTurnAction first) && first.Side == BattleSide.Player, "우선도가 스피드보다 우선");
        Check(!turn.TryGetNextAction(out _), "진행 중 중복 행동 차단");
        turn.CompleteAction(false);
        turn.CompleteAction(false);
        Check(turn.TryGetNextAction(out first) && first.Side == BattleSide.Enemy && turn.IsLastAction, "중복 완료로 행동 유실 방지");
        turn.CompleteAction(false);
        Check(turn.TurnNumber == 2 && turn.Phase == TurnPhase.WaitingPlayer, "다음 턴 진행");
        turn.SetPlayerAction(new BattleTurnAction(BattleSide.Player, 0, 1));
        turn.SetEnemyAction(new BattleTurnAction(BattleSide.Enemy, 0, 100));
        turn.TryGetNextAction(out first);
        Check(first.Side == BattleSide.Enemy, "같은 우선도에서 스피드 비교");
        int playerFirst = 0;
        UnityEngine.Random.InitState(17);
        for (int i = 0; i < 100; i++)
        {
            turn.StartBattle();
            turn.SetPlayerAction(new BattleTurnAction(BattleSide.Player, 0, 10));
            turn.SetEnemyAction(new BattleTurnAction(BattleSide.Enemy, 0, 10));
            turn.TryGetNextAction(out first);
            if (first.Side == BattleSide.Player)
                playerFirst++;
        }
        Check(playerFirst > 20 && playerFirst < 80, "동률에서 양측 랜덤 선공");
    }

    // 기절 교체와 PP 소진, 무승부 및 경험치를 검증한다
    private static void VerifyBattle()
    {
        BattleSystem system = System(Unit(Skill(40)), Unit(Skill(40)));
        system.StartBattle();
        SkillData skill = system.PlayerState.GetSkill(0);
        Check(system.SelectPlayerSkill(0) && system.PlayerState.GetCurrentPp(skill) == 10, "선택 시 PP 보존");
        Check(!system.SelectPlayerSkill(0), "중복 선택 차단");
        system.SelectEnemySkill(0);
        system.StopBattle();
        Check(system.Phase == TurnPhase.Ended, "중단 시 진행 상태 정리");

        BattleState strong = Unit(Skill(100000));
        Set(strong.GetSkill(0), "priority", 1);
        BattleState weak = Unit(Skill(1));
        BattleMember reserve = new();
        Set(reserve, "character", weak.Character);
        Set(reserve, "aether", weak.Aether);
        system = System(strong, weak);
        Set(system, "enemyReserves", new[] { reserve });
        system.StartBattle();
        BattleState defeated = system.EnemyState;
        system.SelectPlayerSkill(0);
        system.SelectEnemySkill(0);
        BattleResolver.TryResolveNext(system, out _, out _);
        Check(defeated.IsDead && system.EnemyState != defeated && system.Phase == TurnPhase.Resolving, "적 기절 후 자동 교체");
        Check(system.PlayerState.Experience == 50, "경험치 한 번 지급");
        BattleResolver.TryResolveNext(system, out BattleState skipped, out BattleResolveResult skippedResult);
        Check(skipped == defeated && !skippedResult.Executed && system.EnemyState.GetCurrentPp(weak.GetSkill(0)) == 10, "교체 멤버가 이전 행동을 실행하지 않음");
        Check(system.TurnNumber == 2, "교체 이후 다음 턴");

        system = System(weak, strong);
        Set(system, "playerReserves", new[] { reserve });
        system.StartBattle();
        system.SelectPlayerSkill(0);
        system.SelectEnemySkill(0);
        BattleResolver.TryResolveNext(system, out _, out _);
        Check(system.Phase == TurnPhase.WaitingReplacement, "플레이어 교체 대기");
        Check(!system.SelectReplacement(0) && system.SelectReplacement(1), "살아 있는 예비 멤버만 선택");
        BattleResolver.TryResolveNext(system, out _, out skippedResult);
        Check(!skippedResult.Executed && system.Phase == TurnPhase.WaitingPlayer, "기절 멤버의 예약 행동 취소");

        SkillData empty = Skill(1);
        Set(empty, "pp", 0);
        system = System(Unit(empty), Unit(empty));
        system.StartBattle();
        Check(!system.SelectPlayerSkill(0) && system.SelectPlayerSkill(-1) && system.SelectEnemySkill(-1), "PP 소진 시 발버둥");
        BattleResolver.TryResolveNext(system, out _, out _);
        BattleResolver.TryResolveNext(system, out _, out _);
        Check(system.Phase == TurnPhase.WaitingPlayer && system.PlayerState.CurrentHp < 1000, "발버둥 턴 완료와 반동");

        SkillData harmless = Skill(0);
        system = System(Unit(harmless), Unit(harmless));
        system.StartBattle();
        system.PlayerState.TakeDamage(999);
        system.EnemyState.TakeDamage(999);
        system.PlayerState.ApplyStatus(BattleStatus.Poison);
        system.EnemyState.ApplyStatus(BattleStatus.Poison);
        system.SelectPlayerSkill(0);
        system.SelectEnemySkill(0);
        BattleResolver.TryResolveNext(system, out _, out _);
        BattleResolver.TryResolveNext(system, out _, out _);
        Check(system.Phase == TurnPhase.Ended && system.Winner == null, "턴 종료 동시 기절 무승부");
    }

    // 코루틴의 문구, 애니메이션, 피해, 부가효과 순서를 검증한다
    private static void VerifyFlow()
    {
        SkillData skill = Skill(40);
        Set(skill, "effectId", 60013);
        BattleState actor = Unit(skill);
        BattleState target = Unit(Skill(1));
        BattleSystem system = System(actor, target);
        BattleFlow flow = system.gameObject.AddComponent<BattleFlow>();
        Set(flow, "battleSystem", system);
        Set(flow, "textDuration", 0f);
        Set(flow, "animationDuration", 0f);
        UnityEvent<string> message = new();
        Set(flow, "onMessage", message);
        List<string> order = new();
        message.AddListener(text => order.Add(text.Contains("사용했다") ? "use" : "effect"));
        flow.AnimationRequested += (source, destination, selected) =>
        {
            Check(destination.CurrentHp == destination.MaxHp && source.GetStat(BattleStat.Attack) == 100, "애니메이션 시점은 피해와 부가효과 전");
            order.Add("animation");
        };
        MethodInfo method = typeof(BattleFlow).GetMethod("ResolveAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Drain((IEnumerator)method.Invoke(flow, new object[] { actor, target, skill }));
        Check(order.Count == 3 && order[0] == "use" && order[1] == "animation" && order[2] == "effect", "사용 문구 → 애니메이션 → 부가효과 문구");
        Check(target.CurrentHp == 976 && actor.GetStat(BattleStat.Attack) == 150, "능력치 상승은 현재 공격의 피해 계산 이후 적용");
        order.Clear();
        Set(skill, "accuracy", 0);
        Drain((IEnumerator)method.Invoke(flow, new object[] { actor, target, skill }));
        Check(order.Count == 2 && !order.Contains("animation") && target.CurrentHp == 976, "빗나간 코루틴은 애니메이션과 피해를 생략");
    }

    // 대기 시간이 없는 중첩 코루틴을 끝까지 진행한다
    private static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
                Drain(nested);
        }
    }

    // 검증용 스킬 SO를 생성한다
    private static SkillData Skill(int power)
    {
        SkillData skill = Create<SkillData>();
        Set(skill, "damage", power);
        Set(skill, "pp", 10);
        Set(skill, "accuracy", 100);
        Set(skill, "criticalChance", 0f);
        Set(skill, "type", ElementType.Fire);
        return skill;
    }

    // 검증용 캐릭터와 에테르를 생성한다
    private static BattleState Unit(SkillData skill, ElementType type = ElementType.Normal)
    {
        CharacterData character = Create<CharacterData>();
        Set(character, "maxHp", 1000);
        Set(character, "attack", 100);
        Set(character, "defense", 80);
        Set(character, "specialAttack", 100);
        Set(character, "specialDefense", 80);
        Set(character, "speed", 10);
        AetherData aether = Create<AetherData>();
        Set(aether, "aetherType", type);
        Set(aether, "skills", new[] { skill });
        return new BattleState(character, aether, level: 50);
    }

    // 검증용 배틀 컴포넌트를 생성한다
    private static BattleSystem System(BattleState player, BattleState enemy)
    {
        GameObject host = new("Battle verification");
        created.Add(host);
        BattleSystem system = host.AddComponent<BattleSystem>();
        Set(system, "playerCharacter", player.Character);
        Set(system, "playerAether", player.Aether);
        Set(system, "playerLevel", 50);
        Set(system, "enemyCharacter", enemy.Character);
        Set(system, "enemyAether", enemy.Aether);
        Set(system, "enemyLevel", 50);
        return system;
    }

    // 검증 종료 시 해제할 SO를 생성한다
    private static T Create<T>() where T : ScriptableObject
    {
        T item = ScriptableObject.CreateInstance<T>();
        created.Add(item);
        return item;
    }

    // 검증용 원본 필드를 설정한다
    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    // 기대값과 다른 결과를 즉시 보고한다
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException("Battle verification failed: " + name);
        checks++;
    }
}
