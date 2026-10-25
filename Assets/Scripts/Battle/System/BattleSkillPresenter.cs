using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>하나의 시간축으로 전투 이미지와 UI 이펙트 및 효과음을 재생한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleSkillPresenter : MonoBehaviour
{
    [SerializeField] private RectTransform playerPosition; // 플레이어 측 이동 대상
    [SerializeField] private RectTransform enemyPosition;  // 적 측 이동 대상
    [SerializeField] private RectTransform effectRoot;     // UI 이펙트 생성 영역
    [SerializeField] private AudioSource audioSource;      // 스킬 효과음 전용 출력
    [SerializeField, Min(0f)] private float targetOffsetX = 20f; // 대상별 안쪽 방향의 이펙트 보정 거리(UI 단위)

    private readonly List<(GameObject instance, float end)> spawned = new(); // 생성 이펙트와 종료 시점
    private Vector2 playerOrigin;                        // 재생 시작 시 플레이어 위치
    private Vector2 enemyOrigin;                         // 재생 시작 시 적 위치
    private bool active;                                // 위치 복원이 필요한 재생 상태
    private int generation;                             // 중단된 재생의 후속 처리를 방지하는 번호

    internal bool Completed { get; private set; }

    // 스킬 연출을 단일 시간축으로 실행하고 효과 적용 결과로 피격 표현을 결정한다.
    internal IEnumerator Play(bool player, SkillPresentationData data, float fallbackDuration, Func<bool> apply)
    {
        Cancel();
        Completed = false;
        if (playerPosition == null || enemyPosition == null || effectRoot == null || audioSource == null)
        {
            Debug.LogError("BattleSkillPresenter: 양측 이미지, 이펙트 영역과 전용 AudioSource를 연결하세요.", this);
            yield break;
        }

        int token = generation;
        playerOrigin = playerPosition.anchoredPosition;
        enemyOrigin = enemyPosition.anchoredPosition;
        active = true;
        RectTransform source = player ? playerPosition : enemyPosition;
        RectTransform target = player ? enemyPosition : playerPosition;
        Vector2 sourceOrigin = player ? playerOrigin : enemyOrigin;
        Vector2 targetOrigin = player ? enemyOrigin : playerOrigin;
        Vector3 direction = (target.position - source.position).normalized;
        Vector2 localDirection = source.parent.InverseTransformVector(direction).normalized;
        float duration = data != null ? data.GetDuration() : Mathf.Max(0.45f, fallbackDuration);
        float impact = data != null ? data.ImpactTime : duration;
        bool[] effectsPlayed = new bool[data != null ? data.Effects.Length : 0];
        bool[] soundsPlayed = new bool[data != null ? data.Sounds.Length : 0];
        float elapsed = 0f;
        bool applied = false;
        bool damaged = false;

        while (token == generation && isActiveAndEnabled)
        {
            // 프레임이 늦어져도 시점을 통과한 효과는 정확히 한 번 적용한다.
            if (!applied && elapsed >= impact)
            {
                applied = true;
                damaged = apply();
                if (token != generation || !isActiveAndEnabled)
                    yield break;
            }

            float progress = Mathf.Clamp01(elapsed / (data != null ? Mathf.Max(0.001f, duration) : 0.45f));
            float move = data != null ? (data.Movement != null ? data.Movement.Evaluate(progress) : 0f) * data.MoveDistance :
                (elapsed < 0.45f ? Mathf.Sin(progress * Mathf.PI) * 32f : 0f);
            source.anchoredPosition = sourceOrigin + localDirection * move;

            float shake = 0f;
            if (data == null && elapsed < 0.45f)
                shake = Mathf.Sin(progress * Mathf.PI * 8f) * 7f * (1f - progress);
            else if (data != null && damaged && data.ShakeDuration > 0f)
            {
                float shakeProgress = Mathf.Clamp01((elapsed - impact) / data.ShakeDuration);
                shake = Mathf.Sin(shakeProgress * Mathf.PI * 8f) * data.ShakeStrength * (1f - shakeProgress);
            }
            target.anchoredPosition = targetOrigin + Vector2.right * shake;

            if (data != null)
            {
                for (int i = 0; i < data.Effects.Length; i++)
                {
                    SkillPresentationData.Effect effect = data.Effects[i];
                    if (effect == null || effectsPlayed[i] || elapsed < Mathf.Max(0f, effect.time) ||
                        (effect.onlyOnDamage && !applied))
                        continue;
                    effectsPlayed[i] = true;
                    if (effect.prefab != null && (!effect.onlyOnDamage || damaged))
                    {
                        RectTransform instance = Instantiate(effect.prefab, effectRoot, false);
                        Vector3 position = effect.position == SkillPresentationData.Position.Actor ? source.position :
                            effect.position == SkillPresentationData.Position.Target ? target.position : (source.position + target.position) * 0.5f;
                        Vector2 offset = effect.offset;
                        // 동일한 연출을 양측에서 공유하되 대상의 몸 안쪽으로 생성 위치를 보정한다.
                        if (effect.position == SkillPresentationData.Position.Target)
                        {
                            // 플레이어 대상은 타겟 영역의 가로 중심을 기준으로 시작점과 방향을 함께 뒤집는다.
                            if (target == playerPosition)
                            {
                                position = target.TransformPoint(new Vector3(target.rect.center.x * 2f, 0f, 0f));
                                offset.x = -offset.x;
                                Vector3 scale = instance.localScale;
                                scale.x = -scale.x;
                                instance.localScale = scale;
                            }
                            offset.x += target == playerPosition ? targetOffsetX : -targetOffsetX;
                        }
                        instance.position = position + effectRoot.TransformVector(offset);
                        float start = Mathf.Max(effect.onlyOnDamage ? impact : 0f, effect.time);
                        spawned.Add((instance.gameObject, start + Mathf.Max(0.01f, effect.lifetime)));
                    }
                }
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
                if (elapsed < spawned[i].end)
                    continue;
                if (spawned[i].instance != null)
                {
                    spawned[i].instance.SetActive(false);
                    Destroy(spawned[i].instance);
                }
                spawned.RemoveAt(i);
            }

            if (elapsed >= duration)
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
