using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ProceduralSFXCompanion.Controls;

public static class BoldTextBlock
{
    private static readonly SolidColorBrush HighlightColor = new SolidColorBrush(Colors.Salmon);
    public static void SetFormattedText(TextBlock element, string? value) => element.SetValue(FormattedTextProperty, value);
    public static string? GetFormattedText(TextBlock element) => element.GetValue(FormattedTextProperty);
    
    public static readonly AttachedProperty<string?> FormattedTextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("FormattedText", typeof(BoldTextBlock));

    static BoldTextBlock()
    {
        FormattedTextProperty.Changed.AddClassHandler<TextBlock>((tb, e) => 
            UpdateInlines(tb, e.NewValue as string));
    }

    private static void UpdateInlines(TextBlock textBlock, string? text)
    {
        textBlock.Inlines!.Clear();
        if (string.IsNullOrEmpty(text)) return;
        
        var parts = Regex.Split(text, @"(<b>.*?</b>)");
        foreach (var part in parts)
        {
            if (part.StartsWith("<b>") && part.EndsWith("</b>"))
            {
                var content = part.Substring(3, part.Length - 7);
                var run = new Run
                {
                    Text = content,
                    FontWeight = FontWeight.Bold,
                    Foreground = HighlightColor,
                };
                textBlock.Inlines.Add(run);
            }
            else
            {
                textBlock.Inlines.Add(new Run(part));
            }
        }
    }
}

public static class BoldItalicTextBlock
{
    public static void SetFormattedText(TextBlock element, string? value) => element.SetValue(FormattedTextProperty, value);
    public static string? GetFormattedText(TextBlock element) => element.GetValue(FormattedTextProperty);
    
    public static readonly AttachedProperty<string?> FormattedTextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("FormattedText", typeof(BoldItalicTextBlock));

    static BoldItalicTextBlock()
    {
        FormattedTextProperty.Changed.AddClassHandler<TextBlock>((tb, e) => 
            UpdateInlines(tb, e.NewValue as string));
    }

    private static void UpdateInlines(TextBlock textBlock, string? text)
    {
        textBlock.Inlines!.Clear();
        if (string.IsNullOrEmpty(text)) return;
        
        var parts = Regex.Split(text, @"(<b>.*?</b>)");
        foreach (var part in parts)
        {
            if (part.StartsWith("<b>") && part.EndsWith("</b>"))
            {
                var content = part.Substring(3, part.Length - 7);
                var run = new Run
                {
                    Text = content,
                    FontWeight = FontWeight.Bold,
                    FontStyle =  FontStyle.Italic,
                };
                textBlock.Inlines.Add(run);
            }
            else
            {
                textBlock.Inlines.Add(new Run(part));
            }
        }
    }
}