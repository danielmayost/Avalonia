using Avalonia;

namespace Sandbox;

public class RatioLayoutManager
{
    private readonly double _defaultRatio;

    private Rect[]? _bounds;
    private bool _isBoundsValid;
    private int _maxItems;
    private IReadOnlyList<double>? _finalRatios;
    private int _arerageElementInRow;

    private double _minItemsHeight;
    private double _columnSpacing;
    private double _rowSpacing;
    private IEnumerable<double>? _itemsRatios;
    private double _availableWidth;

    public RatioLayoutManager(double defaultRatio)
    {
        _defaultRatio = defaultRatio;
    }

    public void SetItemsRatios(IEnumerable<double>? itemsRatios)
    {
        _itemsRatios = itemsRatios;
        _isBoundsValid = false;
    }

    public void EnsureBounds(double minItemHeight, double columnSpacing, double rowSpacing, 
        double availableWidth, int countItems)
    {
        ValidateBounds(minItemHeight, columnSpacing, rowSpacing, availableWidth);

        //TODO: countItems affect from new index
        if (countItems > _maxItems)
        {
            _maxItems = countItems;
            _isBoundsValid = false;
        }
        
        if (!_isBoundsValid)
        {
            CalculateBounds();
        }
    }

    public Rect[] GetBounds()
    {
        if (_bounds == null || !_isBoundsValid)
        {
            throw new InvalidOperationException("bounds isn't up date");
        }

        return _bounds;
    }

    public Range GetRange(Rect viewport, int itemCount)
    {
        if (_bounds == null || !_isBoundsValid || itemCount > _maxItems)
        {
            throw new InvalidOperationException("bounds isn't up date");
        }

        if (viewport.Top > _bounds[itemCount - 1].Bottom)
        {
            return new Range(itemCount - 1, itemCount - 1);
        }

        // estimate anchorIndex
        int arerageElementInRow = _arerageElementInRow;
        double extentHeight = _bounds[itemCount - 1].Bottom;
        double averageHeight = extentHeight / (itemCount / arerageElementInRow);
        int anchorIndex = (int)(viewport.Top / averageHeight) * arerageElementInRow;
        anchorIndex = Math.Max(0, Math.Min(anchorIndex, itemCount - 1));

        //compute whether element fall into viewport

        bool shouldForward = _bounds[anchorIndex].Top <= viewport.Bottom;
        bool shouldBackward = _bounds[anchorIndex].Bottom >= viewport.Top;
        bool outOfRangeTop = _bounds[anchorIndex].Bottom < viewport.Top;
        bool outOfRangeBottom = _bounds[anchorIndex].Top > viewport.Bottom;

        int startIndex = 0;
        int endIndex = itemCount - 1;

        int i;

        i = anchorIndex;
        if (shouldForward)
        {
            if (outOfRangeTop)
            {
                while (i < itemCount)
                {
                    if (_bounds[i].Bottom >= viewport.Top)
                    {
                        startIndex = i;
                        break;
                    }

                    i++;
                }
            }

            while (i < itemCount)
            {
                if (_bounds[i].Top > viewport.Bottom)
                {
                    endIndex = i - 1;
                    break;
                }

                i++;
            }
        }

        i = anchorIndex;
        if (shouldBackward)
        {
            if (outOfRangeBottom)
            {
                while (i > 0)
                {
                    if (_bounds[i].Top <= viewport.Bottom)
                    {
                        endIndex = i;
                        break;
                    }

                    i--;
                }
            }

            while (i > 0)
            {
                if (_bounds[i].Bottom < viewport.Top)
                {
                    startIndex = i + 1;
                    break;
                }

                i--;
            }
        }

        return new Range(startIndex, endIndex);
    }

    private void ValidateBounds(double minItemHeight, double columnSpacing, double rowSpacing, 
        double availableWidth)
    {
        if (minItemHeight != _minItemsHeight ||
            columnSpacing != _columnSpacing ||
            rowSpacing != _rowSpacing ||
            availableWidth != _availableWidth)
        {
            _isBoundsValid = false;

            _minItemsHeight = minItemHeight;
            _columnSpacing = columnSpacing;
            _rowSpacing = rowSpacing;
            _availableWidth = availableWidth;
        }
    }

    private void CalculateBounds()
    {
        EnsureRatios();

        _arerageElementInRow = 0;

        double availableWidth = _availableWidth;
        var ratios = _finalRatios;
        double rowSpacing = _rowSpacing;
        double columnSpacing = _columnSpacing;
        int maxItems = _maxItems;
        
        Rect[] finalRect = new Rect[_maxItems];
        int rowCounter = 0;

        double top = 0;
        double left = 0;
        List<Size> currentRow;

        int finalCounter = 0;

        for (int i = 0; i < maxItems; i++)
        {
            var sizeElement = GetSizeElement(i);

            currentRow = new();
            currentRow.Add(sizeElement);
            
            left = 0;
            left += sizeElement.Width;

            while (left < availableWidth && i + 1 < maxItems)
            {
                i++;
                left += columnSpacing;

                sizeElement = GetSizeElement(i);

                if (left + sizeElement.Width > availableWidth)
                {
                    i--;
                    break;
                }
                
                currentRow.Add(sizeElement);
                left += sizeElement.Width;
            }

            //End Line
            left = 0;
            Size[] rowFinalSize = GetFinalRowSize(currentRow);

            for (int j = 0; j < rowFinalSize.Length; j++)
            {
                var currentSize = rowFinalSize[j];

                Point position = new Point(left, top);
                finalRect[finalCounter] = new Rect(position, currentSize);

                left += (currentSize.Width + columnSpacing);
                finalCounter ++;
            }

            rowCounter ++;
            top += rowFinalSize[0].Height + rowSpacing;
        }

        _isBoundsValid = true;

        _arerageElementInRow = maxItems / rowCounter;
        _bounds = finalRect;
    }

    private void EnsureRatios()
    {
        double[] ratios = new double[_maxItems];

        int i = 0;

        if (_itemsRatios != null)
        {
            foreach (double ratio in _itemsRatios)
            {
                ratios[i] = ratio;
                i++;
            }
        }

        for (;i < _maxItems; i++)
        {
            ratios[i] = _defaultRatio;
        }

        _finalRatios = ratios;
    }

    private Size GetSizeElement(int index)
    {
        double ratio = _finalRatios![index];

        double height = _minItemsHeight;
        double width = ratio * height;

        return new Size(width, height);
    }

    private Size[] GetFinalRowSize(IReadOnlyList<Size> sizeElements)
    {
        double columnSpacing = _columnSpacing;
        double availableWidth = _availableWidth;
        int countElements = sizeElements.Count;

        double extentElements = sizeElements.Sum(x => x.Width);
        availableWidth -= (countElements - 1) * columnSpacing;

        double emptyArea = availableWidth - extentElements;

        Size[] finalSize = new Size[countElements];
        for (int i = 0; i < countElements; i++)
        {
            double currentWidth = sizeElements[i].Width;
            double ratioExtent = currentWidth / extentElements;
            double additionalWidth = emptyArea * ratioExtent;

            double finalWidth = currentWidth + additionalWidth;
            finalSize[i] = sizeElements[i].WithWidthRatio(finalWidth);
        }

        return finalSize;
    }
}

internal static class SizeExtension
{
    public static Size WithWidthRatio(this Size size, double width)
    {
        double ratio = size.AspectRatio;
        return new Size(width, width / ratio);
    }
}