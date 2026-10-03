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

    private enum Menu { Root, Skills, Aethers, Info, End }

    private Menu menu;
    private int selected;
    private int aetherPage;
    private Coroutine animationRoutine;
    private Vector2 playerOrigin;
    private Vector2 enemyOrigin;

    // 버튼과 전투 위치의 초기 상태를 준비한다
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
        flow.PresentationEnded += Refresh;
    }

    // 이벤트와 진행 중 공격 연출을 정리한다
    private void OnDisable()
    {
        if (battle != null)
            battle.StateChanged -= Refresh;

        if (flow != null)
        {
            flow.AnimationRequested -= Animate;
            flow.PresentationEnded -= Refresh;
        }

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = null;

        if (playerPosition != null)
            playerPosition.anchoredPosition = playerOrigin;

        if (enemyPosition != null)
            enemyPosition.anchoredPosition = enemyOrigin;
    }

    // 다른 컴포넌트 초기화 이후 현재 상태를 표시한다
    private void Start()
    {
        Refresh();
    }

    // 전투 메뉴의 키보드 입력을 처리한다
    private void Update()
    {
        Keyboard key = Keyboard.current;

        if (key == null || flow.IsBusy || menu == Menu.End)
            return;

        if (key.eKey.wasPressedThisFrame || key.escapeKey.wasPressedThisFrame)
        {
            Back();
            return;
        }

        int step =
            key.aKey.wasPressedThisFrame ? -1 :
            key.dKey.wasPressedThisFrame ? 1 :
            key.leftArrowKey.wasPressedThisFrame ? -1 :
            key.rightArrowKey.wasPressedThisFrame ? 1 :
            key.upArrowKey.wasPressedThisFrame || key.downArrowKey.wasPressedThisFrame ? 2 :
            0;

        if (step != 0)
            Focus(step);

        if (key.fKey.wasPressedThisFrame)
            Choose(selected);
    }

    // 현재 전투 상태와 메뉴를 다시 표시한다
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
            else if (menu == Menu.End)
                menu = Menu.Root;
        }

        ShowMenu();
    }

    // 캐릭터 이름과 레벨 및 현재 HP를 표시한다
    private static void ShowUnit(BattleState unit, TMP_Text info, TMP_Text health, Image fill)
    {
        string status = unit.Status == BattleStatus.None ? "" : $"  [{unit.Status}]";

        info.text = $"{unit.Character.Charactername}   <size=75%>Lv.{unit.Level}</size>{status}";
        health.text = $"{unit.CurrentHp} / {unit.MaxHp}";

        float ratio = (float)unit.CurrentHp / unit.MaxHp;

        fill.rectTransform.anchorMax = new Vector2(ratio, 1f);

        fill.color = ratio > 0.5f ? new Color32(72, 184, 120, 255) :
            ratio > 0.2f ? new Color32(235, 188, 72, 255) :
            new Color32(223, 91, 88, 255);
    }

    // 현재 메뉴에서 사용할 선택지를 표시한다
    private void ShowMenu()
    {
        for (int i = 0; i < choices.Length; i++)
        {
            labels[i].text = "—";
            choices[i].interactable = false;
        }

        back.interactable = !flow.IsBusy && menu != Menu.Root && menu != Menu.End;

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
                SetChoice(1, "에테르", true);
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
                    {
                        SetChoice(
                            i,
                            $"{skill.Skillname}\n<size=65%>{skill.Type}   PP {player.GetCurrentPp(skill)}/{skill.PP}</size>",
                            player.GetCurrentPp(skill) > 0);
                    }
                }

                if (player.GetAvailableSkill() < 0)
                    SetChoice(0, "발버둥", true);

                message.text = "사용할 기술을 선택하세요.";
                detail.text = "에테르 기술 3개 + 무기 기술 1개";
                break;

            case Menu.Aethers:
                ShowAethers();
                break;

            case Menu.Info:
                message.text =
                    $"에테르: {player.Aether.AetherName}\n" +
                    $"무기: {(player.Weapon != null ? player.Weapon.WeaponName : "없음")}";

                detail.text =
                    $"공격 {player.GetStat(BattleStat.Attack)} / 방어 {player.GetStat(BattleStat.Defense)}\n" +
                    $"특공 {player.GetStat(BattleStat.SpecialAttack)} / 특방 {player.GetStat(BattleStat.SpecialDefense)} / 속도 {player.Speed}";

                SetChoice(0, "돌아가기", true);
                break;

            case Menu.End:
                message.text = battle.Winner == BattleSide.Player ? "전투에서 승리했다!" :
                    battle.Winner == BattleSide.Enemy ? "전투에서 패배했다!" :
                    "전투가 무승부로 끝났다!";

                detail.text = "2초 후 맵으로 돌아갑니다";
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

    // 보유한 에테르를 세 개씩 표시한다
    private void ShowAethers()
    {
        int count = battle.OwnedAethers.Count;
        int pages = Mathf.Max(1, (count + 2) / 3);

        aetherPage = Mathf.Clamp(aetherPage, 0, pages - 1);

        int start = aetherPage * 3;

        for (int i = 0; i < 3 && start + i < count; i++)
        {
            AetherData aether = battle.OwnedAethers[start + i];
            bool equipped = aether.itemId == battle.PlayerState.Aether.itemId;

            SetChoice(
                i,
                $"<size=70%>{aether.AetherName}</size>\n<size=65%>{aether.AetherType}{(equipped ? "  장착 중" : "  교체")}</size>",
                !equipped);
        }

        SetChoice(
            3,
            $"다음 페이지\n<size=65%>{aetherPage + 1} / {pages}</size>",
            pages > 1);

        message.text =
            $"<size=80%>현재: {battle.PlayerState.Aether.AetherName}</size>\n교체할 에테르를 선택하세요.";

        detail.text = count > 1 ?
            "교체하면 내 턴을 소비하고 적이 행동합니다" :
            "교체 가능한 다른 에테르가 없습니다";
    }

    // 한 선택 슬롯의 문구와 입력 가능 상태를 설정한다
    private void SetChoice(int index, string text, bool enabled)
    {
        labels[index].text = text;
        choices[index].interactable = enabled;
    }

    // 현재 선택 항목에 포커스하고 상세 정보를 표시한다
    private void SelectCurrent()
    {
        if (EventSystem.current != null && choices[selected].interactable)
            EventSystem.current.SetSelectedGameObject(choices[selected].gameObject);

        SkillData skill = menu == Menu.Skills ?
            battle.PlayerState.GetSkill(selected) :
            null;

        if (skill != null)
        {
            detail.text =
                $"{skill.Type} / {skill.AttackType}   위력 {skill.Damage}\n" +
                $"명중 {skill.Accuracy}%   우선도 {skill.Priority}";
        }

        if (menu != Menu.Aethers || selected >= 3)
            return;

        int index = aetherPage * 3 + selected;

        if (index >= battle.OwnedAethers.Count)
            return;

        AetherData aether = battle.OwnedAethers[index];
        string skills = "";

        if (aether.Skills != null)
        {
            foreach (SkillData item in aether.Skills)
            {
                if (item != null)
                    skills += (skills.Length > 0 ? " / " : "") + item.Skillname;
            }
        }

        detail.text =
            $"교체 시 내 턴 소비 · {aether.AetherType}\n" +
            $"{(skills.Length > 0 ? skills : "에테르 기술 없음")}";
    }

    // 입력할 수 없는 슬롯을 건너뛰며 선택 위치를 이동한다
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

    // 선택 메뉴를 이동하거나 선택한 행동을 전투 흐름에 전달한다
    private void Choose(int index)
    {
        if (flow.IsBusy || menu == Menu.End || !choices[index].interactable)
            return;

        selected = index;

        switch (menu)
        {
            case Menu.Root:
                menu = index == 0 ? Menu.Skills :
                    index == 1 ? Menu.Aethers :
                    Menu.Info;
                break;

            case Menu.Skills:
                menu = Menu.Root;
                flow.SelectPlayerSkill(
                    battle.PlayerState.GetAvailableSkill() < 0 ? -1 : index);
                break;

            case Menu.Aethers:
                if (index == 3)
                {
                    aetherPage =
                        (aetherPage + 1) %
                        Mathf.Max(1, (battle.OwnedAethers.Count + 2) / 3);
                }
                else
                {
                    menu = Menu.Root;

                    if (!flow.SelectPlayerAether(aetherPage * 3 + index))
                        menu = Menu.Aethers;
                }
                break;

            case Menu.Info:
                menu = Menu.Root;
                break;
        }

        Refresh();
    }

    // 현재 하위 메뉴를 닫고 기본 메뉴로 돌아간다
    private void Back()
    {
        if (flow.IsBusy || menu == Menu.End)
            return;

        menu = Menu.Root;
        Refresh();
    }

    // 공격 주체에 맞는 간단한 공격 연출을 시작한다
    private void Animate(BattleState actor, BattleState target, SkillData skill)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine =
            StartCoroutine(PlayAttack(actor == battle.PlayerState));
    }

    // 공격자 돌진과 대상 흔들림을 짧게 재생한다
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

            source.anchoredPosition =
                sourceOrigin +
                (targetOrigin - sourceOrigin).normalized *
                (Mathf.Sin(progress * Mathf.PI) * 32f);

            target.anchoredPosition =
                targetOrigin +
                Vector2.right *
                (Mathf.Sin(progress * Mathf.PI * 8f) * 7f * (1f - progress));

            yield return null;
        }

        source.anchoredPosition = sourceOrigin;
        target.anchoredPosition = targetOrigin;
        animationRoutine = null;
    }
}