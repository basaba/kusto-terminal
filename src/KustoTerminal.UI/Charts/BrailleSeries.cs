using System.Drawing;
using System.Text;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Charts;

public class BrailleSeries : ISeries
{
    private readonly List<PointF> _points;
    private GraphView _graph;
    private RectangleF _graphBounds;
    
    // Braille canvas for high-resolution drawing
    private Dictionary<(int x, int y), byte> _brailleCanvas;

    // Braille pattern constants (Unicode U+2800 base + pattern bits)
    private const char BRAILLE_BASE = '\u2800';
    
    // Braille dot patterns (2x4 grid):
    // ⠁ ⠂
    // ⠄ ⠈  
    // ⠐ ⠠
    // ⢀ ⡀
    private static readonly byte[,] DOT_PATTERNS = new byte[2, 4]
    {
        { 0x01, 0x02, 0x04, 0x40 }, // Left column (dots 1, 2, 3, 7)
        { 0x08, 0x10, 0x20, 0x80 }  // Right column (dots 4, 5, 6, 8)
    };

    public BrailleSeries()
    {
        _points = new List<PointF>();
        _brailleCanvas = new Dictionary<(int x, int y), byte>();
    }

    public void AddPoints(IEnumerable<PointF> points)
    {
        _points.AddRange(points);
    }
    
    public void DrawSeries(GraphView graph, Rectangle drawBounds, RectangleF graphBounds)
    {
        _graph = graph;
        _graphBounds = graphBounds;
        _brailleCanvas.Clear();
        
        var points = _points.ToArray();
        for (int i = 0; i < points.Length - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            DrawSmoothLine(p1, p2);
        }
        
        // Render the braille canvas to the graph
        RenderBrailleCanvas();

        foreach (var point in points)
        {
            DrawPoint(point, new Rune('x'));
        }
    }

    private void DrawSmoothLine(PointF p1, PointF p2)
    {
        // Convert points to screen coordinates with sub-pixel precision
        var screen1 = _graph.GraphSpaceToScreen(p1);
        var screen2 = _graph.GraphSpaceToScreen(p2);
        
        // Scale up by 2x4 for Braille sub-pixel resolution
        float x0 = screen1.X * 2.0f;
        float y0 = screen1.Y * 4.0f;
        float x1 = screen2.X * 2.0f;
        float y1 = screen2.Y * 4.0f;
        
        // Use floating-point Bresenham for smoother lines
        float dx = Math.Abs(x1 - x0);
        float dy = Math.Abs(y1 - y0);
        
        float sx = x0 < x1 ? 1.0f : -1.0f;
        float sy = y0 < y1 ? 1.0f : -1.0f;
        
        float err = dx - dy;
        float currentX = x0;
        float currentY = y0;
        
        while (true)
        {
            // Set the sub-pixel in the Braille canvas
            SetBrailleSubPixel((int)Math.Round(currentX), (int)Math.Round(currentY));
            
            // Check if we've reached the end point
            if (Math.Abs(currentX - x1) < 0.5f && Math.Abs(currentY - y1) < 0.5f)
                break;
                
            float e2 = 2.0f * err;
            
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

    private void SetBrailleSubPixel(int subX, int subY)
    {
        // Convert sub-pixel coordinates to character cell coordinates
        int charX = subX / 2;
        int charY = subY / 4;
        
        // Get the position within the 2x4 Braille character
        int dotX = subX % 2;
        int dotY = subY % 4;
        
        // Ensure we're within valid bounds
        if (dotX < 0 || dotX >= 2 || dotY < 0 || dotY >= 4)
            return;
            
        // Get the bit pattern for this dot position
        byte dotPattern = DOT_PATTERNS[dotX, dotY];
        
        // Add the dot to the canvas
        var key = (charX, charY);
        if (_brailleCanvas.ContainsKey(key))
        {
            _brailleCanvas[key] |= dotPattern;
        }
        else
        {
            _brailleCanvas[key] = dotPattern;
        }
    }

    private void RenderBrailleCanvas()
    {
        foreach (var kvp in _brailleCanvas)
        {
            var (x, y) = kvp.Key;
            var pattern = kvp.Value;
            
            // Create the Braille character from the pattern
            char brailleChar = (char)(BRAILLE_BASE + pattern);
            
            // Draw the character to the graph
            _graph.AddRune(x, y, new Rune(brailleChar));
        }
    }

    private void DrawPoint(PointF point, Rune rune)
    {
        var screenRect = _graph.GraphSpaceToScreen(point);
        _graph.AddRune(screenRect.X, screenRect.Y, rune);
    }
}
