using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowDocumentList = System.Windows.Documents.List;

namespace Flow.Launcher.Plugin.Obsidian.Utilities;

public sealed class PreviewPalette
{
    private const string WindowBackgroundKey = "Color01B";
    private const string BodyTextKey = "Color04B";
    private const string TitleTextKey = "Color05B";
    private const string SubtleTextKey = "Color08B";
    private const string CodeBackgroundKey = "Color06B";
    private const string BorderKey = "Color21B";
    private const string LinkKey = "Color18B";

    private static readonly Brush FallbackWindowBackground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
    private static readonly Brush FallbackBodyText = new SolidColorBrush(Color.FromRgb(0xcf, 0xcf, 0xcf));
    private static readonly Brush FallbackTitleText = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
    private static readonly Brush FallbackSubtleText = new SolidColorBrush(Color.FromRgb(0x87, 0x87, 0x87));
    private static readonly Brush FallbackCodeBackground = new SolidColorBrush(Color.FromRgb(0x2d, 0x2d, 0x2d));
    private static readonly Brush FallbackBorder = new SolidColorBrush(Color.FromRgb(0x46, 0x46, 0x46));
    private static readonly Brush FallbackLink = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

    public Brush WindowBackground { get; }
    public Brush BodyText { get; }
    public Brush TitleText { get; }
    public Brush SubtleText { get; }
    public Brush CodeBackground { get; }
    public Brush Border { get; }
    public Brush Link { get; }
    public Brush HighlightBackground { get; }

    private PreviewPalette(
        Brush windowBackground,
        Brush bodyText,
        Brush titleText,
        Brush subtleText,
        Brush codeBackground,
        Brush border,
        Brush link
    )
    {
        WindowBackground = windowBackground;
        BodyText = bodyText;
        TitleText = titleText;
        SubtleText = subtleText;
        CodeBackground = codeBackground;
        Border = border;
        Link = link;
        HighlightBackground = new SolidColorBrush(Color.FromArgb(0x46, 0xff, 0xd8, 0x3b));
    }

    // The ColorXXB keys resolve against Flow Launcher's active theme; the fallbacks
    // mirror the vendored Dark theme so the panel stays readable outside Flow's window.
    public static PreviewPalette FromApplicationResources() =>
        new(
            Resolve(WindowBackgroundKey, FallbackWindowBackground),
            Resolve(BodyTextKey, FallbackBodyText),
            Resolve(TitleTextKey, FallbackTitleText),
            Resolve(SubtleTextKey, FallbackSubtleText),
            Resolve(CodeBackgroundKey, FallbackCodeBackground),
            Resolve(BorderKey, FallbackBorder),
            Resolve(LinkKey, FallbackLink)
        );

    private static Brush Resolve(string key, Brush fallback)
    {
        object? resource = Application.Current?.TryFindResource(key);
        return resource as SolidColorBrush ?? fallback;
    }
}

public static class MarkdownToFlowDocument
{
    private const double BodyFontSize = 14;
    private const double CodeFontSize = 12.5;

    private static readonly FontFamily MonoFontFamily = new("Cascadia Mono, Consolas, Microsoft YaHei UI");

    private static readonly Regex HeadingPattern = new(
        @"^\s{0,3}(?<level>#{1,6})\s+(?<text>.*?)\s*#*\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex HorizontalRulePattern = new(
        @"^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex QuoteLinePattern = new(
        @"^\s{0,3}>\s?(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex CalloutPattern = new(
        @"^\[!(?<label>\w+)\]\s*(?<remainder>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex ListItemPattern = new(
        @"^(?<indent>\s*)(?<marker>[-*+]|\d{1,3}[.)])\s+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex CheckboxPattern = new(
        @"^\[(?<checked> |x|X)\]\s+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex TableSeparatorCellPattern = new(
        @"^:?-+:?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex InlinePattern = new(
        @"(?<code>`[^`\n]+`)" +
        @"|(?<embed>!\[\[[^\]\n]+(?:\|[^\]\n]+)?\]\])" +
        @"|(?<image>!\[[^\]\n]*\]\([^)\n]*\))" +
        @"|(?<wikilink>\[\[[^\]\n|]+(?:\|[^\]\n]+)?\]\])" +
        @"|(?<link>\[[^\]\n]+\]\([^)\n]*\))" +
        @"|(?<bold>\*\*[^*\n]+\*\*|__[^_\n]+__)" +
        @"|(?<strike>~~[^~\n]+~~)" +
        @"|(?<highlight>==[^=\n]+==)" +
        @"|(?<italic>\*[^*\n]+\*|(?<![\w_])_[^_\n]+_(?![\w_]))" +
        @"|(?<tag>(?<!\S)#[^\s#,.;:!?()\[\]{}""']+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public static FlowDocument Render(string markdown, PreviewPalette palette)
    {
        FlowDocument document = CreateDocument(palette);
        List<string> lines = SplitLines(markdown);
        int index = 0;

        while (index < lines.Count)
        {
            string line = lines[index];
            string trimmed = line.TrimStart();

            if (trimmed.Length is 0)
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                index = AppendCodeBlock(document, lines, index, palette);
                continue;
            }

            if (HeadingPattern.IsMatch(line))
            {
                document.Blocks.Add(CreateHeading(line, palette));
                index++;
                continue;
            }

            if (HorizontalRulePattern.IsMatch(line))
            {
                document.Blocks.Add(CreateHorizontalRule(palette));
                index++;
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                index = AppendBlockQuote(document, lines, index, palette);
                continue;
            }

            if (ListItemPattern.IsMatch(line))
            {
                index = AppendList(document, lines, index, palette);
                continue;
            }

            if (trimmed.StartsWith('|'))
            {
                index = AppendTable(document, lines, index, palette);
                continue;
            }

            index = AppendParagraph(document, lines, index, palette);
        }

        return document;
    }

    public static FlowDocument RenderPlainText(string content, PreviewPalette palette)
    {
        FlowDocument document = CreateDocument(palette);
        Paragraph paragraph = CreateCodeStyleParagraph(new Thickness(0, 2, 0, 10), palette);

        List<string> lines = SplitLines(content);
        for (int i = 0; i < lines.Count; i++)
        {
            paragraph.Inlines.Add(new Run(lines[i]));
            if (i < lines.Count - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    public static FlowDocument RenderMessage(string message, PreviewPalette palette)
    {
        FlowDocument document = CreateDocument(palette);
        Paragraph paragraph = new()
        {
            Margin = new Thickness(0, 4, 0, 8),
            Foreground = palette.SubtleText,
        };
        paragraph.Inlines.Add(new Run(message));
        document.Blocks.Add(paragraph);
        return document;
    }

    private static FlowDocument CreateDocument(PreviewPalette palette) =>
        new()
        {
            PagePadding = new Thickness(0),
            Foreground = palette.BodyText,
            FontSize = BodyFontSize,
        };

    private static List<string> SplitLines(string content)
    {
        List<string> lines = content.Replace("\r\n", "\n").Replace('\t', ' ').Split('\n').ToList();

        // Strip the YAML front matter block when it opens the note.
        if (lines.Count > 0 && lines[0].Trim() is "---")
        {
            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i].Trim() is "---")
                {
                    lines.RemoveRange(0, i + 1);
                    break;
                }
            }
        }

        return lines;
    }

    private static Paragraph CreateHeading(string line, PreviewPalette palette)
    {
        Match heading = HeadingPattern.Match(line);
        int level = heading.Groups["level"].Length;

        Paragraph paragraph = new()
        {
            Margin = new Thickness(0, level <= 2 ? 14 : 10, 0, 6),
            FontSize = level switch
            {
                1 => 22,
                2 => 19,
                3 => 16.5,
                4 => 15,
                _ => BodyFontSize,
            },
            FontWeight = FontWeights.SemiBold,
            Foreground = palette.TitleText,
        };

        AddInlines(paragraph.Inlines, heading.Groups["text"].Value, palette);
        return paragraph;
    }

    private static Block CreateHorizontalRule(PreviewPalette palette) =>
        new BlockUIContainer(new Rectangle
        {
            Height = 1,
            Fill = palette.Border,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 10),
        });

    private static Paragraph CreateCodeStyleParagraph(Thickness margin, PreviewPalette palette) =>
        new()
        {
            Margin = margin,
            Padding = new Thickness(10, 8, 10, 8),
            Background = palette.CodeBackground,
            FontFamily = MonoFontFamily,
            FontSize = CodeFontSize,
        };

    private static int AppendCodeBlock(FlowDocument document, List<string> lines, int start, PreviewPalette palette)
    {
        string fence = lines[start].TrimStart()[..3];
        int index = start + 1;
        List<string> content = [];

        while (index < lines.Count && !lines[index].TrimStart().StartsWith(fence))
        {
            content.Add(lines[index]);
            index++;
        }

        if (index < lines.Count)
        {
            index++;
        }

        Paragraph paragraph = CreateCodeStyleParagraph(new Thickness(0, 2, 0, 10), palette);
        for (int i = 0; i < content.Count; i++)
        {
            paragraph.Inlines.Add(new Run(content[i]));
            if (i < content.Count - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        document.Blocks.Add(paragraph);
        return index;
    }

    private static int AppendBlockQuote(FlowDocument document, List<string> lines, int start, PreviewPalette palette)
    {
        int index = start;
        List<string> inner = [];

        while (index < lines.Count)
        {
            Match quote = QuoteLinePattern.Match(lines[index]);
            if (!quote.Success)
            {
                break;
            }

            inner.Add(quote.Groups["text"].Value);
            index++;
        }

        Paragraph paragraph = new()
        {
            Margin = new Thickness(0, 2, 0, 10),
            Padding = new Thickness(10, 6, 8, 6),
            Background = palette.CodeBackground,
            BorderBrush = palette.Border,
            BorderThickness = new Thickness(3, 0, 0, 0),
        };

        if (inner.Count > 0)
        {
            Match callout = CalloutPattern.Match(inner[0]);
            if (callout.Success)
            {
                paragraph.Inlines.Add(new Run(callout.Groups["label"].Value.ToUpperInvariant())
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = palette.TitleText,
                });

                string remainder = callout.Groups["remainder"].Value;
                if (remainder.Length > 0)
                {
                    paragraph.Inlines.Add(new Run($" {remainder}"));
                }

                inner.RemoveAt(0);
                if (inner.Count > 0)
                {
                    paragraph.Inlines.Add(new LineBreak());
                }
            }
        }

        for (int i = 0; i < inner.Count; i++)
        {
            AddInlines(paragraph.Inlines, inner[i], palette);
            if (i < inner.Count - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        document.Blocks.Add(paragraph);
        return index;
    }

    private static int AppendList(FlowDocument document, List<string> lines, int start, PreviewPalette palette)
    {
        List<ListItemNode> flatItems = [];
        int index = start;

        while (index < lines.Count)
        {
            Match item = ListItemPattern.Match(lines[index]);
            if (!item.Success)
            {
                break;
            }

            flatItems.Add(new ListItemNode(
                item.Groups["indent"].Value.Length / 2,
                !item.Groups["marker"].Value[0].IsListBulletMarker(),
                item.Groups["text"].Value
            ));
            index++;
        }

        document.Blocks.Add(RenderListItems(BuildListItemTree(flatItems), palette));
        return index;
    }

    private static int AppendTable(FlowDocument document, List<string> lines, int start, PreviewPalette palette)
    {
        int index = start;
        List<string[]> rows = [];
        bool hasSeparator = false;

        while (index < lines.Count && lines[index].TrimStart().StartsWith('|'))
        {
            string[] cells = SplitTableRow(lines[index]);
            if (cells.Length > 0 && cells.All(cell => TableSeparatorCellPattern.IsMatch(cell)))
            {
                hasSeparator = true;
            }
            else
            {
                rows.Add(cells);
            }

            index++;
        }

        bool hasHeader = hasSeparator && rows.Count > 0;
        document.Blocks.Add(CreateTable(rows, hasHeader, palette));
        return index;
    }

    private static Table CreateTable(List<string[]> rows, bool hasHeader, PreviewPalette palette)
    {
        Table table = new()
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 2, 0, 10),
            BorderBrush = palette.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        int columnCount = rows.Count > 0 ? rows.Max(row => row.Length) : 1;
        for (int c = 0; c < columnCount; c++)
        {
            table.Columns.Add(new TableColumn());
        }

        TableRowGroup rowGroup = new();
        table.RowGroups.Add(rowGroup);

        for (int r = 0; r < rows.Count; r++)
        {
            TableRow row = new();
            if (hasHeader && r is 0)
            {
                row.Background = palette.CodeBackground;
            }

            for (int c = 0; c < columnCount; c++)
            {
                TableCell cell = new()
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = palette.Border,
                    BorderThickness = hasHeader && r is 0 ? new Thickness(0, 0, 0, 1) : new Thickness(0),
                };

                Paragraph paragraph = new() { Margin = new Thickness(0) };
                if (hasHeader && r is 0)
                {
                    paragraph.FontWeight = FontWeights.SemiBold;
                }

                AddInlines(paragraph.Inlines, c < rows[r].Length ? rows[r][c] : string.Empty, palette);
                cell.Blocks.Add(paragraph);
                row.Cells.Add(cell);
            }

            rowGroup.Rows.Add(row);
        }

        return table;
    }

    private static int AppendParagraph(FlowDocument document, List<string> lines, int start, PreviewPalette palette)
    {
        int index = start;
        List<string> content = [];

        while (index < lines.Count)
        {
            string trimmed = lines[index].TrimStart();
            if (trimmed.Length is 0 || IsBlockStart(trimmed))
            {
                break;
            }

            content.Add(lines[index].Trim());
            index++;
        }

        Paragraph paragraph = new() { Margin = new Thickness(0, 0, 0, 8) };
        AddMultilineInlines(paragraph.Inlines, content, palette);
        document.Blocks.Add(paragraph);
        return index;
    }

    private static bool IsBlockStart(string trimmedLine) =>
        trimmedLine.StartsWith("```")
        || trimmedLine.StartsWith("~~~")
        || trimmedLine.StartsWith('>')
        || trimmedLine.StartsWith('|')
        || HeadingPattern.IsMatch(trimmedLine)
        || HorizontalRulePattern.IsMatch(trimmedLine)
        || ListItemPattern.IsMatch(trimmedLine);

    private static void AddMultilineInlines(InlineCollection inlines, List<string> lines, PreviewPalette palette)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            AddInlines(inlines, lines[i], palette);
            if (i < lines.Count - 1)
            {
                inlines.Add(new LineBreak());
            }
        }
    }

    private static void AddInlines(InlineCollection inlines, string text, PreviewPalette palette)
    {
        int position = 0;

        for (
            Match match = InlinePattern.Match(text, position);
            match.Success;
            match = InlinePattern.Match(text, position)
        )
        {
            if (match.Index > position)
            {
                inlines.Add(new Run(text[position..match.Index]));
            }

            AddStyledInline(inlines, match, palette);
            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }
    }

    private static void AddStyledInline(InlineCollection inlines, Match match, PreviewPalette palette)
    {
        if (match.Groups["code"].Success)
        {
            inlines.Add(CreateInlineCodeRun(match.Value[1..^1], palette));
            return;
        }

        if (match.Groups["embed"].Success)
        {
            string target = match.Value[3..^2].Split('|')[0].Split('#')[0];
            inlines.Add(new Run($"[图片: {target}]") { Foreground = palette.SubtleText });
            return;
        }

        if (match.Groups["image"].Success)
        {
            string alt = match.Value[2..match.Value.IndexOf(']')];
            inlines.Add(new Run($"[图片: {alt}]") { Foreground = palette.SubtleText });
            return;
        }

        if (match.Groups["wikilink"].Success)
        {
            inlines.Add(CreateLinkSpan(match.Value[2..^2].Split('|')[^1], palette));
            return;
        }

        if (match.Groups["link"].Success)
        {
            string value = match.Value;
            string display = value[1..value.IndexOf(']')];
            Span linkSpan = new()
            {
                Foreground = palette.Link,
                TextDecorations = TextDecorations.Underline,
            };
            AddInlines(linkSpan.Inlines, display, palette);
            inlines.Add(linkSpan);
            return;
        }

        if (match.Groups["bold"].Success)
        {
            Span bold = new() { FontWeight = FontWeights.Bold };
            AddInlines(bold.Inlines, match.Value[2..^2], palette);
            inlines.Add(bold);
            return;
        }

        if (match.Groups["strike"].Success)
        {
            Span strike = new() { TextDecorations = TextDecorations.Strikethrough };
            AddInlines(strike.Inlines, match.Value[2..^2], palette);
            inlines.Add(strike);
            return;
        }

        if (match.Groups["highlight"].Success)
        {
            Span highlight = new() { Background = palette.HighlightBackground };
            AddInlines(highlight.Inlines, match.Value[2..^2], palette);
            inlines.Add(highlight);
            return;
        }

        if (match.Groups["italic"].Success)
        {
            Span italic = new() { FontStyle = FontStyles.Italic };
            AddInlines(italic.Inlines, match.Value[1..^1], palette);
            inlines.Add(italic);
            return;
        }

        if (match.Groups["tag"].Success)
        {
            inlines.Add(new Run(match.Value) { Foreground = palette.Link });
        }
    }

    private static Run CreateInlineCodeRun(string text, PreviewPalette palette) =>
        new(text)
        {
            FontFamily = MonoFontFamily,
            FontSize = CodeFontSize,
            Background = palette.CodeBackground,
        };

    private static Span CreateLinkSpan(string text, PreviewPalette palette) =>
        new(new Run(text))
        {
            Foreground = palette.Link,
            TextDecorations = TextDecorations.Underline,
        };

    private static List<ListItemNode> BuildListItemTree(List<ListItemNode> flatItems)
    {
        List<ListItemNode> roots = [];
        Stack<(int Level, ListItemNode Node)> ancestors = [];

        foreach (ListItemNode item in flatItems)
        {
            while (ancestors.Count > 0 && ancestors.Peek().Level >= item.Level)
            {
                ancestors.Pop();
            }

            if (ancestors.Count is 0)
            {
                roots.Add(item);
            }
            else
            {
                ancestors.Peek().Node.Children.Add(item);
            }

            ancestors.Push((item.Level, item));
        }

        return roots;
    }

    private static FlowDocumentList RenderListItems(List<ListItemNode> items, PreviewPalette palette)
    {
        FlowDocumentList list = new()
        {
            MarkerStyle = items[0].Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(24, 2, 0, 8),
        };

        foreach (ListItemNode item in items)
        {
            ListItem listItem = new();
            Paragraph paragraph = new() { Margin = new Thickness(0, 1, 0, 1) };

            Match checkbox = CheckboxPattern.Match(item.Text);
            if (checkbox.Success)
            {
                paragraph.Inlines.Add(new Run(checkbox.Groups["checked"].Value.Trim() is "" ? "☐ " : "☑ ")
                {
                    Foreground = palette.SubtleText,
                });
                AddInlines(paragraph.Inlines, checkbox.Groups["text"].Value, palette);
            }
            else
            {
                AddInlines(paragraph.Inlines, item.Text, palette);
            }

            listItem.Blocks.Add(paragraph);

            if (item.Children.Count > 0)
            {
                listItem.Blocks.Add(RenderListItems(item.Children, palette));
            }

            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static string[] SplitTableRow(string line)
    {
        string trimmed = line.Trim().Trim('|');
        return trimmed.Length is 0 ? [] : trimmed.Split('|').Select(cell => cell.Trim()).ToArray();
    }

    private sealed class ListItemNode(int level, bool ordered, string text)
    {
        public int Level { get; } = level;
        public bool Ordered { get; } = ordered;
        public string Text { get; } = text;
        public List<ListItemNode> Children { get; } = [];
    }
}

internal static class CharExtensions
{
    public static bool IsListBulletMarker(this char marker) => marker is '-' or '*' or '+';
}
