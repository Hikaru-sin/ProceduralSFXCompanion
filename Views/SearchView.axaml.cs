using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ProceduralSFXCompanion.ViewModels;

namespace ProceduralSFXCompanion.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            vm.SearchedEntries.CollectionChanged += SearchedEntriesOnCollectionChanged;
        }
    }

    private void SearchedEntriesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => 
        {
            SearchItemsScrollViewer.Offset = new Vector(0, 0);
        });
    }
}