using System;
using UnityEngine;

namespace UnityGameData
{
    public sealed class GameDataSmokeTest : MonoBehaviour
    {
        private void Start() { RunSmokeTest(); }
        [ContextMenu("Run Smoke Test")]
        public void RunSmokeTest()
        {
            try
            {
                var data=GameDataCatalog.Load(Read("ItemManager"),Read("SkillManager"),
                    Read("SkillEffectManager"),Read("StatusEffectManager"));
                foreach (string error in data.Errors) Debug.LogError(error);
                foreach (string warning in data.Warnings) Debug.LogWarning(warning);
                Require(data.Errors.Count==0,"CSV errors reported; valid rows remain accessible");
                Require(data.Items.Count==9 && data.Skills.Count==3 && data.SkillEffects.Count==1 && data.StatusEffects.Count==1,"Expected baseline row counts");
                var effect=data.SkillEffects.GetById(60001);
                var status=data.StatusEffects.GetById(effect.StatusEffectId);
                Require(effect.StatusEffectId==70001 && status.StatusEffectKey=="paralysis","Effect to status reference");
                Require(effect.Duration==0 && effect.Chance==30,"Effect settings");
                Require(data.Items.GetById(40001).SkillId1==50001 && data.Items.GetById(40002).SkillId1==50002 && data.Items.GetById(40003).SkillId1==50003,"Aether skill references");
                foreach (int skillId in new [] {50001,50002,50003})
                    Require(data.Skills.GetById(skillId).EffectId==0,"Original skill remains unlinked");
                var states=new StatusEffectState(data.StatusEffects);
                states.Apply(70001); states.OnBattleEnd();
                Require(states.Contains(70001),"Paralysis remains after battle");
                states.ApplyTreatment(70001);
                Require(!states.Contains(70001),"Paralysis removed by treatment");
                Debug.Log("GameData structural smoke test passed. Review warnings; combat behavior is not verified.");
            }
            catch (Exception ex) { Debug.LogError("GameData smoke test failed: " + ex); }
        }
        private static string Read(string name)
        {
            var asset=Resources.Load<TextAsset>("GameData/" + name);
            if (asset==null) throw new InvalidOperationException("Missing CSV: " + name);
            return asset.text;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
