using System;
using System.Collections.Generic;
using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Drawing;

namespace KustoTerminal.UI.Charts;

/// <summary>
/// A canvas that uses Unicode Braille patterns to achieve sub-character resolution,
/// similar to the termdash library. Each character cell contains 2x4 pixels that can be
/// independently set, providing smooth line rendering in terminal environments.
/// </summary>
public class BrailleCanvas
{
    // Braille Unicode range: U+2800 to U+28FF
    private const int BrailleCharOffset = 0x2800;
    private const int BrailleLastChar = 0x28FF;
    
    // Resolution multipliers
    private const int ColMult = 2; // 2 pixels per character width
    private const int RowMult = 4; // 4 pixels per character height
    
    // Pixel position to braille bit mapping
    private static readonly Dictionary<(int x, int y), int> PixelRunes = new()
    {
        { (0, 0), 0x01 }, { (1, 0), 0x08 },
        { (0, 1), 0x02 }, { (1, 1), 0x10 },
        { (0, 2), 0x04 }, { (1, 2), 0x20 },
        { (0, 3), 0x40 }, { (1, 3), 0x80 }
    };
    
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly char[,] _cells;
    public BrailleCanvas(int width, int height)
    {
        _width = width;
        _height = height;
        _cellWidth = (width + ColMult - 1) / ColMult; // Ceiling division
        _cellHeight = (height + RowMult - 1) / RowMult;
        _cells = new char[_cellWidth, _cellHeight];
        Clear();
    }
    
    /// <summary>
    /// Gets the pixel width of the canvas
    /// </summary>
    public int Width => _width;
    
    /// <summary>
    /// Gets the pixel height of the canvas
    /// </summary>
    public int Height => _height;
    
    /// <summary>
    /// Gets the character cell width
    /// </summary>
    public int CellWidth => _cellWidth;
    
    /// <summary>
    /// Gets the character cell height
    /// </summary>
    public int CellHeight => _cellHeight;
    
    /// <summary>
    /// Clears all pixels on the canvas
    /// </summary>
    public void Clear()
    {
        for (int x = 0; x < _cellWidth; x++)
        {
            for (int y = 0; y < _cellHeight; y++)
            {
                _cells[x, y] = (char)BrailleCharOffset; // Empty braille character
            }
        }
    }
    
    /// <summary>
    /// Sets a pixel at the specified coordinates
    /// </summary>
    public void SetPixel(int x, int y)
    {
        if (!IsValidPixel(x, y)) return;
        
        var (cellX, cellY) = GetCellCoordinates(x, y);
        var (subX, subY) = GetSubPixelCoordinates(x, y);
        
        if (PixelRunes.TryGetValue((subX, subY), out int pixelBit))
        {
            char currentChar = _cells[cellX, cellY];
            int currentValue = IsBrailleChar(currentChar) ? currentChar : BrailleCharOffset;
            _cells[cellX, cellY] = (char)(currentValue | pixelBit);
        }
    }
    
    /// <summary>
    /// Clears a pixel at the specified coordinates
    /// </summary>
    public void ClearPixel(int x, int y)
    {
        if (!IsValidPixel(x, y)) return;
        
        var (cellX, cellY) = GetCellCoordinates(x, y);
        var (subX, subY) = GetSubPixelCoordinates(x, y);
        
        if (PixelRunes.TryGetValue((subX, subY), out int pixelBit))
        {
            char currentChar = _cells[cellX, cellY];
            if (IsBrailleChar(currentChar))
            {
                _cells[cellX, cellY] = (char)(currentChar & ~pixelBit);
            }
        }
    }
    
    /// <summary>
    /// Draws a line between two points using Bresenham's algorithm
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        int x = x0;
        int y = y0;
        
        while (true)
        {
            SetPixel(x, y);
            
            if (x == x1 && y == y1) break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }
    
    /// <summary>
    /// Renders the canvas to a Terminal.Gui view
    /// </summary>
    public void Render(GraphView view)
    {
        var viewBounds = view.Frame;
        
        for (int cellY = 0; cellY < _cellHeight && cellY < viewBounds.Height; cellY++)
        {
            for (int cellX = 0; cellX < _cellWidth && cellX < viewBounds.Width; cellX++)
            {
                char brailleChar = _cells[cellX, cellY];
                
                if (brailleChar != BrailleCharOffset) // Only draw non-empty characters
                {
                    view.AddRune(cellX, cellY, new System.Text.Rune(brailleChar));
                }
            }
        }
    }
    
    private bool IsValidPixel(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;
    
    private (int cellX, int cellY) GetCellCoordinates(int x, int y) => (x / ColMult, y / RowMult);
    
    private (int subX, int subY) GetSubPixelCoordinates(int x, int y) => (x % ColMult, y % RowMult);
    
    private static bool IsBrailleChar(char c) => c >= BrailleCharOffset && c <= BrailleLastChar;
}
