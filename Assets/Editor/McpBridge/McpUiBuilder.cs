// Unity-side handlers for every MCP UI tool. All run on the editor main thread.
// Style mirrors the project's existing SleekTicTacToeBuilder.cs helpers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;

namespace McpBridge
{
    public static class McpUiBuilder
    {
        // --- dispatch -------------------------------------------------------
        public static JsonValue Handle(string tool, JsonValue args)
        {
            switch (tool)
            {
                case "ping": return HandlePing();
                case "project_info": return HandleProjectInfo();

                case "list_hierarchy": return HandleListHierarchy(args);
                case "find_object": return HandleFindObject(args);

                case "create_canvas": return HandleCreateCanvas(args);
                case "create_panel": return HandleCreatePanel(args);
                case "create_empty": return HandleCreateEmpty(args);

                case "create_text": return HandleCreateText(args);
                case "create_button": return HandleCreateButton(args);
                case "create_image": return HandleCreateImage(args);
                case "create_input_field": return HandleCreateInputField(args);
                case "create_vrcurl_input_field": return HandleCreateVrcUrlInputField(args);
                case "create_toggle": return HandleCreateToggle(args);
                case "create_slider": return HandleCreateSlider(args);
                case "create_scroll_view": return HandleCreateScrollView(args);

                case "set_parent": return HandleSetParent(args);
                case "set_anchors": return HandleSetAnchors(args);
                case "set_size_delta": return HandleSetSizeDelta(args);
                case "set_anchored_position": return HandleSetAnchoredPosition(args);
                case "set_pivot": return HandleSetPivot(args);
                case "set_local_scale": return HandleSetLocalScale(args);
                case "set_world_position": return HandleSetWorldPosition(args);
                case "set_rotation_euler": return HandleSetRotationEuler(args);

                case "set_color": return HandleSetColor(args);
                case "set_text": return HandleSetText(args);
                case "set_font_size": return HandleSetFontSize(args);
                case "set_button_colors": return HandleSetButtonColors(args);
                case "set_active": return HandleSetActive(args);
                case "remove_component": return HandleRemoveComponent(args);
                case "set_layer": return HandleSetLayer(args);
                case "set_sorting_order": return HandleSetSortingOrder(args);
                case "set_canvas_render_mode": return HandleSetCanvasRenderMode(args);

                case "add_layout_group": return HandleAddLayoutGroup(args);
                case "add_layout_element": return HandleAddLayoutElement(args);
                case "add_shadow": return HandleAddShadow(args);
                case "add_outline": return HandleAddOutline(args);

                case "rename_object": return HandleRenameObject(args);
                case "delete_object": return HandleDeleteObject(args);
                case "save_scene": return HandleSaveScene();
                case "create_prefab": return HandleCreatePrefab(args);
                case "export_package": return HandleExportPackage(args);
                case "create_material": return HandleCreateMaterial(args);
                case "add_component": return HandleAddComponent(args);
                case "set_component_property": return HandleSetComponentProperty(args);
                case "create_udon_script": return HandleCreateUdonScript(args);
                case "attach_udon_behaviour": return HandleAttachUdonBehaviour(args);
                case "refresh_udon_sharp": return HandleRefreshUdonSharp();
                case "wire_button_to_udon": return HandleWireButtonToUdon(args);
                case "add_vrc_uishape": return HandleAddVrcUiShape(args);
                case "set_sprite_import": return HandleSetSpriteImport(args);
                case "set_sibling_index": return HandleSetSiblingIndex(args);
                case "set_image_sprite": return HandleSetImageSprite(args);
                case "set_renderer_material": return HandleSetRendererMaterial(args);
                case "set_mesh_filter": return HandleSetMeshFilter(args);
                case "set_mirror_layers": return HandleSetMirrorLayers(args);

                default:
                    throw new Exception("Unknown tool: " + tool);
            }
        }

        // --- helpers --------------------------------------------------------
        private static Font GetFont(string name = null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var f = Resources.Load<Font>(name);
                if (f != null) return f;
                var builtin = Resources.GetBuiltinResource<Font>(name);
                if (builtin != null) return builtin;
            }
            var f2022 = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f2022 != null) return f2022;
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static GameObject CreateUIObject(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return go;
        }

        private static void AddLayoutElement(GameObject go, float? w = null, float? h = null)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (w.HasValue) { le.preferredWidth = w.Value; le.minWidth = w.Value; }
            if (h.HasValue) { le.preferredHeight = h.Value; le.minHeight = h.Value; }
            var rect = go.GetComponent<RectTransform>();
            if (rect != null && (w.HasValue || h.HasValue))
                rect.sizeDelta = new Vector2(w ?? rect.sizeDelta.x, h ?? rect.sizeDelta.y);
        }

        private static string PathOf(GameObject go)
        {
            if (go == null) return null;
            if (go.transform.parent == null) return go.name;
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null) { sb.Insert(0, t.name + "/"); t = t.parent; }
            return sb.ToString();
        }

        private static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            Transform current = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == parts[0]) { current = root.transform; break; }
            }
            if (current == null) return null;
            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
                if (current == null) return null;
            }
            return current != null ? current.gameObject : null;
        }

        private static Transform FindRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindRecursive(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static GameObject FindByName(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var t = FindRecursive(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static GameObject ResolveTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) throw new Exception("target is required");
            if (target.Contains("/"))
            {
                var go = FindByPath(target);
                if (go != null) return go;
            }
            var byName = FindByName(target);
            if (byName != null) return byName;
            throw new Exception("GameObject not found: " + target);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        private static TextAnchor ParseAnchor(string s, TextAnchor def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            if (Enum.TryParse(s, true, out TextAnchor a)) return a;
            return def;
        }

        private static RectOffset ToRectOffset(List<JsonValue> a)
        {
            if (a == null || a.Count < 4) return new RectOffset();
            return new RectOffset((int)a[0].num, (int)a[1].num, (int)a[2].num, (int)a[3].num);
        }

        private static JsonValue Created(GameObject go)
        {
            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("name", go.name);
            o.Set("created", true);
            return o;
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        // --- health / info --------------------------------------------------
        private static JsonValue HandlePing()
        {
            var o = JsonValue.Object();
            o.Set("ok", true);
            o.Set("unity", Application.unityVersion);
            o.Set("platform", Application.platform.ToString());
            o.Set("time", DateTime.Now.ToString("o"));
            return o;
        }

        private static JsonValue HandleProjectInfo()
        {
            var o = JsonValue.Object();
            o.Set("unity", Application.unityVersion);
            o.Set("productName", PlayerSettings.productName);
            o.Set("companyName", PlayerSettings.companyName);
            o.Set("activeScene", SceneManager.GetActiveScene().path);
            o.Set("dataPath", Application.dataPath);
            return o;
        }

        // --- hierarchy ------------------------------------------------------
        private static JsonValue HandleListHierarchy(JsonValue args)
        {
            GameObject rootGo = null;
            if (args.Has("root")) rootGo = ResolveTarget(args.Str("root", null));
            var o = JsonValue.Object();
            o.Set("root", rootGo != null ? PathOf(rootGo) : "");
            o.Set("children", BuildHierarchy(rootGo != null ? rootGo.transform : null));
            return o;
        }

        private static JsonValue BuildHierarchy(Transform root)
        {
            var node = JsonValue.Object();
            Transform t = root;
            node.Set("name", t != null ? t.name : "(scene root)");
            node.Set("path", t != null ? PathOf(t.gameObject) : "");
            node.Set("active", t != null ? t.gameObject.activeInHierarchy : true);
            var children = new JsonValue { type = JsonType.Array, arr = new List<JsonValue>() };
            if (t == null)
            {
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                    children.arr.Add(BuildHierarchy(go.transform));
            }
            else
            {
                for (int i = 0; i < t.childCount; i++)
                    children.arr.Add(BuildHierarchy(t.GetChild(i)));
            }
            node.Set("children", children);
            return node;
        }

        private static JsonValue HandleFindObject(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var comps = new JsonValue { type = JsonType.Array, arr = new List<JsonValue>() };
            foreach (var c in go.GetComponents<Component>())
            {
                var co = JsonValue.Object();
                co.Set("type", c.GetType().FullName);
                comps.arr.Add(co);
            }
            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("name", go.name);
            o.Set("active", go.activeInHierarchy);
            o.Set("layer", LayerMask.LayerToName(go.layer));
            o.Set("components", comps);
            return o;
        }

        // --- canvas / containers -------------------------------------------
        private static JsonValue HandleCreateCanvas(JsonValue args)
        {
            var name = args.Str("name", "Canvas");
            var mode = args.Str("render_mode", "ScreenSpaceOverlay");
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Canvas");
            var canvas = go.GetComponent<Canvas>();
            var scaler = go.GetComponent<CanvasScaler>();
            var rect = go.GetComponent<RectTransform>();

            switch (mode)
            {
                case "ScreenSpaceOverlay":
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    break;
                case "ScreenSpaceCamera":
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    var camName = args.Str("screen_camera", null);
                    if (!string.IsNullOrEmpty(camName))
                    {
                        var cam = ResolveTarget(camName)?.GetComponent<Camera>();
                        if (cam != null) canvas.worldCamera = cam;
                    }
                    else if (Camera.main != null) canvas.worldCamera = Camera.main;
                    break;
                case "WorldSpace":
                    canvas.renderMode = RenderMode.WorldSpace;
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    var size = JsonValue.ToV2(args.Arr("size"), new Vector2(940, 1240));
                    rect.sizeDelta = size;
                    var pos = JsonValue.ToV3(args.Arr("world_position"), new Vector3(0, 1.3f, 1.2f));
                    go.transform.position = pos;
                    var rot = JsonValue.ToV3(args.Arr("world_rotation_euler"), Vector3.zero);
                    go.transform.rotation = Quaternion.Euler(rot);
                    float scale = (float)args.Num("world_scale", 0.0012);
                    go.transform.localScale = Vector3.one * scale;
                    break;
                default:
                    throw new Exception("Unknown render_mode: " + mode);
            }

            canvas.sortingOrder = args.Int("sorting_order", 0);
            canvas.pixelPerfect = args.Bool("pixel_perfect", false);
            EnsureEventSystem();
            Selection.activeGameObject = go;
            return Created(go);
        }

        private static JsonValue HandleCreatePanel(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var go = CreateUIObject(parent.transform, args.Str("name", "Panel"),
                JsonValue.ToV2(args.Arr("size"), new Vector2(400, 300)));
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Panel");
            var img = go.AddComponent<Image>();
            img.color = JsonValue.ToColor(args.Get("color"), new Color(0.04f, 0.06f, 0.09f, 0.95f));
            img.raycastTarget = args.Bool("raycast_target", true);
            if (args.Has("layout")) ApplyLayoutGroup(go, args);
            AddLayoutElement(go,
                args.Arr("size") != null ? (float?)JsonValue.ToV2(args.Arr("size"), Vector2.zero).x : null,
                args.Arr("size") != null ? (float?)JsonValue.ToV2(args.Arr("size"), Vector2.zero).y : null);
            return Created(go);
        }

        private static JsonValue HandleCreateEmpty(JsonValue args)
        {
            Transform parent = null;
            if (args.Has("parent")) parent = ResolveTarget(args.Str("parent", null)).transform;
            var go = CreateUIObject(parent, args.Str("name", "Empty"),
                args.Arr("size") != null ? JsonValue.ToV2(args.Arr("size"), Vector2.zero) : Vector2.zero);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Empty");
            return Created(go);
        }

        // --- text -----------------------------------------------------------
        private static JsonValue HandleCreateText(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var go = new GameObject(args.Str("name", "Text"), typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Text");
            var label = go.GetComponent<Text>();
            label.text = args.Str("text", "");
            label.fontSize = args.Int("font_size", 24);
            label.color = JsonValue.ToColor(args.Get("color"), Color.white);
            label.alignment = ParseAnchor(args.Str("alignment", "MiddleCenter"), TextAnchor.MiddleCenter);
            label.supportRichText = args.Bool("rich_text", true);
            label.raycastTarget = args.Bool("raycast_target", false);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.font = GetFont(args.Str("font", null));
            return Created(go);
        }

        // --- button ---------------------------------------------------------
        private static JsonValue HandleCreateButton(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var go = new GameObject(args.Str("name", "Button"), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Button");
            var bg = args.Get("background_color");
            var img = go.GetComponent<Image>();
            img.color = JsonValue.ToColor(bg, JsonValue.ParseHex("#182031FF", new Color(0.09f, 0.13f, 0.19f, 1)));
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            SetButtonColors(button,
                args.Has("normal") ? JsonValue.ToColor(args.Get("normal"), Color.white) : Color.white,
                args.Has("highlighted") ? JsonValue.ToColor(args.Get("highlighted"), Color.white) : new Color(0.96f, 0.98f, 1f, 1f),
                args.Has("pressed") ? JsonValue.ToColor(args.Get("pressed"), Color.white) : new Color(0.78f, 0.86f, 1f, 1f),
                args.Has("disabled") ? JsonValue.ToColor(args.Get("disabled"), Color.white) : new Color(0.06f, 0.08f, 0.11f, 0.5f),
                (float)args.Num("fade_duration", 0.08));
            var label = CreateTextChild(go.transform, "Label", args.Str("label", ""), args.Int("font_size", 24),
                JsonValue.ToColor(args.Get("text_color"), Color.white));
            Stretch(label.GetComponent<RectTransform>());
            label.GetComponent<Text>().raycastTarget = false;
            if (args.Arr("size") != null)
                AddLayoutElement(go, JsonValue.ToV2(args.Arr("size"), Vector2.zero).x, JsonValue.ToV2(args.Arr("size"), Vector2.zero).y);
            return Created(go);
        }

        private static void SetButtonColors(Button button, Color normal, Color highlighted, Color pressed, Color disabled, float fade)
        {
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = highlighted;
            colors.disabledColor = disabled;
            colors.fadeDuration = fade;
            button.colors = colors;
        }

        private static GameObject CreateTextChild(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.font = GetFont();
            return go;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        // --- image ----------------------------------------------------------
        private static JsonValue HandleCreateImage(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var go = new GameObject(args.Str("name", "Image"), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Image");
            var img = go.GetComponent<Image>();
            img.color = JsonValue.ToColor(args.Get("color"), Color.white);
            img.raycastTarget = args.Bool("raycast_target", true);
            var sp = args.Str("sprite_path", null);
            if (!string.IsNullOrEmpty(sp))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sp);
                if (sprite != null) img.sprite = sprite;
            }
            if (args.Arr("size") != null)
                AddLayoutElement(go, JsonValue.ToV2(args.Arr("size"), Vector2.zero).x, JsonValue.ToV2(args.Arr("size"), Vector2.zero).y);
            return Created(go);
        }

        // --- input field ----------------------------------------------------
        private static JsonValue HandleCreateInputField(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var go = new GameObject(args.Str("name", "InputField"), typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create InputField");
            var img = go.GetComponent<Image>();
            img.color = JsonValue.ToColor(args.Get("background_color"), new Color(0.13f, 0.13f, 0.13f, 1));
            var input = go.GetComponent<InputField>();

            var textChild = CreateTextChild(go.transform, "Text", "", args.Int("font_size", 24),
                JsonValue.ToColor(args.Get("text_color"), Color.white));
            var tr = textChild.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 1);
            tr.offsetMin = new Vector2(10, 6); tr.offsetMax = new Vector2(-10, -6);
            textChild.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            textChild.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            textChild.GetComponent<Text>().supportRichText = false;

            var ph = CreateTextChild(go.transform, "Placeholder", args.Str("placeholder", "Enter text..."),
                args.Int("font_size", 24), new Color(1, 1, 1, 0.5f));
            var phr = ph.GetComponent<RectTransform>();
            phr.anchorMin = new Vector2(0, 0); phr.anchorMax = new Vector2(1, 1);
            phr.offsetMin = new Vector2(10, 6); phr.offsetMax = new Vector2(-10, -6);
            ph.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            ph.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            ph.GetComponent<Text>().fontStyle = FontStyle.Italic;

            input.textComponent = textChild.GetComponent<Text>();
            input.placeholder = ph.GetComponent<Text>();
            if (Enum.TryParse(args.Str("content_type", "Standard"), true, out InputField.ContentType ct)) input.contentType = ct;
            if (args.Arr("size") != null)
                AddLayoutElement(go, JsonValue.ToV2(args.Arr("size"), Vector2.zero).x, JsonValue.ToV2(args.Arr("size"), Vector2.zero).y);
            return Created(go);
        }

        // --- vrc url input field --------------------------------------------
        private static JsonValue HandleCreateVrcUrlInputField(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var vrcUrlType = FindType("VRC.SDK3.Components.VRCUrlInputField");
            if (vrcUrlType == null) throw new Exception("VRCUrlInputField type not found. Is the VRChat SDK installed?");

            var go = new GameObject(args.Str("name", "VrcUrlInputField"), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create VRCUrlInputField");
            var img = go.GetComponent<Image>();
            img.color = JsonValue.ToColor(args.Get("background_color"), new Color(0.13f, 0.13f, 0.13f, 1));
            var input = Undo.AddComponent(go, vrcUrlType);

            var textChild = CreateTextChild(go.transform, "Text", "", args.Int("font_size", 24),
                JsonValue.ToColor(args.Get("text_color"), Color.white));
            var tr = textChild.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 1);
            tr.offsetMin = new Vector2(10, 6); tr.offsetMax = new Vector2(-10, -6);
            textChild.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            textChild.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            textChild.GetComponent<Text>().supportRichText = false;

            var ph = CreateTextChild(go.transform, "Placeholder", args.Str("placeholder", "Enter URL..."),
                args.Int("font_size", 24), new Color(1, 1, 1, 0.5f));
            var phr = ph.GetComponent<RectTransform>();
            phr.anchorMin = new Vector2(0, 0); phr.anchorMax = new Vector2(1, 1);
            phr.offsetMin = new Vector2(10, 6); phr.offsetMax = new Vector2(-10, -6);
            ph.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            ph.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            ph.GetComponent<Text>().fontStyle = FontStyle.Italic;

            SetFieldOrProp(input, "m_TextComponent", textChild.GetComponent<Text>());
            SetFieldOrProp(input, "m_Placeholder", ph.GetComponent<Text>());

            if (args.Arr("size") != null)
                AddLayoutElement(go, JsonValue.ToV2(args.Arr("size"), Vector2.zero).x, JsonValue.ToV2(args.Arr("size"), Vector2.zero).y);
            return Created(go);
        }

        private static void SetFieldOrProp(Component c, string name, object value)
        {
            var t = c.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(c, value); return; }
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(c, value); return; }
            f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) { f.SetValue(c, value); return; }
            p = t.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(c, value); return; }
            throw new Exception($"Field/property '{name}' not found on {t.FullName}");
        }

        // --- toggle ---------------------------------------------------------
        private static JsonValue HandleCreateToggle(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var go = new GameObject(args.Str("name", "Toggle"), typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Toggle");
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            var bg = CreateUIObject(go.transform, "Background", new Vector2(28, 28));
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.09f, 0.13f, 0.19f, 1);
            var bgLe = bg.AddComponent<LayoutElement>(); bgLe.preferredWidth = 28; bgLe.preferredHeight = 28;

            var check = CreateUIObject(bg.transform, "Checkmark", Vector2.zero);
            var checkRect = check.GetComponent<RectTransform>(); Stretch(checkRect);
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.2f, 0.88f, 1f, 1f);

            var label = CreateTextChild(go.transform, "Label", args.Str("label", "Toggle"),
                args.Int("font_size", 24), Color.white);
            var le = label.AddComponent<LayoutElement>(); le.flexibleWidth = 1;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = args.Bool("is_on", false);
            if (args.Arr("size") != null)
                AddLayoutElement(go, JsonValue.ToV2(args.Arr("size"), Vector2.zero).x, JsonValue.ToV2(args.Arr("size"), Vector2.zero).y);
            return Created(go);
        }

        // --- slider ---------------------------------------------------------
        private static JsonValue HandleCreateSlider(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var size = args.Arr("size") != null ? JsonValue.ToV2(args.Arr("size"), Vector2.zero) : new Vector2(160, 20);
            var go = new GameObject(args.Str("name", "Slider"), typeof(RectTransform), typeof(Image), typeof(Slider));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Slider");
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.09f, 0.13f, 0.19f, 1);
            var bgRect = go.GetComponent<RectTransform>();
            bgRect.sizeDelta = size;
            var slider = go.GetComponent<Slider>();

            var fillArea = CreateUIObject(go.transform, "Fill Area", Vector2.zero);
            var faRect = fillArea.GetComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0, 0.25f); faRect.anchorMax = new Vector2(1, 0.75f);
            faRect.offsetMin = new Vector2(0, 0); faRect.offsetMax = new Vector2(0, 0);

            var fill = CreateUIObject(fillArea.transform, "Fill", Vector2.zero);
            var fRect = fill.GetComponent<RectTransform>(); Stretch(fRect);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.88f, 1f, 1f);

            var handleArea = CreateUIObject(go.transform, "Handle Slide Area", Vector2.zero);
            var haRect = handleArea.GetComponent<RectTransform>();
            haRect.anchorMin = new Vector2(0, 0); haRect.anchorMax = new Vector2(1, 1);
            haRect.offsetMin = new Vector2(10, 0); haRect.offsetMax = new Vector2(-10, 0);

            var handle = CreateUIObject(handleArea.transform, "Handle", new Vector2(20, 0));
            var hRect = handle.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.5f, 0); hRect.anchorMax = new Vector2(0.5f, 1);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect = fRect;
            slider.handleRect = hRect;
            slider.targetGraphic = handleImg;
            slider.minValue = (float)args.Num("min_value", 0);
            slider.maxValue = (float)args.Num("max_value", 1);
            slider.value = (float)args.Num("value", 0);
            slider.wholeNumbers = args.Bool("whole_numbers", false);
            return Created(go);
        }

        // --- scroll view ----------------------------------------------------
        private static JsonValue HandleCreateScrollView(JsonValue args)
        {
            var parent = ResolveTarget(args.Str("parent", null));
            var size = args.Arr("size") != null ? JsonValue.ToV2(args.Arr("size"), Vector2.zero) : new Vector2(400, 400);
            var go = new GameObject(args.Str("name", "ScrollView"), typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create ScrollView");
            go.GetComponent<RectTransform>().sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.11f, 0.4f);
            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;

            var viewport = CreateUIObject(go.transform, "Viewport", Vector2.zero);
            var vpRect = viewport.GetComponent<RectTransform>(); Stretch(vpRect);
            var vpImg = viewport.AddComponent<Image>(); vpImg.color = new Color(1, 1, 1, 0);
            var mask = viewport.AddComponent<Mask>(); mask.showMaskGraphic = false;

            var content = CreateUIObject(viewport.transform, "Content", Vector2.zero);
            var cRect = content.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1); cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0.5f, 1); cRect.offsetMin = Vector2.zero; cRect.offsetMax = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter; vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var csf = content.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRect;
            scroll.content = cRect;
            return Created(go);
        }

        // --- transforms -----------------------------------------------------
        private static JsonValue HandleSetParent(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var parent = ResolveTarget(args.Str("parent", null));
            Undo.SetTransformParent(go.transform, parent.transform, false, "MCP Set Parent");
            return Created(go);
        }

        private static JsonValue HandleSetAnchors(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var r = go.GetComponent<RectTransform>();
            if (r == null) throw new Exception("Target has no RectTransform");
            if (args.Arr("min") != null) r.anchorMin = JsonValue.ToV2(args.Arr("min"), r.anchorMin);
            if (args.Arr("max") != null) r.anchorMax = JsonValue.ToV2(args.Arr("max"), r.anchorMax);
            return Created(go);
        }

        private static JsonValue HandleSetSizeDelta(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var r = go.GetComponent<RectTransform>();
            if (r == null) throw new Exception("Target has no RectTransform");
            r.sizeDelta = JsonValue.ToV2(args.Arr("size"), r.sizeDelta);
            return Created(go);
        }

        private static JsonValue HandleSetAnchoredPosition(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var r = go.GetComponent<RectTransform>();
            if (r == null) throw new Exception("Target has no RectTransform");
            r.anchoredPosition = JsonValue.ToV2(args.Arr("position"), r.anchoredPosition);
            return Created(go);
        }

        private static JsonValue HandleSetPivot(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var r = go.GetComponent<RectTransform>();
            if (r == null) throw new Exception("Target has no RectTransform");
            r.pivot = JsonValue.ToV2(args.Arr("pivot"), r.pivot);
            return Created(go);
        }

        private static JsonValue HandleSetLocalScale(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            go.transform.localScale = JsonValue.ToV3(args.Arr("scale"), go.transform.localScale);
            return Created(go);
        }

        private static JsonValue HandleSetWorldPosition(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            go.transform.position = JsonValue.ToV3(args.Arr("position"), go.transform.position);
            return Created(go);
        }

        private static JsonValue HandleSetRotationEuler(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            go.transform.localRotation = Quaternion.Euler(JsonValue.ToV3(args.Arr("euler"), go.transform.localEulerAngles));
            return Created(go);
        }

        // --- visuals --------------------------------------------------------
        private static JsonValue HandleSetColor(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var color = JsonValue.ToColor(args.Get("color"), Color.white);
            var img = go.GetComponent<Image>();
            if (img != null) { Undo.RecordObject(img, "MCP Set Color"); img.color = color; return Created(go); }
            var txt = go.GetComponent<Text>();
            if (txt != null) { Undo.RecordObject(txt, "MCP Set Color"); txt.color = color; return Created(go); }
            throw new Exception("Target has no Image or Text to color");
        }

        private static JsonValue HandleSetText(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var text = args.Str("text", "");
            var t = go.GetComponent<Text>();
            if (t != null) { t.text = text; return Created(go); }
            var input = go.GetComponent<InputField>();
            if (input != null) { input.text = text; return Created(go); }
            var label = go.transform.Find("Label")?.GetComponent<Text>();
            if (label != null) { label.text = text; return Created(go); }
            throw new Exception("Target has no Text/InputField/label to set");
        }

        private static JsonValue HandleSetFontSize(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var t = go.GetComponent<Text>();
            if (t == null) throw new Exception("Target has no Text");
            t.fontSize = args.Int("font_size", t.fontSize);
            return Created(go);
        }

        private static JsonValue HandleSetButtonColors(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var button = go.GetComponent<Button>();
            if (button == null) throw new Exception("Target has no Button");
            var colors = button.colors;
            if (args.Has("normal")) colors.normalColor = JsonValue.ToColor(args.Get("normal"), colors.normalColor);
            if (args.Has("highlighted")) colors.highlightedColor = JsonValue.ToColor(args.Get("highlighted"), colors.highlightedColor);
            if (args.Has("pressed")) colors.pressedColor = JsonValue.ToColor(args.Get("pressed"), colors.pressedColor);
            if (args.Has("disabled")) colors.disabledColor = JsonValue.ToColor(args.Get("disabled"), colors.disabledColor);
            if (args.Has("fade_duration")) colors.fadeDuration = (float)args.Num("fade_duration", colors.fadeDuration);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            return Created(go);
        }

        private static JsonValue HandleSetActive(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            go.SetActive(args.Bool("active", true));
            var o = JsonValue.Object(); o.Set("path", PathOf(go)); o.Set("active", go.activeSelf); return o;
        }

        private static JsonValue HandleRemoveComponent(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var typeName = args.Str("component_type", null);
            var t = FindType(typeName);
            if (t == null) throw new Exception("Unknown type: " + typeName);
            var comp = go.GetComponent(t);
            if (comp != null) UnityEngine.Object.DestroyImmediate(comp);
            var o = JsonValue.Object(); o.Set("path", PathOf(go)); o.Set("removed", comp != null); return o;
        }

        private static JsonValue HandleSetLayer(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var lv = args.Get("layer");
            int layer = go.layer;
            if (lv.type == JsonType.String) layer = LayerMask.NameToLayer(lv.str);
            else if (lv.type == JsonType.Number) layer = (int)lv.num;
            go.layer = layer;
            var o = JsonValue.Object(); o.Set("path", PathOf(go)); o.Set("layer", LayerMask.LayerToName(layer)); return o;
        }

        private static JsonValue HandleSetSortingOrder(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) throw new Exception("Target has no Canvas");
            canvas.sortingOrder = args.Int("order", canvas.sortingOrder);
            return Created(go);
        }

        private static JsonValue HandleSetCanvasRenderMode(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) throw new Exception("Target has no Canvas");
            var mode = args.Str("render_mode", "ScreenSpaceOverlay");
            switch (mode)
            {
                case "ScreenSpaceOverlay": canvas.renderMode = RenderMode.ScreenSpaceOverlay; break;
                case "ScreenSpaceCamera":
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    var camName = args.Str("screen_camera", null);
                    if (!string.IsNullOrEmpty(camName))
                    {
                        var cam = ResolveTarget(camName)?.GetComponent<Camera>();
                        if (cam != null) canvas.worldCamera = cam;
                    }
                    break;
                case "WorldSpace":
                    canvas.renderMode = RenderMode.WorldSpace;
                    if (args.Arr("world_position") != null) go.transform.position = JsonValue.ToV3(args.Arr("world_position"), go.transform.position);
                    if (args.Arr("world_rotation_euler") != null) go.transform.rotation = Quaternion.Euler(JsonValue.ToV3(args.Arr("world_rotation_euler"), Vector3.zero));
                    if (args.Has("world_scale")) go.transform.localScale = Vector3.one * (float)args.Num("world_scale", 0.0012);
                    break;
            }
            return Created(go);
        }

        // --- layout helpers -------------------------------------------------
        private static void ApplyLayoutGroup(GameObject go, JsonValue args)
        {
            var type = args.Str("layout", "");
            var pad = ToRectOffset(args.Arr("padding"));
            var spacing = args.Arr("spacing") != null ? JsonValue.ToV2(args.Arr("spacing"), Vector2.zero) : Vector2.zero;
            var align = ParseAnchor(args.Str("alignment", "UpperLeft"), TextAnchor.UpperLeft);
            bool expandW = args.Bool("child_force_expand_width", false);
            bool expandH = args.Bool("child_force_expand_height", false);

            if (type == "vertical")
            {
                var g = EnsureComponent<VerticalLayoutGroup>(go);
                g.padding = pad; g.spacing = spacing.x; g.childAlignment = align;
                g.childForceExpandWidth = expandW; g.childForceExpandHeight = expandH;
            }
            else if (type == "horizontal")
            {
                var g = EnsureComponent<HorizontalLayoutGroup>(go);
                g.padding = pad; g.spacing = spacing.x; g.childAlignment = align;
                g.childForceExpandWidth = expandW; g.childForceExpandHeight = expandH;
            }
            else if (type == "grid")
            {
                var g = EnsureComponent<GridLayoutGroup>(go);
                g.padding = pad; g.spacing = spacing; g.childAlignment = align;
                if (args.Arr("cell_size") != null) g.cellSize = JsonValue.ToV2(args.Arr("cell_size"), new Vector2(100, 100));
                if (args.Has("columns"))
                {
                    g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    g.constraintCount = args.Int("columns", 1);
                }
            }
            else throw new Exception("Unknown layout type: " + type);
        }

        private static JsonValue HandleAddLayoutGroup(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            ApplyLayoutGroup(go, args);
            return Created(go);
        }

        private static JsonValue HandleAddLayoutElement(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var le = EnsureComponent<LayoutElement>(go);
            if (args.Has("preferred_width")) le.preferredWidth = (float)args.Num("preferred_width", 0);
            if (args.Has("preferred_height")) le.preferredHeight = (float)args.Num("preferred_height", 0);
            if (args.Has("min_width")) le.minWidth = (float)args.Num("min_width", 0);
            if (args.Has("min_height")) le.minHeight = (float)args.Num("min_height", 0);
            if (args.Has("flexible_width")) le.flexibleWidth = (float)args.Num("flexible_width", 0);
            if (args.Has("flexible_height")) le.flexibleHeight = (float)args.Num("flexible_height", 0);
            return Created(go);
        }

        private static JsonValue HandleAddShadow(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var shadow = EnsureComponent<Shadow>(go);
            shadow.effectColor = JsonValue.ToColor(args.Get("color"), new Color(0, 0, 0, 0.45f));
            if (args.Arr("effect_distance") != null) shadow.effectDistance = JsonValue.ToV2(args.Arr("effect_distance"), new Vector2(0, -10));
            return Created(go);
        }

        private static JsonValue HandleAddOutline(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var outline = EnsureComponent<Outline>(go);
            outline.effectColor = JsonValue.ToColor(args.Get("color"), Color.black);
            if (args.Arr("effect_distance") != null) outline.effectDistance = JsonValue.ToV2(args.Arr("effect_distance"), new Vector2(1, -1));
            return Created(go);
        }

        // --- object / asset management -------------------------------------
        private static JsonValue HandleRenameObject(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            Undo.RecordObject(go, "MCP Rename");
            go.name = args.Str("new_name", go.name);
            return Created(go);
        }

        private static JsonValue HandleDeleteObject(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var path = PathOf(go);
            Undo.DestroyObjectImmediate(go);
            var o = JsonValue.Object(); o.Set("deleted", true); o.Set("path", path); return o;
        }

        private static JsonValue HandleSaveScene()
        {
            var scene = SceneManager.GetActiveScene();
            bool ok;
            if (string.IsNullOrEmpty(scene.path))
            {
                ok = EditorSceneManager.SaveScene(scene, "Assets/Scenes/Untitled.unity");
            }
            else
            {
                ok = EditorSceneManager.SaveScene(scene);
            }
            var o = JsonValue.Object(); o.Set("saved", ok); o.Set("scene", scene.path); return o;
        }

        private static JsonValue HandleCreatePrefab(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var assetPath = args.Str("asset_path", null);
            if (string.IsNullOrEmpty(assetPath)) throw new Exception("asset_path is required");
            EnsureAssetDir(assetPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, assetPath);
            var o = JsonValue.Object();
            o.Set("prefab", prefab != null);
            o.Set("assetPath", assetPath);
            return o;
        }

        private static JsonValue HandleExportPackage(JsonValue args)
        {
            var assetPaths = args.Arr("asset_paths");
            var exportPath = args.Str("export_path", null);
            if (string.IsNullOrEmpty(exportPath)) throw new Exception("export_path is required");
            var paths = new string[assetPaths.Count];
            for (int i = 0; i < assetPaths.Count; i++)
                paths[i] = assetPaths[i].str;
            AssetDatabase.ExportPackage(paths, exportPath, ExportPackageOptions.Recurse);
            var o = JsonValue.Object();
            o.Set("exported", true);
            o.Set("exportPath", exportPath);
            o.Set("assetCount", paths.Length);
            return o;
        }

        private static JsonValue HandleCreateMaterial(JsonValue args)
        {
            var assetPath = args.Str("asset_path", null);
            if (string.IsNullOrEmpty(assetPath)) throw new Exception("asset_path is required");
            EnsureAssetDir(assetPath);
            var shaderName = args.Str("shader", "UI/Default");
            var shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (args.Has("color")) mat.color = JsonValue.ToColor(args.Get("color"), Color.white);
            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.SaveAssets();
            var o = JsonValue.Object(); o.Set("material", true); o.Set("assetPath", assetPath); return o;
        }

        private static void EnsureAssetDir(string assetPath)
        {
            var full = AssetPathToFullPath(assetPath);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            var rel = assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath.TrimStart('/');
            return Path.Combine(Application.dataPath, rel);
        }

        private static JsonValue HandleAddComponent(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var typeName = args.Str("component_type", null);
            var t = FindType(typeName);
            if (t == null) throw new Exception("Unknown component type: " + typeName);
            var c = go.GetComponent(t);
            if (c == null) c = Undo.AddComponent(go, t);
            var o = JsonValue.Object(); o.Set("path", PathOf(go)); o.Set("component", t.FullName); return o;
        }

        private static JsonValue HandleSetComponentProperty(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var typeName = args.Str("component_type", null);
            var propName = args.Str("property", null);
            var t = FindType(typeName);
            if (t == null) throw new Exception("Unknown component type: " + typeName);
            var c = go.GetComponent(t);
            if (c == null) throw new Exception("Component not found on target: " + typeName);

            var field = t.GetField(propName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                Undo.RecordObject(c, "MCP Set Property");
                field.SetValue(c, ConvertValue(args.Get("value"), field.FieldType));
                return Created(go);
            }
            var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                Undo.RecordObject(c, "MCP Set Property");
                prop.SetValue(c, ConvertValue(args.Get("value"), prop.PropertyType));
                return Created(go);
            }
            field = t.GetField(propName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                Undo.RecordObject(c, "MCP Set Property");
                field.SetValue(c, ConvertValue(args.Get("value"), field.FieldType));
                return Created(go);
            }
            prop = t.GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                Undo.RecordObject(c, "MCP Set Property");
                prop.SetValue(c, ConvertValue(args.Get("value"), prop.PropertyType));
                return Created(go);
            }
            throw new Exception($"Property '{propName}' not found on {typeName}");
        }

        private static object ConvertValue(JsonValue v, Type targetType)
        {
            if (targetType == typeof(string)) return v.type == JsonType.String ? v.str : Json.Stringify(v);
            if (targetType == typeof(float)) return (float)(v.type == JsonType.Number ? v.num : 0);
            if (targetType == typeof(double)) return v.type == JsonType.Number ? v.num : 0;
            if (targetType == typeof(int)) return (int)Math.Round(v.type == JsonType.Number ? v.num : 0);
            if (targetType == typeof(bool)) return v.type == JsonType.Bool ? v.b : v.str == "true";
            if (targetType == typeof(Color)) return JsonValue.ToColor(v, Color.white);
            if (targetType == typeof(Vector2)) return JsonValue.ToV2(v.arr, Vector2.zero);
            if (targetType == typeof(Vector3)) return JsonValue.ToV3(v.arr, Vector3.zero);
            if (targetType == typeof(Vector4))
            {
                if (v.arr != null && v.arr.Count >= 4) return new Vector4((float)v.arr[0].num, (float)v.arr[1].num, (float)v.arr[2].num, (float)v.arr[3].num);
                return Vector4.zero;
            }
            if (targetType.IsEnum)
            {
                var name = v.type == JsonType.String ? v.str : ((int)Math.Round(v.num)).ToString();
                return Enum.Parse(targetType, name, true);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType) && v.type == JsonType.String)
            {
                if (string.IsNullOrEmpty(v.str)) return null;
                var refGo = ResolveTarget(v.str);
                if (targetType == typeof(GameObject)) return refGo;
                return refGo.GetComponent(targetType);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType) && v.type == JsonType.Null) return null;
            return null;
        }

        // --- udon script ----------------------------------------------------
        private static JsonValue HandleCreateUdonScript(JsonValue args)
        {
            var assetPath = args.Str("asset_path", null);
            var className = args.Str("class_name", "UdonScript");
            if (string.IsNullOrEmpty(assetPath)) throw new Exception("asset_path is required");
            if (!assetPath.EndsWith(".cs")) assetPath += ".cs";
            EnsureAssetDir(assetPath);
            var full = AssetPathToFullPath(assetPath);
            var body = args.Str("body", null);
            bool fileExisted = File.Exists(full);
            if (!fileExisted || !string.IsNullOrEmpty(body))
            {
                if (string.IsNullOrEmpty(body)) body = DefaultUdonScaffold(className);
                File.WriteAllText(full, body);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // Create the UdonSharpProgramAsset that UdonSharp needs to compile
            // the script into a Udon program. Without this, attach_udon_behaviour
            // fails with "Unable to find valid U# program asset". This mirrors
            // what the UdonSharp editor's "Create Script" menu does.
            string programAssetPath = System.IO.Path.ChangeExtension(assetPath, ".asset");
            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            bool programCreated = false;
            if (monoScript != null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programAssetPath);
                if (existing == null)
                {
                    var newProgramAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    newProgramAsset.sourceCsScript = monoScript;
                    AssetDatabase.CreateAsset(newProgramAsset, programAssetPath);
                    programCreated = true;
                }
                else
                {
                    programCreated = true; // already exists
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var o = JsonValue.Object();
            o.Set("created", true);
            o.Set("assetPath", assetPath);
            o.Set("className", className);
            o.Set("programAssetPath", programAssetPath);
            o.Set("programAssetCreated", programCreated);
            o.Set("note", "Type becomes available after Unity recompiles.");
            return o;
        }

        private static string DefaultUdonScaffold(string className)
        {
            return
"using UdonSharp;\n" +
"using UnityEngine;\n" +
"using VRC.SDKBase;\n" +
"using VRC.Udon.Common.Interfaces;\n" +
"\n" +
"[UdonBehaviourSyncMode(BehaviourSyncMode.None)]\n" +
"public class " + className + " : UdonSharpBehaviour\n" +
"{\n" +
"    void Start()\n" +
"    {\n" +
"        \n" +
"    }\n" +
"}\n";
        }

        // --- attach UdonSharp behaviour -------------------------------------
        private static JsonValue HandleAttachUdonBehaviour(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var typeName = args.Str("class_name", null);
            var t = FindType(typeName);
            if (t == null) throw new Exception("Unknown UdonSharp type: " + typeName);
            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(t))
                throw new Exception(typeName + " does not extend UdonSharpBehaviour");

            // Remove any existing raw component of this type first to avoid duplicates.
            var existing = go.GetComponent(t);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            // UdonSharpUndo.AddComponent adds the proxy + runs RunBehaviourSetupWithUndo
            // which creates the backing UdonBehaviour with the compiled program asset.
            var proxy = UdonSharpUndo.AddComponent(go, t);
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);

            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("proxyType", t.FullName);
            o.Set("hasUdonBehaviour", backing != null);
            return o;
        }

        // --- refresh UdonSharp program assets -------------------------------
        private static JsonValue HandleRefreshUdonSharp()
        {
            // Trigger UdonSharp's "Refresh All UdonSharp Assets" menu item,
            // which scans for UdonSharpBehaviour scripts and creates/updates
            // their program assets. Needed after create_udon_script before
            // attach_udon_behaviour will work.
            bool ok = EditorApplication.ExecuteMenuItem("VRChat SDK/Udon Sharp/Refresh All UdonSharp Assets");
            var o = JsonValue.Object();
            o.Set("menuExecuted", ok);
            // Also force a general asset refresh.
            AssetDatabase.Refresh();
            return o;
        }

        // --- wire button onClick to UdonBehaviour SendCustomEvent -----------
        // UdonSharp cannot compile AddListener(delegate), so we wire button
        // events in the editor with persistent UnityEvent listeners that call
        // SendCustomEvent on the UdonBehaviour at runtime.
        private static JsonValue HandleWireButtonToUdon(JsonValue args)
        {
            var buttonGo = ResolveTarget(args.Str("button", null));
            var button = buttonGo.GetComponent<Button>();
            if (button == null) throw new Exception("Target has no Button: " + args.Str("button", ""));

            var udonGo = ResolveTarget(args.Str("udon_target", null));
            var udon = udonGo.GetComponent<VRC.Udon.UdonBehaviour>();
            if (udon == null) throw new Exception("Target has no UdonBehaviour: " + args.Str("udon_target", ""));

            var eventName = args.Str("event_name", null);
            if (string.IsNullOrEmpty(eventName)) throw new Exception("event_name is required");

            // Use SerializedObject to add a persistent onClick listener that calls
            // udon.SendCustomEvent(eventName). This is the same mechanism the
            // Unity Inspector uses when you add a button event in the editor.
            var so = new SerializedObject(button);
            var events = so.FindProperty("m_OnClick");
            if (events == null) throw new Exception("Could not find m_OnClick on Button");

            var persistentCalls = events.FindPropertyRelative("m_PersistentCalls.m_Calls");

            // Clear any existing persistent calls to avoid duplicates on re-wire.
            persistentCalls.ClearArray();

            // Add one new persistent call.
            persistentCalls.InsertArrayElementAtIndex(0);
            var call = persistentCalls.GetArrayElementAtIndex(0);

            // The target is the UdonBehaviour component itself.
            call.FindPropertyRelative("m_Target").objectReferenceValue = udon;

            // CRITICAL: m_TargetAssemblyTypeName is the field Unity uses at
            // runtime to find the target type via reflection. Without it the
            // persistent listener silently fails to invoke the method.
            // Format: "FullNamespace.Type, AssemblyName"
            var typeNameProp = call.FindPropertyRelative("m_TargetAssemblyTypeName");
            if (typeNameProp != null)
            {
                var t = udon.GetType();
                typeNameProp.stringValue = t.FullName + ", " + t.Assembly.GetName().Name;
            }

            call.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
            call.FindPropertyRelative("m_Mode").enumValueIndex = 4; // PersistentListenerMode.String
            call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = eventName;
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // UnityEventCallState.RuntimeOnly

            so.ApplyModifiedProperties();

            var o = JsonValue.Object();
            o.Set("button", PathOf(buttonGo));
            o.Set("udonTarget", PathOf(udonGo));
            o.Set("eventName", eventName);
            o.Set("wired", true);
            return o;
        }

        // --- VRC_UiShape -----------------------------------------------------
        // VRChat requires VRC_UiShape on Canvas GameObjects for UI interaction.
        // This handler adds it, ensures a BoxCollider, and marks the scene dirty
        // so it actually serializes on save.
        private static JsonValue HandleAddVrcUiShape(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));

            // Find the VRCUiShape type (SDK3: VRC.SDK3.Components.VRCUiShape)
            System.Type uiShapeType = null;
            string[] typeNames = { "VRC.SDK3.Components.VRCUiShape", "VRC.SDKBase.VRC_UiShape" };
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var name in typeNames)
                    {
                        uiShapeType = asm.GetType(name);
                        if (uiShapeType != null) break;
                    }
                    if (uiShapeType != null) break;
                }
                catch { }
            }
            if (uiShapeType == null) throw new Exception("VRCUiShape type not found in any assembly");

            // Add VRC_UiShape if not present
            var existing = go.GetComponent(uiShapeType);
            if (existing == null)
            {
                // Try Undo.AddComponent first, fall back to direct AddComponent
                existing = Undo.AddComponent(go, uiShapeType);
                if (existing == null)
                {
                    existing = go.AddComponent(uiShapeType);
                }
                if (existing == null) throw new Exception("Failed to add VRC_UiShape (both Undo.AddComponent and AddComponent returned null)");
            }

            // Ensure BoxCollider with correct size
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(go);
            var rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                col.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 0.01f);
                col.center = Vector3.zero;
            }

            // Mark dirty so it serializes on save
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("uiShape", true);
            o.Set("boxColliderSize", col.size.ToString());
            return o;
        }

        // --- Texture import settings -----------------------------------------
        private static JsonValue HandleSetSpriteImport(JsonValue args)
        {
            var assetPath = args.Str("asset_path", null);
            if (string.IsNullOrEmpty(assetPath)) throw new Exception("asset_path is required");

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new Exception("Not a texture: " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();

            var o = JsonValue.Object();
            o.Set("assetPath", assetPath);
            o.Set("textureType", "Sprite");
            return o;
        }

        // --- Sibling reorder --------------------------------------------------
        private static JsonValue HandleSetSiblingIndex(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            int index = (int)args.Num("index", -1);
            if (index < 0) throw new Exception("index must be >= 0");
            go.transform.SetSiblingIndex(index);
            EditorUtility.SetDirty(go);
            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("index", index);
            return o;
        }

        // --- Set Image Sprite ------------------------------------------------
        private static JsonValue HandleSetImageSprite(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var img = go.GetComponent<Image>();
            if (img == null) throw new Exception("Target has no Image component: " + args.Str("target", ""));

            var spritePath = args.Str("sprite_path", null);
            if (string.IsNullOrEmpty(spritePath)) throw new Exception("sprite_path is required");

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) throw new Exception("Sprite not found at: " + spritePath);

            img.sprite = sprite;
            img.preserveAspect = args.Bool("preserve_aspect", true);
            EditorUtility.SetDirty(go);

            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("sprite", sprite.name);
            return o;
        }

        // --- Set Renderer Material -------------------------------------------
        private static JsonValue HandleSetRendererMaterial(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) throw new Exception("Target has no Renderer: " + args.Str("target", ""));

            var matPath = args.Str("material_path", null);
            if (!string.IsNullOrEmpty(matPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) throw new Exception("Material not found at: " + matPath);
                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(go);
                var o = JsonValue.Object();
                o.Set("path", PathOf(go));
                o.Set("material", mat.name);
                return o;
            }

            // Try by GUID
            var guid = args.Str("material_guid", null);
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) throw new Exception("No asset found for GUID: " + guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) throw new Exception("GUID does not point to a Material: " + guid);
                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(go);
                var o = JsonValue.Object();
                o.Set("path", PathOf(go));
                o.Set("material", mat.name);
                return o;
            }

            throw new Exception("Either material_path or material_guid is required");
        }

        // --- Set MeshFilter Mesh --------------------------------------------
        private static JsonValue HandleSetMeshFilter(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) throw new Exception("Target has no MeshFilter: " + args.Str("target", ""));

            // Try to find mesh by name
            var meshName = args.Str("mesh_name", null);
            if (!string.IsNullOrEmpty(meshName))
            {
                // Try builtin meshes first
                var builtin = GetBuiltinMesh(meshName);
                if (builtin != null)
                {
                    mf.sharedMesh = builtin;
                    EditorUtility.SetDirty(go);
                    var o = JsonValue.Object();
                    o.Set("path", PathOf(go));
                    o.Set("mesh", builtin.name);
                    return o;
                }
                // Try asset path
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshName);
                if (mesh != null)
                {
                    mf.sharedMesh = mesh;
                    EditorUtility.SetDirty(go);
                    var o = JsonValue.Object();
                    o.Set("path", PathOf(go));
                    o.Set("mesh", mesh.name);
                    return o;
                }
            }

            // Try by GUID
            var guid = args.Str("mesh_guid", null);
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh != null)
                    {
                        mf.sharedMesh = mesh;
                        EditorUtility.SetDirty(go);
                        var o = JsonValue.Object();
                        o.Set("path", PathOf(go));
                        o.Set("mesh", mesh.name);
                        return o;
                    }
                }
            }

            throw new Exception("Could not find mesh. Use mesh_name (e.g. 'Quad') or mesh_guid.");
        }

        private static Mesh GetBuiltinMesh(string name)
        {
            // Resources.GetBuiltinResource takes a path like "Quad.fbx"
            var path = name + ".fbx";
            var mesh = Resources.GetBuiltinResource<Mesh>(path);
            if (mesh != null) return mesh;
            // Also try without extension
            mesh = Resources.GetBuiltinResource<Mesh>(name);
            return mesh;
        }

        // --- Set Mirror Reflect Layers ---------------------------------------
        private static JsonValue HandleSetMirrorLayers(JsonValue args)
        {
            var go = ResolveTarget(args.Str("target", null));
            var mirrorType = FindType("VRC.SDK3.Components.VRCMirrorReflection");
            if (mirrorType == null) mirrorType = FindType("VRC.SDKBase.VRC_MirrorReflection");
            if (mirrorType == null) throw new Exception("VRCMirrorReflection type not found");

            var comp = go.GetComponent(mirrorType);
            if (comp == null) throw new Exception("Target has no VRCMirrorReflection: " + args.Str("target", ""));

            // Set m_ReflectLayers via SerializedObject so LayerMask serializes correctly
            var so = new SerializedObject(comp);
            var prop = so.FindProperty("m_ReflectLayers");
            if (prop == null) throw new Exception("m_ReflectLayers property not found");

            int bits = (int)args.Num("layers", -1);
            prop.intValue = bits;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(go);

            var o = JsonValue.Object();
            o.Set("path", PathOf(go));
            o.Set("reflectLayers", bits);
            return o;
        }
    }
}

