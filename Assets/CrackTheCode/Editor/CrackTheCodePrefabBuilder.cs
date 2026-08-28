using System;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

[InitializeOnLoad]
public static class CrackTheCodePrefabBuilder
{
    private const string PrefabPath = "Assets/CrackTheCode/CrackTheCode.prefab";
    private const string BuildKey = "VrcAsStupidName.CrackTheCode.BuildVersion";
    private const int BuildVersion = 24;

    private static readonly Color Background = Hex("#050C16FF");
    private static readonly Color Panel = Hex("#0A1726F5");
    private static readonly Color PanelLight = Hex("#10263AFF");
    private static readonly Color Cyan = Hex("#28DDF5FF");
    private static readonly Color CyanSoft = Hex("#28DDF533");
    private static readonly Color Green = Hex("#27E58AFF");
    private static readonly Color White = Hex("#EAFBFFFF");
    private static readonly Color Muted = Hex("#7FA6B8FF");
    private static readonly Color Red = Hex("#FF355DFF");
    private static EditorApplication.CallbackFunction pendingPlacement;

    static CrackTheCodePrefabBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
        EditorApplication.update += UpdateCheck;
    }

    private static int _retryCount = 0;

    private static void UpdateCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling) return;
        if (EditorPrefs.GetInt(BuildKey, 0) >= BuildVersion) { EditorApplication.update -= UpdateCheck; return; }
        _retryCount++;
        if (_retryCount > 300) { EditorApplication.update -= UpdateCheck; return; }
        if (_retryCount < 60) return;
        EditorApplication.update -= UpdateCheck;
        Debug.Log("[CrackTheCode] UpdateCheck triggered build after " + _retryCount + " frames.");
        EditorPrefs.SetInt(BuildKey, BuildVersion);
        Build();
    }

    [MenuItem("Tools/Crack the Code/Rebuild Grouped Asset")]
    public static void RebuildFromMenu()
    {
        EditorPrefs.SetInt(BuildKey, 0);
        Build();
    }

    private static void BuildIfNeeded()
    {
        if (EditorPrefs.GetInt(BuildKey, 0) >= BuildVersion) return;
        EditorPrefs.SetInt(BuildKey, BuildVersion);
        Build();
    }

    private static void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject("CrackTheCode");
        try
        {
            GameObject assetRoot = CreateObject("CrackTheCode_Asset", root.transform, false);
            assetRoot.transform.localScale = Vector3.one * 0.1f;

            GameObject systems = CreateObject("GameSystems", assetRoot.transform, false);

            GameObject eventSystemObject = CreateObject("CrackTheCode_EventSystem", assetRoot.transform, false);
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = false;
            eventSystemObject.AddComponent<StandaloneInputModule>();

            GameObject canvasObject = CreateObject("UI_Canvas", assetRoot.transform, true);
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1400f, 1600f);
            canvasRect.localScale = Vector3.one * 0.01f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 25;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 3f;
            canvasObject.AddComponent<GraphicRaycaster>();
            BoxCollider collider = canvasObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1400f, 1600f, 10f);
            collider.isTrigger = true;
            AddUiShape(canvasObject);

            GameObject glow = CreateImage("OuterGlow", canvasObject.transform, CyanSoft, new Vector2(1430f, 1630f), Vector2.zero);
            AddOutline(glow, CyanSoft, new Vector2(10f, -10f));
            GameObject frame = CreateImage("Frame", canvasObject.transform, Background, new Vector2(1400f, 1600f), Vector2.zero);
            AddOutline(frame, Cyan, new Vector2(4f, -4f));
            CreateImage("TopSignal", frame.transform, Cyan, new Vector2(1280f, 8f), new Vector2(0f, 748f));
            CreateImage("LeftCircuit", frame.transform, CyanSoft, new Vector2(6f, 1360f), new Vector2(-646f, -10f));
            CreateImage("RightCircuit", frame.transform, CyanSoft, new Vector2(6f, 1360f), new Vector2(646f, -10f));

            GameObject header = CreateImage("Header", frame.transform, Panel, new Vector2(1260f, 220f), new Vector2(0f, 620f));
            AddOutline(header, CyanSoft, new Vector2(2f, -2f));
            CreateText("Title", header.transform, "CRACK THE CODE", 76f, White, TextAnchor.MiddleCenter, new Vector2(900f, 100f), new Vector2(-120f, 34f), FontStyle.Bold, 4f, font);
            CreateText("Subtitle", header.transform, "DEDUCTION  //  MEMORY  //  EXACT POSITION", 25f, Muted, TextAnchor.MiddleCenter, new Vector2(900f, 50f), new Vector2(-120f, -50f), FontStyle.Normal, 2f, font);
            CreateText("ModeText", header.transform, "LOCAL-FIRST TERMINAL", 24f, Cyan, TextAnchor.MiddleCenter, new Vector2(300f, 86f), new Vector2(450f, 35f), FontStyle.Bold, 1f, font);
            CreateText("PlayersText", header.transform, "P1  OPEN     //     P2  OPEN", 20f, Muted, TextAnchor.MiddleCenter, new Vector2(430f, 56f), new Vector2(385f, -55f), FontStyle.Normal, 0f, font);

            GameObject statusCard = CreateImage("StatusCard", frame.transform, PanelLight, new Vector2(1260f, 170f), new Vector2(0f, 408f));
            AddOutline(statusCard, CyanSoft, new Vector2(2f, -2f));
            CreateText("StatusText", statusCard.transform, "LOGIC PROTOCOL READY", 38f, White, TextAnchor.MiddleLeft, new Vector2(720f, 78f), new Vector2(-220f, 30f), FontStyle.Bold, 1f, font);
            CreateText("TimerText", statusCard.transform, "SECURE CHANNEL", 26f, Cyan, TextAnchor.MiddleRight, new Vector2(400f, 70f), new Vector2(390f, 32f), FontStyle.Bold, 1f, font);
            CreateText("AttemptsText", statusCard.transform, "EXACT POSITION MATCHES ONLY", 23f, Muted, TextAnchor.MiddleLeft, new Vector2(1100f, 52f), new Vector2(-50f, -48f), FontStyle.Normal, 1f, font);

            GameObject digitGrid = CreateImage("DigitGrid", frame.transform, Color.clear, new Vector2(1260f, 145f), new Vector2(0f, 225f));
            HorizontalLayoutGroup digitLayout = digitGrid.AddComponent<HorizontalLayoutGroup>();
            digitLayout.spacing = 14f;
            digitLayout.childAlignment = TextAnchor.MiddleCenter;
            digitLayout.childControlWidth = false;
            digitLayout.childControlHeight = false;
            digitLayout.childForceExpandWidth = false;
            digitLayout.childForceExpandHeight = false;
            for (int i = 0; i < 10; i++)
            {
                GameObject cell = CreateImage("Digit_" + i, digitGrid.transform, Hex("#0E1D2BFF"), new Vector2(112f, 132f), Vector2.zero);
                LayoutElement layout = cell.AddComponent<LayoutElement>();
                layout.preferredWidth = 112f;
                layout.preferredHeight = 132f;
                AddOutline(cell, CyanSoft, new Vector2(2f, -2f));
                CreateText("Label", cell.transform, "Ã‚Â·", 64f, White, TextAnchor.MiddleCenter, new Vector2(112f, 132f), Vector2.zero, FontStyle.Bold, 0f, font);
            }

            CreateButton("JoinButton", frame.transform, "JOIN GAME", new Vector2(620f, 138f), new Vector2(0f, 8f), Cyan, Background, 36f, font);

            GameObject keypad = CreateImage("KeypadPanel", frame.transform, Color.clear, new Vector2(1000f, 610f), new Vector2(0f, -255f));
            GridLayoutGroup grid = keypad.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 120f);
            grid.spacing = new Vector2(24f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "ERASE", "0", "CONFIRM" };
            for (int i = 0; i < labels.Length; i++)
            {
                Color accent = i == 11 ? Green : i == 9 ? Red : Cyan;
                float size = i == 9 || i == 11 ? 28f : 42f;
                CreateButton("Key_" + labels[i], keypad.transform, labels[i], new Vector2(300f, 120f), Vector2.zero, accent, PanelLight, size, font);
            }

            GameObject footer = CreateImage("Footer", frame.transform, Panel, new Vector2(1260f, 124f), new Vector2(0f, -698f));
            AddOutline(footer, CyanSoft, new Vector2(2f, -2f));
            CreateText("PersonalBestText", footer.transform, "PERSONAL BEST  LOADING...", 25f, Muted, TextAnchor.MiddleLeft, new Vector2(770f, 90f), new Vector2(-205f, 0f), FontStyle.Bold, 1f, font);
            CreateButton("ResetButton", footer.transform, "NEW ROUND", new Vector2(360f, 82f), new Vector2(410f, 0f), Green, PanelLight, 27f, font);

            keypad.SetActive(false);
            digitGrid.SetActive(false);
            GameObject.Find("ResetButton")?.SetActive(false);

            // VRChat docs: Canvas layer must be Default (NOT UI) for interaction.
            // Setting to UI layer makes it non-interactable.
            int defaultLayer = LayerMask.NameToLayer("Default");
            SetLayerRecursive(canvasObject, defaultLayer);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            string prefabPath = PrefabPath;
            pendingPlacement = () => DeferredPlaceAndWire(prefabPath);
            EditorApplication.update += pendingPlacement;
            Debug.Log("[Crack The Code] Grouped prefab created at " + PrefabPath);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void DeferredPlaceAndWire(string prefabPath)
    {
        if (!EditorSceneManager.GetActiveScene().isLoaded) return;
        EditorApplication.update -= pendingPlacement;
        pendingPlacement = null;

        GameObject existing = GameObject.Find("CrackTheCode");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;
        instance.name = "CrackTheCode";
        instance.transform.position = new Vector3(0f, 1.5f, 2f);
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Add UdonBehaviour to the scene instance and wire buttons to it.
        // This avoids the prefab serialization fileID problem.
        Transform systems = instance.transform.Find("CrackTheCode_Asset/GameSystems");
        if (systems == null)
        {
            Debug.LogError("[Crack The Code] GameSystems not found in scene instance.");
            return;
        }

        UdonBehaviour backing = Undo.AddComponent<UdonBehaviour>(systems.gameObject);
        UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/CrackTheCode/CodeBreakerGame.asset");
        if (programAsset != null)
        {
            backing.programSource = programAsset;
            SerializedObject serializedBacking = new SerializedObject(backing);
            SerializedProperty syncMethod = serializedBacking.FindProperty("_syncMethod");
            if (syncMethod != null) syncMethod.intValue = 3;
            SerializedProperty interactable = serializedBacking.FindProperty("Interactable");
            if (interactable != null) interactable.boolValue = false;
            serializedBacking.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError("[Crack The Code] Could not load CodeBreakerGame program asset.");
        }

        Transform frame = instance.transform.Find("CrackTheCode_Asset/UI_Canvas/Frame");
        if (frame != null && backing != null)
        {
            Button join = frame.Find("JoinButton")?.GetComponent<Button>();
            if (join != null) Wire(join, backing, "JoinGame");

            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "ERASE", "0", "CONFIRM" };
            string[] events = { "Digit1", "Digit2", "Digit3", "Digit4", "Digit5", "Digit6", "Digit7", "Digit8", "Digit9", "EraseDigit", "Digit0", "ConfirmEntry" };
            Transform keypad = frame.Find("KeypadPanel");
            if (keypad != null)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    Button key = keypad.Find("Key_" + labels[i])?.GetComponent<Button>();
                    if (key != null) Wire(key, backing, events[i]);
                }
            }

            Button reset = frame.Find("Footer/ResetButton")?.GetComponent<Button>();
            if (reset != null) Wire(reset, backing, "ResetGame");
        }

        // Save the scene with the wired instance. The button wiring persists as
        // scene-level overrides on the prefab instance.
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[Crack The Code] Placed and wired instance into active scene.");
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }

    private static GameObject CreateObject(string name, Transform parent, bool rectTransform)
    {
        GameObject go = rectTransform ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = 0;
        return go;
    }

    private static void CreateGroupPopup(Transform canvasParent, Font font)
    {
        // Popup panel - slides up from bottom
        GameObject popup = CreateImage("GroupPopup", canvasParent, Hex("#0A1520EE"), new Vector2(900f, 420f), new Vector2(0f, -600f));
        Image popupImg = popup.GetComponent<Image>();
        popupImg.raycastTarget = true;
        AddOutline(popup, Cyan, new Vector2(4f, -4f));

        // Glow accent bar at top
        CreateImage("AccentBar", popup.transform, Cyan, new Vector2(860f, 6f), new Vector2(0f, 195f));

        // Title
        CreateText("Title", popup.transform, "CHROMIX", 56f, White, TextAnchor.MiddleCenter, new Vector2(860f, 80f), new Vector2(0f, 130f), FontStyle.Bold, 3f, font);
        CreateText("Subtitle", popup.transform, "JOIN THE COMMUNITY", 28f, Cyan, TextAnchor.MiddleCenter, new Vector2(860f, 44f), new Vector2(0f, 70f), FontStyle.Normal, 1f, font);

        // Description
        CreateText("Description", popup.transform, "Connect with other codebreakers. Get updates, share strategies, and compete on the leaderboard.", 20f, Muted, TextAnchor.MiddleCenter, new Vector2(820f, 60f), new Vector2(0f, 10f), FontStyle.Normal, 0f, font);

        // Status text (shows "OPENING GROUP PAGE..." when clicked)
        CreateText("StatusText", popup.transform, "JOIN THE CHROMIX GROUP", 22f, CyanSoft, TextAnchor.MiddleCenter, new Vector2(820f, 36f), new Vector2(0f, -40f), FontStyle.Bold, 0f, font);

        // Join button
        GameObject joinBtn = CreateButton("JoinGroupButton", popup.transform, "JOIN GROUP", new Vector2(360f, 80f), new Vector2(-110f, -130f), Green, PanelLight, 32f, font);

        // Close button
        GameObject closeBtn = CreateButton("CloseButton", popup.transform, "MAYBE LATER", new Vector2(260f, 80f), new Vector2(250f, -130f), Hex("#2A3A4AFF"), PanelLight, 24f, font);
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0.01f;
        return go;
    }

    private static Text CreateText(string name, Transform parent, string value, float fontSize, Color color, TextAnchor alignment, Vector2 size, Vector2 position, FontStyle style, float spacing, Font font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.fontSize = Mathf.RoundToInt(fontSize);
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.font = font;
        return text;
    }

    private static GameObject CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 position, Color accent, Color baseColor, float fontSize, Font font)
    {
        GameObject go = CreateImage(name, parent, baseColor, size, position);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.3f, 0.35f, 0.4f, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        AddOutline(go, accent, new Vector2(3f, -3f));
        CreateText("Label", go.transform, label, fontSize, accent, TextAnchor.MiddleCenter, size - new Vector2(20f, 12f), Vector2.zero, FontStyle.Bold, 2f, font);
        return go;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void AddUiShape(GameObject target)
    {
        target.AddComponent<VRCUiShape>();
    }

    private static void Wire(Button button, UdonBehaviour udon, string eventName)
    {
        if (button == null || udon == null) return;
        // Use AddStringPersistentListener to properly create a persistent call
        // that invokes udon.SendCustomEvent(eventName) with the string argument.
        UnityAction<string> action = new UnityAction<string>(udon.SendCustomEvent);
        UnityEventTools.AddStringPersistentListener(button.onClick, action, eventName);
    }

    private static Color Hex(string value)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
    }
}
public class CrackTheCodeAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path.StartsWith("Assets/CrackTheCode/") && path.EndsWith(".cs"))
            {
                CrackTheCodePrefabBuilder.RebuildFromMenu();
                return;
            }
        }
    }
}