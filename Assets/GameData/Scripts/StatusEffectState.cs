using System;
using System.Collections.Generic;

namespace UnityGameData
{
    // 전투 밖에서도 캐릭터가 같은 객체를 보유해야 상태가 유지됩니다.
    // 상태의 존재만 기록하며 중첩 수치, 효과 타이머, 세이브 파일은 처리하지 않습니다.
    public sealed class StatusEffectState
    {
        private readonly StatusEffectManager definitions;
        private readonly HashSet<int> active = new HashSet<int>();
        public StatusEffectState(StatusEffectManager definitions)
        {
            if (definitions==null) throw new ArgumentNullException("definitions");
            this.definitions=definitions;
        }
        public bool Contains(int statusEffectId) { return active.Contains(statusEffectId); }
        public void Apply(int statusEffectId)
        {
            definitions.GetById(statusEffectId);
            active.Add(statusEffectId);
        }
        public void OnBattleEnd()
        {
            // 기획서 변경 16: 현재 사용자가 지정한 유지 예외는 마비 70001뿐입니다.
            // ID 범위로 종류를 추론하지 않으며, 신규 예외는 사용자 지정 후에만 추가합니다.
            active.RemoveWhere(id => id != 70001);
        }
        // 유효한 별도 치료행위가 완료된 경우에만 게임 코드가 호출합니다.
        public bool ApplyTreatment(int statusEffectId) { return active.Remove(statusEffectId); }
    }
}
