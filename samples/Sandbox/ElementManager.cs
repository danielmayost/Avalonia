using System.Collections.Specialized;
using Avalonia.Layout.Utils;
using Avalonia.Logging;
using Avalonia.Layout;
using Avalonia;

namespace Sandbox;

public class ElementManager
{
    private readonly List<ILayoutable?> _realizedElements = new List<ILayoutable?>();
    private readonly Rect[]? _realizedElementLayoutBounds;
    private int _firstRealizedDataIndex;
    private VirtualizingLayoutContext? _context;

    public int RealizedElementCount => _context?.ItemCount ?? 0;

    public ElementManager()
    {
        
    }

    public void SetContext(VirtualizingLayoutContext virtualContext)
    {
        _context = virtualContext;
    }

    public void OnBeginMeasure()
    {
        if (_context != null)
        {
            DiscardElementsOutsideWindow(_context.RealizationRect);
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

        int endIndex = realizedIndex + count;
        _realizedElements.RemoveRange(realizedIndex, endIndex - realizedIndex);
        //_realizedElementLayoutBounds.RemoveRange(realizedIndex, endIndex - realizedIndex);

        if (realizedIndex == 0)
        {
            _firstRealizedDataIndex = _realizedElements.Count == 0 ?
                -1 : _firstRealizedDataIndex + count;
        }
    }

    public void DiscardElementsOutsideWindow(bool forward, int startIndex)
    {
        if (IsDataIndexRealized(startIndex))
        {
            int rangeIndex = GetRealizedRangeIndexFromDataIndex(startIndex);

            if (forward)
            {
                ClearRealizedRange(rangeIndex, RealizedElementCount - rangeIndex);
            }
            else
            {
                ClearRealizedRange(0, rangeIndex + 1);
            }
        }
    }

    public void ClearRealizedRange()
    {
        ClearRealizedRange(0, RealizedElementCount);
    }

    public Rect GetLayoutBoundsForRealizedIndex(int realizedIndex)
    {
        return _realizedElementLayoutBounds[realizedIndex];
    }

    public void SetLayoutBoundsForDataIndex(int dataIndex, in Rect bounds)
    {
        int realizedIndex = GetRealizedRangeIndexFromDataIndex(dataIndex);
        _realizedElementLayoutBounds[realizedIndex] = bounds;
    }
    
    public void SetLayoutBoundsForRealizedIndex(int realizedIndex, in Rect bounds)
    {
        _realizedElementLayoutBounds[realizedIndex] = bounds;
    }

    public bool IsDataIndexRealized(int index)
    {

            int realizedCount = RealizedElementCount;
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

    public bool IsWindowConnected(in Rect window)
    {
        bool intersects = false;

        if (_realizedElementLayoutBounds.Length > 0)
        {
            var firstElementBounds = _realizedElementLayoutBounds[0];
            var lastElementBounds = _realizedElementLayoutBounds[RealizedElementCount - 1];

            var windowStart = window.Y;
            var windowEnd = window.Y + window.Height;
            var firstElementStart = firstElementBounds.Y;
            var lastElementEnd = lastElementBounds.Y + lastElementBounds.Height;

            intersects =
                firstElementStart <= windowEnd &&
                lastElementEnd >= windowStart;
        }

        return intersects;
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

    private void DiscardElementsOutsideWindow(in Rect window)
    {
        int realizedRangeSize = RealizedElementCount;
        int frontCutoffIndex = -1;
        int backCutoffIndex = realizedRangeSize;

        for (int i = 0;
            i < realizedRangeSize &&
            !Intersects(window, _realizedElementLayoutBounds[i]);
            ++i)
        {
            ++frontCutoffIndex;
        }

        for (int i = realizedRangeSize - 1;
            i >= 0 &&
            !Intersects(window, _realizedElementLayoutBounds[i]);
            --i)
        {
            --backCutoffIndex;
        }

        if (backCutoffIndex < realizedRangeSize - 1)
        {
            ClearRealizedRange(backCutoffIndex + 1, realizedRangeSize - backCutoffIndex - 1);
        }

        if (frontCutoffIndex > 0)
        {
            ClearRealizedRange(0, Math.Min(frontCutoffIndex, RealizedElementCount));
        }
    }

    private static bool Intersects(in Rect lhs, in Rect rhs)
    {
        var lhsStart = lhs.Y;
        var lhsEnd = lhs.Y + lhs.Height;
        var rhsStart = rhs.Y;
        var rhsEnd = rhs.Y + rhs.Height;

        return lhsEnd >= rhsStart && lhsStart <= rhsEnd;
    }

    private void OnItemsAdded(int index, int count)
    {
        // Using the old indices here (before it was updated by the collection change)
        // if the insert data index is between the first and last realized data index, we need
        // to insert items.
        int lastRealizedDataIndex = _firstRealizedDataIndex + RealizedElementCount - 1;
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