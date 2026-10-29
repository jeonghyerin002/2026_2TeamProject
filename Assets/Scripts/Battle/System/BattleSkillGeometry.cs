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
        float start = effect.GetStart(impact);
        float arrival = Mathf.Max(start, impact);
        float progress = arrival > start ? Mathf.Clamp01((elapsed - start) / (arrival - start)) :
            (elapsed >= arrival ? 1f : 0f);
        float clipTime = elapsed < arrival ? progress * effect.animationImpactTime :
            effect.animationImpactTime + elapsed - arrival;
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

        Vector3 direction = instance.parent.InverseTransformVector(hit - launch);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector3 scale = effect.prefab.localScale * Mathf.Max(0.01f, effect.sizeScale);
        bool mirror = !player && effect.mirrorForEnemy;
        if (mirror)
            scale.x = -scale.x;
        instance.localScale = scale;
        instance.localRotation = (effect.rotateToDirection ? Quaternion.Euler(0f, 0f, angle - (mirror ? 180f : 0f)) :
            Quaternion.identity) * effect.prefab.localRotation;

        Vector3 position;
        if (effect.Moves)
            position = Vector3.Lerp(launch, hit, progress);
        else if (effect.playback == SkillPresentationData.Playback.AtActor ||
            (effect.playback == SkillPresentationData.Playback.Legacy && effect.position == SkillPresentationData.Position.Actor))
            position = launch;
        else if (effect.playback == SkillPresentationData.Playback.AtCenter ||
            (effect.playback == SkillPresentationData.Playback.Legacy && effect.position == SkillPresentationData.Position.Center))
            position = (launch + hit) * 0.5f;
        else
            position = hit;

        // 프레임 내부의 이동과 크기 변화까지 보정하므로 도착 후 잔상이 몸 밖으로 흘러가지 않는다.
        instance.position = position + instance.parent.TransformVector(effect.offset) -
            instance.TransformVector(GetLocalPoint(instance, image, contact));
    }
}
