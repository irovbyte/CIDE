using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
namespace CIDE.Helpers;

public class SearchHighlightTransformer : DocumentColorizingTransformer
{
    private readonly List<TextSegment> _searchResults = [];
    private int _currentIndex = -1;
    private readonly IBrush _matchBackground = new SolidColorBrush(Color.FromArgb(80, 246, 185, 77));
    private readonly IBrush _currentMatchBackground = new SolidColorBrush(Color.FromArgb(160, 255, 150, 50));
    public void UpdateSearch(IEnumerable<TextSegment> results, int currentIndex)
    {
        _searchResults.Clear();
        _searchResults.AddRange(results);
        _currentIndex = currentIndex;
    }
    protected override void ColorizeLine(DocumentLine line)
    {
        if (_searchResults.Count == 0)
        {
            return;
        }
        var lineStart = line.Offset;
        var lineEnd = lineStart + line.Length;
        var matchesInLine = _searchResults.Where(s => s.EndOffset > lineStart && s.StartOffset < lineEnd);
        foreach (var match in matchesInLine)
        {
            var start = Math.Max(lineStart, match.StartOffset);
            var end = Math.Min(lineEnd, match.EndOffset);
            if (start < end)
            {
                var isCurrent = _searchResults.IndexOf(match) == _currentIndex;
                ChangeLinePart(start, end, element => element.TextRunProperties.SetBackgroundBrush(isCurrent ? _currentMatchBackground : _matchBackground));
            }
        }
    }
}
