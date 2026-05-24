using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;
using System;

namespace CIDE.Helpers;

public class SmartIndentationStrategy : IIndentationStrategy
{
    public void IndentLine(TextDocument document, DocumentLine line)
    {
        if (document == null || line == null || line.PreviousLine == null)
            return;
        var prevLine = line.PreviousLine;
        var prevText = document.GetText(prevLine);
        var indent = "";
        foreach (char c in prevText)
        {
            if (c == ' ' || c == '\t')
                indent += c;
            else
                break;
        }
        if (prevText.TrimEnd().EndsWith('{'))
        {
            indent += "    ";
        }
        document.Insert(line.Offset, indent);
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        for (int i = beginLine; i <= endLine; i++)
        {
            IndentLine(document, document.GetLineByNumber(i));
        }
    }
}
