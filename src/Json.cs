// Minimal JSON reader/writer. Keeps config.json readable and avoids a
// System.Web / Newtonsoft dependency in a single-file .NET Framework build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sf6Rain
{
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("empty json");
            var p = new Parser(text);
            object value = p.Value();
            p.White();
            if (!p.Done) throw new FormatException("trailing characters at " + p.Pos);
            return value;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append(((bool)v) ? "true" : "false"); return; }
            if (v is int || v is long || v is short || v is byte)
            {
                sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (v is float || v is double || v is decimal)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 1e15)
                    sb.Append(((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture));
                else
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            var dict = v as IDictionary;
            if (dict != null)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(",\n");
                    first = false;
                    Indent(sb, depth + 1);
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(": ");
                    WriteValue(sb, e.Value, depth + 1);
                }
                sb.Append('\n');
                Indent(sb, depth);
                sb.Append('}');
                return;
            }
            var list = v as IList;
            if (list != null)
            {
                if (list.Count == 0) { sb.Append("[]"); return; }
                bool simple = true;
                foreach (object item in list)
                    if (item is IDictionary || item is IList) { simple = false; break; }
                if (simple)
                {
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        WriteValue(sb, list[i], depth + 1);
                    }
                    sb.Append(']');
                    return;
                }
                sb.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",\n");
                    Indent(sb, depth + 1);
                    WriteValue(sb, list[i], depth + 1);
                }
                sb.Append('\n');
                Indent(sb, depth);
                sb.Append(']');
                return;
            }
            WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        static void Indent(StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // typed lookups, tolerant of hand-edited files
        public static Dictionary<string, object> AsObject(object v)
        {
            return v as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        public static List<object> AsArray(object v)
        {
            return v as List<object> ?? new List<object>();
        }

        public static string Str(Dictionary<string, object> o, string key, string fallback)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return fallback;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double Num(Dictionary<string, object> o, string key, double fallback)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return fallback;
            double d;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                                   NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : fallback;
        }

        public static int Int(Dictionary<string, object> o, string key, int fallback)
        {
            return (int)Math.Round(Num(o, key, fallback));
        }

        public static bool Bool(Dictionary<string, object> o, string key, bool fallback)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            bool b;
            return bool.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out b) ? b : fallback;
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;
            public Parser(string s) { _s = s; _i = 0; }
            public int Pos { get { return _i; } }
            public bool Done { get { return _i >= _s.Length; } }

            public void White()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object Value()
            {
                White();
                if (Done) throw new FormatException("unexpected end");
                char c = _s[_i];
                if (c == '{') return Object();
                if (c == '[') return Array();
                if (c == '"') return String();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                return Number();
            }

            Dictionary<string, object> Object()
            {
                var o = new Dictionary<string, object>();
                _i++; White();
                if (!Done && _s[_i] == '}') { _i++; return o; }
                while (true)
                {
                    White();
                    if (Done || _s[_i] != '"') throw new FormatException("expected key at " + _i);
                    string k = String();
                    White();
                    if (Done || _s[_i] != ':') throw new FormatException("expected ':' at " + _i);
                    _i++;
                    o[k] = Value();
                    White();
                    if (Done) throw new FormatException("unclosed object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return o; }
                    throw new FormatException("expected ',' or '}' at " + _i);
                }
            }

            List<object> Array()
            {
                var a = new List<object>();
                _i++; White();
                if (!Done && _s[_i] == ']') { _i++; return a; }
                while (true)
                {
                    a.Add(Value());
                    White();
                    if (Done) throw new FormatException("unclosed array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return a; }
                    throw new FormatException("expected ',' or ']' at " + _i);
                }
            }

            string String()
            {
                _i++;
                var sb = new StringBuilder();
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) break;
                        char e = _s[_i++];
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
                                if (_i + 4 <= _s.Length)
                                {
                                    sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber));
                                    _i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }
                throw new FormatException("unclosed string");
            }

            object Number()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                if (start == _i) throw new FormatException("bad value at " + _i);
                double d;
                if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float,
                                     CultureInfo.InvariantCulture, out d))
                    throw new FormatException("bad number at " + start);
                return d;
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                    throw new FormatException("expected " + word + " at " + _i);
                _i += word.Length;
            }
        }
    }
}
