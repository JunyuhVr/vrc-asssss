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
public static class ChromixTriviaBuilder
{
    private const string PrefabPath = "Assets/ChromixTrivia/ChromixTrivia.prefab";
    private const string BuildKey = "Chromix.Trivia.BuildVersion";
    private const int BuildVersion = 9;

    private static EditorApplication.CallbackFunction pendingPlacement;

    static ChromixTriviaBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Tools/Chromix Trivia/Rebuild Trivia Asset")]
    public static void RebuildFromMenu()
    {
        EditorPrefs.SetInt(BuildKey, BuildVersion);
        Build();
    }

    private static void BuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetInt(BuildKey, 0) >= BuildVersion) return;
        EditorPrefs.SetInt(BuildKey, BuildVersion);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Build();
        };
    }

    // --- Colors ---
    private static readonly Color Cyan = Hex("#28D8F5FF");
    private static readonly Color Magenta = Hex("#F528D8FF");
    private static readonly Color Green = Hex("#2AE088FF");
    private static readonly Color Orange = Hex("#F5A028FF");
    private static readonly Color Background = Hex("#050C16FF");
    private static readonly Color Panel = Hex("#0E1D2BFF");
    private static readonly Color PanelLight = Hex("#142838FF");
    private static readonly Color White = Hex("#F0FBFFFF");
    private static readonly Color Muted = Hex("#6B8499FF");
    private static readonly Color Dim = Hex("#1A2A3AFF");

    private static void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject("ChromixTrivia");
        try
        {
            GameObject assetRoot = CreateObject("ChromixTrivia_Asset", root.transform, false);
            assetRoot.transform.localScale = Vector3.one * 0.1f;

            GameObject systems = CreateObject("TriviaSystems", assetRoot.transform, false);

            GameObject eventSystemObject = CreateObject("ChromixTrivia_EventSystem", assetRoot.transform, false);
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = false;
            eventSystemObject.AddComponent<StandaloneInputModule>();

            // Canvas
            GameObject canvasObject = CreateObject("UI_Canvas", assetRoot.transform, true);
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1600f, 1800f);
            canvasRect.localScale = Vector3.one * 0.01f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 3f;
            canvasObject.AddComponent<GraphicRaycaster>();
            BoxCollider collider = canvasObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1600f, 1800f, 10f);
            collider.isTrigger = true;
            canvasObject.AddComponent<VRCUiShape>();

            // Outer glow
            CreateImage("OuterGlow", canvasObject.transform, Hex("#0A1520CC"), new Vector2(1560f, 1760f), new Vector2(0f, 0f));

            // Main frame
            GameObject frame = CreateImage("Frame", canvasObject.transform, Background, new Vector2(1500f, 1700f), new Vector2(0f, 0f));
            AddOutline(frame, Cyan, new Vector2(4f, -4f));

            // --- Header ---
            GameObject header = CreateImage("Header", frame.transform, Panel, new Vector2(1420f, 160f), new Vector2(0f, 760f));
            AddOutline(header, Cyan, new Vector2(2f, -2f));
            CreateText("Title", header.transform, "CHROMIX TRIVIA", 52f, White, TextAnchor.MiddleCenter, new Vector2(700f, 100f), new Vector2(-200f, 0f), FontStyle.Bold, 3f, font);
            CreateText("CategoryText", header.transform, "READY", 24f, Cyan, TextAnchor.MiddleCenter, new Vector2(300f, 50f), new Vector2(450f, 20f), FontStyle.Normal, 1f, font);
            CreateText("RoundText", header.transform, "ROUND 0 / 15", 24f, Muted, TextAnchor.MiddleCenter, new Vector2(300f, 50f), new Vector2(450f, -30f), FontStyle.Bold, 1f, font);

            // --- Status Card ---
            GameObject statusCard = CreateImage("StatusCard", frame.transform, PanelLight, new Vector2(1420f, 80f), new Vector2(0f, 640f));
            AddOutline(statusCard, Cyan, new Vector2(2f, -2f));
            CreateText("StatusText", statusCard.transform, "JOIN TO PLAY!", 28f, White, TextAnchor.MiddleCenter, new Vector2(1400f, 60f), Vector2.zero, FontStyle.Bold, 1f, font);

            // --- Question Panel ---
            GameObject questionPanel = CreateImage("QuestionPanel", frame.transform, Panel, new Vector2(1420f, 200f), new Vector2(0f, 480f));
            AddOutline(questionPanel, Cyan, new Vector2(2f, -2f));
            Text qText = CreateText("QuestionText", questionPanel.transform, "", 32f, White, TextAnchor.MiddleCenter, new Vector2(1380f, 160f), Vector2.zero, FontStyle.Bold, 1f, font);
            qText.horizontalOverflow = HorizontalWrapMode.Wrap;
            qText.verticalOverflow = VerticalWrapMode.Overflow;

            // --- Answer Panel ---
            GameObject answerPanel = CreateImage("AnswerPanel", frame.transform, Dim, new Vector2(1420f, 500f), new Vector2(0f, 120f));
            answerPanel.GetComponent<Image>().raycastTarget = false;

            string[] answerLabels = { "A", "B", "C", "D" };
            for (int i = 0; i < 4; i++)
            {
                float y = 180f - i * 120f;
                GameObject ans = CreateImage("Answer_" + i, answerPanel.transform, PanelLight, new Vector2(1380f, 100f), new Vector2(0f, y));
                AddOutline(ans, Cyan, new Vector2(2f, -2f));
                Button btn = ans.AddComponent<Button>();
                btn.targetGraphic = ans.GetComponent<Image>();
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                colors.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
                colors.fadeDuration = 0.08f;
                btn.colors = colors;
                CreateText("Label", ans.transform, answerLabels[i] + ". ", 28f, Cyan, TextAnchor.MiddleLeft, new Vector2(1340f, 80f), new Vector2(0f, 0f), FontStyle.Bold, 1f, font);
                ans.SetActive(false);
            }

            // --- Player Panels (4 players) ---
            Color[] playerColors = { Cyan, Magenta, Green, Orange };
            for (int i = 0; i < 4; i++)
            {
                int p = i + 1;
                float x = -510f + i * 340f;
                GameObject panel = CreateImage("P" + p + "Panel", frame.transform, Dim, new Vector2(300f, 260f), new Vector2(x, -450f));
                AddOutline(panel, playerColors[i], new Vector2(3f, -3f));

                CreateText("NameText", panel.transform, "OPEN SLOT", 22f, Muted, TextAnchor.MiddleCenter, new Vector2(280f, 40f), new Vector2(0f, 90f), FontStyle.Bold, 1f, font);
                CreateText("ScoreText", panel.transform, "--", 48f, playerColors[i], TextAnchor.MiddleCenter, new Vector2(280f, 80f), new Vector2(0f, 20f), FontStyle.Bold, 2f, font);

                // Join button
                GameObject joinBtn = CreateButton("JoinBtn", panel.transform, "JOIN", new Vector2(260f, 60f), new Vector2(0f, -60f), playerColors[i], PanelLight, 24f, font);

                // Buzz button
                GameObject buzzBtn = CreateButton("BuzzBtn", panel.transform, "BUZZ!", new Vector2(260f, 60f), new Vector2(0f, -120f), playerColors[i], PanelLight, 28f, font);
                buzzBtn.SetActive(false);
            }

            // --- Controls ---
            GameObject controls = CreateImage("Controls", frame.transform, Panel, new Vector2(1420f, 100f), new Vector2(0f, -760f));
            AddOutline(controls, Cyan, new Vector2(2f, -2f));
            CreateButton("StartBtn", controls.transform, "START GAME", new Vector2(400f, 70f), new Vector2(-250f, 0f), Green, PanelLight, 30f, font);
            CreateButton("ResetBtn", controls.transform, "RESET", new Vector2(400f, 70f), new Vector2(250f, 0f), Hex("#2A3A4AFF"), PanelLight, 30f, font);

            // Decorative circuit lines
            CreateImage("LeftCircuit", frame.transform, Cyan, new Vector2(4f, 1600f), new Vector2(-720f, 0f));
            CreateImage("RightCircuit", frame.transform, Cyan, new Vector2(4f, 1600f), new Vector2(720f, 0f));
            CreateImage("TopSignal", frame.transform, Cyan, new Vector2(1400f, 4f), new Vector2(0f, 830f));

            // Layer must be Default for VRChat interaction
            int defaultLayer = LayerMask.NameToLayer("Default");
            SetLayerRecursive(canvasObject, defaultLayer);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            string prefabPath = PrefabPath;
            pendingPlacement = () => DeferredPlaceAndWire(prefabPath);
            EditorApplication.update += pendingPlacement;
            Debug.Log("[ChromixTrivia] Grouped prefab created at " + PrefabPath);
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

        GameObject existing = GameObject.Find("ChromixTrivia");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;
        instance.name = "ChromixTrivia";
        instance.transform.position = new Vector3(-3f, 1.5f, 2f);
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Transform systems = instance.transform.Find("ChromixTrivia_Asset/TriviaSystems");
        if (systems == null)
        {
            Debug.LogError("[ChromixTrivia] TriviaSystems not found.");
            return;
        }

        UdonBehaviour backing = Undo.AddComponent<UdonBehaviour>(systems.gameObject);
        UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/ChromixTrivia/ChromixTriviaGame.asset");
        if (programAsset == null)
        {
            // Create the UdonSharp program asset manually
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/ChromixTrivia/ChromixTriviaGame.cs");
            if (script != null)
            {
                programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                programAsset.sourceCsScript = script;
                AssetDatabase.CreateAsset(programAsset, "Assets/ChromixTrivia/ChromixTriviaGame.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ChromixTrivia] Created UdonSharp program asset manually.");
            }
            else
            {
                Debug.LogError("[ChromixTrivia] Could not find ChromixTriviaGame.cs.");
                return;
            }
        }

        backing.programSource = programAsset;
        SerializedObject serializedBacking = new SerializedObject(backing);
        SerializedProperty syncMethod = serializedBacking.FindProperty("_syncMethod");
        if (syncMethod != null) syncMethod.intValue = 3; // Manual
        SerializedProperty interactable = serializedBacking.FindProperty("Interactable");
        if (interactable != null) interactable.boolValue = false;
        serializedBacking.ApplyModifiedPropertiesWithoutUndo();

        // Wire all buttons
        Transform frame = instance.transform.Find("ChromixTrivia_Asset/UI_Canvas/Frame");
        if (frame != null)
        {
            // Player join buttons
            for (int i = 1; i <= 4; i++)
            {
                Button joinBtn = frame.Find("P" + i + "Panel/JoinBtn")?.GetComponent<Button>();
                if (joinBtn != null) Wire(joinBtn, backing, "JoinP" + i);

                Button buzzBtn = frame.Find("P" + i + "Panel/BuzzBtn")?.GetComponent<Button>();
                if (buzzBtn != null) Wire(buzzBtn, backing, "BuzzP" + i);
            }

            // Answer buttons
            string[] answerEvents = { "AnswerA", "AnswerB", "AnswerC", "AnswerD" };
            for (int i = 0; i < 4; i++)
            {
                Button ansBtn = frame.Find("AnswerPanel/Answer_" + i)?.GetComponent<Button>();
                if (ansBtn != null) Wire(ansBtn, backing, answerEvents[i]);
            }

            // Start and Reset
            Button startBtn = frame.Find("Controls/StartBtn")?.GetComponent<Button>();
            if (startBtn != null) Wire(startBtn, backing, "StartGame");

            Button resetBtn = frame.Find("Controls/ResetBtn")?.GetComponent<Button>();
            if (resetBtn != null) Wire(resetBtn, backing, "ResetGame");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[ChromixTrivia] Placed and wired instance into active scene.");
    }

    // --- Helper methods ---
    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    private static GameObject CreateObject(string name, Transform parent, bool rectTransform)
    {
        GameObject go = rectTransform ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = 0;
        return go;
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

    private static void Wire(Button button, UdonBehaviour udon, string eventName)
    {
        if (button == null || udon == null) return;
        UnityAction<string> action = new UnityAction<string>(udon.SendCustomEvent);
        UnityEventTools.AddStringPersistentListener(button.onClick, action, eventName);
    }

    private static Color Hex(string value)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
    }
}
public class ChromixTriviaAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path.StartsWith("Assets/ChromixTrivia/") && path.EndsWith(".cs"))
            {
                ChromixTriviaBuilder.RebuildFromMenu();
                return;
            }
        }
    }
}