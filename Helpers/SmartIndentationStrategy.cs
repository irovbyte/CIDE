using System;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
namespace CIDE.Helpers;

public class SmartIndentationStrategy : IIndentationStrategy
{
    public void IndentLine(TextDocument document, DocumentLine line)
    {
        if (document == null || line == null || line.PreviousLine == null)
        {
            return;
        }
        var prevLine = line.PreviousLine;
        var prevText = document.GetText(prevLine);
        var indent = "";
        foreach (var c in prevText)
        {
            if (c is ' ' or '\t')
            {
                indent += c;
            }
            else
            {
                break;
            }
        }
        if (prevText.TrimEnd().EndsWith('{'))
        {
            indent += "    ";
        }
        document.Insert(line.Offset, indent);
    }
    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        for (var i = beginLine; i <= endLine; i++)
        {
            IndentLine(document, document.GetLineByNumber(i));
        }
    }
}
