using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>새 UI 스킬 클립에서 접촉점 초안을 만들고 독립 미리보기로 연결한다.</summary>
[CustomEditor(typeof(SkillPresentationData))]
public sealed class SkillPresentationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Contact Point는 실제 표시 그림의 왼쪽 아래 (0,0), 오른쪽 위 (1,1) 기준입니다. 내부 이동이 있는 클립은 접촉점 초안을 생성한 뒤 미리보기에서 타격 시점과 크기를 확인하세요.", MessageType.Info);
        if (GUILayout.Button("클립에서 프레임별 접촉점 초안 생성"))
            Bake((SkillPresentationData)target);
        if (GUILayout.Button("스킬 위치 미리보기 열기"))
            BattleSkillPreview.Open();
    }

    // 원본 텍스처와 임포터를 변경하지 않고 가장 큰 불투명 덩어리의 중심을 설정에 기록한다.
    public static void Bake(SkillPresentationData data)
    {
        Undo.RecordObject(data, "스킬 접촉점 생성");
        foreach (SkillPresentationData.Effect effect in data.Effects)
        {
            if (effect?.animation == null)
                continue;
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(effect.animation))
            {
                if (binding.type != typeof(Image) || binding.propertyName != "m_Sprite" || binding.path != "")
                    continue;
                List<SkillPresentationData.ContactFrame> frames = new();
                Dictionary<Texture2D, Texture2D> decoded = new();
                try
                {
                    foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(effect.animation, binding))
                    {
                        if (!(key.value is Sprite sprite))
                            continue;
                        if (!decoded.TryGetValue(sprite.texture, out Texture2D texture))
                        {
                            string path = AssetDatabase.GetAssetPath(sprite.texture);
                            if (!File.Exists(path))
                                throw new IOException("스프라이트 원본 파일을 찾을 수 없습니다: " + path);
                            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            decoded.Add(sprite.texture, texture);
                            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
                                throw new IOException("PNG/JPG로 읽을 수 있는 UI 스프라이트가 필요합니다: " + path);
                        }
                        frames.Add(new SkillPresentationData.ContactFrame { time = key.time, point = FindCenter(texture, sprite.rect) });
                    }
                    frames.Sort((a, b) => a.time.CompareTo(b.time));
                    effect.contactFrames = frames.ToArray();
                }
                catch (System.Exception error)
                {
                    Debug.LogError("접촉점 생성 실패: " + error.Message, data);
                }
                finally
                {
                    foreach (Texture2D texture in decoded.Values)
                        DestroyImmediate(texture);
                }
            }
        }
        EditorUtility.SetDirty(data);
        Debug.Log("접촉점 초안을 생성했습니다. 파편 대신 주 타격 부분을 선택했는지 미리보기에서 확인하세요.", data);
    }

    // 작은 파편을 제외한 가장 큰 연결 영역의 알파 가중 중심을 표시 영역의 상대 좌표로 구한다.
    private static Vector2 FindCenter(Texture2D texture, Rect bounds)
    {
        int width = Mathf.RoundToInt(bounds.width);
        int height = Mathf.RoundToInt(bounds.height);
        Color[] pixels = texture.GetPixels((int)bounds.x, (int)bounds.y, width, height);
        bool[] visited = new bool[pixels.Length];
        Queue<int> pending = new();
        int largest = 0;
        Vector2 result = new(0.5f, 0.5f);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (visited[i] || pixels[i].a <= 32f / 255f)
                continue;
            visited[i] = true;
            pending.Enqueue(i);
            int count = 0;
            float mass = 0f;
            Vector2 sum = Vector2.zero;
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                int x = index % width;
                int y = index / width;
                count++;
                mass += pixels[index].a;
                sum += new Vector2(x + 0.5f, y + 0.5f) * pixels[index].a;
                Visit(index - 1, x > 0, pixels, visited, pending);
                Visit(index + 1, x + 1 < width, pixels, visited, pending);
                Visit(index - width, y > 0, pixels, visited, pending);
                Visit(index + width, y + 1 < height, pixels, visited, pending);
            }
            if (count > largest)
            {
                largest = count;
                result = new Vector2(sum.x / mass / width, sum.y / mass / height);
            }
        }
        return result;
    }

    // 미방문 불투명 픽셀만 탐색 대기열에 추가한다.
    private static void Visit(int index, bool inside, Color[] pixels, bool[] visited, Queue<int> pending)
    {
        if (!inside || visited[index] || pixels[index].a <= 32f / 255f)
            return;
        visited[index] = true;
        pending.Enqueue(index);
    }
}
