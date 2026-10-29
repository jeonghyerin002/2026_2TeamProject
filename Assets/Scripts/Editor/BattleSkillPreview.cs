using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>원본 씬이나 전투 상태를 변경하지 않고 캐릭터 조합별 스킬 접촉 위치를 확인한다.</summary>
public sealed class BattleSkillPreview : EditorWindow
{
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
        preview?.Cleanup();
        preview = null;
        effects.Clear();
        images.Clear();
    }

    // 재생 상태에서만 미리보기 시간축을 진행한다.
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
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
        playerData = (CharacterData)EditorGUILayout.ObjectField("플레이어 설정", playerData, typeof(CharacterData), false);
        playerSprite = (Sprite)EditorGUILayout.ObjectField("플레이어 이미지", playerSprite, typeof(Sprite), false);
        enemyData = (CharacterData)EditorGUILayout.ObjectField("NPC 설정", enemyData, typeof(CharacterData), false);
        presentation = (SkillPresentationData)EditorGUILayout.ObjectField("스킬 연출", presentation, typeof(SkillPresentationData), false);
        playerAttacks = EditorGUILayout.Toggle("플레이어 → NPC", playerAttacks);
        if (presentation == null || playerData == null || enemyData == null || playerSprite == null || enemyData.BattleSprite == null)
        {
            EditorGUILayout.HelpBox("양측 데이터와 이미지, 스킬 연출을 선택하세요. 파란 점은 발사, 빨간 점은 피격 지점입니다.", MessageType.Info);
            return;
        }
        elapsed = EditorGUILayout.Slider("재생 시각(초)", elapsed, 0f, presentation.GetDuration());
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "일시 정지" : "처음부터 재생"))
            {
                if (!playing)
                    elapsed = 0f;
                playing = !playing;
            }
            if (GUILayout.Button("타격 순간"))
            {
                playing = false;
                elapsed = presentation.ImpactTime;
            }
            if (GUILayout.Button("설정 다시 읽기"))
                builtPresentation = null;
        }
        EditorGUILayout.LabelField("파랑: 발사 / 빨강: 피격 — 도착 후 남는 그림도 시간축으로 확인하세요.");
        DrawPreview(GUILayoutUtility.GetAspectRect(1.5f));
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
            preview.camera.transform.position = new Vector3(480f, 320f, -10f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.white;
            preview.camera.nearClipPlane = 0.1f;
            preview.camera.farClipPlane = 100f;
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
            bool visible = elapsed >= effect.GetStart(presentation.ImpactTime) && elapsed <= effect.GetEnd(presentation.ImpactTime);
            effects[i].gameObject.SetActive(visible);
            if (visible)
                BattleSkillGeometry.Sample(effects[i], images[i], effect, launch, hit, playerAttacks, elapsed, presentation.ImpactTime);
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
