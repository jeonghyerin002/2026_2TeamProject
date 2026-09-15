using System;
using System.Collections.Generic;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIManager : MonoBehaviour
{
    [System.Serializable]
    public struct SkillButtonGroup
    {
        public Button button;
    }

    [Header("Panels")]
    public GameObject attackPanel;

    [Header("Attack Buttons")]
    public List<SkillButtonGroup> skillButtons = new List<SkillButtonGroup>();

    //버튼 클릭 시 TurnManager로 인덱스를 전달할 이벤트
    public event Action<int> OnSkillButtonClicked;

    void Awake()
    {
        SetupButtonEvents();
    }

    void SetupButtonEvents()
    {
        for (int i = 0; i < skillButtons.Count; i++)
        {
            int index = i;
            if (skillButtons[i].button != null)
            {
                skillButtons[i].button.onClick.RemoveAllListeners();
                skillButtons[i].button.onClick.AddListener(() => OnSkillButtonClicked?.Invoke(index));
            }
        }
    }

    public void ToggleAttackPanel(bool isActive)
    {
        if (attackPanel !=  null)
        {
            attackPanel.SetActive(isActive);
        }
    }

    public void RefreshSkillButtons (List<AttackData> attackList)
    {
        for (int i = 0; i < skillButtons.Count; i++)
        {
            var buttonGroup = skillButtons[i];
            if (buttonGroup.button == null) continue;

            //플레이어 스킬 목록 범위 내
            if (i < attackList.Count && attackList[i] != null)
            {
                AttackData attack = attackList[i];
                buttonGroup.button.gameObject.SetActive(true);

                //남은 횟수가 있을 때만 버튼 활성화
                buttonGroup.button.interactable = (attack.currentCount > 0);

                //버튼 하위의 TextMeshProUGUI 컴포넌트를 코드로 자동으로 가져와 텍스트 변경
                TextMeshProUGUI buttonText = buttonGroup.button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = $"{attack.attackName}\n({attack.currentCount}/{attack.fullCount})";
                }
            }
            else
            {
                buttonGroup.button.gameObject.SetActive(false);
            }
        }
    }
    void Start()
    {
        
    }


    void Update()
    {
        
    }
}
