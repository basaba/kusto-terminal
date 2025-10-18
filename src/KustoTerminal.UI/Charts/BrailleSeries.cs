using System.Drawing;
using System.Text;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Charts;

public class BrailleSeries : ISeries
{
    private readonly List<PointF> _points;
    private GraphView _graph;
    private RectangleF _graphBounds;

    public BrailleSeries()
    {
        _points = new List<PointF>();
    }

    public void AddPoints(IEnumerable<PointF> points)
    {
        _points.AddRange(points);
    }
    
    public void DrawSeries(GraphView graph, Rectangle drawBounds, RectangleF graphBounds)
    {
        _graph = graph;
        _graphBounds = graphBounds;
        var points = _points.ToArray();
        for (int i = 0; i < points.Length - 1; i++)
        {
            var p1 =  points[i];
            var p2 = points[i + 1];
            DrawLine(p1, p2);
        }
    }

    private void DrawLine(PointF p1, PointF p2)
    {
        // Convert points to screen coordinates for pixel-level drawing
        var screen1 = _graph.GraphSpaceToScreen(p1);
        var screen2 = _graph.GraphSpaceToScreen(p2);
        
        // Bresenham's line algorithm
        int x0 = screen1.X;
        int y0 = screen1.Y;
        int x1 = screen2.X;
        int y1 = screen2.Y;
        
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        
        int sx = x0 < x1 ? 1 : -1;  // Step direction for x
        int sy = y0 < y1 ? 1 : -1;  // Step direction for y
        
        int err = dx - dy;  // Error term
        
        int currentX = x0;
        int currentY = y0;
        
        while (true)
        {
            // Draw point directly at screen coordinates
            _graph.AddRune(currentX, currentY, new Rune('⠿')); // Full Braille block character
            
            // Check if we've reached the end point
            if (currentX == x1 && currentY == y1)
                break;
                
            int e2 = 2 * err;
            
            // Decide whether to step in x direction
            if (e2 > -dy)
            {
                err -= dy;
                currentX += sx;
            }
            
            // Decide whether to step in y direction
            if (e2 < dx)
            {
                err += dx;
                currentY += sy;
            }
        }
    }

    private void DrawPoint(PointF point, Rune rune)
    {
        var screenRect = _graph.GraphSpaceToScreen(point);
        _graph.AddRune(screenRect.X, screenRect.Y, rune);
    }
}
