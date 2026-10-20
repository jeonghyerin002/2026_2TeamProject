using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>모든 씬 진입 시 검은 화면을 걷어내는 공통 연출</summary>
[DisallowMultipleComponent]
public sealed class SceneFadeIn : MonoBehaviour
{
    // 검은 화면에서 완전히 드러날 때까지 걸리는 실제 시간(초)
    private const float FadeDuration = 0.8f;
    // 이 오브젝트가 소유하고 씬 사이에 유지하는 화면과 실행 중인 코루틴
    private Canvas overlayCanvas;
    private Image overlayImage;
    private Coroutine fadeRoutine;
    // 현재 Play 세션의 전환 소유자와 중복 로딩 방지 상태
    private static SceneFadeIn instance;
    private bool isTransitioning;

    // 씬 교체 요청부터 퇴장·로딩·진입 완료까지 다른 전환을 차단
    public static bool IsTransitioning => instance != null && instance.isTransitioning;

    // Domain Reload가 꺼진 반복 Play에서도 이전 소유자 참조를 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        instance = null;
    }

    /// <summary>전환 하나만 수락하며 콜백은 암전 후 로딩 직전과 로딩 직후 실행</summary>
    public static bool LoadScene(string sceneName, System.Action beforeLoad = null, System.Action afterLoad = null)
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
        instance.fadeRoutine = instance.StartCoroutine(instance.Transition(sceneName, beforeLoad, afterLoad));
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
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = false;
        }
        isTransitioning = false;
    }

    // 연속 전환 시 이전 연출을 취소하고 새 씬의 첫 화면을 검게 덮음
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
    private IEnumerator Transition(string sceneName, System.Action beforeLoad, System.Action afterLoad)
    {
        try
        {
            overlayCanvas.enabled = true;
            yield return FadeTo(1f);
            beforeLoad?.Invoke();
            // 완전히 검게 덮인 상태에서만 실제 씬 로딩을 시작
            var operation = SceneManager.LoadSceneAsync(sceneName);
            yield return operation;
            afterLoad?.Invoke();
            yield return FadeIn();
        }
        finally
        {
            // 콜백 예외나 비활성화로 중단돼도 검은 화면과 전환 잠금을 남기지 않음
            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = false;
            }
            isTransitioning = false;
            fadeRoutine = null;
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
