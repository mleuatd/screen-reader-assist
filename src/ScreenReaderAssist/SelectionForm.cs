using System.Drawing;
using System.Drawing.Drawing2D;
namespace ScreenReaderAssist;

public sealed class SelectionForm : Form
{
    private readonly Bitmap _screenshot;
    private Rectangle _selection;
    private Point _start;
    private bool _dragging;
    private readonly bool _balloonMode;
    private readonly Rectangle _desktop;
    private readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 850 };
    private Point _hoverLocal;
    private Point _lastMouseLocal = new(-1000,-1000);
    public int HoverVersion { get; private set; }
    public bool HoverEnabled { get; private set; } = true;
    public event Action<Point,int>? HoverRequested;
    public Point? ChosenTapScreenPoint { get; private set; }
    public Rectangle? ChosenScreenBounds { get; private set; }
    public Point? ClickScreenPoint { get; private set; }
    public Rectangle DesktopBounds => _desktop;

    public SelectionForm(bool balloonMode = false)
    {
        _balloonMode = balloonMode;
        _desktop = ScreenCapture.VirtualBounds;
        _screenshot = ScreenCapture.Capture(_desktop);
        _hoverTimer.Tick += (_,_) => {
            _hoverTimer.Stop();
            if (!_balloonMode && !_dragging && HoverEnabled && Visible)
                HoverRequested?.Invoke(PointToScreen(_hoverLocal),HoverVersion);
        };
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = _desktop;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = 1;
        AccessibleName = balloonMode ? "漫画の吹き出しをクリック" : "読み取りモード。カーソルを約1秒止めるとその文字を読み上げ。タップで文字を読み、ドラッグで範囲選択。Hで自動読み上げの切替。Escapeで取消";
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate(); Focus();
        var pos = PointToClient(Cursor.Position);
        _selection = SelectionGeometry.AroundTap(pos,ClientRectangle);
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_screenshot, Point.Empty);
        if (!_balloonMode && _selection.Width > 0 && _selection.Height > 0) {
            using var brush = new SolidBrush(Color.FromArgb(55, Color.Yellow));
            using var pen = new Pen(Color.Yellow, 3);
            e.Graphics.FillRectangle(brush, _selection); e.Graphics.DrawRectangle(pen, _selection);
        }
        using var bg = new SolidBrush(Color.FromArgb(215, Color.Black));
        e.Graphics.FillRectangle(bg, 5, 5, Math.Min(Width-10, 820), 38);
        TextRenderer.DrawText(e.Graphics, _balloonMode ? "漫画: 吹き出しの内部をクリック / Esc: 中止" : $"カーソル静止: 自動読み上げ {(HoverEnabled ? "ON" : "OFF")} / H: 切替 / タップ: 読む / ドラッグ: 範囲 / Esc: 終了", Font, new Point(15,12), Color.White);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _hoverTimer.Stop(); HoverVersion++;
        if (_balloonMode) { ClickScreenPoint = PointToScreen(e.Location); DialogResult = DialogResult.OK; Close(); return; }
        _start = e.Location; _selection = Rectangle.Empty; _dragging = true; Capture = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) {
            if (_balloonMode || !HoverEnabled || !ClientRectangle.Contains(e.Location)) return;
            if (Math.Abs(e.X-_lastMouseLocal.X)<9 && Math.Abs(e.Y-_lastMouseLocal.Y)<9) return;
            _lastMouseLocal=e.Location;
            _hoverLocal=e.Location;
            HoverVersion++;
            _hoverTimer.Stop();
            _hoverTimer.Start();
            return;
        }
        _selection = SelectionGeometry.FromDrag(_start,e.Location,ClientRectangle);
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging || e.Button != MouseButtons.Left) return;
        _dragging = false; Capture = false;
        _selection = SelectionGeometry.FromDrag(_start,e.Location,ClientRectangle);
        // A single touch / click selects a useful region around that point.
        if (_selection.Width < 8 || _selection.Height < 8) {
            ChosenTapScreenPoint = PointToScreen(e.Location);
            _selection = SelectionGeometry.AroundTap(e.Location,ClientRectangle,440,180);
        }
        Accept();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { _hoverTimer.Stop(); HoverVersion++; DialogResult = DialogResult.Cancel; Close(); return true; }
        if (keyData == Keys.H && !_balloonMode) {
            HoverEnabled=!HoverEnabled; HoverVersion++; _hoverTimer.Stop(); Invalidate(); return true;
        }
        if (keyData == Keys.Enter && !_balloonMode) { Accept(); return true; }
        if (_balloonMode) return base.ProcessCmdKey(ref msg, keyData);
        int dx = 0, dy = 0;
        switch (keyData & Keys.KeyCode) {
            case Keys.Left: dx = -10; break; case Keys.Right: dx = 10; break;
            case Keys.Up: dy = -10; break; case Keys.Down: dy = 10; break;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
        if ((keyData & Keys.Shift) != 0) {
            int w = Math.Max(20, Math.Min(Width - _selection.Left, _selection.Width + dx));
            int h = Math.Max(20, Math.Min(Height - _selection.Top, _selection.Height + dy));
            _selection = new Rectangle(_selection.Left, _selection.Top, w, h);
        } else {
            _selection = new Rectangle(Math.Clamp(_selection.X + dx, 0, Math.Max(0, Width - _selection.Width)), Math.Clamp(_selection.Y + dy, 0, Math.Max(0, Height - _selection.Height)), _selection.Width, _selection.Height);
        }
        Invalidate(); return true;
    }
    private void Accept()
    {
        if (_selection.Width < 8 || _selection.Height < 8) return;
        ChosenScreenBounds = SelectionGeometry.ToScreen(_selection,_desktop);
        DialogResult = DialogResult.OK; Close();
    }
    public Bitmap CopyScreenshot() => (Bitmap)_screenshot.Clone();
    public Bitmap CopyLocalRectangle(Rectangle local)
    {
        var clipped=Rectangle.Intersect(local,new Rectangle(Point.Empty,_screenshot.Size));
        if (clipped.Width<1 || clipped.Height<1) throw new ArgumentOutOfRangeException(nameof(local));
        return _screenshot.Clone(clipped,_screenshot.PixelFormat);
    }
    public Bitmap CopyChosenBitmap()
    {
        if (ChosenScreenBounds is not Rectangle chosen) throw new InvalidOperationException("範囲が選択されていません。");
        var local=new Rectangle(chosen.X-_desktop.X,chosen.Y-_desktop.Y,chosen.Width,chosen.Height);
        return _screenshot.Clone(local,_screenshot.PixelFormat);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hoverTimer.Stop(); _hoverTimer.Dispose(); _screenshot.Dispose(); }
        base.Dispose(disposing);
    }
}
