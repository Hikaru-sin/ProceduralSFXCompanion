using System;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Avalonia;
using WeCantSpell.Hunspell;

namespace ProceduralSFXCompanion.Controls;

public class SpellChecker : DocumentColorizingTransformer
{
    private readonly WordList _wordList;

    public SpellChecker(WordList wordList)
    {
        _wordList = wordList;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        int lineStart = line.Offset;
        string text = CurrentContext.Document.GetText(line);
        
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b\w+\b");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (!_wordList.Check(match.Value))
            {
                // Apply the "Error" style to the misspelled word
                ChangeLinePart(
                    lineStart + match.Index, 
                    lineStart + match.Index + match.Length, 
                    element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(Brushes.Coral);
                    });
            }
        }
    }
}