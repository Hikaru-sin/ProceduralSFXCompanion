using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ProceduralSFXCompanion.Collections;

/// <summary>
/// Fire 1 reset event after adding a list of items
/// </summary>
/// <typeparam name="T"></typeparam>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public void StartSuppressNotification()
    {
        _suppressNotification = true;
    }

    public void StopSuppressNotification()
    {
        _suppressNotification = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
    
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        // Only fire the event if we aren't suppressing it
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    public void AddRange(IEnumerable<T> items)
    {
        StartSuppressNotification();
        try
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            StopSuppressNotification();
        }
    }
}