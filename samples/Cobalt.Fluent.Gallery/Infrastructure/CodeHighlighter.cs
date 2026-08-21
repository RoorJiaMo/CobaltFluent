namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>一段着色区间。Start/Length 落在本行文本上。</summary>
public readonly record struct TokenSpan(int Start, int Length, TokenKind Kind);

public enum TokenKind
{
    Plain,
    Comment,
    String,
    Keyword,   // C# 关键字 / XAML 标签名
}

/// <summary>源码查看器里的一行：行号、文本、着色区间（按 Start 升序，互不重叠）。</summary>
public sealed record CodeLine(int Number, string Text, IReadOnlyList<TokenSpan> Spans);

/// <summary>
/// 轻量语法着色。零依赖是全库前提，所以不引编辑器组件，
/// 自己扫一遍：只认注释 / 字符串 / 关键字（XAML 里是标签名）三类，
/// 类型名、属性名一律不追——三种颜色已经够把代码读开了，追全等于重写编译器。
///
/// 逐行产出、跨行状态（块注释、逐字字符串）单独结转，
/// 这样查看器可以按行虚拟化，千行的主题文件也只画可见的几十行。
/// </summary>
public static class CodeHighlighter
{
    private static readonly HashSet<string> CsKeywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "get", "goto", "if", "implicit", "in", "init", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "not", "null", "object", "operator", "out",
        "override", "params", "partial", "private", "protected", "public", "readonly", "record",
        "ref", "return", "sbyte", "sealed", "set", "short", "sizeof", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "ushort", "using", "var", "virtual", "void", "volatile", "when", "where",
        "while", "with", "yield", "nameof", "and", "or", "required", "file",
    ];

    public static IReadOnlyList<CodeLine> Highlight(string source, bool isXaml)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var result = new List<CodeLine>(lines.Length);

        // 跨行状态
        var inXmlComment = false;      // <!-- … -->
        var inXmlTag = false;          // 多行标签：属性折行的 <Border \n  Background=…
        var inCsBlockComment = false;  // /* … */
        var inCsVerbatim = false;      // @"…" 折行

        for (var i = 0; i < lines.Length; i++)
        {
            var text = lines[i];
            var spans = new List<TokenSpan>();

            if (isXaml)
                ScanXaml(text, spans, ref inXmlComment, ref inXmlTag);
            else
                ScanCs(text, spans, ref inCsBlockComment, ref inCsVerbatim);

            result.Add(new CodeLine(i + 1, text, spans));
        }

        return result;
    }

    // ======================= XAML =======================

    private static void ScanXaml(string s, List<TokenSpan> spans, ref bool inComment, ref bool inTag)
    {
        var p = 0;
        while (p < s.Length)
        {
            if (inComment)
            {
                var end = s.IndexOf("-->", p, StringComparison.Ordinal);
                if (end < 0) { spans.Add(new(p, s.Length - p, TokenKind.Comment)); return; }
                spans.Add(new(p, end + 3 - p, TokenKind.Comment));
                p = end + 3;
                inComment = false;
                continue;
            }

            if (inTag)
            {
                // 标签体内：引号里是字符串，> 收尾。属性名保持素色。
                var c = s[p];
                if (c is '"' or '\'')
                {
                    var close = s.IndexOf(c, p + 1);
                    if (close < 0) { spans.Add(new(p, s.Length - p, TokenKind.String)); return; }
                    spans.Add(new(p, close + 1 - p, TokenKind.String));
                    p = close + 1;
                    continue;
                }
                if (c == '>')
                {
                    spans.Add(new(p, 1, TokenKind.Keyword));
                    p++;
                    inTag = false;
                    continue;
                }
                if (c == '/' && p + 1 < s.Length && s[p + 1] == '>')
                {
                    spans.Add(new(p, 2, TokenKind.Keyword));
                    p += 2;
                    inTag = false;
                    continue;
                }
                p++;
                continue;
            }

            if (s.AsSpan(p).StartsWith("<!--"))
            {
                inComment = true;
                continue;
            }

            if (s[p] == '<')
            {
                // <、可选的 /，加元素名，整段算标签色
                var q = p + 1;
                if (q < s.Length && s[q] is '/' or '?' or '!') q++;
                while (q < s.Length && (char.IsLetterOrDigit(s[q]) || s[q] is '.' or ':' or '_' or '-')) q++;
                spans.Add(new(p, q - p, TokenKind.Keyword));
                p = q;
                inTag = true;
                continue;
            }

            p++;
        }
    }

    // ======================= C# =======================

    private static void ScanCs(string s, List<TokenSpan> spans, ref bool inBlock, ref bool inVerbatim)
    {
        var p = 0;
        while (p < s.Length)
        {
            if (inBlock)
            {
                var end = s.IndexOf("*/", p, StringComparison.Ordinal);
                if (end < 0) { spans.Add(new(p, s.Length - p, TokenKind.Comment)); return; }
                spans.Add(new(p, end + 2 - p, TokenKind.Comment));
                p = end + 2;
                inBlock = false;
                continue;
            }

            if (inVerbatim)
            {
                // @"…" 里 "" 是转义的引号，单个 " 收尾
                var q = p;
                while (q < s.Length)
                {
                    if (s[q] == '"')
                    {
                        if (q + 1 < s.Length && s[q + 1] == '"') { q += 2; continue; }
                        q++;
                        inVerbatim = false;
                        break;
                    }
                    q++;
                }
                spans.Add(new(p, q - p, TokenKind.String));
                p = q;
                continue;
            }

            var c = s[p];

            if (c == '/' && p + 1 < s.Length && s[p + 1] == '/')
            {
                spans.Add(new(p, s.Length - p, TokenKind.Comment));
                return;
            }

            if (c == '/' && p + 1 < s.Length && s[p + 1] == '*')
            {
                inBlock = true;
                continue;
            }

            if (c == '@' && p + 1 < s.Length && s[p + 1] == '"')
            {
                spans.Add(new(p, 2, TokenKind.String));
                p += 2;
                inVerbatim = true;
                continue;
            }

            if (c == '"')
            {
                var q = p + 1;
                while (q < s.Length && s[q] != '"')
                {
                    if (s[q] == '\\') q++;
                    q++;
                }
                var len = Math.Min(q + 1, s.Length) - p;
                spans.Add(new(p, len, TokenKind.String));
                p += len;
                continue;
            }

            if (c == '\'')
            {
                var q = p + 1;
                while (q < s.Length && s[q] != '\'')
                {
                    if (s[q] == '\\') q++;
                    q++;
                }
                var len = Math.Min(q + 1, s.Length) - p;
                spans.Add(new(p, len, TokenKind.String));
                p += len;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var q = p;
                while (q < s.Length && (char.IsLetterOrDigit(s[q]) || s[q] == '_')) q++;
                if (CsKeywords.Contains(s[p..q]))
                    spans.Add(new(p, q - p, TokenKind.Keyword));
                p = q;
                continue;
            }

            p++;
        }
    }
}
