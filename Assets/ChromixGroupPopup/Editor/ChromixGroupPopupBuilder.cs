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
public static class ChromixGroupPopupBuilder
{
    private const string PrefabPath = "Assets/ChromixGroupPopup/ChromixGroupPopup.prefab";
    private const string BuildKey = "Chromix.GroupPopup.BuildVersion";
    private const int BuildVersion = 12;

    private static EditorApplication.CallbackFunction pendingPlacement;

    static ChromixGroupPopupBuilder()
    {
        BuildIfNeeded();
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

    private static readonly Color Cyan = Hex("#28D8F5FF");
    private static readonly Color CyanSoft = Hex("#14A0B8FF");
    private static readonly Color Green = Hex("#2AE088FF");
    private static readonly Color Background = Hex("#0A1520EE");
    private static readonly Color Panel = Hex("#0E1D2BFF");
    private static readonly Color PanelLight = Hex("#142838FF");
    private static readonly Color White = Hex("#F0FBFFFF");
    private static readonly Color Muted = Hex("#6B8499FF");

    private static void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject("ChromixGroupPopup");
        try
        {
            GameObject assetRoot = CreateObject("ChromixGroupPopup_Asset", root.transform, false);
            assetRoot.transform.localScale = Vector3.one * 0.1f;

            GameObject systems = CreateObject("PopupSystems", assetRoot.transform, false);

            GameObject eventSystemObject = CreateObject("ChromixGroupPopup_EventSystem", assetRoot.transform, false);
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = false;
            eventSystemObject.AddComponent<StandaloneInputModule>();

            GameObject canvasObject = CreateObject("UI_Canvas", assetRoot.transform, true);
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1400f, 1600f);
            canvasRect.localScale = Vector3.one * 0.01f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 3f;
            canvasObject.AddComponent<GraphicRaycaster>();
            BoxCollider collider = canvasObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(900f, 520f, 10f);
            collider.isTrigger = true;
            canvasObject.AddComponent<VRCUiShape>();

            // Popup panel - taller to fit description text
            GameObject popup = CreateImage("GroupPopup", canvasObject.transform, Background, new Vector2(900f, 520f), new Vector2(0f, 0f));
            Image popupImg = popup.GetComponent<Image>();
            popupImg.raycastTarget = true;
            AddOutline(popup, Cyan, new Vector2(4f, -4f));

            // Accent bar
            CreateImage("AccentBar", popup.transform, Cyan, new Vector2(860f, 6f), new Vector2(0f, 245f));

            // Title and subtitle
            CreateText("Title", popup.transform, "CHROMIX", 56f, White, TextAnchor.MiddleCenter, new Vector2(860f, 80f), new Vector2(0f, 180f), FontStyle.Bold, 3f, font);
            CreateText("Subtitle", popup.transform, "JOIN THE COMMUNITY", 28f, Cyan, TextAnchor.MiddleCenter, new Vector2(860f, 44f), new Vector2(0f, 120f), FontStyle.Normal, 1f, font);

            // Description - wrapped text, wider box
            Text desc = CreateText("Description", popup.transform, "Chromix is a social VR club where creativity meets fun. Hang out, play games, perform, dance, compete, and make unforgettable memories.", 20f, Muted, TextAnchor.MiddleCenter, new Vector2(820f, 100f), new Vector2(0f, 40f), FontStyle.Normal, 0f, font);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;

            // Status text
            CreateText("StatusText", popup.transform, "JOIN THE CHROMIX GROUP", 22f, CyanSoft, TextAnchor.MiddleCenter, new Vector2(820f, 36f), new Vector2(0f, -50f), FontStyle.Bold, 0f, font);

            // Join button
            CreateButton("JoinGroupButton", popup.transform, "JOIN GROUP", new Vector2(360f, 80f), new Vector2(-110f, -160f), Green, PanelLight, 32f, font);

            // Close button
            CreateButton("CloseButton", popup.transform, "MAYBE LATER", new Vector2(260f, 80f), new Vector2(250f, -160f), Hex("#2A3A4AFF"), PanelLight, 24f, font);

            // CanvasGroup for fade/slide animation
            CanvasGroup group = popup.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

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
            Debug.Log("[ChromixPopup] Grouped prefab created at " + PrefabPath);
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

        GameObject existing = GameObject.Find("ChromixGroupPopup");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;
        instance.name = "ChromixGroupPopup";
        instance.transform.position = new Vector3(2f, 1.5f, 2f);
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Add ChromixGroupPopup UdonBehaviour to the GroupPopup object
        Transform popup = instance.transform.Find("ChromixGroupPopup_Asset/UI_Canvas/GroupPopup");
        if (popup == null)
        {
            Debug.LogError("[ChromixPopup] GroupPopup not found in scene instance.");
            return;
        }

        UdonBehaviour backing = Undo.AddComponent<UdonBehaviour>(popup.gameObject);
        UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/ChromixGroupPopup/ChromixGroupPopup.asset");
        if (programAsset == null)
        {
            // Create the UdonSharp program asset manually if UdonSharp hasn't done it yet.
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/ChromixGroupPopup/ChromixGroupPopup.cs");
            if (script != null)
            {
                programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                programAsset.sourceCsScript = script;
                AssetDatabase.CreateAsset(programAsset, "Assets/ChromixGroupPopup/ChromixGroupPopup.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ChromixPopup] Created UdonSharp program asset manually.");
            }
            else
            {
                Debug.LogError("[ChromixPopup] Could not find ChromixGroupPopup.cs MonoScript.");
                return;
            }
        }

        backing.programSource = programAsset;
        // Force UdonSharp to compile all programs and generate serialized assets
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
        SerializedObject serializedBacking = new SerializedObject(backing);
        SerializedProperty syncMethod = serializedBacking.FindProperty("_syncMethod");
        if (syncMethod != null) syncMethod.intValue = 0; // None
        SerializedProperty interactable = serializedBacking.FindProperty("Interactable");
        if (interactable != null) interactable.boolValue = false;
        SerializedProperty groupIdProp = serializedBacking.FindProperty("groupId");
        if (groupIdProp != null) groupIdProp.stringValue = "grp_07a66a18-762d-48c6-8e07-d2ef08388546";
        serializedBacking.ApplyModifiedPropertiesWithoutUndo();

        Button joinBtn = popup.Find("JoinGroupButton")?.GetComponent<Button>();
        if (joinBtn != null) Wire(joinBtn, backing, "JoinGroup");

        Button closeBtn = popup.Find("CloseButton")?.GetComponent<Button>();
        if (closeBtn != null) Wire(closeBtn, backing, "HidePopup");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[ChromixPopup] Placed and wired instance into active scene.");
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
