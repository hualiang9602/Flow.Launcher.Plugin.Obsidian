using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Obsidian.Models;
using Flow.Launcher.Plugin.Obsidian.Views;

namespace Flow.Launcher.Plugin.Obsidian.Extensions;

public static class QueryExtensions
{
    public static List<Result> ToResults(this IEnumerable<File> source) =>
        source.Select(WithPreviewPanel).ToList();

    // Flow Launcher builds the control from Lazy.Value only when the result is selected,
    // so each query gets a fresh preview showing the note's current content.
    private static Result WithPreviewPanel(File file)
    {
        file.PreviewPanel = new Lazy<UserControl>(() => new NotePreview(file));
        return file;
    }
}
