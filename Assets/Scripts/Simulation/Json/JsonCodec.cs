using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BabyChampion.Simulation
{
    /// <summary>
    /// Minimal JSON for plain data classes: public instance fields of type bool, int, uint, long,
    /// double, float, string, enum (written by name), nested classes, List&lt;T&gt; and arrays.
    /// The Simulation assembly cannot use UnityEngine.JsonUtility, so saves and data export use this.
    /// Doubles are written with round-trip precision, so save/load reproduces state exactly.
    /// </summary>
    public static class JsonCodec
    {
        const BindingFlags FieldFlags = BindingFlags.Public | BindingFlags.Instance;

        public static string Serialize(object value, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, value?.GetType(), pretty, 0);
            return sb.ToString();
        }

        public static T Deserialize<T>(string json) => (T)Deserialize(json, typeof(T));

        public static object Deserialize(string json, Type type)
        {
            var parser = new Parser(json);
            object node = parser.ParseValue();
            parser.ExpectEnd();
            return Convert(node, type, "$");
        }

        static IEnumerable<FieldInfo> Fields(Type type) =>
            type.GetFields(FieldFlags).Where(f => !f.IsNotSerialized && !f.IsInitOnly).OrderBy(f => f.MetadataToken);

        // ---------------------------------------------------------------- writing

        static void Write(StringBuilder sb, object value, Type type, bool pretty, int depth)
        {
            if (value == null) { sb.Append("null"); return; }
            type = value.GetType();

            switch (value)
            {
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: sb.Append(FormatDouble(d)); return;
                case float f: sb.Append(FormatDouble(f)); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case uint u: sb.Append(u.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case Enum e: WriteString(sb, e.ToString()); return;
                case IList list:
                    sb.Append('[');
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        NewLine(sb, pretty, depth + 1);
                        Write(sb, list[k], null, pretty, depth + 1);
                    }
                    if (list.Count > 0) NewLine(sb, pretty, depth);
                    sb.Append(']');
                    return;
            }

            sb.Append('{');
            bool first = true;
            foreach (var field in Fields(type))
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, depth + 1);
                WriteString(sb, field.Name);
                sb.Append(pretty ? ": " : ":");
                Write(sb, field.GetValue(value), field.FieldType, pretty, depth + 1);
            }
            if (!first) NewLine(sb, pretty, depth);
            sb.Append('}');
        }

        static string FormatDouble(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) throw new FormatException($"JSON cannot store {d}");
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
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

        // ---------------------------------------------------------------- reading

        /// <summary>A number kept as text until the target type is known.</summary>
        sealed class JsonNumber
        {
            public readonly string Text;
            public JsonNumber(string text) => Text = text;
        }

        static object Convert(object node, Type type, string path)
        {
            if (node == null)
            {
                if (type.IsValueType) throw new FormatException($"{path}: null for {type.Name}");
                return null;
            }

            if (type == typeof(string)) return node as string ?? throw new FormatException($"{path}: expected a string");
            if (type == typeof(bool)) return node is bool b ? b : throw new FormatException($"{path}: expected true or false");
            if (type.IsEnum)
            {
                if (node is string name) return Enum.Parse(type, name);
                throw new FormatException($"{path}: expected an enum name for {type.Name}");
            }
            if (type == typeof(double) || type == typeof(float) || type == typeof(int) || type == typeof(uint) || type == typeof(long))
            {
                if (!(node is JsonNumber number)) throw new FormatException($"{path}: expected a number");
                if (type == typeof(double)) return double.Parse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (type == typeof(float)) return float.Parse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (type == typeof(int)) return int.Parse(number.Text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (type == typeof(uint)) return uint.Parse(number.Text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                return long.Parse(number.Text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                if (!(node is List<object> items)) throw new FormatException($"{path}: expected an array");
                Type elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                if (type.IsArray)
                {
                    var array = Array.CreateInstance(elementType, items.Count);
                    for (int i = 0; i < items.Count; i++) array.SetValue(Convert(items[i], elementType, $"{path}[{i}]"), i);
                    return array;
                }
                var list = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < items.Count; i++) list.Add(Convert(items[i], elementType, $"{path}[{i}]"));
                return list;
            }

            if (!(node is Dictionary<string, object> obj)) throw new FormatException($"{path}: expected an object for {type.Name}");
            object result = Activator.CreateInstance(type);
            foreach (var field in Fields(type))
            {
                if (obj.TryGetValue(field.Name, out object fieldNode))
                    field.SetValue(result, Convert(fieldNode, field.FieldType, $"{path}.{field.Name}"));
            }
            return result;
        }

        sealed class Parser
        {
            readonly string _text;
            int _pos;

            public Parser(string text) => _text = text ?? throw new ArgumentNullException(nameof(text));

            public void ExpectEnd()
            {
                SkipWhitespace();
                if (_pos != _text.Length) throw Error("unexpected text after the JSON value");
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (_pos >= _text.Length) throw Error("unexpected end");
                char c = _text[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || char.IsDigit(c)) return ParseNumber();
                        throw Error($"unexpected '{c}'");
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>();
                _pos++;
                SkipWhitespace();
                if (Peek() == '}') { _pos++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    if (Peek() != ':') throw Error("expected ':'");
                    _pos++;
                    result[key] = ParseValue();
                    SkipWhitespace();
                    char c = Peek();
                    _pos++;
                    if (c == '}') return result;
                    if (c != ',') throw Error("expected ',' or '}'");
                }
            }

            List<object> ParseArray()
            {
                var result = new List<object>();
                _pos++;
                SkipWhitespace();
                if (Peek() == ']') { _pos++; return result; }
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    char c = Peek();
                    _pos++;
                    if (c == ']') return result;
                    if (c != ',') throw Error("expected ',' or ']'");
                }
            }

            string ParseString()
            {
                if (Peek() != '"') throw Error("expected a string");
                _pos++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (_pos >= _text.Length) throw Error("unterminated string");
                    char c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_pos >= _text.Length) throw Error("unterminated escape");
                    char e = _text[_pos++];
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
                            if (_pos + 4 > _text.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _pos += 4;
                            break;
                        default: throw Error($"bad escape \\{e}");
                    }
                }
            }

            JsonNumber ParseNumber()
            {
                int start = _pos;
                while (_pos < _text.Length && "+-0123456789.eE".IndexOf(_text[_pos]) >= 0) _pos++;
                return new JsonNumber(_text.Substring(start, _pos - start));
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0) throw Error($"expected {word}");
                _pos += word.Length;
            }

            char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

            void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            FormatException Error(string message) => new FormatException($"JSON error at {_pos}: {message}");
        }
    }
}
