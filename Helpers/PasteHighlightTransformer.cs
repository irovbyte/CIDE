using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
namespace CIDE.Helpers;

public class PasteHighlightTransformer : DocumentColorizingTransformer
{
    public int StartOffset { get; set; } = -1;
    public int EndOffset { get; set; } = -1;
    public double CurrentOpacity { get; set; }
    protected override void ColorizeLine(DocumentLine line)
    {
        if (CurrentOpacity <= 0 || StartOffset < 0 || EndOffset < 0)
        {
            return;
        }
        var lineStart = line.Offset;
        var lineEnd = line.Offset + line.Length;
        if (EndOffset < lineStart || StartOffset > lineEnd)
        {
            return;
        }
        var highlightStart = Math.Max(lineStart, StartOffset);
        var highlightEnd = Math.Min(lineEnd, EndOffset);
        if (highlightStart < highlightEnd)
        {
            ChangeLinePart(highlightStart, highlightEnd, element =>
                element.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.FromArgb((byte)(255 * CurrentOpacity), 255, 255, 255))));
        }
    }
}
