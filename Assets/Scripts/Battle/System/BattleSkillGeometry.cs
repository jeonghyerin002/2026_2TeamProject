using UnityEngine;
using UnityEngine.UI;

/// <summary>전투 이미지의 표시 영역과 스킬 접촉점을 공통 좌표로 변환한다.</summary>
public static class BattleSkillGeometry
{
    // 원본 비율 유지로 생긴 여백을 제외한 실제 표시 영역의 상대 좌표를 월드 위치로 변환한다.
    public static Vector3 GetPoint(RectTransform rect, Vector2 point)
    {
        return rect.TransformPoint(GetLocalPoint(rect, rect.GetComponent<Image>(), point));
    }

    // UI Image의 Simple 표시 영역을 기준으로 상대 좌표를 계산한다.
    private static Vector3 GetLocalPoint(RectTransform rect, Image image, Vector2 point)
    {
        Rect bounds = rect.rect;
        if (image != null && image.preserveAspect && image.sprite != null && bounds.width > 0f && bounds.height > 0f)
        {
            float ratio = image.sprite.rect.width / image.sprite.rect.height;
            if (ratio > bounds.width / bounds.height)
            {
                float height = bounds.width / ratio;
                bounds.y += (bounds.height - height) * rect.pivot.y;
                bounds.height = height;
            }
            else
            {
                float width = bounds.height * ratio;
                bounds.x += (bounds.width - width) * rect.pivot.x;
                bounds.width = width;
            }
        }
        return new Vector3(bounds.x + bounds.width * point.x, bounds.y + bounds.height * point.y, 0f);
    }

    // 이동과 애니메이션을 하나의 시각으로 평가해 프레임 지연과 반전 여부에 관계없이 타격 중심을 맞춘다.
    public static void Sample(RectTransform instance, Image image, SkillPresentationData.Effect effect,
        Vector3 launch, Vector3 hit, bool player, float elapsed, float impact)
    {
        // 기존 호출은 공격자 이미지 기준점 없이 동일한 표본화 경로 사용
        Sample(instance, image, effect, launch, hit, player, elapsed, impact, null);
    }


    // 공격자 이미지 기준의 고정 연출까지 포함한 프레임 표본화
    public static void Sample(RectTransform instance, Image image, SkillPresentationData.Effect effect,
        Vector3 launch, Vector3 hit, bool player, float elapsed, float impact, RectTransform actor)
    {
        float start = effect.GetStart(impact);
        float arrival = Mathf.Max(start, impact);
        float progress = arrival > start ? Mathf.Clamp01((elapsed - start) / (arrival - start)) :
            (elapsed >= arrival ? 1f : 0f);

        // 선택한 이동 스킬만 구간별 속도를 적용하고 이미지와 위치의 진행률 동기화
        if (effect.Moves && effect.ApproachProgress != null && effect.ApproachProgress.length > 0)
        {
            progress = Mathf.Clamp01(effect.ApproachProgress.Evaluate(progress));
        }

        float clipTime = elapsed < arrival ? progress * effect.animationImpactTime :
            effect.animationImpactTime + elapsed - arrival;

        // 유지 이미지는 원본 클립 시각으로 이동 진행률과 자세를 함께 고정
        if (effect.HeldAnimationTime >= 0f)
        {
            clipTime = effect.HeldAnimationTime;
            progress = effect.animationImpactTime > 0f ? Mathf.Clamp01(clipTime / effect.animationImpactTime) : 1f;
        }

        Vector2 contact = effect.contactPoint;
        if (effect.animation != null)
        {
            clipTime = Mathf.Clamp(clipTime, 0f, effect.animation.length);
            if (effect.contactFrames != null)
                foreach (SkillPresentationData.ContactFrame frame in effect.contactFrames)
                    if (Mathf.Abs(clipTime - frame.time) < 0.00001f)
                    {
                        clipTime = frame.time;
                        break;
                    }
            effect.animation.SampleAnimation(instance.gameObject, clipTime);
            if (effect.contactFrames != null)
                foreach (SkillPresentationData.ContactFrame frame in effect.contactFrames)
                {
                    if (clipTime < frame.time)
                        break;
                    contact = frame.point;
                }
        }

        // 선택한 스킬만 Target 기준 UI 좌표에서 출발하고 기존 스킬은 공격자 발사점 유지
        Vector3 travelStart = launch;
        if (effect.Moves && effect.TargetApproachOffset != Vector2.zero)
        {
            travelStart = hit + instance.parent.TransformVector(effect.TargetApproachOffset);
        }

        // 선택된 발사 기준점에 시작 위치 보정만 적용
        if (effect.Moves)
        {
            travelStart += instance.parent.TransformVector(effect.TravelStartOffset);
        }

        // 이동 연출의 도착점만 UI 좌표로 보정하고 원본 피격점 유지
        Vector3 travelEnd = hit;
        if (effect.Moves)
        {
            travelEnd += instance.parent.TransformVector(effect.TravelEndOffset);
        }

        // 실제 이동 시작점과 도착점 사이의 방향을 이미지 회전에 전달
        Vector3 direction = instance.parent.InverseTransformVector(travelEnd - travelStart);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector3 scale = effect.prefab.localScale * Mathf.Max(0.01f, effect.sizeScale);

        // NPC 사용 시에만 설정된 추가 크기 배율 적용
        if (!player)
        {
            scale *= effect.EnemySizeScale;
        }

        bool mirror = !player && effect.mirrorForEnemy;
        if (mirror)
            scale.x = -scale.x;
        instance.localScale = scale;

        // 지정된 클립 시각까지 원본 기울기를 보정하고 이후에는 프리팹의 기본 각도 사용
        bool rotatesToDirection = effect.rotateToDirection &&
            (effect.DirectionRotationEndTime <= 0f || clipTime < effect.DirectionRotationEndTime);
        float directionAngle = angle - (mirror ? 180f : 0f) +
            (mirror ? -effect.DirectionAngleOffset : effect.DirectionAngleOffset);
        instance.localRotation = (rotatesToDirection ? Quaternion.Euler(0f, 0f, directionAngle) :
            Quaternion.identity) * effect.prefab.localRotation;

        // 초반 프레임에 지정한 입체 각도를 이동 방향과 독립적으로 적용
        if (effect.DirectionRotationEndTime > 0f && clipTime < effect.DirectionRotationEndTime &&
            effect.InitialRotationEuler != Vector3.zero)
        {
            // 원본 프레임마다 다른 균열 기울기를 동일한 지면 방향으로 보정
            Vector3 initialRotation = effect.InitialRotationEuler;
            if (effect.InitialRotationZ != null && effect.InitialRotationZ.length > 0)
            {
                initialRotation.z = effect.InitialRotationZ.Evaluate(clipTime);
            }

            instance.localRotation = Quaternion.Euler(initialRotation) * effect.prefab.localRotation;
        }

        Vector3 position;
        if (effect.Moves)
            position = Vector3.Lerp(travelStart, travelEnd, progress);
        else if (effect.playback == SkillPresentationData.Playback.AtActor ||
            (effect.playback == SkillPresentationData.Playback.Legacy && effect.position == SkillPresentationData.Position.Actor))
        {
            // 선택한 고정 연출은 발사점 대신 실제 공격자 이미지의 기준점 사용
            position = effect.UseActorAnchor && actor != null ? GetPoint(actor, effect.ActorAnchor) : launch;
        }
        else if (effect.playback == SkillPresentationData.Playback.AtCenter ||
            (effect.playback == SkillPresentationData.Playback.Legacy && effect.position == SkillPresentationData.Position.Center))
            position = (launch + hit) * 0.5f;
        else
            position = hit;

        // 클립 프레임의 위치만 UI 평면에서 보정하고 이미지 깊이와 형상 유지
        Vector2 frameOffset = effect.offset;
        if (effect.FrameOffsetX != null && effect.FrameOffsetX.length > 0)
        {
            // 선택한 스킬의 NPC 사용 시 가로 이동만 반전하고 세로 이동 유지
            float horizontal = effect.FrameOffsetX.Evaluate(clipTime);
            frameOffset.x += !player && effect.MirrorFrameOffsetForEnemy ? -horizontal : horizontal;
        }

        if (effect.FrameOffsetY != null && effect.FrameOffsetY.length > 0)
        {
            frameOffset.y += effect.FrameOffsetY.Evaluate(clipTime);
        }

        // 프레임 내부의 이동과 크기 변화까지 보정하므로 도착 후 잔상이 몸 밖으로 흘러가지 않는다.
        instance.position = position + instance.parent.TransformVector(frameOffset) -
            instance.TransformVector(GetLocalPoint(instance, image, contact));
    }
}
