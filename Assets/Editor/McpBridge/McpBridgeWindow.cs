// Editor window for the MCP UI bridge. Menu: Tools > MCP UI Bridge > Show Bridge Window
using System;
using UnityEditor;
using UnityEngine;

namespace McpBridge
{
    public class McpBridgeWindow : EditorWindow
    {
        private const string AutoStartPref = "McpBridge.AutoStart";
        private Vector2 _scroll;

        [MenuItem("Tools/MCP UI Bridge/Show Bridge Window")]
        public static void ShowWindow()
        {
            var w = GetWindow<McpBridgeWindow>("MCP UI Bridge");
            w.minSize = new Vector2(360, 240);
        }

        [InitializeOnLoadMethod]
        private static void AutoStartOnLoad()
        {
            // Re-enable listening after a domain reload (recompile) if it was active before,
            // OR if the user opted into auto-start. Without this, every script edit would
            // silently stop the bridge mid-build.
            bool wasListening = EditorPrefs.GetBool("McpBridge.Listening", false);
            bool autoStart = EditorPrefs.GetBool(AutoStartPref, false);
            if ((wasListening || autoStart) && !EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (!McpBridgeServer.IsListening) McpBridgeServer.StartListening();
                };
            }
        }

        private void OnEnable()
        {
            // refresh state on focus
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(6);
            GUILayout.Label("MCP UI Bridge", EditorStyles.boldLabel);
            GUILayout.Label("Lets an AI (via the Python MCP server) build UI live in this scene.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(8);

            // Queue dir
            EditorGUI.BeginChangeCheck();
            var dir = EditorGUILayout.TextField("Queue Folder", McpBridgeServer.QueueDir);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(dir)) McpBridgeServer.QueueDir = dir;
            if (GUILayout.Button("Reset to project default", EditorStyles.miniButton))
                McpBridgeServer.QueueDir = McpBridgeServer.DefaultQueueDir();

            GUILayout.Space(8);

            // Start / stop
            EditorGUILayout.BeginHorizontal();
            if (!McpBridgeServer.IsListening)
            {
                if (GUILayout.Button("Start Listening", GUILayout.Height(28))) McpBridgeServer.StartListening();
            }
            else
            {
                GUI.color = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button("Stop Listening", GUILayout.Height(28))) McpBridgeServer.StopListening();
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Auto start
            EditorGUI.BeginChangeCheck();
            var auto = EditorGUILayout.Toggle("Auto-start when Unity loads", EditorPrefs.GetBool(AutoStartPref, false));
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(AutoStartPref, auto);

            GUILayout.Space(10);
            DrawStatus();
            GUILayout.Space(8);
            DrawLegend();
        }

        private void DrawStatus()
        {
            GUILayout.Label("Status", EditorStyles.boldLabel);
            var style = new GUIStyle(EditorStyles.label);
            if (McpBridgeServer.IsListening)
            {
                style.normal.textColor = new Color(0.3f, 0.8f, 0.4f);
                GUILayout.Label("● Listening", style);
            }
            else
            {
                style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label("○ Idle", style);
            }

            EditorGUILayout.LabelField("Processed", McpBridgeServer.Processed.ToString());
            EditorGUILayout.LabelField("Failed", McpBridgeServer.Failed.ToString());
            EditorGUILayout.LabelField("Last activity", McpBridgeServer.LastActivity == default ? "—" : McpBridgeServer.LastActivity.ToString("HH:mm:ss"));
            if (!string.IsNullOrEmpty(McpBridgeServer.LastError))
            {
                var es = new GUIStyle(EditorStyles.wordWrappedLabel);
                es.normal.textColor = new Color(0.9f, 0.5f, 0.4f);
                EditorGUILayout.LabelField("Last error", McpBridgeServer.LastError, es);
            }

            if (GUILayout.Button("Reset stats", EditorStyles.miniButton))
            {
                McpBridgeServer.ResetStats();
            }
        }

        private void DrawLegend()
        {
            GUILayout.Space(6);
            GUILayout.Label("How it works", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(90));
            GUILayout.Label(
                "1. Open this window and click Start Listening.\n" +
                "2. In your AI client (Devin), the unity-ui-builder MCP server is configured.\n" +
                "3. Ask the AI to build a UI. It calls tools that write commands to:\n" +
                "   <Queue Folder>/commands.jsonl\n" +
                "4. This window polls that file, executes the Unity calls, and writes results.\n" +
                "5. Keep Unity open while the AI is building.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
    }
}
