using UnityEngine;

namespace Game.Data
{
    /// <summary>Loads skills before items, then checks every item-to-skill foreign key.</summary>
    public sealed class GameDataBootstrap : MonoBehaviour
    {
        [SerializeField] private SkillManager skillManager;
        [SerializeField] private ItemManager itemManager;
        [SerializeField] private TextAsset skillCsv;
        [SerializeField] private TextAsset itemCsv;

        private void Awake()
        {
            if (skillManager == null || itemManager == null) { Debug.LogError("[GameDataBootstrap] Assign both managers."); return; }
            var skillsOk = skillManager.Load(skillCsv);
            var itemsOk = itemManager.Load(itemCsv);
            var referencesOk = itemManager.ValidateSkillReferences(skillManager);
            Debug.Log("[GameDataBootstrap] Load complete. Valid=" + (skillsOk && itemsOk && referencesOk));
        }
    }
}
