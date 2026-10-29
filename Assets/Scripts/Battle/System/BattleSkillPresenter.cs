using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>하나의 시간축으로 전투 이미지와 UI 이펙트 및 효과음을 재생한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleSkillPresenter : MonoBehaviour
{
    [SerializeField] private RectTransform playerPosition; // 플레이어 측 이동 대상
    [SerializeField] private RectTransform enemyPosition;  // 적 측 이동 대상
    [SerializeField] private RectTransform effectRoot;     // 배경·양측 이미지·생성 이펙트를 함께 흔드는 전투 표시 영역
    [SerializeField] private AudioSource audioSource;      // 스킬 효과음 전용 출력
    [SerializeField] private SkillPresentationData defaultPresentation; // 전용 연출이 없는 스킬에 사용할 기본 공격
    [SerializeField] private CharacterData playerCharacter; // 단독 씬 실행에 사용할 플레이어의 위치 데이터
    [SerializeField] private CharacterData enemyCharacter;  // 단독 씬 실행에 사용할 상대의 위치 데이터
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.18f; // 실제 피격 후 두 번 깜빡이는 시간(초)
    [SerializeField, Range(0f, 1f)] private float hitFlashAlpha = 0.35f; // 깜빡임 중 원래 투명도에 적용할 배율

    [Header("피격 화면 흔들림")]
    [SerializeField, Min(0f)] private float screenShakeStrength = 36f; // 플레이어 피격 기준의 상하 화면 흔들림 최대 거리(UI 단위), NPC 피격은 이 값의 1/1.5 사용
    [SerializeField, Min(0.01f)] private float screenShakeDuration = 0.32f; // 양측 공통으로 실제 피격 후 화면 흔들림 시간(초)

    [Header("피해 숫자")]
    [SerializeField] private TMP_FontAsset damageFont; // 기존 전투 UI와 같은 글꼴을 사용하는 피해 표시
    [SerializeField, Min(1f)] private float damageFontSize = 42f; // Canvas 기준 피해 숫자의 글자 크기
    [SerializeField, Min(0.01f)] private float damageDuration = 0.85f; // 피해 숫자가 곡선을 따라 이동하고 사라지는 전체 시간(초)
    [SerializeField, Min(0f)] private float damageRise = 100f; // 피격 지점에서 최고점까지 올라갈 높이(UI 단위)
    [SerializeField, Min(0f)] private float damageTravel = 180f; // 피격 지점에서 바깥쪽으로 이동할 전체 거리(UI 단위)
    [SerializeField, Min(0f)] private float damageDrop = 20f; // 종료 시 피격 지점보다 아래로 내려갈 거리(UI 단위)
    [SerializeField, Range(0.1f, 0.9f)] private float damagePeakTime = 0.65f; // 전체 시간 중 최고점에 도달하는 비율, 클수록 낙하가 짧고 빠름
    [SerializeField, Range(0f, 180f)] private float damageRotation = 75f; // 이동 방향으로 기울어지는 최종 각도(도), 0이면 회전하지 않음

    private readonly List<(TMP_Text text, Vector2 origin, float direction, float elapsed)> damageNumbers = new(); // 발생 위치와 좌우 방향을 유지하며 독립적으로 이동하는 피해 숫자

    private readonly List<(GameObject instance, float end)> spawned = new(); // 생성 이펙트와 종료 시점
    private Vector2 playerOrigin;                        // 재생 시작 시 플레이어 위치
    private Vector2 enemyOrigin;                         // 재생 시작 시 적 위치
    private bool active;                                // 위치 복원이 필요한 재생 상태
    private int generation;                             // 중단된 재생의 후속 처리를 방지하는 번호
    private Image hitImage;                             // 현재 피격 중인 캐릭터 이미지
    private Color hitColor;                             // 피격 강조 종료 시 복원할 원래 색상
    private Vector2 effectOrigin;                       // 화면 흔들림 종료 시 복원할 전투 표시 영역의 위치
    private float screenShakeElapsed;                   // 피격 후 화면 흔들림 경과 시간(초)
    private float screenHitStrength;                    // 이번 피격 대상에 맞춰 확정한 화면 흔들림 강도(UI 단위)
    private bool screenShaking;                         // 양측 공통으로 실제 피해가 발생한 동안만 화면 흔들림을 실행하는 상태

    internal bool Completed { get; private set; }

    // 스킬 연출을 단일 시간축으로 실행하고 효과 적용 결과로 피격 표현을 결정한다.
    internal IEnumerator Play(bool player, SkillPresentationData data, float fallbackDuration, Func<BattleResolveResult> apply,
        CharacterData playerData = null, CharacterData enemyData = null, ElementType skillType = ElementType.Normal)
    {
        Cancel(false);
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
        effectOrigin = effectRoot.anchoredPosition;
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
                BattleResolveResult result = apply();
                damaged = result.Damage > 0;
                if (token != generation || !isActiveAndEnabled)
                    yield break;
                UpdateEffects(data, instances, images, effectsPlayed, player, launch, hit, poseTime, impact, true, damaged, true);
                if (damaged)
                {
                    ShowDamage(result, skillType, hit, player ? 1f : -1f);
                    screenShaking = true;
                    screenShakeElapsed = 0f;
                    // player는 공격 주체이므로 플레이어 공격으로 NPC가 맞을 때만 강도를 낮춘다.
                    screenHitStrength = screenShakeStrength / (player ? 1.5f : 1f);
                    duration = Mathf.Max(duration, impact + Mathf.Max(0.01f, screenShakeDuration));
                }
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

    // 스킬과 같은 피격 좌표에 실제 감소한 HP를 표시하고 대상의 바깥쪽 이동 방향을 저장한다.
    private void ShowDamage(BattleResolveResult result, ElementType skillType, Vector3 hit, float direction)
    {
        TMP_FontAsset font = damageFont != null ? damageFont : TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogError("BattleSkillPresenter: 피해 숫자를 표시할 Damage Font를 연결하세요.", this);
            return;
        }

        TMP_Text label = new GameObject("Damage Number", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.gameObject.layer = effectRoot.gameObject.layer;
        RectTransform rect = label.rectTransform;
        rect.SetParent(effectRoot, false);
        rect.sizeDelta = new Vector2(240f, 90f);
        rect.position = hit;
        rect.localRotation = Quaternion.identity;
        label.font = font;
        var style = GetDamageStyle(result);
        label.fontSize = style.size;
        label.fontStyle = style.weight;
        label.alignment = TextAlignmentOptions.Center;
        label.color = GetDamageColor(skillType);
        // 속성의 색감을 유지한 채 30% 어둡게 만든 외곽선을 두껍게 적용해 숫자의 경계를 구분한다.
        label.outlineColor = Color.Lerp(label.color, Color.black, 0.3f);
        label.outlineWidth = 0.525f;
        label.raycastTarget = false;
        label.text = result.Damage.ToString();
        damageNumbers.Add((label, rect.anchoredPosition, direction, 0f));
    }

    // 급소 또는 유리한 상성은 크게 굵게, 불리한 상성은 작게 표시하며 급소 강조를 우선한다.
    private (float size, FontStyles weight) GetDamageStyle(BattleResolveResult result)
    {
        bool emphasized = result.Critical || result.Effectiveness > 1f;
        float offset = emphasized ? 4f : result.Effectiveness < 1f ? -2f : 0f;
        return (Mathf.Max(1f, damageFontSize + offset), emphasized ? FontStyles.Bold : FontStyles.Normal);
    }

    // 사용한 스킬의 속성을 확정된 피해 숫자 색상표에 연결하며 발버둥과 알 수 없는 값은 일반 색상을 사용한다.
    private static Color32 GetDamageColor(ElementType type)
    {
        return type switch
        {
            ElementType.Fire => new Color32(239, 68, 68, 255),
            ElementType.Water => new Color32(59, 130, 246, 255),
            ElementType.Grass => new Color32(34, 197, 94, 255),
            ElementType.Electric => new Color32(250, 204, 21, 255),
            ElementType.Fighting => new Color32(249, 115, 22, 255),
            ElementType.Rock => new Color32(51, 51, 51, 255),
            ElementType.Earth => new Color32(139, 90, 43, 255),
            _ => new Color32(128, 128, 128, 255)
        };
    }

    private void Update()
    {
        // 코루틴이 월드 좌표로 이펙트를 배치하기 전에 이전 프레임의 화면 이동을 제거한다.
        if (active && effectRoot != null)
            effectRoot.anchoredPosition = effectOrigin;
        if (screenShaking)
        {
            screenShakeElapsed += Time.deltaTime;
            if (screenShakeElapsed >= Mathf.Max(0.01f, screenShakeDuration))
                screenShaking = false;
        }
        // 공격 연출이 끝나도 숫자의 수명을 독립적으로 진행해 다음 공격에서 지우지 않는다.
        for (int i = damageNumbers.Count - 1; i >= 0; i--)
        {
            var number = damageNumbers[i];
            number.elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(number.elapsed / Mathf.Max(0.01f, damageDuration));
            if (number.text == null || progress >= 1f)
            {
                if (number.text != null)
                {
                    number.text.gameObject.SetActive(false);
                    Destroy(number.text.gameObject);
                }
                damageNumbers.RemoveAt(i);
                continue;
            }

            RectTransform rect = number.text.rectTransform;
            rect.anchoredPosition = number.origin + GetDamageOffset(progress, number.direction);
            // 피격 순간에는 정자세를 유지하고 오른쪽 이동은 시계 방향, 왼쪽 이동은 반시계 방향으로 점점 빠르게 기울인다.
            rect.localRotation = Quaternion.Euler(0f, 0f, -number.direction * damageRotation * progress * progress);
            rect.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(progress / 0.2f));
            // 늦게 생성되는 스킬 이펙트에도 숫자가 가려지지 않도록 앞에 유지한다.
            rect.SetAsLastSibling();
            number.text.alpha = 1f - Mathf.InverseLerp(0.8f, 1f, progress);
            damageNumbers[i] = number;
        }
    }

    private void LateUpdate()
    {
        // 이미지 이동과 접촉점 계산을 마친 뒤 공통 부모만 움직여 타격 위치가 흔들림에 밀려 누적되지 않게 한다.
        if (active && screenShaking && effectRoot != null)
            effectRoot.anchoredPosition = effectOrigin + GetScreenShakeOffset(screenShakeElapsed,
                screenShakeDuration, screenHitStrength);
    }

    // 상하 진동을 감쇠해 강한 충격 후 가로 이동 없이 원래 위치로 돌아오는 화면 이동량을 계산한다.
    private static Vector2 GetScreenShakeOffset(float elapsed, float duration, float strength)
    {
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        float fade = (1f - progress) * (1f - progress);
        return Vector2.up * (Mathf.Sin(elapsed * Mathf.PI * 48f) * Mathf.Max(0f, strength) * fade);
    }

    // 최고점까지 완만하게 감속하고, 짧은 낙하 구간에서는 가속해 바깥쪽으로 빠르게 떨어뜨린다.
    private Vector2 GetDamageOffset(float progress, float direction)
    {
        float peak = Mathf.Clamp(damagePeakTime, 0.1f, 0.9f);
        float height;
        if (progress < peak)
        {
            float ascent = progress / peak;
            height = damageRise * (1f - (1f - ascent) * (1f - ascent));
        }
        else
        {
            // 최고점의 수직 속도를 0으로 이어 곡선이 꺾이지 않도록 하고 낙하 속도만 빠르게 높인다.
            float fall = (progress - peak) / (1f - peak);
            height = damageRise - (damageRise + damageDrop) * fall * fall;
        }
        return new Vector2(direction * damageTravel * progress, height);
    }

    // 전투 재시작과 비활성화 시 남은 숫자를 즉시 숨기고 제거한다.
    private void ClearDamage()
    {
        foreach (var number in damageNumbers)
        {
            if (number.text == null)
                continue;
            number.text.gameObject.SetActive(false);
            Destroy(number.text.gameObject);
        }
        damageNumbers.Clear();
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
    internal void Cancel(bool clearDamage = true)
    {
        generation++;
        Completed = false;
        Restore();
        if (clearDamage)
            ClearDamage();
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
            if (effectRoot != null)
                effectRoot.anchoredPosition = effectOrigin;
            if (playerPosition != null)
                playerPosition.anchoredPosition = playerOrigin;
            if (enemyPosition != null)
                enemyPosition.anchoredPosition = enemyOrigin;
        }
        active = false;
        screenShaking = false;
        screenShakeElapsed = 0f;
        screenHitStrength = 0f;
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
