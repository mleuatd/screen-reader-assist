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
    public Rectangle? ChosenScreenBounds { get; private set; }
    public Point? ClickScreenPoint { get; private set; }

    public SelectionForm(bool balloonMode = false)
    {
        _balloonMode = balloonMode;
        _desktop = ScreenCapture.VirtualBounds;
        _screenshot = ScreenCapture.Capture(_desktop);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = _desktop;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = .98;
        AccessibleName = balloonMode ? "漫画の吹き出しをクリック" : "読み取る範囲をドラッグ。矢印キーで移動、Shift+矢印でサイズ調整、Enterで確定、Escapeで取消";
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate(); Focus();
        var pos = PointToClient(Cursor.Position);
        _selection = new Rectangle(Math.Clamp(pos.X-200, 0, Math.Max(0, Width-1)), Math.Clamp(pos.Y-80, 0, Math.Max(0, Height-1)), 320, 160);
        _selection = Rectangle.Intersect(_selection, ClientRectangle);
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
        TextRenderer.DrawText(e.Graphics, _balloonMode ? "漫画: 吹き出しの内部をクリック / Esc: 中止" : "ドラッグ: 選択 / 矢印: 移動 / Shift+矢印: 拡大縮小 / Enter: 決定 / Esc: 中止", Font, new Point(15,12), Color.White);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (_balloonMode) { ClickScreenPoint = PointToScreen(e.Location); DialogResult = DialogResult.OK; Close(); return; }
        _start = e.Location; _selection = Rectangle.Empty; _dragging = true; Capture = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        var x = Math.Min(_start.X, e.X); var y = Math.Min(_start.Y, e.Y);
        _selection = Rectangle.Intersect(Rectangle.FromLTRB(x, y, Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y)), ClientRectangle);
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging || e.Button != MouseButtons.Left) return;
        _dragging = false; Capture = false;
        if (_selection.Width >= 8 && _selection.Height >= 8) Accept();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
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
        ChosenScreenBounds = new Rectangle(_selection.X + _desktop.X, _selection.Y + _desktop.Y, _selection.Width, _selection.Height);
        DialogResult = DialogResult.OK; Close();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _screenshot.Dispose();
        base.Dispose(disposing);
    }
}
