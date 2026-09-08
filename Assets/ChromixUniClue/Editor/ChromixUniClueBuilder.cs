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
public static class ChromixUniClueBuilder
{
    private const string RootName = "ChromixUniClue";
    private const string PrefabPath = "Assets/ChromixUniClue/ChromixUniClue.prefab";
    private const string ProgramPath = "Assets/ChromixUniClue/ChromixUniClueGame.asset";
    private const string BuildKey = "Chromix.UniClue.BuildVersion";
    private const int BuildVersion = 11;
    private static bool _pending;

    private static readonly Color Background = Hex("#111416FA");
    private static readonly Color Panel = Hex("#202426F8");
    private static readonly Color PanelLight = Hex("#2A3033FF");
    private static readonly Color Cyan = Hex("#55CFF2FF");
    private static readonly Color Green = Hex("#91E47AFF");
    private static readonly Color Red = Hex("#F07D76FF");
    private static readonly Color Gold = Hex("#FFC857FF");
    private static readonly Color White = Hex("#F4F4F2FF");
    private static readonly Color Muted = Hex("#94999BFF");
    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    static ChromixUniClueBuilder()
    {
        EditorApplication.delayCall += ScheduleIfNeeded;
    }

    [MenuItem("Tools/Chromix UniClue/Rebuild Grouped Asset")]
    public static void RebuildFromMenu()
    {
        EditorPrefs.SetInt(BuildKey, 0);
        ScheduleIfNeeded();
    }

    [MenuItem("Tools/Chromix UniClue/Export Drag-and-Drop Package")]
    public static void ExportDragAndDropPackage()
    {
        string defaultName = "ChromixUniClue_DragAndDrop.unitypackage";
        string defaultDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop));
        string exportPath = EditorUtility.SaveFilePanel("Export ChromixUniClue Package", defaultDir, defaultName, "unitypackage");
        if (string.IsNullOrEmpty(exportPath)) return;

        // Collect all asset paths to export
        var assetPaths = new System.Collections.Generic.List<string>
        {
            "Assets/ChromixUniClue"
        };

        // Include the serialized Udon program asset so the prefab works without recompilation
        string programAssetGuid = "048c88e4ab3fe51479d56e28e18acebb";
        string serializedProgramPath = AssetDatabase.GUIDToAssetPath(programAssetGuid);
        if (!string.IsNullOrEmpty(serializedProgramPath) && System.IO.File.Exists(serializedProgramPath))
        {
            assetPaths.Add(serializedProgramPath);
        }

        // Also check for the source .cs script's compiled program by GUID
        string sourceScriptGuid = "5bc798d982d696643a09f0bdcea88946";
        string sourceSerializedPath = AssetDatabase.GUIDToAssetPath(sourceScriptGuid);
        if (!string.IsNullOrEmpty(sourceSerializedPath) && System.IO.File.Exists(sourceSerializedPath))
        {
            assetPaths.Add(sourceSerializedPath);
        }

        AssetDatabase.ExportPackage(
            assetPaths.ToArray(),
            exportPath,
            ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

        if (System.IO.File.Exists(exportPath))
        {
            Debug.Log("[ChromixUniClue] Package exported to: " + exportPath);
            EditorUtility.DisplayDialog("Export Complete",
                "ChromixUniClue package exported successfully.\n\n" + exportPath +
                "\n\nDrag the .unitypackage into any VRChat project to install.", "OK");
        }
        else
        {
            Debug.LogError("[ChromixUniClue] Package export failed.");
            EditorUtility.DisplayDialog("Export Failed", "Failed to export the package. Check the console for details.", "OK");
        }
    }

    private static void ScheduleIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetInt(BuildKey, 0) >= BuildVersion || _pending) return;
        _pending = true;
        EditorApplication.update += DeferredBuild;
    }

    private static void DeferredBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= DeferredBuild;
        _pending = false;
        if (EditorPrefs.GetInt(BuildKey, 0) >= BuildVersion) return;
        EditorPrefs.SetInt(BuildKey, BuildVersion);
        Build();
    }

    private static void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject(RootName);
        try
        {
            GameObject asset = CreateObject("ChromixUniClue_Asset", root.transform, false);
            GameObject systems = CreateObject("GameSystems", asset.transform, false);
            GameObject eventSystemObject = CreateObject("ChromixUniClue_EventSystem", asset.transform, false);
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = false;
            eventSystemObject.AddComponent<StandaloneInputModule>();
            GameObject portraitRig = CreateObject("PortraitRig", asset.transform, false);

            GameObject canvasObject = CreateObject("UI_Canvas", asset.transform, true);
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2000f, 1200f);
            canvasRect.localScale = Vector3.one * 0.001f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 4f;
            canvasObject.AddComponent<GraphicRaycaster>();
            BoxCollider collider = canvasObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2000f, 1200f, 10f);
            collider.isTrigger = true;
            canvasObject.AddComponent<VRCUiShape>();

            GameObject stage = CreateImage("Stage", canvasObject.transform, Background, new Vector2(1960f, 1160f), Vector2.zero);
            AddOutline(stage, Hex("#4D5457FF"), new Vector2(4f, -4f));
            CreateImage("TopAccent", stage.transform, Cyan, new Vector2(1840f, 6f), new Vector2(0f, 548f));
            CreateText("Brand", stage.transform, "UniClue", 64, White, TextAnchor.MiddleLeft, new Vector2(500f, 90f), new Vector2(-660f, 478f), FontStyle.Bold, font);
            CreateText("Subtitle", stage.transform, "ONE WORD. UNIQUE CLUES. ONE TEAM.", 20, Cyan, TextAnchor.MiddleLeft, new Vector2(600f, 40f), new Vector2(-610f, 426f), FontStyle.Bold, font);
            GameObject tutorialButton = CreateButton("TutorialButton", stage.transform, "HOW TO PLAY", new Vector2(260f, 58f), new Vector2(760f, 480f), Cyan, Panel, 20, font);

            GameObject seats = CreateImage("Seats", stage.transform, Clear, new Vector2(1900f, 1140f), Vector2.zero);
            Vector2[] seatPositions =
            {
                new Vector2(-660f, 300f), new Vector2(0f, 430f), new Vector2(660f, 300f),
                new Vector2(-660f, -300f), new Vector2(0f, -430f), new Vector2(660f, -300f)
            };
            for (int i = 0; i < 6; i++)
            {
                RenderTexture texture = GetPortraitTexture(i);
                GameObject seat = CreateImage("Seat" + i, seats.transform, Panel, new Vector2(260f, 208f), seatPositions[i]);
                AddOutline(seat, Hex("#575D60FF"), new Vector2(3f, -3f));
                RawImage portrait = CreateRawImage("Portrait", seat.transform, texture, new Vector2(224f, 126f), new Vector2(0f, 24f));
                portrait.color = Color.white;
                CreateText("Placeholder", portrait.transform, "◇", 58, Muted, TextAnchor.MiddleCenter, new Vector2(224f, 126f), Vector2.zero, FontStyle.Normal, font);
                CreateText("NameText", seat.transform, "JOIN", 22, White, TextAnchor.MiddleCenter, new Vector2(232f, 36f), new Vector2(0f, -63f), FontStyle.Bold, font);
                CreateText("RoleText", seat.transform, "OPEN SEAT", 14, Muted, TextAnchor.MiddleCenter, new Vector2(232f, 24f), new Vector2(0f, -88f), FontStyle.Bold, font);
                Button seatButton = seat.AddComponent<Button>();
                seatButton.targetGraphic = seat.GetComponent<Image>();
                seatButton.navigation = new Navigation { mode = Navigation.Mode.None };

                GameObject cameraObject = CreateObject("PortraitCamera" + i, portraitRig.transform, false);
                Camera portraitCamera = cameraObject.AddComponent<Camera>();
                portraitCamera.enabled = false;
                portraitCamera.targetTexture = texture;
                portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                portraitCamera.backgroundColor = Hex("#181C1EFF");
                portraitCamera.fieldOfView = 42f;
                portraitCamera.nearClipPlane = 0.03f;
                portraitCamera.farClipPlane = 2f;
                portraitCamera.depth = -20f - i;
                // Layer 9 = PlayerNetwork (remote avatars)
                // Layer 10 = PlayerLocal (local avatar — VRChat hides head on non-mirror cameras)
                // Layer 18 = MirrorReflection (VRChat renders full local avatar clone here, including head)
                portraitCamera.cullingMask = (1 << 9) | (1 << 10) | (1 << 18);
                portraitCamera.allowHDR = false;
                portraitCamera.allowMSAA = false;
            }

            GameObject main = CreateImage("MainPanel", stage.transform, PanelLight, new Vector2(1040f, 610f), Vector2.zero);
            AddOutline(main, Hex("#555B5EFF"), new Vector2(4f, -4f));
            CreateText("ModeText", main.transform, "0 / 6 PLAYERS  ·  2 REQUIRED", 18, Muted, TextAnchor.MiddleLeft, new Vector2(500f, 34f), new Vector2(-238f, 270f), FontStyle.Bold, font);
            CreateText("StatusText", main.transform, "SELECT AN OPEN SEAT TO JOIN", 25, White, TextAnchor.MiddleCenter, new Vector2(900f, 54f), new Vector2(0f, 218f), FontStyle.Bold, font);
            GameObject leaveButton = CreateButton("LeaveButton", main.transform, "LEAVE GAME", new Vector2(220f, 48f), new Vector2(370f, 270f), Red, Panel, 17, font);

            GameObject lobby = CreateImage("LobbyView", main.transform, Clear, new Vector2(980f, 400f), new Vector2(0f, -30f));
            CreateText("Logo", lobby.transform, "UniClue", 96, White, TextAnchor.MiddleCenter, new Vector2(760f, 120f), new Vector2(0f, 105f), FontStyle.Bold, font);
            CreateImage("LogoAccent", lobby.transform, Cyan, new Vector2(290f, 5f), new Vector2(-102f, 44f));
            CreateText("LobbyHint", lobby.transform, "HOST SELECTS A MODE WHEN 2+ PLAYERS JOIN", 18, Muted, TextAnchor.MiddleCenter, new Vector2(800f, 40f), new Vector2(0f, 2f), FontStyle.Bold, font);
            GameObject classicButton = CreateButton("ClassicButton", lobby.transform, "CLASSIC  ·  8 ROUNDS", new Vector2(400f, 74f), new Vector2(-220f, -88f), Green, Panel, 22, font);
            GameObject partyButton = CreateButton("PartyButton", lobby.transform, "PARTY  ·  TIMED 12", new Vector2(400f, 74f), new Vector2(220f, -88f), Cyan, Panel, 22, font);

            GameObject game = CreateImage("GameView", main.transform, Clear, new Vector2(980f, 470f), new Vector2(0f, -43f));
            CreateText("RoundText", game.transform, "ROUND 0 / 8", 18, Cyan, TextAnchor.MiddleLeft, new Vector2(300f, 34f), new Vector2(-322f, 190f), FontStyle.Bold, font);
            CreateText("ScoreText", game.transform, "TEAM SCORE  0", 18, Green, TextAnchor.MiddleCenter, new Vector2(300f, 34f), new Vector2(0f, 190f), FontStyle.Bold, font);
            CreateText("TimerText", game.transform, "NO TIMER", 18, Gold, TextAnchor.MiddleRight, new Vector2(300f, 34f), new Vector2(322f, 190f), FontStyle.Bold, font);
            GameObject wordCard = CreateImage("WordCard", game.transform, Background, new Vector2(850f, 100f), new Vector2(0f, 120f));
            AddOutline(wordCard, Cyan, new Vector2(2f, -2f));
            CreateText("WordText", wordCard.transform, "MYSTERY WORD", 38, White, TextAnchor.MiddleCenter, new Vector2(810f, 78f), Vector2.zero, FontStyle.Bold, font);

            GameObject clueEntry = CreateImage("ClueEntry", game.transform, Clear, new Vector2(900f, 76f), new Vector2(0f, 25f));
            GameObject clueInputObject = CreateInput("ClueInput", clueEntry.transform, "TYPE ONE UNIQUE CLUE", new Vector2(620f, 62f), new Vector2(-115f, 0f), font);
            GameObject clueSubmit = CreateButton("SubmitButton", clueEntry.transform, "LOCK CLUE", new Vector2(220f, 62f), new Vector2(320f, 0f), Cyan, Panel, 19, font);

            GameObject guessEntry = CreateImage("GuessEntry", game.transform, Clear, new Vector2(900f, 76f), new Vector2(0f, 25f));
            GameObject guessInputObject = CreateInput("GuessInput", guessEntry.transform, "GUESS ANYTIME", new Vector2(470f, 62f), new Vector2(-190f, 0f), font);
            GameObject guessSubmit = CreateButton("SubmitButton", guessEntry.transform, "GUESS", new Vector2(180f, 62f), new Vector2(160f, 0f), Green, Panel, 19, font);
            GameObject skipButton = CreateButton("SkipButton", guessEntry.transform, "SKIP", new Vector2(160f, 62f), new Vector2(350f, 0f), Gold, Panel, 19, font);

            // Word Pick View — 3 masked word buttons for the picker to choose from
            GameObject wordPickView = CreateImage("WordPickView", game.transform, Clear, new Vector2(900f, 76f), new Vector2(0f, 25f));
            CreateButton("WordChoice0", wordPickView.transform, "[ **** ]", new Vector2(280f, 62f), new Vector2(-300f, 0f), Cyan, Panel, 22, font);
            CreateButton("WordChoice1", wordPickView.transform, "[ **** ]", new Vector2(280f, 62f), new Vector2(0f, 0f), Cyan, Panel, 22, font);
            CreateButton("WordChoice2", wordPickView.transform, "[ **** ]", new Vector2(280f, 62f), new Vector2(300f, 0f), Cyan, Panel, 22, font);

            GameObject clueBoard = CreateImage("ClueBoard", game.transform, Clear, new Vector2(900f, 150f), new Vector2(0f, -78f));
            for (int i = 0; i < 6; i++)
            {
                float x = -300f + (i % 3) * 300f;
                float y = i < 3 ? 38f : -38f;
                GameObject clue = CreateImage("ClueCard" + i, clueBoard.transform, Background, new Vector2(270f, 60f), new Vector2(x, y));
                CreateText("Clue" + i, clue.transform, "—", 20, White, TextAnchor.MiddleCenter, new Vector2(250f, 45f), Vector2.zero, FontStyle.Bold, font);
            }
            CreateText("FeedbackText", game.transform, "", 24, White, TextAnchor.MiddleCenter, new Vector2(700f, 40f), new Vector2(0f, -178f), FontStyle.Bold, font);
            GameObject resetButton = CreateButton("ResetButton", game.transform, "RESET", new Vector2(150f, 44f), new Vector2(390f, -190f), Red, Panel, 16, font);

            GameObject tutorial = CreateImage("Tutorial", stage.transform, Background, new Vector2(780f, 900f), Vector2.zero);
            AddOutline(tutorial, Cyan, new Vector2(4f, -4f));
            CreateText("Title", tutorial.transform, "HOW TO PLAY", 38, White, TextAnchor.MiddleLeft, new Vector2(500f, 60f), new Vector2(-92f, 385f), FontStyle.Bold, font);
            GameObject tutorialClose = CreateButton("CloseButton", tutorial.transform, "×", new Vector2(60f, 60f), new Vector2(335f, 390f), Red, Panel, 32, font);
            string rules = "UniClue is a cooperative word game.\n\nEach round, one player is the PICKER. They are given 3 mystery words shown as [****] and pick one — without seeing what it is.\n\nEveryone else sees the picked word and gives one-word hints to the picker.\n\nIf two hints match, both are discarded.\n\nThe picker can guess the word at any time.\n\nCLASSIC has 8 relaxed rounds. PARTY has 12 rounds with timers.";
            CreateText("Rules", tutorial.transform, rules, 24, White, TextAnchor.UpperLeft, new Vector2(650f, 580f), new Vector2(0f, 40f), FontStyle.Normal, font);
            CreateText("Scoring", tutorial.transform, "CORRECT   +1      INCORRECT   -1      SKIP   0", 23, Green, TextAnchor.MiddleCenter, new Vector2(650f, 70f), new Vector2(0f, -345f), FontStyle.Bold, font);
            tutorial.SetActive(false);
            game.SetActive(false);
            clueEntry.SetActive(false);
            guessEntry.SetActive(false);
            wordPickView.SetActive(false);
            clueBoard.SetActive(false);
            leaveButton.SetActive(false);
            classicButton.SetActive(false);
            partyButton.SetActive(false);
            resetButton.SetActive(false);

            SetLayerRecursive(root, 0);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Vector3 position = new Vector3(6f, 1.6f, 0f);
            Quaternion rotation = Quaternion.Euler(0f, -90f, 0f);
            Scene scene = EditorSceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != RootName) continue;
                position = roots[i].transform.position;
                rotation = roots[i].transform.rotation;
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            Transform sceneSystems = instance.transform.Find("ChromixUniClue_Asset/GameSystems");
            UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
            if (programAsset == null)
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/ChromixUniClue/ChromixUniClueGame.cs");
                programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                programAsset.sourceCsScript = script;
                AssetDatabase.CreateAsset(programAsset, ProgramPath);
                AssetDatabase.SaveAssets();
            }
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
            ChromixUniClueGame gameScript = UdonSharpUndo.AddComponent<ChromixUniClueGame>(sceneSystems.gameObject);
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(gameScript);
            backing.programSource = programAsset;
            SerializedObject serializedBacking = new SerializedObject(backing);
            SerializedProperty syncMethod = serializedBacking.FindProperty("_syncMethod");
            if (syncMethod != null) syncMethod.intValue = 3;
            serializedBacking.ApplyModifiedPropertiesWithoutUndo();

            Transform sceneStage = instance.transform.Find("ChromixUniClue_Asset/UI_Canvas/Stage");
            Wire(sceneStage.Find("TutorialButton").GetComponent<Button>(), backing, "ToggleTutorial");
            Wire(sceneStage.Find("Tutorial/CloseButton").GetComponent<Button>(), backing, "CloseTutorial");
            Wire(sceneStage.Find("MainPanel/LeaveButton").GetComponent<Button>(), backing, "LeaveGame");
            Wire(sceneStage.Find("MainPanel/LobbyView/ClassicButton").GetComponent<Button>(), backing, "StartClassic");
            Wire(sceneStage.Find("MainPanel/LobbyView/PartyButton").GetComponent<Button>(), backing, "StartParty");
            Wire(sceneStage.Find("MainPanel/GameView/ClueEntry/SubmitButton").GetComponent<Button>(), backing, "SubmitClue");
            Wire(sceneStage.Find("MainPanel/GameView/GuessEntry/SubmitButton").GetComponent<Button>(), backing, "SubmitGuess");
            Wire(sceneStage.Find("MainPanel/GameView/GuessEntry/SkipButton").GetComponent<Button>(), backing, "SkipGuess");
            Wire(sceneStage.Find("MainPanel/GameView/WordPickView/WordChoice0").GetComponent<Button>(), backing, "PickWord0");
            Wire(sceneStage.Find("MainPanel/GameView/WordPickView/WordChoice1").GetComponent<Button>(), backing, "PickWord1");
            Wire(sceneStage.Find("MainPanel/GameView/WordPickView/WordChoice2").GetComponent<Button>(), backing, "PickWord2");
            Wire(sceneStage.Find("MainPanel/GameView/ResetButton").GetComponent<Button>(), backing, "ResetGame");
            for (int i = 0; i < 6; i++) Wire(sceneStage.Find("Seats/Seat" + i).GetComponent<Button>(), backing, "JoinSeat" + i);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ChromixUniClue] Built one grouped scene instance.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[ChromixUniClue] Build failed: " + exception);
            EditorPrefs.SetInt(BuildKey, 0);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static RenderTexture GetPortraitTexture(int index)
    {
        string path = "Assets/ChromixUniClue/Portrait" + index + ".renderTexture";
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture != null) return texture;
        texture = new RenderTexture(256, 144, 16, RenderTextureFormat.ARGB32);
        texture.name = "UniCluePortrait" + index;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static GameObject CreateObject(string name, Transform parent, bool rect)
    {
        GameObject gameObject = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0.01f;
        return gameObject;
    }

    private static RawImage CreateRawImage(string name, Transform parent, Texture texture, Vector2 size, Vector2 position)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        RawImage image = gameObject.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, Vector2 dimensions, Vector2 position, FontStyle style, Font font)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        Text text = gameObject.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 position, Color accent, Color baseColor, int fontSize, Font font)
    {
        GameObject gameObject = CreateImage(name, parent, baseColor, size, position);
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = gameObject.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        button.colors = colors;
        AddOutline(gameObject, accent, new Vector2(2f, -2f));
        CreateText("Label", gameObject.transform, label, fontSize, accent, TextAnchor.MiddleCenter, size - new Vector2(16f, 10f), Vector2.zero, FontStyle.Bold, font);
        return gameObject;
    }

    private static GameObject CreateInput(string name, Transform parent, string placeholderValue, Vector2 size, Vector2 position, Font font)
    {
        GameObject gameObject = CreateImage(name, parent, Background, size, position);
        AddOutline(gameObject, Hex("#5B6468FF"), new Vector2(2f, -2f));
        InputField input = gameObject.AddComponent<InputField>();
        Text value = CreateText("Text", gameObject.transform, "", 22, White, TextAnchor.MiddleLeft, size - new Vector2(36f, 12f), Vector2.zero, FontStyle.Normal, font);
        Text placeholder = CreateText("Placeholder", gameObject.transform, placeholderValue, 19, Muted, TextAnchor.MiddleLeft, size - new Vector2(36f, 12f), Vector2.zero, FontStyle.Italic, font);
        input.textComponent = value;
        input.placeholder = placeholder;
        input.characterLimit = 24;
        input.lineType = InputField.LineType.SingleLine;
        input.navigation = new Navigation { mode = Navigation.Mode.None };
        return gameObject;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void SetLayerRecursive(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        for (int i = 0; i < gameObject.transform.childCount; i++) SetLayerRecursive(gameObject.transform.GetChild(i).gameObject, layer);
    }

    private static void Wire(Button button, UdonBehaviour udon, string eventName)
    {
        UnityAction<string> action = new UnityAction<string>(udon.SendCustomEvent);
        UnityEventTools.AddStringPersistentListener(button.onClick, action, eventName);
    }

    private static Color Hex(string value)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
    }
}
