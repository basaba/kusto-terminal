using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.Controls;

/// <summary>
/// A label with a sweeping shimmer light effect across static text.
/// </summary>
public class ShimmerLabel : View
{
    private string _message = "";
    private int _shimmerPos;
    private System.Threading.Timer? _timer;
    private bool _isAnimating;

    private readonly Color _baseColor = Color.DarkGray;
    private readonly Color _glowColor = Color.White;
    private readonly Color _midColor = Color.Gray;
    private const int GlowRadius = 6;

    public ShimmerLabel()
    {
        Height = 1;
        CanFocus = false;
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            SetNeedsDraw();
        }
    }

    public void StartShimmer()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _shimmerPos = -GlowRadius;
        _timer = new System.Threading.Timer(OnTick, null, 0, 100);
    }

    public void StopShimmer()
    {
        _isAnimating = false;
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTick(object? state)
    {
        Application.Invoke(() =>
        {
            if (!_isAnimating || !Visible) return;
            _shimmerPos++;
            if (_shimmerPos > _message.Length + GlowRadius)
                _shimmerPos = -GlowRadius;
            SetNeedsDraw();
        });
        (Application.Driver as KustoTerminal.Driver.KustoConsoleDriver)?.Wakeup();
    }

    protected override bool OnDrawingContent()
    {
        var driver = Application.Driver;
        if (driver == null) return true;

        var viewport = Viewport;
        var bg = GetScheme().Normal.Background;

        for (int i = 0; i < viewport.Width; i++)
        {
            Move(i, 0);
            if (i < _message.Length)
            {
                var dist = Math.Abs(i - _shimmerPos);
                Color fg;
                if (dist == 0)
                    fg = _glowColor;
                else if (dist <= 1)
                    fg = _midColor;
                else if (dist <= GlowRadius)
                    fg = _midColor;
                else
                    fg = _baseColor;

                driver.SetAttribute(new Attribute(fg, bg));
                driver.AddStr(_message[i].ToString());
            }
            else
            {
                driver.SetAttribute(new Attribute(_baseColor, bg));
                driver.AddStr(" ");
            }
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
            _timer = null;
        }
        base.Dispose(disposing);
    }
}
