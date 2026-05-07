using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using ProceduralSFXCompanion.Controls;
using ProceduralSFXCompanion.Utilities;
using ProceduralSFXCompanion.ViewModels;
using WeCantSpell.Hunspell;

namespace ProceduralSFXCompanion.Views;

public partial class EditView : UserControl
{
    public EditView()
    {
        InitializeComponent();
        
        // TODO: Change these to XAML binding
        string assetsPath = Path.Combine(AppContext.BaseDirectory, Constants.DictionariesFolder);
        var wordList = WordList.CreateFromFiles(Path.Combine(assetsPath, "en_US.dic"), Path.Combine(assetsPath,"en_US.aff"));
        var textEditor = this.FindControl<TextEditor>("DescriptionTextBox");
        if (textEditor is not null)
        {
            textEditor.TextArea.TextView.LineTransformers.Add(new SpellChecker(wordList));
        }
        
        var dropZone = this.FindControl<Grid>("FileDropZone");
        if (dropZone is not null)
        {
            DragDrop.AddDragOverHandler(dropZone, OnDragOver);
            DragDrop.AddDropHandler(dropZone, OnDrop);
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        using IStorageItem? file = e.DataTransfer.TryGetFile();
        if (file is null)
            return;
        
        string? filePath = file.TryGetLocalPath();
        if(filePath is null)
            return;
        
        if (DataContext is EditViewModel vm)
        {
            _ = vm.HandleDroppedFiles(filePath);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }
}