using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>모든 씬 진입 시 검은 화면을 걷어내는 공통 연출</summary>
[DisallowMultipleComponent]
public sealed class SceneFadeIn : MonoBehaviour
{
    // 검은 화면에서 완전히 드러날 때까지 걸리는 실제 시간(초)
    private const float FadeDuration = 0.65f;
    private const float TextCharacterInterval = 0.08f; // 요약 문장의 글자별 출력 간격(실제 초)
    private const float TextFadeOutDuration = 1.5f; // 요약 전체가 사라지는 시간(실제 초)
    // 이 오브젝트가 소유하고 씬 사이에 유지하는 화면과 실행 중인 코루틴
    private Canvas overlayCanvas;
    private Image overlayImage;
    private Coroutine fadeRoutine;
    // 현재 Play 세션의 전환 소유자와 중복 로딩 방지 상태
    private static SceneFadeIn instance;
    private bool isTransitioning;
    private bool hasLoadedFirstScene; // 앱 실행 후 첫 씬의 진입 연출 판단 여부

    private TextMeshProUGUI transitionLabel; // 암전 상태에서만 표시하는 임시 요약 문장

    // 씬 교체 요청부터 퇴장·로딩·진입 완료까지 다른 전환을 차단
    public static bool IsTransitioning => instance != null && instance.isTransitioning;

    // Domain Reload가 꺼진 반복 Play에서도 이전 소유자 참조를 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        instance = null;
    }

    // 전환 하나만 수락하며 선택적 문장은 암전 후 씬 로딩 전에 표시
    public static bool LoadScene(string sceneName, System.Action beforeLoad = null, System.Action afterLoad = null,
        string transitionText = null, TMP_FontAsset transitionFont = null, float textDuration = 0f)
    {
        if (instance == null || !instance.isActiveAndEnabled || IsTransitioning)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneFadeIn: 빌드 씬 목록에서 '{sceneName}'을 확인하세요.", instance);
            return false;
        }

        // 첫 yield 이전에 잠가 같은 프레임의 다른 요청도 거절
        instance.isTransitioning = true;
        if (instance.fadeRoutine != null)
        {
            instance.StopCoroutine(instance.fadeRoutine);
        }
        instance.fadeRoutine = instance.StartCoroutine(instance.Transition(sceneName, beforeLoad, afterLoad, transitionText, transitionFont, textDuration));
        return true;
    }

    // 씬을 직접 실행해도 첫 렌더링 전에 공통 화면을 구성
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // 설정 중 OnEnable이 호출되지 않도록 비활성 상태에서 구성
        var root = new GameObject("Scene Fade In");
        root.SetActive(false);
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        // 별도 텍스처 없이 화면 전체를 덮으며 입력은 기존 씬이 계속 처리
        var panel = new GameObject("Black Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = panel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var fade = root.AddComponent<SceneFadeIn>();
        fade.overlayCanvas = canvas;
        fade.overlayImage = image;
        instance = fade;
        root.SetActive(true);
    }

    // 활성 기간에만 로딩 완료를 구독하며 외부에서 잘못 추가한 컴포넌트는 차단
    private void OnEnable()
    {
        if (overlayCanvas == null || overlayImage == null)
        {
            Debug.LogError("SceneFadeIn: 자동 생성 전용 컴포넌트입니다. 씬에 직접 추가한 컴포넌트를 제거하세요.", this);
            enabled = false;
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // 비활성화와 Play 종료 시 구독 및 소유한 연출을 함께 정리
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        ClearTransitionLabel();
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = false;
        }
        isTransitioning = false;
    }

    // 연속 전환 시 이전 연출을 취소하고 새 씬의 첫 화면을 검게 덮음
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 최초 실행 화면이 시작 메뉴인 경우에만 검은 화면 없이 즉시 표시
        if (!hasLoadedFirstScene)
        {
            hasLoadedFirstScene = true;
            if (scene.name == "StartMenu")
            {
                overlayImage.color = new Color(0f, 0f, 0f, 0f);
                overlayCanvas.enabled = false;
                return;
            }
        }

        // 소유한 로딩은 코루틴에서 콜백과 진입 연출까지 순서대로 처리
        if (isTransitioning)
        {
            return;
        }
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        overlayImage.color = Color.black;
        overlayCanvas.enabled = true;
        fadeRoutine = StartCoroutine(FadeIn());
    }

    // 지속 오브젝트가 전체 전환을 소유해 출발 씬이 파괴되어도 연출을 완료
    private IEnumerator Transition(string sceneName, System.Action beforeLoad, System.Action afterLoad,
        string transitionText, TMP_FontAsset transitionFont, float textDuration)
    {
        try
        {
            overlayCanvas.enabled = true;
            yield return FadeTo(1f);
            if (!string.IsNullOrWhiteSpace(transitionText))
            {
                ShowTransitionLabel(transitionText, transitionFont);
                yield return AnimateTransitionLabel(textDuration);
                ClearTransitionLabel();
                // 텍스트가 없는 검은 화면을 최소 한 프레임 표시한 뒤 로딩
                yield return null;
            }
            beforeLoad?.Invoke();
            // 완전히 검게 덮인 상태에서만 실제 씬 로딩을 시작
            var operation = SceneManager.LoadSceneAsync(sceneName);
            yield return operation;
            afterLoad?.Invoke();
            yield return FadeIn();
        }
        finally
        {
            ClearTransitionLabel();
            // 콜백 예외나 비활성화로 중단돼도 검은 화면과 전환 잠금을 남기지 않음
            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = false;
            }
            isTransitioning = false;
            fadeRoutine = null;
        }
    }

    // 암전 오버레이 위에 화면 크기에 맞춘 중앙 정렬 문장을 배치
    private void ShowTransitionLabel(string text, TMP_FontAsset font)
    {
        var label = new GameObject("Transition Summary", typeof(RectTransform), typeof(CanvasRenderer));
        label.transform.SetParent(overlayCanvas.transform, false);
        transitionLabel = label.AddComponent<TextMeshProUGUI>();
        var rect = transitionLabel.rectTransform;
        rect.anchorMin = new Vector2(0.05f, 0.25f);
        rect.anchorMax = new Vector2(0.95f, 0.75f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        transitionLabel.font = font;
        // 폰트의 기본 줄 높이를 기준으로 세로 간격을 1.5배 적용
        transitionLabel.richText = true;
        transitionLabel.text = "<line-height=150%>" + text + "</line-height>";
        transitionLabel.color = Color.white;
        transitionLabel.alignment = TextAlignmentOptions.Center;
        // 지정한 줄바꿈을 유지하면서 좁은 화면에서는 글자 크기를 줄여 표시
        transitionLabel.textWrappingMode = TextWrappingModes.NoWrap;
        transitionLabel.enableAutoSizing = true;
        transitionLabel.fontSizeMin = 1f;
        transitionLabel.fontSizeMax = Screen.height * (32f / 1080f);
        transitionLabel.raycastTarget = false;
    }

    // 원래 줄 배치를 유지하며 글자를 순서대로 출력한 뒤 전체 문장을 페이드 아웃
    private IEnumerator AnimateTransitionLabel(float holdDuration)
    {
        transitionLabel.ForceMeshUpdate();
        // 마지막 글자가 출력된 시점부터 유지 시간을 계산
        var revealDuration = transitionLabel.textInfo.characterCount * TextCharacterInterval;
        var fadeOutStart = revealDuration + Mathf.Max(0f, holdDuration);
        var totalDuration = fadeOutStart + TextFadeOutDuration;
        var elapsed = 0f;
        var startedFrame = Time.frameCount;
        while (elapsed < totalDuration)
        {
            // 시작 프레임의 입력과 길게 누른 입력으로 여러 줄이 연속 완성되는 것을 방지
            if (Time.frameCount > startedFrame && elapsed < revealDuration && IsSummaryAdvancePressed())
            {
                elapsed = CompleteCurrentSummaryLine(elapsed);
            }
            ApplyTransitionTextAlpha(elapsed, fadeOutStart);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        ApplyTransitionTextAlpha(totalDuration, fadeOutStart);
    }

    // 마우스 이동과 스크롤을 제외하고 키 또는 마우스 버튼의 새 입력을 확인
    private static bool IsSummaryAdvancePressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }
        var mouse = Mouse.current;
        return mouse != null && (mouse.leftButton.wasPressedThisFrame ||
            mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame ||
            mouse.forwardButton.wasPressedThisFrame || mouse.backButton.wasPressedThisFrame);
    }

    // 첫 미출력 글자가 속한 줄의 끝으로 출력 시점만 이동하여 다음 줄과 유지 시간을 보존
    private float CompleteCurrentSummaryLine(float elapsed)
    {
        var info = transitionLabel.textInfo;
        for (var index = 0; index < info.characterCount; index++)
        {
            if (!info.characterInfo[index].isVisible || elapsed >= (index + 1) * TextCharacterInterval)
            {
                continue;
            }
            var line = info.characterInfo[index].lineNumber;
            var lastCharacter = info.lineInfo[line].lastCharacterIndex;
            return Mathf.Max(elapsed, (lastCharacter + 1) * TextCharacterInterval);
        }
        return info.characterCount * TextCharacterInterval;
    }

    // 글자별 출력 시점과 공통 퇴장 시점으로 각 글자의 정점 투명도를 계산
    private void ApplyTransitionTextAlpha(float elapsed, float fadeOutStart)
    {
        // 해상도 변경에 따른 텍스트 재배치도 현재 프레임의 줄 정보에 반영
        transitionLabel.ForceMeshUpdate();
        var info = transitionLabel.textInfo;
        var fadeOutAlpha = 1f - Mathf.Clamp01((elapsed - fadeOutStart) / TextFadeOutDuration);
        for (var index = 0; index < info.characterCount; index++)
        {
            var character = info.characterInfo[index];
            if (!character.isVisible)
            {
                continue;
            }
            var visibleAlpha = elapsed >= (index + 1) * TextCharacterInterval ? 1f : 0f;
            var alpha = (byte)Mathf.RoundToInt(255f * visibleAlpha * fadeOutAlpha);
            var colors = info.meshInfo[character.materialReferenceIndex].colors32;
            for (var vertex = 0; vertex < 4; vertex++)
            {
                colors[character.vertexIndex + vertex].a = alpha;
            }
        }
        transitionLabel.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    // 정상 완료와 중단 모두에서 요약 문장이 다음 씬에 남지 않도록 정리
    private void ClearTransitionLabel()
    {
        if (transitionLabel != null)
        {
            transitionLabel.gameObject.SetActive(false);
            Destroy(transitionLabel.gameObject);
            transitionLabel = null;
        }
    }

    // 현재 투명도에서 목표 투명도까지 실제 시간 기준으로 보간
    private IEnumerator FadeTo(float targetAlpha)
    {
        // 진입 도중 이동해도 투명도가 튀지 않도록 현재 값에서 시작
        var initialAlpha = overlayImage.color.a;
        var elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            overlayImage.color = new Color(0f, 0f, 0f,
                Mathf.Lerp(initialAlpha, targetAlpha, Mathf.Clamp01(elapsed / FadeDuration)));
        }
    }

    // 첫 프레임은 검은색으로 표시하고 일시정지와 무관하게 투명도를 감소
    private IEnumerator FadeIn()
    {
        // 씬의 Start 초기화가 반영된 뒤 실제 시간으로 연출 진행
        yield return null;
        yield return FadeTo(0f);

        overlayCanvas.enabled = false;
        fadeRoutine = null;
    }
}
