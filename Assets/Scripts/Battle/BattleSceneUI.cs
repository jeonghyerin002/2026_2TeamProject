using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>전투 화면의 메뉴와 상태 표시를 기존 전투 흐름에 연결한다</summary>
public sealed class BattleSceneUI : MonoBehaviour
{
    [SerializeField] private BattleSystem battle;
    [SerializeField] private BattleFlow flow;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text playerInfo;
    [SerializeField] private TMP_Text enemyInfo;
    [SerializeField] private TMP_Text playerHealth;
    [SerializeField] private TMP_Text enemyHealth;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private TMP_Text turnLabel;
    [SerializeField] private Image playerFill;
    [SerializeField] private Image enemyFill;
    [SerializeField] private RectTransform playerPosition;
    [SerializeField] private RectTransform enemyPosition;
    [SerializeField] private Button[] choices;
    [SerializeField] private TMP_Text[] labels;
    [SerializeField] private Button back;

    private enum Menu { Root, Skills, Party, Info, End }
    private Menu menu;
    private int selected;
    private int partyPage;
    private Coroutine animationRoutine;
    private Vector2 playerOrigin;
    private Vector2 enemyOrigin;

    // 참조와 버튼 입력을 준비한다
    private void Awake()
    {
        playerOrigin = playerPosition.anchoredPosition;
        enemyOrigin = enemyPosition.anchoredPosition;
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            choices[i].onClick.AddListener(() => Choose(index));
        }
        back.onClick.AddListener(Back);
    }

    // 전투 상태와 연출 이벤트를 구독한다
    private void OnEnable()
    {
        battle.StateChanged += Refresh;
        flow.AnimationRequested += Animate;
    }

    // 구독과 진행 중 연출을 정리한다
    private void OnDisable()
    {
        if (battle != null)
            battle.StateChanged -= Refresh;
        if (flow != null)
            flow.AnimationRequested -= Animate;
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = null;
        if (playerPosition != null)
            playerPosition.anchoredPosition = playerOrigin;
        if (enemyPosition != null)
            enemyPosition.anchoredPosition = enemyOrigin;
    }

    // 다른 컴포넌트 초기화 순서와 무관하게 현재 상태를 표시한다
    private void Start()
    {
        Refresh();
    }

    // 프로젝트 조작 키와 방향키를 처리한다
    private void Update()
    {
        Keyboard key = Keyboard.current;
        if (key == null || flow.IsBusy)
            return;
        if (key.eKey.wasPressedThisFrame || key.escapeKey.wasPressedThisFrame)
        {
            Back();
            return;
        }
        int step = key.aKey.wasPressedThisFrame ? -1 : key.dKey.wasPressedThisFrame ? 1 :
            key.leftArrowKey.wasPressedThisFrame ? -1 : key.rightArrowKey.wasPressedThisFrame ? 1 :
            key.upArrowKey.wasPressedThisFrame || key.downArrowKey.wasPressedThisFrame ? 2 : 0;
        if (step != 0)
            Focus(step);
        if (key.fKey.wasPressedThisFrame)
            Choose(selected);
    }

    // 외부 연출 완료 시에도 메뉴 잠금을 갱신한다
    public void Refresh()
    {
        if (battle.PlayerState == null || battle.EnemyState == null)
            return;
        ShowUnit(battle.PlayerState, playerInfo, playerHealth, playerFill);
        ShowUnit(battle.EnemyState, enemyInfo, enemyHealth, enemyFill);
        turnLabel.text = $"TURN {battle.TurnNumber:00}";
        if (!flow.IsBusy)
        {
            if (battle.Phase == TurnPhase.Ended)
                menu = Menu.End;
            else if (battle.Phase == TurnPhase.WaitingReplacement)
                menu = Menu.Party;
            else if (menu == Menu.End)
                menu = Menu.Root;
        }
        ShowMenu();
    }

    // 이름과 레벨, 상태이상, 실제 HP를 표시한다
    private static void ShowUnit(BattleState unit, TMP_Text info, TMP_Text health, Image fill)
    {
        string status = unit.Status == BattleStatus.None ? "" : $"  [{unit.Status}]";
        info.text = $"{unit.Character.Charactername}   <size=75%>Lv.{unit.Level}</size>{status}";
        health.text = $"{unit.CurrentHp} / {unit.MaxHp}";
        float ratio = (float)unit.CurrentHp / unit.MaxHp;
        fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        fill.color = ratio > 0.5f ? new Color32(72, 184, 120, 255) :
            ratio > 0.2f ? new Color32(235, 188, 72, 255) : new Color32(223, 91, 88, 255);
    }

    // 현재 메뉴의 네 슬롯과 안내를 표시한다
    private void ShowMenu()
    {
        for (int i = 0; i < choices.Length; i++)
        {
            labels[i].text = "—";
            choices[i].interactable = false;
        }
        back.interactable = !flow.IsBusy && menu != Menu.Root && menu != Menu.End && battle.Phase != TurnPhase.WaitingReplacement;
        if (flow.IsBusy)
        {
            detail.text = "전투 진행 중…";
            return;
        }
        BattleState player = battle.PlayerState;
        switch (menu)
        {
            case Menu.Root:
                SetChoice(0, "싸운다", true);
                SetChoice(1, "파티", true);
                SetChoice(2, "빌드 정보", true);
                SetChoice(3, "도망", false);
                message.text = $"{player.Character.Charactername}, 무엇을 할까?";
                detail.text = "A / D 선택    F 결정    E 뒤로\n이 전투에서는 도망칠 수 없습니다";
                break;
            case Menu.Skills:
                for (int i = 0; i < choices.Length; i++)
                {
                    SkillData skill = player.GetSkill(i);
                    if (skill != null)
                        SetChoice(i, $"{skill.Skillname}\n<size=65%>{skill.Type}   PP {player.GetCurrentPp(skill)}/{skill.PP}</size>", player.GetCurrentPp(skill) > 0);
                }
                if (player.GetAvailableSkill() < 0)
                    SetChoice(0, "발버둥", true);
                message.text = "사용할 기술을 선택하세요.";
                detail.text = "에테르 기술 3개 + 무기 기술 1개";
                break;
            case Menu.Party:
                int start = partyPage * 3;
                for (int i = 0; i < 3 && start + i < battle.PlayerParty.Count; i++)
                {
                    BattleState member = battle.PlayerParty[start + i];
                    SetChoice(i, $"{member.Character.Charactername}\n<size=65%>HP {member.CurrentHp}/{member.MaxHp}{(member == player ? "  출전 중" : "")}</size>",
                        battle.Phase == TurnPhase.WaitingReplacement && !member.IsDead && member != player);
                }
                SetChoice(3, "다음 페이지", battle.PlayerParty.Count > 3);
                message.text = battle.Phase == TurnPhase.WaitingReplacement ? "다음에 나올 아군을 선택하세요." : "현재 파티를 확인합니다.";
                detail.text = battle.Phase == TurnPhase.WaitingReplacement ? "살아 있는 예비 멤버를 선택하세요" : "기절했을 때 예비 멤버로 교체할 수 있습니다";
                break;
            case Menu.Info:
                message.text = $"에테르: {player.Aether.AetherName}\n무기: {(player.Weapon != null ? player.Weapon.WeaponName : "없음")}";
                detail.text = $"공격 {player.GetStat(BattleStat.Attack)} / 방어 {player.GetStat(BattleStat.Defense)}\n특공 {player.GetStat(BattleStat.SpecialAttack)} / 특방 {player.GetStat(BattleStat.SpecialDefense)} / 속도 {player.Speed}";
                SetChoice(0, "돌아가기", true);
                break;
            case Menu.End:
                message.text = battle.Winner == BattleSide.Player ? "전투에서 승리했다!" : battle.Winner == BattleSide.Enemy ? "전투에서 패배했다!" : "전투가 무승부로 끝났다!";
                detail.text = "다시 시작하면 HP와 PP가 초기화됩니다";
                SetChoice(0, "다시 전투", true);
                break;
        }
        if (!choices[selected].interactable)
        {
            selected = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                if (!choices[i].interactable)
                    continue;
                selected = i;
                break;
            }
        }
        SelectCurrent();
    }

    // 슬롯 문구와 입력 가능 상태를 설정한다
    private void SetChoice(int index, string text, bool enabled)
    {
        labels[index].text = text;
        choices[index].interactable = enabled;
    }

    // 선택된 버튼에 포커스와 기술 상세를 표시한다
    private void SelectCurrent()
    {
        if (EventSystem.current != null && choices[selected].interactable)
            EventSystem.current.SetSelectedGameObject(choices[selected].gameObject);
        SkillData skill = menu == Menu.Skills ? battle.PlayerState.GetSkill(selected) : null;
        if (skill != null)
            detail.text = $"{skill.Type} / {skill.AttackType}   위력 {skill.Damage}\n명중 {skill.Accuracy}%   우선도 {skill.Priority}";
    }

    // 비활성 슬롯을 건너뛰며 선택한다
    private void Focus(int step)
    {
        for (int i = 0; i < choices.Length; i++)
        {
            selected = (selected + step + choices.Length) % choices.Length;
            if (choices[selected].interactable)
                break;
            if (step == 2)
                step = 1;
        }
        SelectCurrent();
    }

    // 선택 메뉴를 이동하거나 기존 전투 흐름에 행동을 전달한다
    private void Choose(int index)
    {
        if (flow.IsBusy || !choices[index].interactable)
            return;
        selected = index;
        switch (menu)
        {
            case Menu.Root:
                menu = index == 0 ? Menu.Skills : index == 1 ? Menu.Party : Menu.Info;
                break;
            case Menu.Skills:
                menu = Menu.Root;
                flow.SelectPlayerSkill(battle.PlayerState.GetAvailableSkill() < 0 ? -1 : index);
                break;
            case Menu.Party:
                if (index == 3)
                    partyPage = (partyPage + 1) % ((battle.PlayerParty.Count + 2) / 3);
                else
                {
                    menu = Menu.Root;
                    flow.SelectReplacement(partyPage * 3 + index);
                }
                break;
            case Menu.Info:
                menu = Menu.Root;
                break;
            case Menu.End:
                menu = Menu.Root;
                partyPage = 0;
                flow.StartBattle();
                break;
        }
        Refresh();
    }

    // 강제 교체를 제외한 하위 메뉴를 닫는다
    private void Back()
    {
        if (flow.IsBusy || battle.Phase == TurnPhase.WaitingReplacement || menu == Menu.End)
            return;
        menu = Menu.Root;
        Refresh();
    }

    // 캐릭터 이미지 없이 전투 위치의 도형을 움직여 공격을 표시한다
    private void Animate(BattleState actor, BattleState target, SkillData skill)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(PlayAttack(actor == battle.PlayerState));
    }

    // 아군과 적의 기본 도형에 짧은 돌진과 피격 흔들림을 적용한다
    private IEnumerator PlayAttack(bool player)
    {
        RectTransform source = player ? playerPosition : enemyPosition;
        RectTransform target = player ? enemyPosition : playerPosition;
        Vector2 sourceOrigin = player ? playerOrigin : enemyOrigin;
        Vector2 targetOrigin = player ? enemyOrigin : playerOrigin;
        float elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.45f);
            source.anchoredPosition = sourceOrigin + (targetOrigin - sourceOrigin).normalized * (Mathf.Sin(progress * Mathf.PI) * 32f);
            target.anchoredPosition = targetOrigin + Vector2.right * (Mathf.Sin(progress * Mathf.PI * 8f) * 7f * (1f - progress));
            yield return null;
        }
        source.anchoredPosition = sourceOrigin;
        target.anchoredPosition = targetOrigin;
        animationRoutine = null;
    }
}
