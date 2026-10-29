using System;
using UnityEngine;

/// <summary>스킬의 이미지 움직임과 시간별 UI 이펙트 및 효과음 설정을 공유한다.</summary>
[CreateAssetMenu(menuName = "Battle/Skill Presentation")]
public sealed class SkillPresentationData : ScriptableObject
{
    public enum Position { Actor, Target, Center }
    public enum Playback { Legacy, Travel, AtTarget, AtActor, AtCenter }

    /// <summary>클립의 시각별로 이미지 내부의 접촉 위치를 지정한다.</summary>
    [Serializable]
    public struct ContactFrame
    {
        public float time;    // 대응하는 클립 내부 시각(초)
        public Vector2 point; // 스프라이트 표시 영역에 대한 접촉점
    }

    /// <summary>UI 이펙트의 생성 위치와 재생 조건을 지정한다.</summary>
    [Serializable]
    public sealed class Effect
    {
        public RectTransform prefab;                         // Canvas 아래에 생성할 UI 이펙트
        [Min(0f)] public float time;                          // 연출 시작 기준 재생 시점(초)
        public Position position = Position.Target;          // 생성 순간의 기준 위치
        public Vector2 offset;                              // 이펙트 영역의 로컬 좌표 보정
        [Min(0.01f)] public float lifetime = 0.3f;            // 생성 후 유지 시간(초)
        public bool onlyOnDamage;                           // 실제 피해가 발생한 경우에만 재생
        [HideInInspector] public bool travelToTarget;        // 이전 이동 설정의 직렬화 호환용 값
        public Playback playback;                            // 이동 또는 고정 위치 재생 방식
        public AnimationClip animation;                      // 공통 시간축으로 표본화할 UI 이미지 클립, 없으면 기존 Animator 사용
        [Min(0f)] public float animationImpactTime = 0.125f; // 도착할 때 보여 줄 클립 내부의 타격 시점(초)
        public Vector2 contactPoint = new(0.5f, 0.5f);       // 표시 이미지 영역 안에서 상대와 접촉할 기준점

        public ContactFrame[] contactFrames = Array.Empty<ContactFrame>(); // 내부 이동이 있는 클립의 시각별 기준점, 비어 있으면 고정 기준점 사용

        [Min(0.01f)] public float sizeScale = 1f;            // 프리팹 크기에 적용할 연출 배율
        public bool rotateToDirection = true;                // 공격 경로에 맞춘 이미지 회전 여부
        public bool mirrorForEnemy = true;                   // 적 공격에 적용할 좌우반전 여부

        public bool Moves => playback == Playback.Travel || (playback == Playback.Legacy && travelToTarget);

        // 피해 전에 표시해야 하는 이동과 실제 피해에 종속된 고정 연출을 구분한다.
        public float GetStart(float impact) => Mathf.Max(onlyOnDamage && !Moves ? impact : 0f, time);

        // 도착 이후에도 남은 애니메이션 프레임을 재생할 수 있는 종료 시점을 반환한다.
        public float GetEnd(float impact) => animation != null ?
            Mathf.Max(impact, GetStart(impact)) + Mathf.Max(0.01f, animation.length - Mathf.Clamp(animationImpactTime, 0f, animation.length)) :
            Mathf.Max(GetStart(impact) + Mathf.Max(0.01f, lifetime), Moves ? impact + 0.01f : 0f);
    }

    /// <summary>효과음의 재생 시점과 음량 및 피해 조건을 지정한다.</summary>
    [Serializable]
    public sealed class Sound
    {
        public AudioClip clip;                              // 재생할 효과음
        [Min(0f)] public float time;                         // 연출 시작 기준 재생 시점(초)
        [Range(0f, 1f)] public float volume = 1f;             // 효과음별 음량 배율
        public bool onlyOnDamage;                           // 실제 피해가 발생한 경우에만 재생
    }

    [SerializeField, Min(0f)] private float duration = 0.6f;       // 연출 전체 길이(초)
    [SerializeField, Min(0f)] private float impactTime = 0.2f;     // 전투 효과 적용 시점(초)
    [SerializeField] private float moveDistance = 32f;           // 대상을 향한 이동 거리(UI 단위)
    [SerializeField] private AnimationCurve movement = new(     // 전체 진행 비율에 따른 이동 배율
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
    [SerializeField, Min(0f)] private float shakeDuration = 0.2f;  // 피해 적용 후 대상 흔들림 시간(초)
    [SerializeField, Min(0f)] private float shakeStrength = 7f;    // 대상 흔들림 크기(UI 단위)
    [SerializeField] private Effect[] effects = Array.Empty<Effect>(); // 시간별 이펙트 목록
    [SerializeField] private Sound[] sounds = Array.Empty<Sound>();    // 시간별 효과음 목록

    public float ImpactTime
    {
        get
        {
            float impact = Mathf.Max(0f, impactTime);
            foreach (Effect effect in Effects)
                if (effect != null && effect.Moves)
                    impact = Mathf.Max(impact, Mathf.Max(0f, effect.time) + 0.01f);
            return impact;
        }
    }
    internal float MoveDistance => moveDistance;
    internal AnimationCurve Movement => movement;
    internal float ShakeDuration => Mathf.Max(0f, shakeDuration);
    internal float ShakeStrength => Mathf.Max(0f, shakeStrength);
    public Effect[] Effects => effects ?? Array.Empty<Effect>();
    internal Sound[] Sounds => sounds ?? Array.Empty<Sound>();

    // 효과 적용과 생성 이펙트 정리까지 포함하는 안전한 종료 시점을 계산한다.
    public float GetDuration()
    {
        float end = Mathf.Max(duration, ImpactTime + ShakeDuration);
        foreach (Effect effect in Effects)
            if (effect != null)
                end = Mathf.Max(end, effect.GetEnd(ImpactTime));
        foreach (Sound sound in Sounds)
            if (sound != null)
                end = Mathf.Max(end, Mathf.Max(0f, sound.time));
        return end;
    }

    private void OnValidate()
    {
        duration = GetDuration();
        foreach (Effect effect in Effects)
            if (effect != null)
            {
                effect.sizeScale = Mathf.Max(0.01f, effect.sizeScale);
                if (effect.animation != null)
                    effect.animationImpactTime = Mathf.Clamp(effect.animationImpactTime, 0f, Mathf.Max(0f, effect.animation.length - 0.001f));
                if (effect.contactFrames != null)
                    Array.Sort(effect.contactFrames, (a, b) => a.time.CompareTo(b.time));
                if (effect.Moves)
                    effect.time = Mathf.Clamp(effect.time, 0f, Mathf.Max(0f, ImpactTime - 0.01f));
                else if (effect.onlyOnDamage)
                    effect.time = Mathf.Max(ImpactTime, effect.time);
            }
        foreach (Sound sound in Sounds)
            if (sound != null && sound.onlyOnDamage)
                sound.time = Mathf.Max(ImpactTime, sound.time);
        duration = GetDuration();
    }
}
