using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using System.Windows.Documents;
using Flow.Launcher.Plugin.Obsidian.Models;
using Flow.Launcher.Plugin.Obsidian.Utilities;
using File = Flow.Launcher.Plugin.Obsidian.Models.File;

namespace Flow.Launcher.Plugin.Obsidian.Views;

public partial class NotePreview
{
    private const int MaxPreviewLength = 262_144;
    private const string TruncationSuffix = "\n\n…… 内容过长，已截断显示";
    private const string UnsupportedMessage = "此格式暂不支持预览，按 Enter 在 Obsidian 中打开。";

    private static readonly string[] PlainTextExtensions = [".txt", ".log", ".csv", ".yml", ".yaml"];

    private readonly PreviewPalette _palette = PreviewPalette.FromApplicationResources();

    public NotePreview(File file)
    {
        InitializeComponent();
        ApplyTheme();
        Render(file);
    }

    private void ApplyTheme()
    {
        RootGrid.Background = _palette.WindowBackground;
        TitleTextBlock.Foreground = _palette.TitleText;
        PathTextBlock.Foreground = _palette.SubtleText;
        HeaderSeparator.Fill = _palette.Border;
    }

    private void Render(File file)
    {
        TitleTextBlock.Text = file.Name;
        PathTextBlock.Text = file.SubTitle;

        string content;
        try
        {
            content = ReadContent(file.FilePath);
        }
        catch (FileNotFoundException)
        {
            DocumentViewer.Document = MarkdownToFlowDocument.RenderMessage("文件不存在或已被移动。", _palette);
            return;
        }
        catch (Exception exception)
        {
            DocumentViewer.Document = MarkdownToFlowDocument.RenderMessage($"无法读取文件：{exception.Message}", _palette);
            return;
        }

        DocumentViewer.Document = BuildDocument(file, content);
    }

    private FlowDocument BuildDocument(File file, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return MarkdownToFlowDocument.RenderMessage("（空笔记）", _palette);
        }

        string extension = file.Extension.ToLowerInvariant();
        return extension switch
        {
            ".md" => MarkdownToFlowDocument.Render(content, _palette),
            ".canvas" => MarkdownToFlowDocument.RenderPlainText(FormatJson(content), _palette),
            _ when PlainTextExtensions.Contains(extension) => MarkdownToFlowDocument.RenderPlainText(content, _palette),
            _ => MarkdownToFlowDocument.RenderMessage(UnsupportedMessage, _palette),
        };
    }

    private static string FormatJson(string content)
    {
        try
        {
            return JsonNode.Parse(content)?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? content;
        }
        catch
        {
            return content;
        }
    }

    private static string ReadContent(string path)
    {
        using StreamReader reader = new(path);

        char[] buffer = new char[MaxPreviewLength + 1];
        int read = reader.ReadBlock(buffer, 0, buffer.Length);

        return read > MaxPreviewLength
            ? string.Concat(buffer.AsSpan(0, MaxPreviewLength), TruncationSuffix)
            : new string(buffer, 0, read);
    }
}
