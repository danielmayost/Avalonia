using System.Collections.Specialized;
using Avalonia.Layout.Utils;
using Avalonia.Logging;
using Avalonia.Layout;
using Avalonia;

namespace Sandbox;

public class ElementManager
{
    private readonly List<ILayoutable?> _realizedElements = new List<ILayoutable?>();
    private int _firstRealizedDataIndex;
    private VirtualizingLayoutContext? _context;

    public int RealizedCount => _realizedElements.Count;
    public int RealizedEndInData => _firstRealizedDataIndex + RealizedCount - 1;


    public ElementManager()
    {
        
    }

    public void SetContext(VirtualizingLayoutContext virtualContext)
    {
        _context = virtualContext;
    }

    public void EnsureAndClear(Range range)
    {
        ClearOutOfRange(range);

        if (_firstRealizedDataIndex > range.Start.Value)
        {
            for (int i = _firstRealizedDataIndex - 1; i >= range.Start.Value; i--)
            {
                var element = CreateElement(i);
                Insert(0, i, element);
            }
        }

        if (range.End.Value > RealizedEndInData)
        {
            for (int i = RealizedEndInData + 1; i <= range.End.Value; i++)
            {
                var element = CreateElement(i);
                Add(element, i);
            }
        }
    }

    private void ClearOutOfRange(Range range)
    {
        if (RealizedCount == 0)
        {
            return;
        }

        if (_firstRealizedDataIndex > range.End.Value || RealizedEndInData <  range.Start.Value)
        {
            ClearRealizedRange();
            return;
        }
        else if (_firstRealizedDataIndex == range.Start.Value && RealizedEndInData == range.End.Value)
        {
            return;
        }

        if (RealizedEndInData > range.End.Value)
        {
            ClearRealizedRange(range.End.Value + 1 - _firstRealizedDataIndex, 
                RealizedEndInData - range.End.Value);
        }

        if (_firstRealizedDataIndex < range.Start.Value)
        {
            ClearRealizedRange(0, range.Start.Value - _firstRealizedDataIndex);
        }
    }

    public ILayoutable GetAt(int realizedIndex)
    {
        ILayoutable? element = _realizedElements[realizedIndex];

        if (element == null)
        {
            int dataIndex = GetDataIndexFromRealizedRangeIndex(realizedIndex);
            element = _context!.GetOrCreateElementAt(dataIndex,
                ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);

            _realizedElements[realizedIndex] = element;
        }

        return element;
    }

    private ILayoutable CreateElement(int dataIndex)
    {
        return _context!.GetOrCreateElementAt(dataIndex,
                ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
    }

    public void Add(ILayoutable element, int dataIndex)
    {
        if (_realizedElements.Count == 0)
        {
            _firstRealizedDataIndex = dataIndex;
        }

        _realizedElements.Add(element);
    }

    public void Insert(int realizedIndex, int dataIndex, ILayoutable? element)
    {
        if (realizedIndex == 0)
        {
            _firstRealizedDataIndex = dataIndex;
        }

        _realizedElements.Insert(realizedIndex, element);
    }

    public void ClearRealizedRange(int realizedIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int index = realizedIndex == 0 ? realizedIndex + i : (realizedIndex + count - 1) - i;
            var elementRef = _realizedElements[index];

            if (elementRef != null)
            {
                _context!.RecycleElement(elementRef);
            }
        }

        _realizedElements.RemoveRange(realizedIndex, count);

        if (realizedIndex == 0)
        {
            _firstRealizedDataIndex = _realizedElements.Count == 0 ?
                0 : _firstRealizedDataIndex + count;
        }
    }

    public void DiscardElementsOutsideWindow(bool forward, int startIndex)
    {
        if (IsDataIndexRealized(startIndex))
        {
            int rangeIndex = GetRealizedRangeIndexFromDataIndex(startIndex);

            if (forward)
            {
                ClearRealizedRange(rangeIndex, RealizedCount - rangeIndex);
            }
            else
            {
                ClearRealizedRange(0, rangeIndex + 1);
            }
        }
    }

    public void ClearRealizedRange()
    {
        ClearRealizedRange(0, RealizedCount);
    }

    public bool IsDataIndexRealized(int index)
    {
        int realizedCount = RealizedCount;
        return
            realizedCount > 0 &&
            GetDataIndexFromRealizedRangeIndex(0) <= index &&
            GetDataIndexFromRealizedRangeIndex(realizedCount - 1) >= index;
    }

    public bool IsIndexValidInData(int currentIndex)
    {
        return (uint)currentIndex < _context!.ItemCount;
    }

    public ILayoutable? GetRealizedElement(int dataIndex)
    {
        return GetAt(GetRealizedRangeIndexFromDataIndex(dataIndex));
    }

    public void EnsureElementRealized(bool forward, int dataIndex, string? layoutId)
    {
        if (IsDataIndexRealized(dataIndex) == false)
        {
            var element = _context!.GetOrCreateElementAt(
                dataIndex,
                ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);

            if (forward)
            {
                Add(element, dataIndex);
            }
            else
            {
                Insert(0, dataIndex, element);
            }
        }
    }

    public void DataSourceChanged(object? source, NotifyCollectionChangedEventArgs args)
    {
        if (_realizedElements.Count > 0)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        OnItemsAdded(args.NewStartingIndex, args.NewItems!.Count);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    {
                        int oldSize = args.OldItems!.Count;
                        int newSize = args.NewItems!.Count;
                        int oldStartIndex = args.OldStartingIndex;
                        int newStartIndex = args.NewStartingIndex;

                        if (oldSize == newSize &&
                            oldStartIndex == newStartIndex &&
                            IsDataIndexRealized(oldStartIndex) &&
                            IsDataIndexRealized(oldStartIndex + oldSize - 1))
                        {
                            // Straight up replace of n items within the realization window.
                            // Removing and adding might causes us to lose the anchor causing us
                            // to throw away all containers and start from scratch.
                            // Instead, we can just clear those items and set the element to
                            // null (sentinel) and let the next measure get new containers for them.
                            var startRealizedIndex = GetRealizedRangeIndexFromDataIndex(oldStartIndex);
                            for (int realizedIndex = startRealizedIndex; realizedIndex < startRealizedIndex + oldSize; realizedIndex++)
                            {
                                var elementRef = _realizedElements[realizedIndex];

                                if (elementRef != null)
                                {
                                    _context!.RecycleElement(elementRef);
                                    _realizedElements[realizedIndex] = null;
                                }
                            }
                        }
                        else
                        {
                            OnItemsRemoved(oldStartIndex, oldSize);
                            OnItemsAdded(newStartIndex, newSize);
                        }
                    }
                    break;

                // Remove clear all realized elements just to align the begavior
                // with ViewManager which resets realized item indices to defaults.
                // Freeing only removed items causes wrong indices to be stored
                // in virtualized info of items under some circumstances.
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset:
                    ClearRealizedRange();
                    break;

                case NotifyCollectionChangedAction.Move:
                    int size = args.OldItems != null ? args.OldItems.Count : 1;
                    OnItemsRemoved(args.OldStartingIndex, size);
                    OnItemsAdded(args.NewStartingIndex, size);
                    break;
            }
        }
    }

    public int GetElementDataIndex(ILayoutable suggestedAnchor)
    {
        var it = _realizedElements.IndexOf(suggestedAnchor);
        return it != -1 ? GetDataIndexFromRealizedRangeIndex(it) : -1;
    }

    public int GetDataIndexFromRealizedRangeIndex(int rangeIndex)
    {
        return rangeIndex + _firstRealizedDataIndex;
    }

    private int GetRealizedRangeIndexFromDataIndex(int dataIndex)
    {
        return dataIndex - _firstRealizedDataIndex;
    }

    private void OnItemsAdded(int index, int count)
    {
        // Using the old indices here (before it was updated by the collection change)
        // if the insert data index is between the first and last realized data index, we need
        // to insert items.
        int lastRealizedDataIndex = _firstRealizedDataIndex + RealizedCount - 1;
        int newStartingIndex = index;
        if (newStartingIndex >= _firstRealizedDataIndex &&
            newStartingIndex <= lastRealizedDataIndex)
        {
            // Inserted within the realized range
            int insertRangeStartIndex = newStartingIndex - _firstRealizedDataIndex;
            for (int i = 0; i < count; i++)
            {
                // Insert null (sentinel) here instead of an element, that way we dont
                // end up creating a lot of elements only to be thrown out in the next layout.
                int insertRangeIndex = insertRangeStartIndex + i;
                int dataIndex = newStartingIndex + i;
                // This is to keep the contiguousness of the mapping
                Insert(insertRangeIndex, dataIndex, null);
            }
        }
        else if (index <= _firstRealizedDataIndex)
        {
            // Items were inserted before the realized range.
            // We need to update m_firstRealizedDataIndex;
            _firstRealizedDataIndex += count;
        }
    }

    private void OnItemsRemoved(int index, int count)
    {
        int lastRealizedDataIndex = _firstRealizedDataIndex + _realizedElements.Count - 1;
        int startIndex = Math.Max(_firstRealizedDataIndex, index);
        int endIndex = Math.Min(lastRealizedDataIndex, index + count - 1);
        bool removeAffectsFirstRealizedDataIndex = (index <= _firstRealizedDataIndex);

        if (endIndex >= startIndex)
        {
            ClearRealizedRange(GetRealizedRangeIndexFromDataIndex(startIndex), endIndex - startIndex + 1);
        }

        if (removeAffectsFirstRealizedDataIndex &&
            _firstRealizedDataIndex != -1)
        {
            _firstRealizedDataIndex -= count;
        }
    }
}