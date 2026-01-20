using System.Drawing.Drawing2D;

namespace KlingerClient.Docking.Charts;

/// <summary>
/// High-performance GDI+ chart renderer
/// All brushes/pens cached, minimal allocations in hot path
/// </summary>
public sealed class ChartRenderer : IDisposable
{
    // Cached brushes
    private readonly SolidBrush _backgroundBrush;
    private readonly SolidBrush _gridBrush;
    private readonly SolidBrush _bullishBrush;
    private readonly SolidBrush _bearishBrush;
    private readonly SolidBrush _textBrush;
    private readonly SolidBrush _crosshairBrush;
    private readonly SolidBrush _ltpLineBrush;
    private readonly SolidBrush _volumeBrush;

    // Cached pens
    private readonly Pen _gridPen;
    private readonly Pen _bullishPen;
    private readonly Pen _bearishPen;
    private readonly Pen _linePen;
    private readonly Pen _crosshairPen;
    private readonly Pen _ltpLinePen;
    private readonly Pen _borderPen;

    // Cached fonts
    private readonly Font _axisFont;
    private readonly Font _labelFont;

    // Layout constants
    private const int PRICE_AXIS_WIDTH = 70;
    private const int TIME_AXIS_HEIGHT = 25;
    private const int VOLUME_HEIGHT_RATIO = 5; // 1/5 of chart area
    private const int MIN_CANDLE_WIDTH = 3;
    private const int MAX_CANDLE_WIDTH = 50;
    private const int CANDLE_SPACING = 2;

    // StringFormat cache
    private readonly StringFormat _rightAlign;
    private readonly StringFormat _centerAlign;

    public ChartRenderer()
    {
        // Dark theme colors
        var bgColor = Color.FromArgb(22, 22, 26);
        var gridColor = Color.FromArgb(40, 40, 45);
        var bullColor = Color.FromArgb(38, 166, 154);     // Green
        var bearColor = Color.FromArgb(239, 83, 80);      // Red
        var textColor = Color.FromArgb(180, 180, 180);
        var crossColor = Color.FromArgb(100, 100, 100);
        var ltpColor = Color.FromArgb(255, 193, 7);       // Amber
        var volumeColor = Color.FromArgb(60, 60, 80);

        _backgroundBrush = new SolidBrush(bgColor);
        _gridBrush = new SolidBrush(gridColor);
        _bullishBrush = new SolidBrush(bullColor);
        _bearishBrush = new SolidBrush(bearColor);
        _textBrush = new SolidBrush(textColor);
        _crosshairBrush = new SolidBrush(crossColor);
        _ltpLineBrush = new SolidBrush(ltpColor);
        _volumeBrush = new SolidBrush(volumeColor);

        _gridPen = new Pen(gridColor, 1) { DashStyle = DashStyle.Dot };
        _bullishPen = new Pen(bullColor, 1);
        _bearishPen = new Pen(bearColor, 1);
        _linePen = new Pen(bullColor, 2) { LineJoin = LineJoin.Round };
        _crosshairPen = new Pen(crossColor, 1) { DashStyle = DashStyle.Dash };
        _ltpLinePen = new Pen(ltpColor, 1) { DashStyle = DashStyle.Dash };
        _borderPen = new Pen(Color.FromArgb(60, 60, 65), 1);

        _axisFont = new Font("Consolas", 8f, FontStyle.Regular);
        _labelFont = new Font("Segoe UI", 9f, FontStyle.Bold);

        _rightAlign = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        _centerAlign = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    }

    public void RenderChart(
        Graphics g,
        Rectangle bounds,
        ReadOnlySpan<CandleData> candles,
        int candleCount,
        ChartType chartType,
        int candleWidth,
        Point? mousePosition,
        decimal? lastPrice)
    {
        if (bounds.Width < 100 || bounds.Height < 100) return;

        // Calculate areas
        var chartArea = new Rectangle(
            bounds.X,
            bounds.Y,
            bounds.Width - PRICE_AXIS_WIDTH,
            bounds.Height - TIME_AXIS_HEIGHT);

        var volumeHeight = chartArea.Height / VOLUME_HEIGHT_RATIO;
        var priceArea = new Rectangle(
            chartArea.X,
            chartArea.Y,
            chartArea.Width,
            chartArea.Height - volumeHeight);

        var volumeArea = new Rectangle(
            chartArea.X,
            priceArea.Bottom,
            chartArea.Width,
            volumeHeight);

        var priceAxisArea = new Rectangle(
            chartArea.Right,
            bounds.Y,
            PRICE_AXIS_WIDTH,
            bounds.Height - TIME_AXIS_HEIGHT);

        var timeAxisArea = new Rectangle(
            bounds.X,
            chartArea.Bottom,
            chartArea.Width,
            TIME_AXIS_HEIGHT);

        // Clear background
        g.FillRectangle(_backgroundBrush, bounds);

        if (candleCount == 0) return;

        // Calculate price range with padding
        decimal minPrice = decimal.MaxValue, maxPrice = decimal.MinValue;
        long maxVolume = 0;

        for (int i = 0; i < candleCount; i++)
        {
            ref readonly var c = ref candles[i];
            if (c.Low < minPrice) minPrice = c.Low;
            if (c.High > maxPrice) maxPrice = c.High;
            if (c.Volume > maxVolume) maxVolume = c.Volume;
        }

        // Add 5% padding
        var priceRange = maxPrice - minPrice;
        if (priceRange == 0) priceRange = 1;
        var padding = priceRange * 0.05m;
        minPrice -= padding;
        maxPrice += padding;
        priceRange = maxPrice - minPrice;

        // Draw grid
        DrawPriceGrid(g, priceArea, minPrice, maxPrice);

        // Draw volume bars
        DrawVolumeBars(g, volumeArea, candles, candleCount, candleWidth, maxVolume);

        // Draw candles/line based on type
        switch (chartType)
        {
            case ChartType.Candlestick:
                DrawCandlesticks(g, priceArea, candles, candleCount, candleWidth, minPrice, priceRange);
                break;
            case ChartType.Line:
                DrawLine(g, priceArea, candles, candleCount, candleWidth, minPrice, priceRange);
                break;
            case ChartType.Area:
                DrawArea(g, priceArea, candles, candleCount, candleWidth, minPrice, priceRange);
                break;
        }

        // Draw last price line
        if (lastPrice.HasValue && lastPrice.Value >= minPrice && lastPrice.Value <= maxPrice)
        {
            var y = PriceToY(priceArea, lastPrice.Value, minPrice, priceRange);
            g.DrawLine(_ltpLinePen, priceArea.Left, y, priceArea.Right, y);
            
            // Price label background
            var labelRect = new Rectangle(priceAxisArea.Left + 2, y - 8, PRICE_AXIS_WIDTH - 4, 16);
            g.FillRectangle(_ltpLineBrush, labelRect);
            g.DrawString(lastPrice.Value.ToString("F2"), _axisFont, _backgroundBrush, labelRect, _rightAlign);
        }

        // Draw price axis
        DrawPriceAxis(g, priceAxisArea, minPrice, maxPrice);

        // Draw time axis
        DrawTimeAxis(g, timeAxisArea, candles, candleCount, candleWidth);

        // Draw crosshair
        if (mousePosition.HasValue && priceArea.Contains(mousePosition.Value))
        {
            DrawCrosshair(g, priceArea, priceAxisArea, mousePosition.Value, minPrice, priceRange, candles, candleCount, candleWidth);
        }

        // Draw border
        g.DrawRectangle(_borderPen, chartArea);
    }

    private void DrawPriceGrid(Graphics g, Rectangle area, decimal minPrice, decimal maxPrice)
    {
        var range = maxPrice - minPrice;
        var step = CalculateNiceStep(range, 8);
        var startPrice = Math.Ceiling(minPrice / step) * step;

        for (var price = startPrice; price <= maxPrice; price += step)
        {
            var y = PriceToY(area, price, minPrice, range);
            g.DrawLine(_gridPen, area.Left, y, area.Right, y);
        }
    }

    private void DrawCandlesticks(Graphics g, Rectangle area, ReadOnlySpan<CandleData> candles, int count, int candleWidth, decimal minPrice, decimal priceRange)
    {
        var totalWidth = candleWidth + CANDLE_SPACING;
        var startX = area.Right - (count * totalWidth);

        for (int i = 0; i < count; i++)
        {
            ref readonly var c = ref candles[i];
            var x = startX + (i * totalWidth);
            
            if (x + candleWidth < area.Left) continue;
            if (x > area.Right) break;

            var yHigh = PriceToY(area, c.High, minPrice, priceRange);
            var yLow = PriceToY(area, c.Low, minPrice, priceRange);
            var yOpen = PriceToY(area, c.Open, minPrice, priceRange);
            var yClose = PriceToY(area, c.Close, minPrice, priceRange);

            var brush = c.IsBullish ? _bullishBrush : _bearishBrush;
            var pen = c.IsBullish ? _bullishPen : _bearishPen;

            var wickX = x + candleWidth / 2;
            
            // Wick
            g.DrawLine(pen, wickX, yHigh, wickX, yLow);

            // Body
            var bodyTop = Math.Min(yOpen, yClose);
            var bodyHeight = Math.Max(1, Math.Abs(yClose - yOpen));
            
            if (candleWidth > 2)
            {
                g.FillRectangle(brush, x, bodyTop, candleWidth, bodyHeight);
            }
        }
    }

    private void DrawLine(Graphics g, Rectangle area, ReadOnlySpan<CandleData> candles, int count, int candleWidth, decimal minPrice, decimal priceRange)
    {
        if (count < 2) return;

        var totalWidth = candleWidth + CANDLE_SPACING;
        var startX = area.Right - (count * totalWidth);

        using var path = new GraphicsPath();
        var points = new PointF[count];

        for (int i = 0; i < count; i++)
        {
            var x = startX + (i * totalWidth) + candleWidth / 2f;
            var y = PriceToY(area, candles[i].Close, minPrice, priceRange);
            points[i] = new PointF(x, y);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(_linePen, points);
        g.SmoothingMode = SmoothingMode.None;
    }

    private void DrawArea(Graphics g, Rectangle area, ReadOnlySpan<CandleData> candles, int count, int candleWidth, decimal minPrice, decimal priceRange)
    {
        if (count < 2) return;

        var totalWidth = candleWidth + CANDLE_SPACING;
        var startX = area.Right - (count * totalWidth);

        var points = new PointF[count + 2];

        for (int i = 0; i < count; i++)
        {
            var x = startX + (i * totalWidth) + candleWidth / 2f;
            var y = PriceToY(area, candles[i].Close, minPrice, priceRange);
            points[i] = new PointF(x, y);
        }

        // Close the polygon
        points[count] = new PointF(points[count - 1].X, area.Bottom);
        points[count + 1] = new PointF(points[0].X, area.Bottom);

        using var brush = new LinearGradientBrush(
            new Point(0, area.Top),
            new Point(0, area.Bottom),
            Color.FromArgb(80, 38, 166, 154),
            Color.FromArgb(10, 38, 166, 154));

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPolygon(brush, points);
        g.DrawLines(_linePen, points.AsSpan(0, count).ToArray());
        g.SmoothingMode = SmoothingMode.None;
    }

    private void DrawVolumeBars(Graphics g, Rectangle area, ReadOnlySpan<CandleData> candles, int count, int candleWidth, long maxVolume)
    {
        if (maxVolume == 0) return;

        var totalWidth = candleWidth + CANDLE_SPACING;
        var startX = area.Right - (count * totalWidth);

        for (int i = 0; i < count; i++)
        {
            ref readonly var c = ref candles[i];
            var x = startX + (i * totalWidth);
            
            if (x + candleWidth < area.Left) continue;
            if (x > area.Right) break;

            var barHeight = (int)(area.Height * ((double)c.Volume / maxVolume));
            var y = area.Bottom - barHeight;

            var brush = c.IsBullish ? _bullishBrush : _bearishBrush;
            g.FillRectangle(brush, x, y, candleWidth, barHeight);
        }
    }

    private void DrawPriceAxis(Graphics g, Rectangle area, decimal minPrice, decimal maxPrice)
    {
        g.FillRectangle(_backgroundBrush, area);
        g.DrawLine(_borderPen, area.Left, area.Top, area.Left, area.Bottom);

        var range = maxPrice - minPrice;
        var step = CalculateNiceStep(range, 8);
        var startPrice = Math.Ceiling(minPrice / step) * step;

        for (var price = startPrice; price <= maxPrice; price += step)
        {
            var y = (int)((1 - (float)((price - minPrice) / range)) * (area.Height - TIME_AXIS_HEIGHT)) + area.Top;
            var labelRect = new Rectangle(area.Left + 5, y - 7, area.Width - 10, 14);
            g.DrawString(price.ToString("F2"), _axisFont, _textBrush, labelRect, _rightAlign);
        }
    }

    private void DrawTimeAxis(Graphics g, Rectangle area, ReadOnlySpan<CandleData> candles, int count, int candleWidth)
    {
        if (count == 0) return;

        g.FillRectangle(_backgroundBrush, area);
        g.DrawLine(_borderPen, area.Left, area.Top, area.Right, area.Top);

        var totalWidth = candleWidth + CANDLE_SPACING;
        var startX = area.Right - (count * totalWidth);
        var labelInterval = Math.Max(1, 60 / (candleWidth + CANDLE_SPACING)); // Approx every 60px

        for (int i = 0; i < count; i += labelInterval)
        {
            var x = startX + (i * totalWidth) + candleWidth / 2;
            if (x < area.Left || x > area.Right) continue;

            var time = candles[i].OpenTime.ToLocalTime();
            var label = time.ToString("HH:mm:ss");
            
            g.DrawLine(_gridPen, x, area.Top, x, area.Top + 4);
            g.DrawString(label, _axisFont, _textBrush, x, area.Top + 6, _centerAlign);
        }
    }

    private void DrawCrosshair(Graphics g, Rectangle priceArea, Rectangle priceAxisArea, Point mouse, decimal minPrice, decimal priceRange, ReadOnlySpan<CandleData> candles, int count, int candleWidth)
    {
        // Horizontal line
        g.DrawLine(_crosshairPen, priceArea.Left, mouse.Y, priceArea.Right, mouse.Y);
        
        // Vertical line
        g.DrawLine(_crosshairPen, mouse.X, priceArea.Top, mouse.X, priceArea.Bottom);

        // Price at cursor
        var price = YToPrice(priceArea, mouse.Y, minPrice, priceRange);
        var priceLabel = price.ToString("F2");
        var labelRect = new Rectangle(priceAxisArea.Left + 2, mouse.Y - 8, PRICE_AXIS_WIDTH - 4, 16);
        g.FillRectangle(_crosshairBrush, labelRect);
        g.DrawString(priceLabel, _axisFont, _textBrush, labelRect, _rightAlign);

        // Find hovered candle
        var totalWidth = candleWidth + CANDLE_SPACING;
        var startX = priceArea.Right - (count * totalWidth);
        var candleIndex = (mouse.X - startX) / totalWidth;

        if (candleIndex >= 0 && candleIndex < count)
        {
            ref readonly var c = ref candles[candleIndex];
            var tooltip = $"O:{c.Open:F2} H:{c.High:F2} L:{c.Low:F2} C:{c.Close:F2} V:{c.Volume:N0}";
            
            var tooltipSize = g.MeasureString(tooltip, _axisFont);
            var tooltipX = Math.Min(mouse.X + 10, priceArea.Right - (int)tooltipSize.Width - 5);
            var tooltipY = Math.Max(mouse.Y - 20, priceArea.Top + 5);
            
            var tooltipRect = new RectangleF(tooltipX, tooltipY, tooltipSize.Width + 8, tooltipSize.Height + 4);
            g.FillRectangle(_gridBrush, tooltipRect);
            g.DrawString(tooltip, _axisFont, _textBrush, tooltipX + 4, tooltipY + 2);
        }
    }

    private static int PriceToY(Rectangle area, decimal price, decimal minPrice, decimal priceRange)
    {
        var ratio = (float)((price - minPrice) / priceRange);
        return area.Bottom - (int)(ratio * area.Height);
    }

    private static decimal YToPrice(Rectangle area, int y, decimal minPrice, decimal priceRange)
    {
        var ratio = 1f - ((float)(y - area.Top) / area.Height);
        return minPrice + (decimal)ratio * priceRange;
    }

    private static decimal CalculateNiceStep(decimal range, int targetLines)
    {
        var roughStep = range / targetLines;
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)roughStep)));
        var residual = roughStep / magnitude;

        decimal niceStep;
        if (residual > 5) niceStep = 10 * magnitude;
        else if (residual > 2) niceStep = 5 * magnitude;
        else if (residual > 1) niceStep = 2 * magnitude;
        else niceStep = magnitude;

        return niceStep;
    }

    public void Dispose()
    {
        _backgroundBrush.Dispose();
        _gridBrush.Dispose();
        _bullishBrush.Dispose();
        _bearishBrush.Dispose();
        _textBrush.Dispose();
        _crosshairBrush.Dispose();
        _ltpLineBrush.Dispose();
        _volumeBrush.Dispose();

        _gridPen.Dispose();
        _bullishPen.Dispose();
        _bearishPen.Dispose();
        _linePen.Dispose();
        _crosshairPen.Dispose();
        _ltpLinePen.Dispose();
        _borderPen.Dispose();

        _axisFont.Dispose();
        _labelFont.Dispose();

        _rightAlign.Dispose();
        _centerAlign.Dispose();
    }
}
