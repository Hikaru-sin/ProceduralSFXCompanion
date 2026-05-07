using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ProceduralSFXCompanion.Controls;

public class TutorialCard : TemplatedControl
{
    public static readonly StyledProperty<int> ImageWidthProperty =
        AvaloniaProperty.Register<TutorialCard, int>(nameof(ImageWidth), defaultValue: 150);
    public int ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }
    
    public static readonly StyledProperty<int> ImageHeightProperty =
        AvaloniaProperty.Register<TutorialCard, int>(nameof(ImageHeight), defaultValue: 150);
    public int ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }
    
    public static readonly StyledProperty<string> TitleProperty =
                           AvaloniaProperty.Register<TutorialCard, string>(nameof(Title), defaultValue: "Title");
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    
    public static readonly StyledProperty<string> ContentProperty =
        AvaloniaProperty.Register<TutorialCard, string>(nameof(Content), defaultValue: "Content");
    public string Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
    
    public static readonly StyledProperty<IImage?> ImageSourceProperty =
        AvaloniaProperty.Register<TutorialCard, IImage?>(nameof(ImageSource), defaultValue: null);
    public IImage? ImageSource
    {
        get => GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<TutorialCard, ICommand?>(nameof(Command));
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<TutorialCard, object?>(nameof(CommandParameter));
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
}