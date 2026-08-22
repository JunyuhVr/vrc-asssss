// Polls the file-based command queue on the Unity editor main thread, dispatches
// commands to McpUiBuilder, and writes one result file per command. Also writes
// a bridge_status.json the Python server can read.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace McpBridge
{
    public static class McpBridgeServer
    {
        public static bool IsListening { get; private set; }

        public static string QueueDir
        {
            get => _queueDir;
            set
            {
                _queueDir = value;
                EditorPrefs.SetString("McpBridge.QueueDir", value);
                if (IsListening) { StopListening(); StartListening(); }
            }
        }

        public static int Processed { get; private set; }
        public static int Failed { get; private set; }
        public static string LastError { get; private set; }
        public static DateTime LastActivity { get; private set; }

        private static string _queueDir;
        private static long _readOffset;
        private static double _lastPollTime;
        private const double PollIntervalSec = 0.05;

        private static string CommandsPath => Path.Combine(QueueDir, "commands.jsonl");
        private static string ResultsDir => Path.Combine(QueueDir, "results");
        private static string StatusPath => Path.Combine(QueueDir, "bridge_status.json");

        static McpBridgeServer()
        {
            var saved = EditorPrefs.GetString("McpBridge.QueueDir", "");
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved)) _queueDir = saved;
            else _queueDir = DefaultQueueDir();
        }

        public static string DefaultQueueDir()
        {
            // Application.dataPath == <project>/Assets ; bridge lives at <project>/McpBridge
            var proj = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.Combine(proj, "McpBridge").Replace('\\', '/');
        }

        public static void StartListening()
        {
            if (IsListening) return;
            try
            {
                Directory.CreateDirectory(QueueDir);
                Directory.CreateDirectory(ResultsDir);
                CleanOldResults();
                _readOffset = File.Exists(CommandsPath) ? new FileInfo(CommandsPath).Length : 0;
                EditorApplication.update += Poll;
                IsListening = true;
                EditorPrefs.SetBool("McpBridge.Listening", true);
                LastActivity = DateTime.Now;
                WriteStatus();
                UnityEngine.Debug.Log("[MCP UI Bridge] Listening at " + QueueDir);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                IsListening = false;
                UnityEngine.Debug.LogError("[MCP UI Bridge] Failed to start: " + e.Message);
            }
        }

        public static void StopListening()
        {
            if (!IsListening) return;
            EditorApplication.update -= Poll;
            IsListening = false;
            EditorPrefs.SetBool("McpBridge.Listening", false);
            WriteStatus();
            UnityEngine.Debug.Log("[MCP UI Bridge] Stopped.");
        }

        public static void ResetStats()
        {
            Processed = 0;
            Failed = 0;
            LastError = null;
            WriteStatus();
        }

        private static void CleanOldResults()
        {
            try
            {
                foreach (var f in Directory.GetFiles(ResultsDir, "*.json"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        private static void Poll()
        {
            if (!IsListening) return;
            if (EditorApplication.timeSinceStartup - _lastPollTime < PollIntervalSec) return;
            _lastPollTime = EditorApplication.timeSinceStartup;

            try
            {
                if (!File.Exists(CommandsPath))
                {
                    WriteStatusThrottled();
                    return;
                }

                List<string> lines;
                using (var fs = new FileStream(CommandsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length <= _readOffset)
                    {
                        WriteStatusThrottled();
                        return;
                    }
                    fs.Seek(_readOffset, SeekOrigin.Begin);
                    using (var sr = new StreamReader(fs))
                    {
                        var tail = sr.ReadToEnd();
                        _readOffset = fs.Length;
                        lines = new List<string>();
                        foreach (var ln in tail.Split('\n'))
                        {
                            var t = ln.Trim();
                            if (!string.IsNullOrEmpty(t)) lines.Add(t);
                        }
                    }
                }

                foreach (var line in lines) ProcessCommand(line);
                WriteStatusThrottled();
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
        }

        private static void ProcessCommand(string line)
        {
            string id = null;
            try
            {
                var cmd = Json.Parse(line);
                id = cmd.Str("id", null);
                var tool = cmd.Str("tool", null);
                var args = cmd.Get("args");
                if (args == null || args.type != JsonType.Object) args = JsonValue.Object();

                var data = McpUiBuilder.Handle(tool, args);
                WriteResult(id, true, data, null);
                Processed++;
                LastActivity = DateTime.Now;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Failed++;
                LastActivity = DateTime.Now;
                WriteResult(id, false, null, e.Message);
            }
        }

        private static void WriteResult(string id, bool ok, JsonValue data, string error)
        {
            if (string.IsNullOrEmpty(id)) return;
            var res = JsonValue.Object();
            res.Set("id", id);
            res.Set("ok", ok);
            res.Set("data", data ?? JsonValue.Null());
            res.Set("error", error ?? "");
            var path = Path.Combine(ResultsDir, id + ".json");
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, Json.Stringify(res));
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            try { File.Move(tmp, path); } catch { try { File.Copy(tmp, path, true); } catch { } }
        }

        private static DateTime _lastStatusWrite;
        private static void WriteStatusThrottled()
        {
            if ((DateTime.Now - _lastStatusWrite).TotalSeconds < 1) return;
            WriteStatus();
        }

        private static void WriteStatus()
        {
            _lastStatusWrite = DateTime.Now;
            try
            {
                var s = JsonValue.Object();
                s.Set("listening", IsListening);
                s.Set("processed", Processed);
                s.Set("failed", Failed);
                s.Set("lastError", LastError ?? "");
                s.Set("lastActivity", LastActivity.ToString("o"));
                s.Set("unity", UnityEngine.Application.unityVersion);
                s.Set("project", UnityEditor.PlayerSettings.productName);
                s.Set("queueDir", QueueDir);
                s.Set("readOffset", (double)_readOffset);
                Directory.CreateDirectory(QueueDir);
                File.WriteAllText(StatusPath, Json.Stringify(s));
            }
            catch { }
        }
    }
}
