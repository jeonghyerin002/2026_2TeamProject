using System;
using System.Collections.Generic;

namespace UnityGameData
{
    public enum StatusApplyResult { Applied, AlreadyPresent, BlockedByPrimaryStatus, ReplacedPoisonWithToxic, SleptAndClearedAll }
    public enum PreActionStatusResult { CanAct, CannotAct, RecoveredAndCanAct, ConfusionSelfDamageAndCannotAct }

    // Owns condition coexistence, condition-local counters, and battle-end cleanup.
    // It does not decide the global order of Accuracy, effects, residual damage, or PP.
    public sealed class StatusEffectState
    {
        private const string Paralysis = "paralysis";
        private const string Poison = "poison";
        private const string Burn = "burn";
        private const string Frostbite = "frostbite";
        private const string Toxic = "toxic";
        private const string Confusion = "confusion";
        private const string Sleep = "sleep";

        private readonly StatusEffectManager definitions;
        private readonly HashSet<int> active = new HashSet<int>();
        private int toxicTurn;
        private int sleepMissedActions;

        public StatusEffectState(StatusEffectManager definitions)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            this.definitions = definitions;
        }

        public bool Contains(int statusEffectId) { return active.Contains(statusEffectId); }
        public bool HasParalysis { get { return ContainsKey(Paralysis); } }
        public bool HasBurn { get { return ContainsKey(Burn); } }
        public bool HasFrostbite { get { return ContainsKey(Frostbite); } }

        public StatusApplyResult TryApply(int statusEffectId, bool isGoToSleep = false)
        {
            StatusEffectData incoming = definitions.GetById(statusEffectId);
            if (isGoToSleep)
            {
                active.Clear();
                active.Add(statusEffectId);
                toxicTurn = 0;
                sleepMissedActions = 0;
                return StatusApplyResult.SleptAndClearedAll;
            }

            if (active.Contains(statusEffectId)) return StatusApplyResult.AlreadyPresent;
            if (incoming.StatusEffectKey == Confusion)
            {
                active.Add(statusEffectId);
                return StatusApplyResult.Applied;
            }

            int poisonId;
            if (incoming.StatusEffectKey == Toxic && TryGetId(Poison, out poisonId) && active.Remove(poisonId))
            {
                active.Add(statusEffectId);
                toxicTurn = 0;
                return StatusApplyResult.ReplacedPoisonWithToxic;
            }

            if (HasAnyPrimaryStatus()) return StatusApplyResult.BlockedByPrimaryStatus;
            active.Add(statusEffectId);
            if (incoming.StatusEffectKey == Toxic) toxicTurn = 0;
            return StatusApplyResult.Applied;
        }

        // Legacy convenience method kept for callers that only need validation.
        public void Apply(int statusEffectId) { TryApply(statusEffectId); }

        public PreActionStatusResult ResolveParalysis(float roll0To100)
        {
            return HasParalysis && roll0To100 < 25f ? PreActionStatusResult.CannotAct : PreActionStatusResult.CanAct;
        }

        public PreActionStatusResult ResolveConfusion(float roll0To100)
        {
            int id;
            if (!TryGetId(Confusion, out id) || !active.Contains(id)) return PreActionStatusResult.CanAct;
            if (roll0To100 < 30f)
            {
                active.Remove(id);
                return PreActionStatusResult.RecoveredAndCanAct;
            }
            return roll0To100 < 60f ? PreActionStatusResult.ConfusionSelfDamageAndCannotAct : PreActionStatusResult.CanAct;
        }

        public PreActionStatusResult ResolveSleep(float roll0To100)
        {
            int id;
            if (!TryGetId(Sleep, out id) || !active.Contains(id)) return PreActionStatusResult.CanAct;
            if (sleepMissedActions == 0)
            {
                sleepMissedActions++;
                return PreActionStatusResult.CannotAct;
            }
            if (sleepMissedActions == 1 && roll0To100 >= 50f)
            {
                sleepMissedActions++;
                return PreActionStatusResult.CannotAct;
            }
            active.Remove(id);
            sleepMissedActions = 0;
            return PreActionStatusResult.RecoveredAndCanAct;
        }

        // Returned damage is based on maximum HP. The battle coordinator chooses
        // when it is applied because faint ordering remains unspecified.
        public float GetAndAdvanceEndTurnDamage(int maximumHealth)
        {
            if (maximumHealth <= 0) return 0f;
            if (ContainsKey(Poison)) return maximumHealth / 8f;
            if (ContainsKey(Burn) || ContainsKey(Frostbite)) return maximumHealth / 16f;
            if (ContainsKey(Toxic))
            {
                toxicTurn++;
                return maximumHealth * toxicTurn / 16f;
            }
            return 0f;
        }

        public void OnBattleEnd()
        {
            RemoveByKey(Confusion);
            RemoveByKey(Sleep);
            toxicTurn = 0;
            sleepMissedActions = 0;
        }

        public bool ApplyTreatment(int statusEffectId) { return active.Remove(statusEffectId); }
        public void CurePoisonAndToxic() { RemoveByKey(Poison); RemoveByKey(Toxic); toxicTurn = 0; }
        public void CureAll() { active.Clear(); toxicTurn = 0; sleepMissedActions = 0; }

        private bool HasAnyPrimaryStatus()
        {
            return ContainsKey(Paralysis) || ContainsKey(Poison) || ContainsKey(Burn) ||
                ContainsKey(Frostbite) || ContainsKey(Toxic) || ContainsKey(Sleep);
        }

        private bool ContainsKey(string key)
        {
            int id;
            return TryGetId(key, out id) && active.Contains(id);
        }

        private void RemoveByKey(string key)
        {
            int id;
            if (TryGetId(key, out id)) active.Remove(id);
        }

        private bool TryGetId(string key, out int id)
        {
            foreach (StatusEffectData definition in definitions.All)
                if (definition.StatusEffectKey == key)
                {
                    id = definition.StatusEffectId;
                    return true;
                }
            id = 0;
            return false;
        }
    }
}
