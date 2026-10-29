using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>하나의 시간축으로 전투 이미지와 UI 이펙트 및 효과음을 재생한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleSkillPresenter : MonoBehaviour
{
    [SerializeField] private RectTransform playerPosition; // 플레이어 측 이동 대상
    [SerializeField] private RectTransform enemyPosition;  // 적 측 이동 대상
    [SerializeField] private RectTransform effectRoot;     // UI 이펙트 생성 영역
    [SerializeField] private AudioSource audioSource;      // 스킬 효과음 전용 출력
    [SerializeField] private SkillPresentationData defaultPresentation; // 전용 연출이 없는 스킬에 사용할 기본 공격
    [SerializeField] private CharacterData playerCharacter; // 단독 씬 실행에 사용할 플레이어의 위치 데이터
    [SerializeField] private CharacterData enemyCharacter;  // 단독 씬 실행에 사용할 상대의 위치 데이터
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.18f; // 실제 피격 후 두 번 깜빡이는 시간(초)
    [SerializeField, Range(0f, 1f)] private float hitFlashAlpha = 0.35f; // 깜빡임 중 원래 투명도에 적용할 배율

    private readonly List<(GameObject instance, float end)> spawned = new(); // 생성 이펙트와 종료 시점
    private Vector2 playerOrigin;                        // 재생 시작 시 플레이어 위치
    private Vector2 enemyOrigin;                         // 재생 시작 시 적 위치
    private bool active;                                // 위치 복원이 필요한 재생 상태
    private int generation;                             // 중단된 재생의 후속 처리를 방지하는 번호
    private Image hitImage;                             // 현재 피격 중인 캐릭터 이미지
    private Color hitColor;                             // 피격 강조 종료 시 복원할 원래 색상

    internal bool Completed { get; private set; }

    // 스킬 연출을 단일 시간축으로 실행하고 효과 적용 결과로 피격 표현을 결정한다.
    internal IEnumerator Play(bool player, SkillPresentationData data, float fallbackDuration, Func<bool> apply,
        CharacterData playerData = null, CharacterData enemyData = null)
    {
        Cancel();
        Completed = false;
        if (data == null)
            data = defaultPresentation;
        if (playerPosition == null || enemyPosition == null || effectRoot == null || audioSource == null)
        {
            Debug.LogError("BattleSkillPresenter: 양측 이미지, 이펙트 영역과 전용 AudioSource를 연결하세요.", this);
            yield break;
        }

        playerData = playerData != null ? playerData : playerCharacter;
        enemyData = enemyData != null ? enemyData : enemyCharacter;
        if (playerData == null || enemyData == null)
        {
            Debug.LogError("BattleSkillPresenter: 양측 CharacterData의 발사 및 피격 지점을 연결하세요.", this);
            yield break;
        }
        int token = generation;
        playerOrigin = playerPosition.anchoredPosition;
        enemyOrigin = enemyPosition.anchoredPosition;
        active = true;
        RectTransform source = player ? playerPosition : enemyPosition;
        RectTransform target = player ? enemyPosition : playerPosition;
        // 캐릭터의 돌진과 피격 흔들림이 공격 경로와 고정 연출을 끌고 가지 않도록 시작 시 확보한다.
        CharacterData sourceData = player ? playerData : enemyData;
        CharacterData targetData = player ? enemyData : playerData;
        Vector3 launch = BattleSkillGeometry.GetPoint(source, sourceData.BattleLaunch);
        Vector3 hit = BattleSkillGeometry.GetPoint(target, targetData.BattleHit);
        hitImage = target.GetComponent<Image>();
        if (hitImage != null)
            hitColor = hitImage.color;
        Vector2 sourceOrigin = player ? playerOrigin : enemyOrigin;
        Vector2 targetOrigin = player ? enemyOrigin : playerOrigin;
        Vector3 direction = (target.position - source.position).normalized;
        Vector2 localDirection = source.parent.InverseTransformVector(direction).normalized;
        float duration = data != null ? data.GetDuration() : Mathf.Max(0.45f, fallbackDuration);
        float movementDuration = data != null ? Mathf.Max(0.001f, duration) : 0.45f;
        float impact = data != null ? data.ImpactTime : duration;
        // 설정된 스킬 길이가 짧거나 기본 연출이어도 피격 강조가 끝나기 전에 복원하지 않는다.
        duration = Mathf.Max(duration, impact + Mathf.Max(0.01f, hitFlashDuration));
        bool[] effectsPlayed = new bool[data != null ? data.Effects.Length : 0];
        RectTransform[] instances = new RectTransform[effectsPlayed.Length];
        Image[] images = new Image[effectsPlayed.Length];
        bool[] soundsPlayed = new bool[data != null ? data.Sounds.Length : 0];
        float elapsed = 0f;
        bool applied = false;
        bool damaged = false;

        while (token == generation && isActiveAndEnabled)
        {
            // 생성, 클립 시각 및 접촉점을 먼저 갱신한 뒤 같은 프레임에 피해를 적용한다.
            bool impactFrame = !applied && elapsed >= impact;
            float poseTime = impactFrame ? impact : elapsed;
            UpdateEffects(data, instances, images, effectsPlayed, player, launch, hit, poseTime, impact, applied, damaged, false);
            // 프레임이 늦어져도 시점을 통과한 효과는 정확히 한 번 적용한다.
            if (!applied && elapsed >= impact)
            {
                applied = true;
                damaged = apply();
                if (token != generation || !isActiveAndEnabled)
                    yield break;
                UpdateEffects(data, instances, images, effectsPlayed, player, launch, hit, poseTime, impact, true, damaged, true);
            }

            float progress = Mathf.Clamp01(elapsed / movementDuration);
            float move = data != null ? (data.Movement != null ? data.Movement.Evaluate(progress) : 0f) * data.MoveDistance :
                (elapsed < 0.45f ? Mathf.Sin(progress * Mathf.PI) * 32f : 0f);
            source.anchoredPosition = sourceOrigin + localDirection * move;

            float shake = 0f;
            if (data != null && damaged && data.ShakeDuration > 0f)
            {
                float shakeProgress = Mathf.Clamp01((elapsed - impact) / data.ShakeDuration);
                shake = Mathf.Sin(shakeProgress * Mathf.PI * 8f) * data.ShakeStrength * (1f - shakeProgress);
            }
            target.anchoredPosition = targetOrigin + Vector2.right * shake;

            if (hitImage != null && damaged)
            {
                float flashProgress = (elapsed - impact) / Mathf.Max(0.01f, hitFlashDuration);
                Color color = hitColor;
                if (flashProgress < 1f && Mathf.FloorToInt(flashProgress * 4f) % 2 == 0)
                    color.a *= hitFlashAlpha;
                hitImage.color = color;
            }

            if (data != null)
            {
                for (int i = 0; i < data.Sounds.Length; i++)
                {
                    SkillPresentationData.Sound sound = data.Sounds[i];
                    if (sound == null || soundsPlayed[i] || elapsed < Mathf.Max(0f, sound.time) ||
                        (sound.onlyOnDamage && !applied))
                        continue;
                    soundsPlayed[i] = true;
                    if (sound.clip != null && (!sound.onlyOnDamage || damaged))
                        audioSource.PlayOneShot(sound.clip, Mathf.Clamp01(sound.volume));
                }
            }

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (poseTime < spawned[i].end)
                    continue;
                if (spawned[i].instance != null)
                {
                    spawned[i].instance.SetActive(false);
                    Destroy(spawned[i].instance);
                }
                spawned.RemoveAt(i);
            }

            if (elapsed >= duration && !impactFrame)
                break;
            yield return null;
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
        }

        if (token == generation && isActiveAndEnabled)
        {
            Restore();
            Completed = true;
        }
    }

    // 피해 조건에 따라 한 번 생성하고 캐시한 이미지 참조로 공통 시간축의 프레임과 접촉점을 갱신한다.
    private void UpdateEffects(SkillPresentationData data, RectTransform[] instances, Image[] images, bool[] played,
        bool player, Vector3 launch, Vector3 hit, float elapsed, float impact, bool applied, bool damaged, bool damageOnly)
    {
        if (data == null)
            return;
        for (int i = 0; i < data.Effects.Length; i++)
        {
            SkillPresentationData.Effect effect = data.Effects[i];
            if (effect == null || (damageOnly && (!effect.onlyOnDamage || effect.Moves)) ||
                elapsed < effect.GetStart(impact) || (effect.onlyOnDamage && !effect.Moves && !applied))
                continue;
            if (!played[i])
            {
                played[i] = true;
                if (effect.prefab == null || (effect.onlyOnDamage && !effect.Moves && !damaged))
                    continue;
                instances[i] = Instantiate(effect.prefab, effectRoot, false);
                images[i] = instances[i].GetComponent<Image>();
                if (effect.animation != null)
                {
                    // Animator의 별도 시간 진행이 피해 프레임을 앞서거나 뒤따르지 않도록 한다.
                    Animator animator = instances[i].GetComponent<Animator>();
                    if (animator != null)
                        animator.enabled = false;
                }
                spawned.Add((instances[i].gameObject, effect.GetEnd(impact)));
            }
            if (instances[i] != null)
                BattleSkillGeometry.Sample(instances[i], images[i], effect, launch, hit, player, elapsed, impact);
        }
    }

    // 재시작과 비활성화 시 예약 재생을 취소하고 소리와 생성물을 정리한다.
    internal void Cancel()
    {
        generation++;
        Completed = false;
        Restore();
        if (audioSource != null)
            audioSource.Stop();
    }

    // 연출이 변경한 이미지 위치와 생성 이펙트를 원래 상태로 되돌린다.
    private void Restore()
    {
        if (hitImage != null)
            hitImage.color = hitColor;
        hitImage = null;
        if (active)
        {
            if (playerPosition != null)
                playerPosition.anchoredPosition = playerOrigin;
            if (enemyPosition != null)
                enemyPosition.anchoredPosition = enemyOrigin;
        }
        active = false;
        foreach (var effect in spawned)
            if (effect.instance != null)
            {
                effect.instance.SetActive(false);
                Destroy(effect.instance);
            }
        spawned.Clear();
    }

    private void OnDisable()
    {
        Cancel();
    }
}
