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
public static class ChromixModToolBuilder
{
    private const string PrefabPath = "Assets/ChromixModTool/ChromixModTool.prefab";
    private const string BuildKey = "Chromix.ModTool.BuildVersion";
    private const int BuildVersion = 16;

    private static bool _buildPending = false;
    private static EditorApplication.CallbackFunction pendingPlacement;

    // Colors
    private static readonly Color Background = new Color(0.04f, 0.08f, 0.12f, 0.96f);
    private static readonly Color Panel = new Color(0.06f, 0.12f, 0.18f, 0.98f);
    private static readonly Color PanelLight = new Color(0.08f, 0.16f, 0.24f, 0.98f);
    private static readonly Color Cyan = new Color(0.157f, 0.847f, 0.961f, 1f);
    private static readonly Color CyanSoft = new Color(0.157f, 0.847f, 0.961f, 0.2f);
    private static readonly Color Magenta = new Color(0.961f, 0.157f, 0.847f, 1f);
    private static readonly Color Green = new Color(0.165f, 0.878f, 0.533f, 1f);
    private static readonly Color Red = new Color(0.961f, 0.220f, 0.220f, 1f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.2f, 1f);
    private static readonly Color White = new Color(0.92f, 0.97f, 1f, 1f);
    private static readonly Color Muted = new Color(0.5f, 0.65f, 0.72f, 1f);
    private static readonly Color Dim = new Color(0.03f, 0.06f, 0.1f, 0.9f);
    private static readonly Color TabActive = new Color(0.157f, 0.847f, 0.961f, 0.25f);
    private static readonly Color TabInactive = new Color(0.06f, 0.12f, 0.18f, 0.98f);

    static ChromixModToolBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (ShouldBuild()) ScheduleBuild();
        };
    }

    private static bool ShouldBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        return EditorPrefs.GetInt(BuildKey, 0) < BuildVersion;
    }

    private static void ScheduleBuild()
    {
        if (_buildPending) return;
        _buildPending = true;
        EditorApplication.update += DeferredBuild;
    }

    private static void DeferredBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling) return;
        EditorApplication.update -= DeferredBuild;
        _buildPending = false;
        if (!ShouldBuild()) return;
        EditorPrefs.SetInt(BuildKey, BuildVersion);
        Build();
    }

    [MenuItem("Tools/Chromix Mod Tool/Rebuild Mod Tool Asset")]
    public static void RebuildFromMenu()
    {
        EditorPrefs.SetInt(BuildKey, 0);
        ScheduleBuild();
    }

    // ==================== Build ====================

    private static void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject("ChromixModTool");
        try
        {
            GameObject assetRoot = CreateObject("ChromixModTool_Asset", root.transform, false);
            GameObject systems = CreateObject("ModSystems", assetRoot.transform, false);

            // EventSystem
            GameObject eventSystem = CreateObject("ChromixModTool_EventSystem", assetRoot.transform, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            // Jail Zone
            GameObject jailZone = CreateObject("JailZone", assetRoot.transform, false);
            jailZone.transform.position = new Vector3(0f, -50f, 0f);

            // Canvas — compact 480x600
            GameObject canvasObj = new GameObject("UI_Canvas");
            canvasObj.transform.SetParent(assetRoot.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObj.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(480f, 600f);
            canvasRect.position = new Vector3(3f, 1.5f, 0f);
            canvasRect.localScale = Vector3.one * 0.0015f;
            BoxCollider collider = canvasObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(480f, 600f, 1f);
            collider.isTrigger = true;
            canvasObj.AddComponent<VRCUiShape>();

            // Main Panel — 460x580
            GameObject panel = CreateImage("Panel", canvasObj.transform, Background, new Vector2(460f, 580f), Vector2.zero);
            AddOutline(panel, Cyan, new Vector2(2f, -2f));

            // Header — y=265, h=45
            GameObject header = CreateImage("Header", panel.transform, Panel, new Vector2(440f, 45f), new Vector2(0f, 265f));
            AddOutline(header, Cyan, new Vector2(1f, -1f));
            CreateText("Title", header.transform, "MOD TOOL", 22f, White, TextAnchor.MiddleCenter, new Vector2(300f, 30f), new Vector2(-50f, 0f), FontStyle.Bold, 2f, font);
            GameObject closeBtn = CreateButton("CloseBtn", header.transform, "X", new Vector2(36f, 36f), new Vector2(195f, 0f), Red, Panel, 20f, font);

            // Tab Bar — y=215, h=40, 5 tabs
            GameObject tabBar = CreateImage("TabBar", panel.transform, Panel, new Vector2(440f, 40f), new Vector2(0f, 215f));
            AddOutline(tabBar, CyanSoft, new Vector2(1f, -1f));
            float[] tabX = { -176f, -88f, 0f, 88f, 176f };
            string[] tabNames = { "PLAYERS", "ACT", "MOVE", "SOUND", "LOG" };
            for (int i = 0; i < 5; i++)
            {
                GameObject tab = CreateTabButton("TabBtn" + i, tabBar.transform, tabNames[i], new Vector2(82f, 32f), new Vector2(tabX[i], 0f), font);
            }

            // ==================== Tab 1: Players ====================
            GameObject tabPlayers = CreateImage("Tab_Players", panel.transform, Dim, new Vector2(440f, 460f), new Vector2(0f, -25f));
            tabPlayers.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible container

            // SelectedInfo — y=185, h=45
            GameObject selectedInfo = CreateImage("SelectedInfo", tabPlayers.transform, PanelLight, new Vector2(440f, 45f), new Vector2(0f, 185f));
            AddOutline(selectedInfo, Gold, new Vector2(1f, -1f));
            CreateText("NameText", selectedInfo.transform, "No player selected", 16f, White, TextAnchor.MiddleLeft, new Vector2(280f, 25f), new Vector2(-70f, 5f), FontStyle.Bold, 1f, font);
            CreateText("RoleText", selectedInfo.transform, "", 12f, Muted, TextAnchor.MiddleLeft, new Vector2(120f, 20f), new Vector2(150f, 5f), FontStyle.Normal, 1f, font);

            // PlayerList — y=-20, h=340
            GameObject playerList = CreateImage("PlayerList", tabPlayers.transform, Dim, new Vector2(440f, 340f), new Vector2(0f, -20f));
            AddOutline(playerList, CyanSoft, new Vector2(1f, -1f));
            CreateText("ListText", playerList.transform, "PLAYERS", 12f, Muted, TextAnchor.UpperLeft, new Vector2(420f, 20f), new Vector2(0f, 150f), FontStyle.Bold, 1f, font);

            for (int i = 0; i < 8; i++)
            {
                float y = 120f - i * 32f;
                GameObject row = CreateImage("Row" + i, playerList.transform, Dim, new Vector2(420f, 28f), new Vector2(0f, y));
                AddOutline(row, CyanSoft, new Vector2(1f, -1f));
                CreateText("NameText", row.transform, "", 14f, White, TextAnchor.MiddleLeft, new Vector2(410f, 24f), new Vector2(5f, 0f), FontStyle.Normal, 1f, font);
                Button rowBtn = row.AddComponent<Button>();
                rowBtn.targetGraphic = row.GetComponent<Image>();
                rowBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock rc = rowBtn.colors;
                rc.normalColor = new Color(1f, 1f, 1f, 0.05f);
                rc.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
                rc.pressedColor = new Color(0.157f, 0.847f, 0.961f, 0.3f);
                rc.fadeDuration = 0.1f;
                rowBtn.colors = rc;
            }

            CreateButton("ScrollUpBtn", playerList.transform, "^", new Vector2(50f, 24f), new Vector2(190f, -145f), Cyan, Panel, 16f, font);
            CreateButton("ScrollDownBtn", playerList.transform, "v", new Vector2(50f, 24f), new Vector2(190f, -172f), Cyan, Panel, 16f, font);

            // ==================== Tab 2: Actions ====================
            GameObject tabActions = CreateImage("Tab_Actions", panel.transform, Dim, new Vector2(440f, 460f), new Vector2(0f, -25f));
            tabActions.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            tabActions.SetActive(false);

            float[] actX = { -165f, -55f, 55f, 165f };
            CreateActionButton("TeleportToBtn", tabActions.transform, "TP TO", new Vector2(100f, 50f), new Vector2(actX[0], 170f), Cyan, font);
            CreateActionButton("BringBtn", tabActions.transform, "BRING", new Vector2(100f, 50f), new Vector2(actX[1], 170f), Cyan, font);
            CreateActionButton("FreezeBtn", tabActions.transform, "FREEZE", new Vector2(100f, 50f), new Vector2(actX[2], 170f), Magenta, font);
            CreateActionButton("MuteBtn", tabActions.transform, "MUTE", new Vector2(100f, 50f), new Vector2(actX[3], 170f), Magenta, font);
            CreateActionButton("BanBtn", tabActions.transform, "BAN", new Vector2(100f, 50f), new Vector2(actX[0], 100f), Red, font);
            CreateActionButton("UnbanBtn", tabActions.transform, "UNBAN", new Vector2(100f, 50f), new Vector2(actX[1], 100f), Green, font);
            CreateActionButton("PromoteBtn", tabActions.transform, "PROMOTE", new Vector2(100f, 50f), new Vector2(actX[2], 100f), Gold, font);
            CreateActionButton("DemoteBtn", tabActions.transform, "DEMOTE", new Vector2(100f, 50f), new Vector2(actX[3], 100f), Muted, font);

            // Action log
            GameObject actionLog = CreateImage("ActionLog", tabActions.transform, Dim, new Vector2(440f, 35f), new Vector2(0f, 10f));
            AddOutline(actionLog, CyanSoft, new Vector2(1f, -1f));
            CreateText("LogText", actionLog.transform, "Ready.", 13f, Muted, TextAnchor.MiddleLeft, new Vector2(420f, 28f), new Vector2(5f, 0f), FontStyle.Normal, 1f, font);

            // ==================== Tab 3: Move (Speed + Size) ====================
            GameObject tabMove = CreateImage("Tab_Move", panel.transform, Dim, new Vector2(440f, 460f), new Vector2(0f, -25f));
            tabMove.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            tabMove.SetActive(false);

            // Speed section
            CreateText("SpeedLabel", tabMove.transform, "SPEED", 14f, Green, TextAnchor.MiddleLeft, new Vector2(100f, 20f), new Vector2(-200f, 175f), FontStyle.Bold, 1f, font);
            CreateActionButton("SpeedSlowBtn", tabMove.transform, "SLOW", new Vector2(130f, 45f), new Vector2(-130f, 130f), Green, font);
            CreateActionButton("SpeedNormalBtn", tabMove.transform, "NORMAL", new Vector2(130f, 45f), new Vector2(0f, 130f), Cyan, font);
            CreateActionButton("SpeedFastBtn", tabMove.transform, "FAST", new Vector2(130f, 45f), new Vector2(130f, 130f), Gold, font);

            // Size section
            CreateText("SizeLabel", tabMove.transform, "SIZE", 14f, Gold, TextAnchor.MiddleLeft, new Vector2(100f, 20f), new Vector2(-200f, 60f), FontStyle.Bold, 1f, font);
            float[] sizeX = { -165f, -55f, 55f, 165f };
            CreateActionButton("SizeSmallBtn", tabMove.transform, "SMALL", new Vector2(100f, 45f), new Vector2(sizeX[0], 15f), Gold, font);
            CreateActionButton("SizeNormalBtn", tabMove.transform, "NORMAL", new Vector2(100f, 45f), new Vector2(sizeX[1], 15f), Cyan, font);
            CreateActionButton("SizeLargeBtn", tabMove.transform, "LARGE", new Vector2(100f, 45f), new Vector2(sizeX[2], 15f), Magenta, font);
            CreateActionButton("SizeGiantBtn", tabMove.transform, "GIANT", new Vector2(100f, 45f), new Vector2(sizeX[3], 15f), Red, font);

            // ==================== Tab 4: Sound ====================
            GameObject tabSound = CreateImage("Tab_Sound", panel.transform, Dim, new Vector2(440f, 460f), new Vector2(0f, -25f));
            tabSound.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            tabSound.SetActive(false);

            CreateText("SoundLabel", tabSound.transform, "VOICE", 14f, Magenta, TextAnchor.MiddleLeft, new Vector2(100f, 20f), new Vector2(-200f, 175f), FontStyle.Bold, 1f, font);
            CreateActionButton("MuteAllBtn", tabSound.transform, "MUTE ALL", new Vector2(200f, 45f), new Vector2(-110f, 130f), Magenta, font);
            CreateActionButton("UnmuteAllBtn", tabSound.transform, "UNMUTE ALL", new Vector2(200f, 45f), new Vector2(110f, 130f), Green, font);
            CreateActionButton("VoiceWorldwideBtn", tabSound.transform, "VOICE WORLDWIDE", new Vector2(420f, 45f), new Vector2(0f, 75f), Cyan, font);

            CreateText("ResetLabel", tabSound.transform, "RESET GAMES", 14f, Cyan, TextAnchor.MiddleLeft, new Vector2(200f, 20f), new Vector2(-150f, 60f), FontStyle.Bold, 1f, font);
            CreateActionButton("ResetCrackBtn", tabSound.transform, "CRACK CODE", new Vector2(200f, 45f), new Vector2(-110f, 15f), Cyan, font);
            CreateActionButton("ResetTriviaBtn", tabSound.transform, "TRIVIA", new Vector2(200f, 45f), new Vector2(110f, 15f), Cyan, font);
            CreateActionButton("ResetVoiceBtn", tabSound.transform, "RESET VOICE", new Vector2(420f, 45f), new Vector2(0f, -40f), Gold, font);

            // ==================== Tab 5: Log ====================
            GameObject tabLog = CreateImage("Tab_Log", panel.transform, Dim, new Vector2(440f, 460f), new Vector2(0f, -25f));
            tabLog.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            tabLog.SetActive(false);

            CreateText("AnalyticsTitle", tabLog.transform, "ANALYTICS / MOD LOG", 14f, Cyan, TextAnchor.UpperLeft, new Vector2(420f, 22f), new Vector2(0f, 200f), FontStyle.Bold, 1f, font);
            for (int i = 0; i < 8; i++)
            {
                float y = 160f - i * 28f;
                CreateText("Log" + i, tabLog.transform, "", 13f, Muted, TextAnchor.MiddleLeft, new Vector2(420f, 24f), new Vector2(5f, y), FontStyle.Normal, 1f, font);
            }

            // Side accents
            CreateImage("LeftAccent", panel.transform, Cyan, new Vector2(2f, 560f), new Vector2(-220f, 0f));
            CreateImage("RightAccent", panel.transform, Cyan, new Vector2(2f, 560f), new Vector2(220f, 0f));

            // Layer
            int defaultLayer = LayerMask.NameToLayer("Default");
            SetLayerRecursive(canvasObj, defaultLayer);

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            string prefabPath = PrefabPath;
            pendingPlacement = () => DeferredPlaceAndWire(prefabPath);
            EditorApplication.update += pendingPlacement;
            Debug.Log("[ChromixModTool] Prefab created at " + PrefabPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[ChromixModTool] Build failed: " + e.Message + "\n" + e.StackTrace);
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

        GameObject existing = GameObject.Find("ChromixModTool");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;
        instance.name = "ChromixModTool";
        instance.transform.position = new Vector3(3f, 1.5f, 0f);

        Transform systems = instance.transform.Find("ChromixModTool_Asset/ModSystems");
        if (systems == null)
        {
            Debug.LogError("[ChromixModTool] ModSystems not found.");
            return;
        }

        UdonBehaviour backing = Undo.AddComponent<UdonBehaviour>(systems.gameObject);
        UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/ChromixModTool/ChromixModTool.asset");
        if (programAsset != null)
        {
            backing.programSource = programAsset;
            SerializedObject sb = new SerializedObject(backing);
            SerializedProperty sync = sb.FindProperty("_syncMethod");
            if (sync != null) sync.intValue = 3;
            SerializedProperty inter = sb.FindProperty("Interactable");
            if (inter != null) inter.boolValue = false;
            sb.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError("[ChromixModTool] Program asset not found.");
            return;
        }

        // Wire jail zone
        Transform jailInScene = instance.transform.Find("ChromixModTool_Asset/JailZone");
        if (jailInScene != null)
        {
            SerializedObject so = new SerializedObject(backing);
            SerializedProperty jp = so.FindProperty("_jailZone");
            if (jp != null) { jp.objectReferenceValue = jailInScene; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        Transform canvas = instance.transform.Find("ChromixModTool_Asset/UI_Canvas");
        if (canvas != null && backing != null)
        {
            Transform panel = canvas.Find("Panel");
            if (panel != null)
            {
                // Close
                Wire(panel.Find("Header/CloseBtn")?.GetComponent<Button>(), backing, "ClosePanel");

                // Tab buttons
                Transform tabBar = panel.Find("TabBar");
                if (tabBar != null)
                {
                    for (int i = 0; i < 5; i++)
                        Wire(tabBar.Find("TabBtn" + i)?.GetComponent<Button>(), backing, "SwitchTab" + i);
                }

                // Tab 1: Players
                Transform tp = panel.Find("Tab_Players");
                if (tp != null)
                {
                    Wire(tp.Find("PlayerList/ScrollUpBtn")?.GetComponent<Button>(), backing, "ScrollUp");
                    Wire(tp.Find("PlayerList/ScrollDownBtn")?.GetComponent<Button>(), backing, "ScrollDown");
                    for (int i = 0; i < 8; i++)
                        Wire(tp.Find("PlayerList/Row" + i)?.GetComponent<Button>(), backing, "SelectRow" + i);
                }

                // Tab 2: Actions
                Transform ta = panel.Find("Tab_Actions");
                if (ta != null)
                {
                    Wire(ta.Find("TeleportToBtn")?.GetComponent<Button>(), backing, "TeleportToPlayer");
                    Wire(ta.Find("BringBtn")?.GetComponent<Button>(), backing, "BringPlayer");
                    Wire(ta.Find("FreezeBtn")?.GetComponent<Button>(), backing, "ToggleFreeze");
                    Wire(ta.Find("MuteBtn")?.GetComponent<Button>(), backing, "ToggleMute");
                    Wire(ta.Find("BanBtn")?.GetComponent<Button>(), backing, "BanPlayer");
                    Wire(ta.Find("UnbanBtn")?.GetComponent<Button>(), backing, "UnbanPlayer");
                    Wire(ta.Find("PromoteBtn")?.GetComponent<Button>(), backing, "PromotePlayer");
                    Wire(ta.Find("DemoteBtn")?.GetComponent<Button>(), backing, "DemotePlayer");
                }

                // Tab 3: Move
                Transform tm = panel.Find("Tab_Move");
                if (tm != null)
                {
                    Wire(tm.Find("SpeedSlowBtn")?.GetComponent<Button>(), backing, "SpeedSlow");
                    Wire(tm.Find("SpeedNormalBtn")?.GetComponent<Button>(), backing, "SpeedNormal");
                    Wire(tm.Find("SpeedFastBtn")?.GetComponent<Button>(), backing, "SpeedFast");
                    Wire(tm.Find("SizeSmallBtn")?.GetComponent<Button>(), backing, "SizeSmall");
                    Wire(tm.Find("SizeNormalBtn")?.GetComponent<Button>(), backing, "SizeNormal");
                    Wire(tm.Find("SizeLargeBtn")?.GetComponent<Button>(), backing, "SizeLarge");
                    Wire(tm.Find("SizeGiantBtn")?.GetComponent<Button>(), backing, "SizeGiant");
                }

                // Tab 4: Sound
                Transform ts = panel.Find("Tab_Sound");
                if (ts != null)
                {
                    Wire(ts.Find("MuteAllBtn")?.GetComponent<Button>(), backing, "MuteAll");
                    Wire(ts.Find("UnmuteAllBtn")?.GetComponent<Button>(), backing, "UnmuteAll");
                    Wire(ts.Find("VoiceWorldwideBtn")?.GetComponent<Button>(), backing, "VoiceWorldwide");
                    Wire(ts.Find("ResetCrackBtn")?.GetComponent<Button>(), backing, "ResetCrack");
                    Wire(ts.Find("ResetTriviaBtn")?.GetComponent<Button>(), backing, "ResetTrivia");
                    Wire(ts.Find("ResetVoiceBtn")?.GetComponent<Button>(), backing, "ResetVoice");
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[ChromixModTool] Placed and wired instance into active scene.");
    }

    // ==================== Helpers ====================

    private static void CreateActionButton(string name, Transform parent, string label, Vector2 size, Vector2 pos, Color accent, Font font)
    {
        GameObject btn = CreateButton(name, parent, label, size, pos, accent, Panel, 16f, font);
        AddOutline(btn, accent, new Vector2(1f, -1f));
    }

    private static GameObject CreateTabButton(string name, Transform parent, string label, Vector2 size, Vector2 pos, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Image img = go.AddComponent<Image>();
        img.color = TabInactive;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        c.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
        c.fadeDuration = 0.08f;
        btn.colors = c;
        CreateText("Label", go.transform, label, 13f, Muted, TextAnchor.MiddleCenter, size - new Vector2(10f, 6f), Vector2.zero, FontStyle.Bold, 1f, font);
        return go;
    }

    private static GameObject CreateObject(string name, Transform parent, bool worldPositionStays)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, worldPositionStays);
        return go;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 size, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static Text CreateText(string name, Transform parent, string content, float fontSize, Color color, TextAnchor anchor, Vector2 size, Vector2 pos, FontStyle style, float spacing, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Text txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = font;
        txt.fontSize = Mathf.RoundToInt(fontSize);
        txt.color = color;
        txt.alignment = anchor;
        txt.fontStyle = style;
        txt.lineSpacing = spacing;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        return txt;
    }

    private static GameObject CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 pos, Color accent, Color bg, float fontSize, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Image img = go.AddComponent<Image>();
        img.color = bg;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        c.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
        c.fadeDuration = 0.08f;
        btn.colors = c;
        CreateText("Label", go.transform, label, fontSize, accent, TextAnchor.MiddleCenter, size - new Vector2(12f, 8f), Vector2.zero, FontStyle.Bold, 1f, font);
        return go;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline o = target.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = distance;
        o.useGraphicAlpha = true;
    }

    private static void Wire(Button button, UdonBehaviour udon, string eventName)
    {
        if (button == null || udon == null) return;
        UnityAction<string> action = new UnityAction<string>(udon.SendCustomEvent);
        UnityEventTools.AddStringPersistentListener(button.onClick, action, eventName);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}

public class ChromixModToolAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path.StartsWith("Assets/ChromixModTool/") && path.EndsWith(".cs"))
            {
                ChromixModToolBuilder.RebuildFromMenu();
                return;
            }
        }
    }
}
