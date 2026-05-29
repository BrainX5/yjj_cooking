using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class SimpleJson
{
    public static object Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return Parser.Parse(json);
    }

    public static string Serialize(object value)
    {
        return Serializer.Serialize(value);
    }

    private sealed class Parser : IDisposable
    {
        private const string WordBreak = "{}[],:\"";

        private readonly string json;
        private int index;

        private Parser(string json)
        {
            this.json = json;
        }

        public static object Parse(string json)
        {
            using (var parser = new Parser(json))
            {
                return parser.ParseValue();
            }
        }

        public void Dispose()
        {
        }

        private IDictionary<string, object> ParseObject()
        {
            var table = new Dictionary<string, object>();

            Read();

            while (true)
            {
                switch (NextToken)
                {
                    case Token.None:
                        return null;
                    case Token.CurlyClose:
                        Read();
                        return table;
                    default:
                        var name = ParseString();
                        if (name == null)
                        {
                            return null;
                        }

                        if (NextToken != Token.Colon)
                        {
                            return null;
                        }

                        Read();
                        table[name] = ParseValue();
                        break;
                }

                switch (NextToken)
                {
                    case Token.Comma:
                        Read();
                        break;
                    case Token.CurlyClose:
                        Read();
                        return table;
                    default:
                        return table;
                }
            }
        }

        private IList<object> ParseArray()
        {
            var array = new List<object>();

            Read();

            var parsing = true;
            while (parsing)
            {
                var token = NextToken;
                switch (token)
                {
                    case Token.None:
                        return null;
                    case Token.SquaredClose:
                        Read();
                        parsing = false;
                        break;
                    default:
                        array.Add(ParseValue());
                        break;
                }

                if (!parsing)
                {
                    break;
                }

                switch (NextToken)
                {
                    case Token.Comma:
                        Read();
                        break;
                    case Token.SquaredClose:
                        Read();
                        parsing = false;
                        break;
                    default:
                        parsing = false;
                        break;
                }
            }

            return array;
        }

        private object ParseValue()
        {
            switch (NextToken)
            {
                case Token.String:
                    return ParseString();
                case Token.Number:
                    return ParseNumber();
                case Token.CurlyOpen:
                    return ParseObject();
                case Token.SquaredOpen:
                    return ParseArray();
                case Token.True:
                    return true;
                case Token.False:
                    return false;
                case Token.Null:
                    return null;
                default:
                    return null;
            }
        }

        private string ParseString()
        {
            var builder = new StringBuilder();

            Read();

            var parsing = true;
            while (parsing)
            {
                if (index == json.Length)
                {
                    break;
                }

                var c = Read();
                switch (c)
                {
                    case '"':
                        parsing = false;
                        break;
                    case '\\':
                        if (index == json.Length)
                        {
                            parsing = false;
                            break;
                        }

                        c = Read();
                        switch (c)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                builder.Append(c);
                                break;
                            case 'b':
                                builder.Append('\b');
                                break;
                            case 'f':
                                builder.Append('\f');
                                break;
                            case 'n':
                                builder.Append('\n');
                                break;
                            case 'r':
                                builder.Append('\r');
                                break;
                            case 't':
                                builder.Append('\t');
                                break;
                            case 'u':
                                var hex = json.Substring(index, 4);
                                builder.Append((char)Convert.ToInt32(hex, 16));
                                index += 4;
                                break;
                        }
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        private object ParseNumber()
        {
            var number = ReadWord();
            if (number.IndexOf('.') == -1 &&
                number.IndexOf('e') == -1 &&
                number.IndexOf('E') == -1)
            {
                long.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInt);
                return parsedInt;
            }

            double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble);
            return parsedDouble;
        }

        private void EatWhitespace()
        {
            while (index < json.Length)
            {
                if (!char.IsWhiteSpace(json[index]))
                {
                    break;
                }

                index++;
            }
        }

        private char PeekChar
        {
            get
            {
                return index < json.Length ? json[index] : '\0';
            }
        }

        private char Read()
        {
            return index < json.Length ? json[index++] : '\0';
        }

        private string ReadWord()
        {
            var builder = new StringBuilder();
            while (index < json.Length && !IsWordBreak(PeekChar))
            {
                builder.Append(Read());
            }

            return builder.ToString();
        }

        private Token NextToken
        {
            get
            {
                EatWhitespace();

                if (index == json.Length)
                {
                    return Token.None;
                }

                switch (PeekChar)
                {
                    case '{':
                        return Token.CurlyOpen;
                    case '}':
                        return Token.CurlyClose;
                    case '[':
                        return Token.SquaredOpen;
                    case ']':
                        return Token.SquaredClose;
                    case ',':
                        return Token.Comma;
                    case '"':
                        return Token.String;
                    case ':':
                        return Token.Colon;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '-':
                        return Token.Number;
                }

                var word = ReadWord();
                switch (word)
                {
                    case "false":
                        return Token.False;
                    case "true":
                        return Token.True;
                    case "null":
                        return Token.Null;
                }

                return Token.None;
            }
        }

        private static bool IsWordBreak(char c)
        {
            return char.IsWhiteSpace(c) || WordBreak.IndexOf(c) != -1;
        }

        private enum Token
        {
            None,
            CurlyOpen,
            CurlyClose,
            SquaredOpen,
            SquaredClose,
            Colon,
            Comma,
            String,
            Number,
            True,
            False,
            Null
        }
    }

    private sealed class Serializer
    {
        private readonly StringBuilder builder = new StringBuilder();

        public static string Serialize(object value)
        {
            var serializer = new Serializer();
            serializer.SerializeValue(value);
            return serializer.builder.ToString();
        }

        private void SerializeValue(object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string stringValue)
            {
                SerializeString(stringValue);
                return;
            }

            if (value is bool boolValue)
            {
                builder.Append(boolValue ? "true" : "false");
                return;
            }

            if (value is IDictionary dictionary)
            {
                SerializeObject(dictionary);
                return;
            }

            if (value is IList list)
            {
                SerializeArray(list);
                return;
            }

            if (value is char charValue)
            {
                SerializeString(charValue.ToString());
                return;
            }

            if (value is IConvertible convertible)
            {
                builder.Append(convertible.ToString(CultureInfo.InvariantCulture));
                return;
            }

            SerializeString(value.ToString());
        }

        private void SerializeObject(IDictionary dictionary)
        {
            var first = true;
            builder.Append('{');

            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                SerializeString(entry.Key.ToString());
                builder.Append(':');
                SerializeValue(entry.Value);
                first = false;
            }

            builder.Append('}');
        }

        private void SerializeArray(IList array)
        {
            builder.Append('[');

            var first = true;
            foreach (var obj in array)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                SerializeValue(obj);
                first = false;
            }

            builder.Append(']');
        }

        private void SerializeString(string value)
        {
            builder.Append('"');

            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32 || c > 126)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            builder.Append('"');
        }
    }
}
