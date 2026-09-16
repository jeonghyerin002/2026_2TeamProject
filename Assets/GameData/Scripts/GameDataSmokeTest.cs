using UnityEngine;

namespace Game.Data
{
    public sealed class GameDataSmokeTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart) Run();
        }

        [ContextMenu("Run Game Data Smoke Test")]
        public void Run()
        {
            var items = ItemManager.Instance;
            var skills = SkillManager.Instance;
            if (items == null || skills == null) { Debug.LogError("[GameDataTest] Managers are not initialized."); return; }
            PrintItem(items.GetItemById(10001));
            var sword = items.GetItemByKey("weapon_training_sword");
            PrintItem(sword);
            if (sword != null) Debug.Log("[GameDataTest] Training sword AttackMultiplier=" + sword.AttackMultiplier);
            PrintAether(items.GetItemById(40001), skills);
            PrintAether(items.GetItemById(40002), skills);
            PrintAether(items.GetItemById(40003), skills);
        }

        private static void PrintItem(ItemData item)
        {
            if (item == null) { Debug.LogError("[GameDataTest] Item lookup failed."); return; }
            Debug.Log("[GameDataTest] Item: " + item.Name + " / ID=" + item.ItemId + " / Key=" + item.ItemKey);
        }

        private static void PrintAether(ItemData item, SkillManager skills)
        {
            if (item == null) { Debug.LogError("[GameDataTest] Aether lookup failed."); return; }
            var skill = skills.GetSkillById(item.SkillId1);
            if (skill == null) { Debug.LogError("[GameDataTest] Missing linked skill for " + item.Name); return; }
            Debug.Log("[GameDataTest] " + item.Name + " | ID: " + item.ItemId + " | Element: " + item.AetherType + " | Linked skill: " + skill.SkillName + " | Attack type: " + skill.AttackType + " | Damage: " + skill.Damage + " | PP: " + skill.PP + " | Accuracy: " + skill.Accuracy);
        }
    }
}
