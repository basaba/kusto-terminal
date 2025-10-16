# Braille Charts for Terminal.Gui

This implementation provides smooth line rendering in terminal environments using Unicode braille patterns, similar to the approach used by the `termdash` library in Go.

## How It Works

### Braille Character Sub-Pixel Resolution

The key innovation is using Unicode braille patterns (U+2800 to U+28FF) to achieve sub-character resolution:

- Each terminal character cell is subdivided into a **2×4 pixel grid** (8 sub-pixels)
- This increases effective resolution from terminal dimensions (e.g., 80×24) to **160×96 pixels**
- Braille characters can represent any combination of these 8 pixels independently

### Pixel Mapping

```
Each cell:     Braille bits:
X→ 0 1         0x01  0x08
Y ┌───┐ ↓      0x02  0x10  
  │● ●│ 0      0x04  0x20
  │● ●│ 1      0x40  0x80
  │● ●│ 2      
  │● ●│ 3      
  └───┘        
```

### Line Drawing Algorithm

Uses Bresenham's line algorithm optimized for braille pixels:
1. Calculate line segments between data points
2. Convert coordinates to braille canvas space
3. Draw pixel-perfect lines using sub-character precision
4. Combine multiple pixels into braille characters for rendering

## Components

### BrailleCanvas

The core rendering engine that manages braille character generation:

```csharp
// Create a canvas with pixel resolution (2x width, 4x height of character grid)
var canvas = new BrailleCanvas(width: 160, height: 96);

// Draw individual pixels
canvas.SetPixel(x: 10, y: 20);

// Draw smooth lines between points
canvas.DrawLine(x1: 0, y1: 0, x2: 50, y2: 30);

// Render to Terminal.Gui view
canvas.Render(view);
```

### BrailleSeries

Manages data points and provides smooth line rendering:

```csharp
// Create a new series
var series = new BrailleSeries("My Data");

// Add data points
series.AddPoint(0.0, 1.5);
series.AddPoint(1.0, 2.3);
series.AddPoint(2.0, 1.8);

// Render to canvas with coordinate transformation
series.RenderToCanvas(canvas, xMin: 0, xMax: 10, yMin: 0, yMax: 5);
```

## Usage Examples

### Basic Usage

```csharp
using KustoTerminal.UI.Charts;

// Create series from data arrays
double[] xValues = { 0, 1, 2, 3, 4, 5 };
double[] yValues = { 0, 1, 4, 9, 16, 25 };
var series = BrailleSeriesExtensions.CreateFromArrays(xValues, yValues, "Quadratic");

// Create canvas and render
var canvas = new BrailleCanvas(80, 40); // Character dimensions * multipliers
var (xMin, xMax) = series.GetXRange();
var (yMin, yMax) = series.GetYRange();

series.RenderToCanvas(canvas, xMin, xMax, yMin, yMax);
```

### Mathematical Functions

```csharp
// Create sine wave
var sineWave = BrailleSeriesExtensions.CreateSineWave(
    frequency: 2.0, 
    amplitude: 1.0, 
    xMin: 0, 
    xMax: Math.PI * 2, 
    points: 200
);

// Create complex wave with multiple frequencies
var complexWave = BrailleSeriesExtensions.CreateComplexWave(
    xMin: 0, 
    xMax: 10, 
    points: 300
);

// Create custom function
var customSeries = BrailleSeriesExtensions.CreateFromFunction(
    x => Math.Exp(-x/5) * Math.Cos(x), 
    xMin: 0, 
    xMax: 20, 
    points: 400,
    name: "Damped Oscillation"
);
```

## Key Features

### Sub-Character Precision
- **2× horizontal resolution** (2 pixels per character width)
- **4× vertical resolution** (4 pixels per character height)
- **Smooth diagonal lines** without jagged edges

### Performance Optimized
- Efficient Bresenham line algorithm
- Minimal memory allocation during rendering
- Fast braille character lookup using bit operations

### Terminal Compatible
- Works with any Unicode-capable terminal
- No external dependencies beyond Terminal.Gui
- Maintains text-based nature for compatibility

## Comparison with Traditional Terminal Graphics

### Before (Character-based)
```
*
 *
  *
   *    <- Jagged, low resolution
    *
```

### After (Braille-based)
```
⡠⠤⡀
    ⠉⠢⡀  <- Smooth, high resolution
       ⠑⢄
```

## Integration with KustoTerminal

This braille chart implementation can be integrated into query result visualization:

```csharp
// Example: Visualize numeric query results as smooth line charts
var queryResults = GetTimeSeriesData(); // Your Kusto query results
var series = new BrailleSeries("Query Results");

foreach (var row in queryResults.Rows)
{
    var timestamp = Convert.ToDouble(row["timestamp"]);
    var value = Convert.ToDouble(row["value"]);
    series.AddPoint(timestamp, value);
}

// Render in results pane
var canvas = new BrailleCanvas(resultsPaneWidth * 2, resultsPaneHeight * 4);
series.RenderToCanvas(canvas, /* appropriate ranges */);
```

## Technical Benefits

1. **Higher Visual Fidelity**: 8× effective resolution increase over character-based graphics
2. **Smooth Lines**: Eliminates jagged appearance of traditional terminal charts
3. **Compatibility**: Works in any terminal that supports Unicode
4. **Performance**: Optimized algorithms for real-time rendering
5. **Extensibility**: Easy to add new chart types and data sources

This implementation brings the smooth line rendering capabilities of `termdash` to the .NET/Terminal.Gui ecosystem, enabling high-quality data visualization in terminal applications.
