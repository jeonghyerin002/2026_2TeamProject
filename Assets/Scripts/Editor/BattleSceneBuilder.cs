using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>편집 가능한 기본 Unity UI로 실제 전투 씬을 생성한다</summary>
public static class BattleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Battle.unity";
    private const string DataPath = "Assets/Data/BattleScene";
    private static readonly Color Ink = new Color32(43, 62, 65, 255);
    private static readonly Color Paper = new Color32(250, 248, 221, 255);
    private static TMP_FontAsset font;

    // 기존 씬을 보존하면서 새 전투 씬을 생성한다
    [MenuItem("Tools/Battle/Create Battle Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Battle.unity already exists. Rename it before generating another scene.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/goorm-sans-bold SDF.asset");
        if (font == null)
            throw new InvalidOperationException("Battle scene requires the existing Korean font.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Scene previous = SceneManager.GetActiveScene();
        bool additive = !Application.isBatchMode && !string.IsNullOrEmpty(previous.path);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        try
        {
            Build();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Battle scene created: " + ScenePath);
        }
        finally
        {
            if (additive)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    // 카메라와 캔버스, 상태창, 메뉴 및 데이터 참조를 생성한다
    private static void Build()
    {
        GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        camera.transform.position = new Vector3(0, 0, -10);
        GameObject events = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject canvasObject = new("Battle Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 640);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        RectTransform screen = Rect("Battle Frame", canvasObject.transform, 0, 0, 960, 640);
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(0.5f, 0.5f);
        screen.anchoredPosition = Vector2.zero;
        Box("Backdrop", screen, 0, 0, 960, 640, Ink);
        Box("Sky", screen, 8, 8, 944, 442, new Color32(223, 236, 218, 255));
        Box("Distant Field", screen, 8, 198, 944, 252, new Color32(209, 226, 185, 255));
        Box("Horizon", screen, 8, 196, 944, 6, new Color32(176, 204, 159, 255));
        for (int i = 0; i < 7; i++)
            Box("Field Stripe", screen, 8, 235 + i * 30, 944, 8, new Color32(200, 219, 176, 255));
        Text("Battle Caption", screen, 32, 20, 450, 26, "AETHER  /  BATTLE", 18, Ink);
        TMP_Text turn = Text("Turn", screen, 790, 20, 135, 26, "TURN 01", 18, Ink);
        Platform(screen, "Enemy Platform", 590, 208, 285, 73);
        Platform(screen, "Player Platform", 65, 367, 365, 82);
        RectTransform enemyPosition = Marker(screen, "Enemy Position", 694, 158, 70, new Color32(207, 125, 91, 255), "ENEMY");
        RectTransform playerPosition = Marker(screen, "Player Position", 199, 295, 92, new Color32(83, 142, 145, 255), "PLAYER");

        RectTransform enemyCard = Panel("Enemy HUD", screen, 43, 67, 395, 122);
        TMP_Text enemyInfo = Text("Name and Level", enemyCard, 20, 12, 355, 35, "Enemy1   Lv.20", 27, Ink);
        Image enemyFill = Health(enemyCard);
        TMP_Text enemyHealth = Text("HP Value", enemyCard, 185, 83, 185, 27, "70 / 70", 21, Ink);
        RectTransform playerCard = Panel("Player HUD", screen, 516, 301, 403, 130);
        TMP_Text playerInfo = Text("Name and Level", playerCard, 20, 12, 365, 35, "Player   Lv.20", 27, Ink);
        Image playerFill = Health(playerCard);
        TMP_Text playerHealth = Text("HP Value", playerCard, 185, 83, 185, 27, "100 / 100", 21, Ink);

        RectTransform dialogue = Panel("Dialogue", screen, 12, 460, 500, 164);
        TMP_Text message = Text("Message", dialogue, 25, 22, 448, 89, "Player, 무엇을 할까?", 28, Ink);
        TMP_Text detail = Text("Details", dialogue, 25, 115, 448, 42, "A / D 선택    F 결정    E 뒤로", 16, Ink);
        RectTransform menu = Panel("Command Panel", screen, 520, 460, 428, 164);
        Button[] choices = new Button[4];
        TMP_Text[] labels = new TMP_Text[4];
        string[] names = { "싸운다", "파티", "빌드 정보", "도망" };
        for (int i = 0; i < 4; i++)
        {
            choices[i] = Choice(menu, 13 + i % 2 * 202, 12 + i / 2 * 70, 198, 66, names[i], out labels[i]);
            choices[i].navigation = new Navigation { mode = Navigation.Mode.None };
        }
        Button back = Choice(screen, 810, 430, 130, 25, "E  뒤로", out TMP_Text backLabel);
        backLabel.fontSize = 16;
        back.navigation = new Navigation { mode = Navigation.Mode.None };

        GameObject host = new("Battle", typeof(BattleSystem), typeof(BattleFlow), typeof(BattleSceneUI));
        BattleSystem battle = host.GetComponent<BattleSystem>();
        BattleFlow flow = host.GetComponent<BattleFlow>();
        BattleSceneUI ui = host.GetComponent<BattleSceneUI>();
        SetupData(battle);
        Set(flow, "battleSystem", battle);
        Set(flow, "battleText", message);
        Set(flow, "textDuration", 1.05f);
        Set(flow, "animationDuration", 0.5f);
        Set(ui, "battle", battle);
        Set(ui, "flow", flow);
        Set(ui, "message", message);
        Set(ui, "playerInfo", playerInfo);
        Set(ui, "enemyInfo", enemyInfo);
        Set(ui, "playerHealth", playerHealth);
        Set(ui, "enemyHealth", enemyHealth);
        Set(ui, "detail", detail);
        Set(ui, "turnLabel", turn);
        Set(ui, "playerFill", playerFill);
        Set(ui, "enemyFill", enemyFill);
        Set(ui, "playerPosition", playerPosition);
        Set(ui, "enemyPosition", enemyPosition);
        Set(ui, "back", back);
        SetArray(ui, "choices", choices);
        SetArray(ui, "labels", labels);
        UnityEngine.Events.UnityEvent changed = (UnityEngine.Events.UnityEvent)typeof(BattleFlow).GetField("onStateChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(flow);
        UnityEventTools.AddPersistentListener(changed, ui.Refresh);
        EditorUtility.SetDirty(flow);
    }

    // 기존 SO를 복제한 씬 전용 빌드에 네 기술을 연결한다
    private static void SetupData(BattleSystem battle)
    {
        Directory.CreateDirectory(DataPath);
        AssetDatabase.Refresh();
        AetherData player = Copy<AetherData>("Assets/Data/Items/Aether_40001.asset", "PlayerAether");
        SetArray(player, "skills", new[] { Skill(50001), Skill(50004), Skill(50006) });
        AetherData enemy = Copy<AetherData>("Assets/Data/Items/Aether_40002.asset", "EnemyAether");
        SetArray(enemy, "skills", new[] { Skill(50002), Skill(50008), Skill(50005) });
        WeaponData weapon = Copy<WeaponData>("Assets/Data/Items/Item_20001.asset", "TrainingWeapon");
        SetArray(weapon, "skills", new[] { Skill(50005) });
        Set(battle, "playerCharacter", AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Character_1.asset"));
        Set(battle, "enemyCharacter", AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Character_2.asset"));
        Set(battle, "playerAether", player);
        Set(battle, "enemyAether", enemy);
        Set(battle, "playerWeapon", weapon);
        Set(battle, "enemyWeapon", weapon);
        Set(battle, "playerLevel", 20);
        Set(battle, "enemyLevel", 20);
    }

    // 원본 데이터의 능력치를 보존한 씬 전용 SO를 생성한다
    private static T Copy<T>(string source, string name) where T : ScriptableObject
    {
        string path = $"{DataPath}/{name}.asset";
        T result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (result != null)
            return result;
        result = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>(source));
        result.name = name;
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    // 기존 기술 원본을 조회한다
    private static SkillData Skill(int id)
    {
        return AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/Data/Skills/Skill_{id}.asset");
    }

    // 위쪽 왼쪽 기준으로 편집 가능한 RectTransform을 생성한다
    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // 외부 이미지 없이 단색 Unity Image를 생성한다
    private static Image Box(string name, Transform parent, float x, float y, float width, float height, Color color)
    {
        Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 픽셀 계단 형태의 전투 발판을 생성한다
    private static void Platform(Transform parent, string name, float x, float y, float width, float height)
    {
        RectTransform root = Rect(name, parent, x, y, width, height);
        for (int i = 0; i < 9; i++)
        {
            float inset = Mathf.Abs(i - 4) * Mathf.Abs(i - 4) * width / 85f;
            Box("Ground", root, inset, i * height / 9f, width - inset * 2, height / 9f + 1,
                i == 8 ? new Color32(126, 158, 117, 255) : new Color32(168, 194, 135, 255));
        }
        Box("Ground Highlight", root, width * 0.23f, 16, width * 0.54f, 6, new Color32(195, 213, 155, 255));
    }

    // 캐릭터 리소스를 대체하는 단순 사각형과 위치 라벨을 생성한다
    private static RectTransform Marker(Transform parent, string name, float x, float y, float size, Color color, string label)
    {
        RectTransform root = Rect(name, parent, x, y, size, size);
        Box("Outline", root, 0, 0, size, size, Ink);
        Box("Unity Image", root, 5, 5, size - 10, size - 10, color);
        Box("Highlight", root, 10, 10, size - 20, 5, new Color(1, 1, 1, 0.35f));
        TMP_Text text = Text("Position Label", root, 0, size * 0.36f, size, 25, label, 13, Paper);
        text.alignment = TextAlignmentOptions.Center;
        return root;
    }

    // 이중 테두리의 고전 RPG 패널을 생성한다
    private static RectTransform Panel(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform root = Rect(name, parent, x, y, width, height);
        Box("Shadow", root, 4, 5, width, height, new Color32(91, 114, 96, 255));
        Box("Border", root, 0, 0, width, height, Ink);
        Box("Trim", root, 4, 4, width - 8, height - 8, new Color32(141, 163, 136, 255));
        Box("Paper", root, 8, 8, width - 16, height - 16, Paper);
        return root;
    }

    // 앵커 너비로 표시되는 HP 막대를 생성한다
    private static Image Health(Transform parent)
    {
        Text("HP Label", parent, 25, 53, 49, 24, "HP", 20, Ink);
        Box("HP Border", parent, 78, 57, 291, 20, Ink);
        Image track = Box("HP Track", parent, 81, 60, 285, 14, new Color32(204, 211, 183, 255));
        Image fill = Box("HP Fill", track.transform, 0, 0, 0, 0, new Color32(72, 184, 120, 255));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        return fill;
    }

    // 원본 글꼴이 포함된 기존 한글 폰트로 문구를 생성한다
    private static TMP_Text Text(string name, Transform parent, float x, float y, float width, float height, string value, float size, Color color)
    {
        TextMeshProUGUI text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    // 선택과 비활성 상태가 구분되는 메뉴 버튼을 생성한다
    private static Button Choice(Transform parent, float x, float y, float width, float height, string caption, out TMP_Text label)
    {
        Image image = Box(caption, parent, x, y, width, height, Color.white);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Paper;
        colors.highlightedColor = new Color32(219, 230, 178, 255);
        colors.selectedColor = new Color32(210, 225, 166, 255);
        colors.pressedColor = new Color32(174, 199, 134, 255);
        colors.disabledColor = new Color32(207, 207, 191, 255);
        colors.fadeDuration = 0f;
        button.colors = colors;
        label = Text("Label", image.transform, 8, 3, width - 16, height - 6, caption, 24, Ink);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return button;
    }

    // 직렬화된 참조와 값을 설정한다
    private static void Set(UnityEngine.Object target, string name, object value)
    {
        SerializedObject data = new(target);
        SerializedProperty property = data.FindProperty(name);
        if (value is int integer)
            property.intValue = integer;
        else if (value is float number)
            property.floatValue = number;
        else
            property.objectReferenceValue = value as UnityEngine.Object;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // Inspector 배열의 객체 연결을 저장한다
    private static void SetArray(UnityEngine.Object target, string name, UnityEngine.Object[] values)
    {
        SerializedObject data = new(target);
        SerializedProperty array = data.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
