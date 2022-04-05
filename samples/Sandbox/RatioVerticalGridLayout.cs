using System.Collections.Specialized;
using Avalonia;
using Avalonia.Layout;

namespace Sandbox;

public class RatioVerticalGridLayout : VirtualizingLayout
{
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<RatioVerticalGridLayout, double>(nameof(RowSpacing));

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<RatioVerticalGridLayout, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<double> MinItemHeightProperty =
        AvaloniaProperty.Register<RatioVerticalGridLayout, double>(nameof(MinItemHeight));
        
    public static readonly DirectProperty<RatioVerticalGridLayout, IEnumerable<double>?> ItemsRatiosProperty =
        AvaloniaProperty.RegisterDirect<RatioVerticalGridLayout, IEnumerable<double>?>(nameof(ItemsRatios), o => o.ItemsRatios, (o, v) => o.ItemsRatios =v);

    private readonly ElementManager _elementManager = new ElementManager();
    private RatioLayoutManager _manager;
    private IEnumerable<double>? _itemsRatios;
    private Rect[]? _bounds;
    private double _defaultRatio = 1;

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double MinItemHeight
    {
        get => GetValue(MinItemHeightProperty);
        set => SetValue(MinItemHeightProperty, value);
    }

    public IEnumerable<double>? ItemsRatios
    {
        get => _itemsRatios;
        set => SetAndRaise(ItemsRatiosProperty, ref _itemsRatios, value);
    }

    public RatioVerticalGridLayout()
    {
        _manager = new RatioLayoutManager(_defaultRatio);
        SubscribeToItems(_itemsRatios);
    }

    static RatioVerticalGridLayout()
    {
        RowSpacingProperty.Changed.AddClassHandler<RatioVerticalGridLayout>((s, e) => s.AffectMeasureProperty(s));
        ColumnSpacingProperty.Changed.AddClassHandler<RatioVerticalGridLayout>((s, e) => s.AffectMeasureProperty(s));
        MinItemHeightProperty.Changed.AddClassHandler<RatioVerticalGridLayout>((s, e) => s.AffectMeasureProperty(s));

        ItemsRatiosProperty.Changed.AddClassHandler<RatioVerticalGridLayout>((s, e) => s.ItemsRatiosChanged(e));
    }

    private void AffectMeasureProperty(RatioVerticalGridLayout layout)
    {
        layout.InvalidateMeasure();
    }

    private void ItemsRatiosCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        InvalidateMeasure();
    }

    private void SubscribeToItems(IEnumerable<double>? items)
    {
        if (items is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += ItemsRatiosCollectionChanged;
        }
    }

    private void ItemsRatiosChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var oldValue = e.OldValue as IEnumerable<double>;
        var newValue = e.NewValue as IEnumerable<double>;;

        if (oldValue is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged -= ItemsRatiosCollectionChanged;
        }

        _manager.SetItemsRatios(newValue);
        SubscribeToItems(newValue);
        InvalidateMeasure();
    }

    protected override void InitializeForContextCore(VirtualizingLayoutContext context)
    {
        base.InitializeForContextCore(context);

        var state = context.LayoutState as RatioVerticalGridLayoutState;
        if (state == null)
        {
            context.LayoutState = new RatioVerticalGridLayoutState();
        }
    }

    protected override void UninitializeForContextCore(VirtualizingLayoutContext context)
    {
        base.UninitializeForContextCore(context);

        context.LayoutState = null;
    }

    protected override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
    {
        RatioVerticalGridLayoutState state = (RatioVerticalGridLayoutState)context.LayoutState!;
        _manager.EnsureBounds(MinItemHeight, ColumnSpacing, RowSpacing, availableSize.Width, context.ItemCount);
        state.LayoutRects = _manager.GetBounds();

        var range = _manager.GetRange(context.RealizationRect, context.ItemCount);
        state.RealizedIndex = range;

        int startIndex = range.Start.Value;
        int endIndex = range.End.Value;

        for (int i = startIndex; i <= endIndex; i++)
        {
            var container = context.GetOrCreateElementAt(i);

            Size current = state.LayoutRects[i].Size;
            if (!container.IsMeasureValid)
            container.Measure(current);
        }

        return new Size(availableSize.Width, state.LayoutRects[^1].Bottom);
    }

    protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
    {
        RatioVerticalGridLayoutState state = (RatioVerticalGridLayoutState)context.LayoutState!;
        if (state.LayoutRects == null)
        {
            return finalSize;
        }

        int startIndex = state.RealizedIndex.Start.Value;
        int endIndex = state.RealizedIndex.End.Value;

        for (int i = startIndex; i <= endIndex; i++)
        {
            var container = context.GetOrCreateElementAt(i);

            if (!container.IsArrangeValid)
            container.Arrange(state.LayoutRects[i]);
        }

        return finalSize;
    }
}

internal class RatioVerticalGridLayoutState
{
    public Range RealizedIndex { get; set; }
    public Range PreviousRealizedIndex { get; set; }
    public Rect[]? LayoutRects { get; set; }

    public RatioVerticalGridLayoutState()
    {
        
    }

    public void FillPreviousIndex()
    {
        PreviousRealizedIndex = RealizedIndex;
    }

    public bool Contains(int index, bool previous)
    {
        if (index == 0)
        {
            return false;
        }

        var range = previous ? PreviousRealizedIndex : RealizedIndex;
        return index >= range.Start.Value && index <= range.End.Value;
    }
}