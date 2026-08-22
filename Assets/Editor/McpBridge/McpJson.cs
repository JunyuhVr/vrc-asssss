// Minimal, dependency-free JSON parser + serializer used by the MCP UI bridge.
// Produces a JsonValue variant that handlers read ergonomically (Get/Str/Num/...).
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace McpBridge
{
    public enum JsonType { Null, Bool, Number, String, Array, Object }

    public sealed class JsonValue
    {
        public JsonType type;
        public bool b;
        public double num;
        public string str;
        public List<JsonValue> arr;
        public Dictionary<string, JsonValue> obj;

        // --- factories ---
        public static JsonValue Null() => new JsonValue { type = JsonType.Null };
        public static JsonValue From(bool v) => new JsonValue { type = JsonType.Bool, b = v };
        public static JsonValue From(double v) => new JsonValue { type = JsonType.Number, num = v };
        public static JsonValue From(string v) => new JsonValue { type = v == null ? JsonType.Null : JsonType.String, str = v ?? "" };
        public static JsonValue FromArray(params JsonValue[] items) => new JsonValue { type = JsonType.Array, arr = new List<JsonValue>(items) };

        public static JsonValue Object()
        {
            var v = new JsonValue { type = JsonType.Object, obj = new Dictionary<string, JsonValue>() };
            return v;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            obj[key] = value;
            return this;
        }

        public JsonValue Set(string key, bool value) { obj[key] = From(value); return this; }
        public JsonValue Set(string key, double value) { obj[key] = From(value); return this; }
        public JsonValue Set(string key, int value) { obj[key] = From((double)value); return this; }
        public JsonValue Set(string key, string value) { obj[key] = From(value); return this; }

        // --- readers ---
        public bool Has(string key) => type == JsonType.Object && obj != null && obj.ContainsKey(key) && obj[key] != null && obj[key].type != JsonType.Null;

        public JsonValue Get(string key)
        {
            if (type != JsonType.Object || obj == null || !obj.ContainsKey(key)) return Null();
            var v = obj[key];
            return v ?? Null();
        }

        public string Str(string key, string def)
        {
            var v = Get(key);
            if (v.type == JsonType.String) return v.str;
            if (v.type == JsonType.Number) return v.num.ToString(CultureInfo.InvariantCulture);
            if (v.type == JsonType.Bool) return v.b ? "true" : "false";
            return def;
        }

        public double Num(string key, double def)
        {
            var v = Get(key);
            if (v.type == JsonType.Number) return v.num;
            if (v.type == JsonType.String && double.TryParse(v.str, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            if (v.type == JsonType.Bool) return v.b ? 1 : 0;
            return def;
        }

        public int Int(string key, int def) => (int)System.Math.Round(Num(key, def));

        public bool Bool(string key, bool def)
        {
            var v = Get(key);
            if (v.type == JsonType.Bool) return v.b;
            if (v.type == JsonType.Number) return v.num != 0;
            if (v.type == JsonType.String) return v.str == "true" || v.str == "1";
            return def;
        }

        public List<JsonValue> Arr(string key)
        {
            var v = Get(key);
            return v.type == JsonType.Array ? v.arr : null;
        }

        // --- typed conversions ---
        public static UnityEngine.Vector2 ToV2(List<JsonValue> a, UnityEngine.Vector2 def)
        {
            if (a == null || a.Count < 2) return def;
            return new UnityEngine.Vector2((float)a[0].num, (float)a[1].num);
        }

        public static UnityEngine.Vector3 ToV3(List<JsonValue> a, UnityEngine.Vector3 def)
        {
            if (a == null || a.Count < 3) return def;
            return new UnityEngine.Vector3((float)a[0].num, (float)a[1].num, (float)a[2].num);
        }

        public static UnityEngine.Color ToColor(JsonValue v, UnityEngine.Color def)
        {
            if (v == null) return def;
            if (v.type == JsonType.Array)
            {
                var a = v.arr;
                if (a == null || a.Count < 3) return def;
                float r = (float)a[0].num, g = (float)a[1].num, b = (float)a[2].num;
                float alpha = a.Count >= 4 ? (float)a[3].num : 1f;
                return new UnityEngine.Color(r, g, b, alpha);
            }
            if (v.type == JsonType.String) return ParseHex(v.str, def);
            return def;
        }

        public static UnityEngine.Color ParseHex(string hex, UnityEngine.Color def)
        {
            if (string.IsNullOrEmpty(hex)) return def;
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length == 6) hex = hex + "FF";
            if (hex.Length != 8) return def;
            if (!byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)) return def;
            if (!byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)) return def;
            if (!byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return def;
            if (!byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a)) return def;
            return new UnityEngine.Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }

    public static class Json
    {
        public static JsonValue Parse(string text)
        {
            int i = 0;
            var v = ParseValue(text, ref i);
            SkipWs(text, ref i);
            return v;
        }

        public static string Stringify(JsonValue v)
        {
            var sb = new StringBuilder();
            WriteValue(sb, v);
            return sb.ToString();
        }

        // --- parser ---
        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return JsonValue.Null();
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return JsonValue.From(ParseString(s, ref i));
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') { i += 4; return JsonValue.Null(); }
            return ParseNumber(s, ref i);
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            var v = JsonValue.Object();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return v;
            }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                var val = ParseValue(s, ref i);
                v.obj[key] = val;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == '}') i++;
                break;
            }
            return v;
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            var v = new JsonValue { type = JsonType.Array, arr = new List<JsonValue>() };
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return v;
            }
            while (true)
            {
                v.arr.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == ']') i++;
                break;
            }
            return v;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 3 < s.Length)
                            {
                                string hex = s.Substring(i, 4);
                                i += 4;
                                sb.Append((char)int.Parse(hex, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static JsonValue ParseBool(string s, ref int i)
        {
            if (s[i] == 't') { i += 4; return JsonValue.From(true); }
            i += 5; return JsonValue.From(false);
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "-+0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var d);
            return JsonValue.From(d);
        }

        // --- serializer ---
        private static void WriteValue(StringBuilder sb, JsonValue v)
        {
            if (v == null || v.type == JsonType.Null) { sb.Append("null"); return; }
            switch (v.type)
            {
                case JsonType.Bool: sb.Append(v.b ? "true" : "false"); break;
                case JsonType.Number: sb.Append(v.num.ToString(CultureInfo.InvariantCulture)); break;
                case JsonType.String: WriteString(sb, v.str); break;
                case JsonType.Array:
                    sb.Append('[');
                    for (int k = 0; k < v.arr.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        WriteValue(sb, v.arr[k]);
                    }
                    sb.Append(']');
                    break;
                case JsonType.Object:
                    sb.Append('{');
                    int j = 0;
                    foreach (var kv in v.obj)
                    {
                        if (j++ > 0) sb.Append(',');
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        WriteValue(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
