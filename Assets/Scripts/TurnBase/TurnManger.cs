using UnityEngine;

// Kept so the existing scene does not lose its serialized component. The previous
// prototype directly changed ScriptableObject HP and PP and must not run with the
// runtime battle system.
public class TurnManager : MonoBehaviour
{
    [Header("Characters")]
    public CharacterData player;
    public CharacterData enemy;

    [Header("Managers")]
    public BattleUIManager uiManager;

    private void OnEnable()
    {
        if (uiManager != null)
            uiManager.OnSkillButtonClicked += HandleSkillButtonClicked;
    }

    private void OnDisable()
    {
        if (uiManager != null)
            uiManager.OnSkillButtonClicked -= HandleSkillButtonClicked;
    }

    private void HandleSkillButtonClicked(int skillIndex)
    {
        Debug.LogWarning("TurnManager is a legacy scene adapter. Route selection to BattleController instead.");
    }
}
