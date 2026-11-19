using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>원본 씬이나 전투 상태를 변경하지 않고 캐릭터 조합별 스킬 접촉 위치를 확인한다.</summary>
public sealed class BattleSkillPreview : EditorWindow
{
    // 입체 회전한 UI가 카메라 앞뒤로 잘리지 않도록 확보할 깊이
    private const float PreviewCameraDistance = 1000f;

    private CharacterData playerData;                   // 미리보기 플레이어의 위치 설정
    private CharacterData enemyData;                    // 미리보기 상대의 이미지와 위치 설정
    private Sprite playerSprite;                       // 뒤에서 바라본 플레이어 이미지
    private SkillPresentationData presentation;         // 확인할 스킬 연출 설정
    private PreviewRenderUtility preview;              // 원본 씬과 분리된 렌더링 공간
    private RectTransform root;                        // 전투 씬과 동일한 크기의 UI 영역
    private RectTransform playerImage;                 // 미리보기 플레이어 표시 영역
    private RectTransform enemyImage;                  // 미리보기 상대 표시 영역
    private RectTransform launchMarker;                // 발사 지점을 나타내는 파란 점
    private RectTransform hitMarker;                   // 피격 지점을 나타내는 빨간 점
    private readonly List<RectTransform> effects = new(); // 미리보기 전용 이펙트 인스턴스
    private readonly List<Image> images = new();        // 반복 표본화에 사용할 이미지 참조
    private SkillPresentationData builtPresentation;    // 현재 생성물의 원본 설정
    private float elapsed;                             // 연출 시작부터 확인할 시각(초)
    private bool playerAttacks = true;                 // 플레이어가 공격하는 방향 선택
    private bool playing;                              // 시간축 자동 진행 여부
    private double previousTime;                       // Editor 갱신 간격 계산용 시각

    // Editor 전용 사운드 출력과 임시 혼합 클립
    private const int PreviewSampleRate = 44100;
    private const int PreviewChannels = 2;
    private AudioClip previewSound;
    private string previewSoundPath;
    private double soundEndTime;
    private string soundStatus;
    private SerializedObject soundSettings;
    private static readonly System.Type AudioUtilType = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    private static readonly MethodInfo PlayPreviewClip = AudioUtilType?.GetMethod("PlayPreviewClip", BindingFlags.Public | BindingFlags.Static);
    private static readonly MethodInfo StopPreviewClips = AudioUtilType?.GetMethod("StopAllPreviewClips", BindingFlags.Public | BindingFlags.Static);
    private static readonly MethodInfo IsPreviewClipPlaying = AudioUtilType?.GetMethod("IsPreviewClipPlaying", BindingFlags.Public | BindingFlags.Static);

    // 전투 진행 없이 설정을 확인할 수 있는 독립 미리보기를 연다.
    [MenuItem("Tools/Battle/Skill Preview")]
    public static void Open()
    {
        BattleSkillPreview window = GetWindow<BattleSkillPreview>("스킬 위치 미리보기");
        if (Selection.activeObject is SkillPresentationData data)
            window.presentation = data;
    }

    private void OnEnable()
    {
        playerData = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Character_1.asset");
        enemyData = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Character_2.asset");
        playerSprite = Resources.Load<Sprite>("player/BattleCharacter");
        previousTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        StopSound();
        playing = false;
        soundSettings?.Dispose();
        soundSettings = null;
        preview?.Cleanup();
        preview = null;
        effects.Clear();
        images.Clear();
    }

    // 재생 상태에서만 미리보기 시간축을 진행한다.
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        // 시각 연출보다 긴 사운드의 끝부분까지 재생한 뒤 임시 에셋 정리
        if (!string.IsNullOrEmpty(previewSoundPath) && now >= soundEndTime)
        {
            StopSound();
        }

        if (playing && presentation != null)
        {
            elapsed += (float)(now - previousTime);
            if (elapsed >= presentation.GetDuration())
            {
                elapsed = presentation.GetDuration();
                playing = false;
            }
            Repaint();
        }
        previousTime = now;
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        playerData = (CharacterData)EditorGUILayout.ObjectField("플레이어 설정", playerData, typeof(CharacterData), false);
        playerSprite = (Sprite)EditorGUILayout.ObjectField("플레이어 이미지", playerSprite, typeof(Sprite), false);
        enemyData = (CharacterData)EditorGUILayout.ObjectField("NPC 설정", enemyData, typeof(CharacterData), false);
        presentation = (SkillPresentationData)EditorGUILayout.ObjectField("스킬 연출", presentation, typeof(SkillPresentationData), false);
        playerAttacks = EditorGUILayout.Toggle("플레이어 → NPC", playerAttacks);
        if (EditorGUI.EndChangeCheck())
        {
            playing = false;
            StopSound();
            soundStatus = null;
        }

        if (presentation == null || playerData == null || enemyData == null || playerSprite == null || enemyData.BattleSprite == null)
        {
            EditorGUILayout.HelpBox("양측 데이터와 이미지, 스킬 연출을 선택하세요. 파란 점은 발사, 빨간 점은 피격 지점입니다.", MessageType.Info);
            return;
        }

        DrawSoundSettings();
        EditorGUI.BeginChangeCheck();
        elapsed = EditorGUILayout.Slider("재생 시각(초)", elapsed, 0f, presentation.GetDuration());
        if (EditorGUI.EndChangeCheck())
        {
            playing = false;
            StopSound();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "일시 정지" : "처음부터 재생"))
            {
                StopSound();
                if (!playing)
                {
                    elapsed = 0f;
                    StartSound();
                    previousTime = EditorApplication.timeSinceStartup;
                }

                playing = !playing;
            }
            if (GUILayout.Button("타격 순간"))
            {
                playing = false;
                StopSound();
                elapsed = presentation.ImpactTime;
            }
            if (GUILayout.Button("설정 다시 읽기"))
            {
                playing = false;
                StopSound();
                builtPresentation = null;
            }
        }
        EditorGUILayout.LabelField("파랑: 발사 / 빨강: 피격 — 도착 후 남는 그림도 시간축으로 확인하세요.");
        EditorGUILayout.LabelField("사운드: 처음부터 재생 시 명중 기준 / 시각 이동 시 정지");
        DrawPreview(GUILayoutUtility.GetAspectRect(1.5f));
    }


    // 선택된 스킬 에셋의 사운드 설정 노출 및 무음 원인 안내
    private void DrawSoundSettings()
    {
        // 선택 변경 시 직렬화 참조 교체 후 최신 Inspector 값 반영
        if (soundSettings == null || soundSettings.targetObject != presentation)
        {
            soundSettings?.Dispose();
            soundSettings = new SerializedObject(presentation);
        }

        soundSettings.Update();
        SerializedProperty sounds = soundSettings.FindProperty("sounds");
        EditorGUILayout.PropertyField(sounds, new GUIContent("스킬 사운드 (에셋에 적용)"), true);
        if (soundSettings.ApplyModifiedProperties())
        {
            playing = false;
            StopSound();
            soundStatus = null;
        }

        // 재생 가능한 클립 부재를 버튼을 누르기 전에도 표시
        bool hasAudibleSound = false;
        for (int i = 0; i < sounds.arraySize; i++)
        {
            SerializedProperty sound = sounds.GetArrayElementAtIndex(i);
            if (sound.FindPropertyRelative("clip").objectReferenceValue != null &&
                sound.FindPropertyRelative("volume").floatValue > 0f)
            {
                hasAudibleSound = true;
                break;
            }
        }

        if (!hasAudibleSound)
        {
            EditorGUILayout.HelpBox("재생할 사운드가 없습니다. 스킬 사운드에 항목을 추가하고 Clip과 Volume을 지정한 뒤 처음부터 재생을 누르세요.", MessageType.Info);
        }

        if (!string.IsNullOrEmpty(soundStatus))
        {
            EditorGUILayout.HelpBox(soundStatus, MessageType.Warning);
        }
    }


    // 효과음의 시간과 음량을 혼합해 편집 모드에서 재생
    private void StartSound()
    {
        soundStatus = null;

        // 내부 Editor API 누락 시 시각 미리보기 유지
        if (PlayPreviewClip == null || StopPreviewClips == null)
        {
            soundStatus = "이 Editor에서 사운드 미리보기 API를 사용할 수 없습니다.";
            Debug.LogWarning(soundStatus);
            return;
        }

        // 원본 임포트 설정 변경 없이 읽을 수 있는 효과음 데이터 수집
        List<(AudioClip clip, float time, float volume, float[] samples)> clips = new();
        float duration = 0f;
        using SerializedObject settings = new(presentation);
        SerializedProperty sounds = settings.FindProperty("sounds");
        for (int i = 0; i < sounds.arraySize; i++)
        {
            SerializedProperty sound = sounds.GetArrayElementAtIndex(i);
            AudioClip clip = sound.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
            float time = Mathf.Max(0f, sound.FindPropertyRelative("time").floatValue);
            float volume = Mathf.Clamp01(sound.FindPropertyRelative("volume").floatValue);

            // 피해 조건 효과음은 실제 전투와 같이 타격 시점 이후 재생
            if (sound.FindPropertyRelative("onlyOnDamage").boolValue)
            {
                time = Mathf.Max(time, presentation.ImpactTime);
            }

            if (clip == null || volume <= 0f)
            {
                continue;
            }

            clip.LoadAudioData();
            float[] samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
            {
                soundStatus = $"{clip.name} 데이터를 읽을 수 없습니다. Audio Import Settings의 Load Type을 Decompress On Load로 확인하세요.";
                Debug.LogWarning(soundStatus, clip);
                continue;
            }

            clips.Add((clip, time, volume, samples));
            duration = Mathf.Max(duration, time + clip.length);
        }

        if (clips.Count == 0)
        {
            return;
        }

        // 개별 음량과 겹치는 효과음을 보존하는 공통 스테레오 시간축 생성
        int frameCount = Mathf.Max(1, Mathf.CeilToInt(duration * PreviewSampleRate));
        float[] mixed = new float[frameCount * PreviewChannels];
        foreach (var entry in clips)
        {
            AudioClip clip = entry.clip;
            int start = Mathf.RoundToInt(entry.time * PreviewSampleRate);
            int count = Mathf.Min(frameCount - start, Mathf.CeilToInt(clip.length * PreviewSampleRate));
            for (int frame = 0; frame < count; frame++)
            {
                float source = (float)frame * clip.frequency / PreviewSampleRate;
                int first = Mathf.Min((int)source, clip.samples - 1);
                int next = Mathf.Min(first + 1, clip.samples - 1);
                for (int channel = 0; channel < PreviewChannels; channel++)
                {
                    int inputChannel = Mathf.Min(channel, clip.channels - 1);
                    float sample = Mathf.Lerp(entry.samples[first * clip.channels + inputChannel],
                        entry.samples[next * clip.channels + inputChannel], source - first);
                    mixed[(start + frame) * PreviewChannels + channel] += sample * entry.volume;
                }
            }
        }

        // Editor 미리보기에서 지원하는 임포트된 WAV로 혼합 데이터 전달
        try
        {
            previewSoundPath = AssetDatabase.GenerateUniqueAssetPath("Assets/SkillPreviewAudio.wav");
            WritePreviewWave(previewSoundPath, mixed);
            AssetDatabase.ImportAsset(previewSoundPath, ImportAssetOptions.ForceSynchronousImport);
            previewSound = AssetDatabase.LoadAssetAtPath<AudioClip>(previewSoundPath);
            if (previewSound == null)
            {
                throw new System.InvalidOperationException("미리보기 오디오 임포트 실패");
            }

            PlayPreviewClip.Invoke(null, new object[] { previewSound, 0, false });
            soundEndTime = EditorApplication.timeSinceStartup + previewSound.length;
            if (IsPreviewClipPlaying != null && !(bool)IsPreviewClipPlaying.Invoke(null, null))
            {
                soundStatus = "Editor가 사운드 재생을 시작하지 못했습니다.";
                StopSound();
            }
        }
        catch (System.Exception error)
        {
            soundStatus = "스킬 미리보기 사운드 재생 실패: " + error.GetBaseException().Message;
            Debug.LogWarning(soundStatus);
            StopSound();
        }
    }


    // 혼합 PCM을 Editor 오디오 미리보기용 임시 WAV 파일로 기록
    private static void WritePreviewWave(string path, float[] samples)
    {
        // 스테레오 16비트 PCM 헤더와 클리핑을 제한한 샘플 기록
        const int BytesPerSample = sizeof(short);
        int dataSize = samples.Length * BytesPerSample;
        using BinaryWriter writer = new(File.Create(path));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)PreviewChannels);
        writer.Write(PreviewSampleRate);
        writer.Write(PreviewSampleRate * PreviewChannels * BytesPerSample);
        writer.Write((short)(PreviewChannels * BytesPerSample));
        writer.Write((short)(BytesPerSample * 8));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        foreach (float sample in samples)
        {
            writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }


    // 사운드 출력 정지 및 소유한 임시 클립 해제
    private void StopSound()
    {
        if (string.IsNullOrEmpty(previewSoundPath))
        {
            return;
        }

        // 소유 클립이 있는 경우에만 Editor 미리보기 출력 정지
        try
        {
            StopPreviewClips?.Invoke(null, null);
        }
        catch (System.Exception error)
        {
            Debug.LogWarning("스킬 미리보기 사운드 정지 실패: " + error.GetBaseException().Message);
        }
        finally
        {
            // 재생을 위해서만 생성한 WAV와 meta를 함께 정리
            previewSound = null;
            AssetDatabase.DeleteAsset(previewSoundPath);
            previewSoundPath = null;
        }
    }


    // 전투 씬의 표시 크기로 구성하되 생성물은 PreviewRenderUtility 안에서만 관리한다.
    private void BuildPreview()
    {
        if (preview == null)
        {
            preview = new PreviewRenderUtility();
            root = EditorUtility.CreateGameObjectWithHideFlags("Skill Preview", HideFlags.HideAndDontSave,
                typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(960f, 640f);
            root.position = new Vector3(480f, 320f, 0f);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            preview.AddSingleGO(root.gameObject);
            playerImage = AddImage("Player", new Vector2(73f, -266f), new Vector2(234f, 261f));
            enemyImage = AddImage("NPC", new Vector2(714f, -127f), new Vector2(200f, 225f));
            enemyImage.GetComponent<Image>().preserveAspect = true;
            launchMarker = AddImage("Launch", Vector2.zero, new Vector2(8f, 8f));
            hitMarker = AddImage("Hit", Vector2.zero, new Vector2(8f, 8f));
            launchMarker.pivot = hitMarker.pivot = new Vector2(0.5f, 0.5f);
            launchMarker.GetComponent<Image>().color = Color.cyan;
            hitMarker.GetComponent<Image>().color = Color.red;
            preview.camera.orthographic = true;
            // Preview 카메라는 Canvas를 생략하므로 독립 공간 안에서 Game 카메라로 UI를 렌더링한다.
            preview.camera.cameraType = CameraType.Game;
            preview.camera.orthographicSize = 320f;
            // 직교 화면 크기를 유지하면서 기울어진 UI 전체를 촬영 범위 안에 배치
            preview.camera.transform.position = new Vector3(480f, 320f, -PreviewCameraDistance);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.white;
            preview.camera.nearClipPlane = 0.1f;
            preview.camera.farClipPlane = PreviewCameraDistance * 2f;
        }
        if (builtPresentation == presentation && effects.Count == presentation.Effects.Length)
            return;
        foreach (RectTransform instance in effects)
            if (instance != null)
                DestroyImmediate(instance.gameObject);
        effects.Clear();
        images.Clear();
        foreach (SkillPresentationData.Effect effect in presentation.Effects)
        {
            RectTransform instance = effect?.prefab != null ? Instantiate(effect.prefab, root, false) : null;
            if (instance != null)
            {
                Animator animator = instance.GetComponent<Animator>();
                if (animator != null)
                    animator.enabled = false;
            }
            effects.Add(instance);
            images.Add(instance != null ? instance.GetComponent<Image>() : null);
        }
        builtPresentation = presentation;
    }

    // 플레이 모드와 같은 좌표 및 클립 표본화 함수를 사용해 선택한 시각을 표시한다.
    private void DrawPreview(Rect area)
    {
        BuildPreview();
        playerImage.GetComponent<Image>().sprite = playerSprite;
        enemyImage.GetComponent<Image>().sprite = enemyData.BattleSprite;
        RectTransform source = playerAttacks ? playerImage : enemyImage;
        RectTransform target = playerAttacks ? enemyImage : playerImage;
        Vector3 launch = BattleSkillGeometry.GetPoint(source, (playerAttacks ? playerData : enemyData).BattleLaunch);
        Vector3 hit = BattleSkillGeometry.GetPoint(target, (playerAttacks ? enemyData : playerData).BattleHit);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
                continue;
            SkillPresentationData.Effect effect = presentation.Effects[i];
            bool visible = elapsed >= effect.GetStart(presentation.ImpactTime) && elapsed < effect.GetEnd(presentation.ImpactTime);
            effects[i].gameObject.SetActive(visible);
            if (visible)
                BattleSkillGeometry.Sample(effects[i], images[i], effect, launch, hit, playerAttacks, elapsed,
                    presentation.ImpactTime, source);
        }
        launchMarker.position = launch;
        hitMarker.position = hit;
        launchMarker.SetAsLastSibling();
        hitMarker.SetAsLastSibling();
        if (Event.current.type != EventType.Repaint)
            return;
        preview.BeginPreview(area, GUIStyle.none);
        Canvas.ForceUpdateCanvases();
        preview.Render(true);
        GUI.DrawTexture(area, preview.EndPreview(), ScaleMode.ScaleToFit, false);
    }

    // 이미지의 원점과 크기를 기존 Battle 씬의 표시 영역에 맞춘다.
    private RectTransform AddImage(string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave,
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
