using System;
using System.Collections.Generic;

namespace UnityGameData
{
    // Tracks SkillEffect.Duration independently from status-condition persistence.
    public sealed class TimedSkillEffectState
    {
        private readonly Dictionary<int, int> remainingTurns = new Dictionary<int, int>();

        public void Apply(SkillEffectData effect)
        {
            if (effect == null) throw new ArgumentNullException("effect");
            remainingTurns[effect.EffectId] = effect.Duration;
        }

        public bool Contains(int effectId) { return remainingTurns.ContainsKey(effectId); }

        // Call at the end of a completed turn. Duration 1 covers the current turn;
        // Duration 0 is kept until battle end.
        public void OnTurnEnd()
        {
            var expired = new List<int>();
            var updated = new Dictionary<int, int>();
            foreach (var pair in remainingTurns)
            {
                if (pair.Value == 0) continue;
                int next = pair.Value - 1;
                if (next <= 0) expired.Add(pair.Key);
                else updated.Add(pair.Key, next);
            }
            foreach (int effectId in expired) remainingTurns.Remove(effectId);
            foreach (var pair in updated) remainingTurns[pair.Key] = pair.Value;
        }

        public void OnBattleEnd()
        {
            remainingTurns.Clear();
        }
    }
}
