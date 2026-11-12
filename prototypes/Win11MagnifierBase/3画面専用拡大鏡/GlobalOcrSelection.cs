// v0.5.1 prototype: global Alt+left / Alt+right drag for text selection on any desktop.
// Low-level hook only suppresses mouse events DURING this explicitly requested gesture.
// No input injection, administrator privileges, cloud OCR, telemetry or clipboard use.
// Japanese Windows.Media.Ocr runs in a separate Windows PowerShell 5.1 process so
// the magnifier UI remains responsive and Windows OCR failures stay isolated.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;

namespace Win11Magnifier
{
    internal sealed class GlobalOcrSelection : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int VK_MENU = 0x12;
        private const int VK_CONTROL = 0x11;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_ESCAPE = 0x1B;
        private const int MIN_DRAG = 14;
        private readonly DedicatedViewer owner;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly SelectionOutline outline;
        private MouseHookProc callback; // pinned for lifetime of native hook
        private IntPtr hook = IntPtr.Zero;
        private bool disposed;
        private bool enabled = true;
        private bool selecting;
        private int selectionButtonKey;
        private int selectionButtonUpMessage;
        private bool busy;
        private bool outlineVisible;
        private Point from;
        private Point to;
        private bool completedSelection;
        private Rectangle pendingRectangle;
        private Point pendingClick;
        private System.Windows.Forms.Timer captureDelay;

        [StructLayout(LayoutKind.Sequential)]
        private struct LowLevelPoint { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LowLevelMouseInfo
        {
            public LowLevelPoint pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private delegate IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, MouseHookProc fn, IntPtr hmod, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr info);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        internal bool Enabled
        {
            get { return enabled; }
            set
            {
                enabled = value;
                if (!enabled) CancelSelection();
            }
        }

        internal GlobalOcrSelection(DedicatedViewer viewer)
        {
            owner = viewer;
            outline = new SelectionOutline();
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 35;
            refreshTimer.Tick += delegate { RefreshOutline(); };
        }

        internal void Start()
        {
            if (disposed || hook != IntPtr.Zero) return;
            callback = OnMouse;
            // WH_MOUSE_LL is invoked in this process on the installing UI thread;
            // unlike injected global hooks, no DLL is copied into target processes.
            IntPtr module = GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
            hook = SetWindowsHookEx(WH_MOUSE_LL, callback, module, 0);
            if (hook == IntPtr.Zero)
                throw new InvalidOperationException("Alt＋ドラッグ用の入力監視を開始できません (" +
                    Marshal.GetLastWin32Error().ToString() + ")");
            refreshTimer.Start();
        }

        private static bool Pressed(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }

        private IntPtr OnMouse(int code, IntPtr message, IntPtr info)
        {
            if (code < 0 || disposed) return CallNextHookEx(hook, code, message, info);
            try
            {
                int msg = message.ToInt32();
                if ((msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN) &&
                    !selecting && enabled && !busy &&
                    Pressed(VK_MENU) && !Pressed(VK_CONTROL) &&
                    !Pressed(VK_LWIN) && !Pressed(VK_RWIN))
                {
                    LowLevelMouseInfo p = (LowLevelMouseInfo)Marshal.PtrToStructure(info, typeof(LowLevelMouseInfo));
                    from = new Point(p.pt.x, p.pt.y);
                    to = from;
                    selectionButtonKey = msg == WM_RBUTTONDOWN ? VK_RBUTTON : VK_LBUTTON;
                    selectionButtonUpMessage = msg == WM_RBUTTONDOWN ? WM_RBUTTONUP : WM_LBUTTONUP;
                    selecting = true;
                    outlineVisible = false;
                    // Suppress only gestures started as Alt+left or Alt+right mouse down.
                    // The target app must not receive a stray click/context menu.
                    return new IntPtr(1);
                }
                if (selecting && (msg == WM_MOUSEMOVE || msg == selectionButtonUpMessage))
                {
                    LowLevelMouseInfo p = (LowLevelMouseInfo)Marshal.PtrToStructure(info, typeof(LowLevelMouseInfo));
                    to = new Point(p.pt.x, p.pt.y);
                    if (msg == selectionButtonUpMessage)
                    {
                        selecting = false;
                        selectionButtonKey = 0;
                        selectionButtonUpMessage = 0;
                        completedSelection = true;
                        pendingClick = from;
                        pendingRectangle = MakeRectangle(from, to);
                        // Paint must disappear before screen capture.
                        if (outline.Visible) outline.Hide();
                        outlineVisible = false;
                        if (owner.IsHandleCreated)
                            owner.BeginInvoke((MethodInvoker)delegate { ScheduleCapture(); });
                    }
                    return new IntPtr(1); // avoid dragging objects in the source app
                }
            }
            catch (Exception)
            {
                // Never let a managed exception escape into the Windows mouse hook.
                CancelSelection();
            }
            return CallNextHookEx(hook, code, message, info);
        }

        private static Rectangle MakeRectangle(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        private void RefreshOutline()
        {
            if (disposed) return;
            if (selecting && (!Pressed(selectionButtonKey) || Pressed(VK_ESCAPE)))
            {
                CancelSelection(); // Missed mouse-up or user requested cancel.
                return;
            }
            if (!selecting) return;
            Rectangle selected = MakeRectangle(from, to);
            if (selected.Width < 7 && selected.Height < 7) return;
            outline.Selection = selected;
            if (!outlineVisible)
            {
                outline.Bounds = SystemInformation.VirtualScreen;
                outline.Show();
                outlineVisible = true;
            }
            outline.Invalidate();
        }

        private void CancelSelection()
        {
            selecting = false;
            selectionButtonKey = 0;
            selectionButtonUpMessage = 0;
            completedSelection = false;
            if (outline.Visible) outline.Hide();
            outlineVisible = false;
        }

        private void ScheduleCapture()
        {
            if (disposed || !enabled || !completedSelection || busy) return;
            completedSelection = false;
            if (captureDelay != null) { captureDelay.Stop(); captureDelay.Dispose(); }
            captureDelay = new System.Windows.Forms.Timer();
            captureDelay.Interval = 90; // Give DWM time to remove selection border.
            captureDelay.Tick += delegate
            {
                captureDelay.Stop(); captureDelay.Dispose(); captureDelay = null;
                StartRecognition(pendingRectangle, pendingClick);
            };
            captureDelay.Start();
        }

        private void StartRecognition(Rectangle selection, Point point)
        {
            if (disposed || busy) return;
            bool isRegion = selection.Width >= MIN_DRAG || selection.Height >= MIN_DRAG;
            Rectangle clip = isRegion ? selection : ShortClickArea(point);
            clip = Rectangle.Intersect(clip, SystemInformation.VirtualScreen);
            if (clip.Width < 1 || clip.Height < 1) return;
            busy = true;
            owner.ReportOcrStatus(isRegion ? "選択範囲の文字を認識しています…" : "クリック位置の文章を探しています…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string text = "";
                string error = "";
                try
                {
                    if (!isRegion) text = TryReadAccessibleText(point);
                    if (String.IsNullOrWhiteSpace(text)) text = RunLocalOcr(clip);
                }
                catch (Exception ex) { error = ex.Message; }
                try
                {
                    if (!disposed && owner.IsHandleCreated && !owner.IsDisposed)
                        owner.BeginInvoke((MethodInvoker)delegate
                        {
                            busy = false;
                            if (disposed || owner.IsDisposed) return;
                            if (error.Length > 0) owner.ReportOcrStatus("OCR処理のエラー: " + error);
                            else owner.SpeakRecognizedText(text);
                        });
                }
                catch (InvalidOperationException) { busy = false; }
            });
        }

        private static Rectangle ShortClickArea(Point at)
        {
            Rectangle b = Screen.FromPoint(at).Bounds;
            int w = Math.Min(740, b.Width);
            int h = Math.Min(155, b.Height);
            int x = Math.Max(b.Left, Math.Min(at.X - 180, b.Right - w));
            int y = Math.Max(b.Top, Math.Min(at.Y - h / 2, b.Bottom - h));
            return new Rectangle(x, y, w, h);
        }

        private static string TryReadAccessibleText(Point at)
        {
            // Read ordinary controls using UI Automation before screen OCR.
            // Many web views, PDF viewers and images expose no text => OCR fallback.
            try
            {
                AutomationElement el = AutomationElement.FromPoint(new System.Windows.Point(at.X, at.Y));
                for (int depth = 0; depth < 3 && el != null; depth++)
                {
                    object pattern;
                    if (el.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                    {
                        TextPattern text = pattern as TextPattern;
                        if (text != null)
                        {
                            TextPatternRange range = text.RangeFromPoint(new System.Windows.Point(at.X, at.Y));
                            range.ExpandToEnclosingUnit(TextUnit.Line);
                            string content = range.GetText(320).Trim();
                            if (content.Length > 0) return content;
                        }
                    }
                    if (el.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
                    {
                        ValuePattern value = pattern as ValuePattern;
                        if (value != null)
                        {
                            string content = (value.Current.Value ?? "").Trim();
                            if (content.Length > 0) return content.Length > 320 ? content.Substring(0, 320) : content;
                        }
                    }
                    el = TreeWalker.ControlViewWalker.GetParent(el);
                }
            }
            catch (Exception) { } // UIA can fail on protected/hung application
            return "";
        }

        private static string RunLocalOcr(Rectangle clip)
        {
            string dir = Path.Combine(Path.GetTempPath(), "Win11MagnifierOcr");
            Directory.CreateDirectory(dir);
            string id = Guid.NewGuid().ToString("N");
            string imageFile = Path.Combine(dir, id + ".png");
            string resultFile = Path.Combine(dir, id + ".txt");
            string errorFile = Path.Combine(dir, id + ".error.txt");
            try
            {
                // Reading the screen only after the selection overlay is hidden.
                using (Bitmap bmp = new Bitmap(clip.Width, clip.Height))
                {
                    using (Graphics graphics = Graphics.FromImage(bmp))
                        graphics.CopyFromScreen(clip.Left, clip.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    bmp.Save(imageFile, System.Drawing.Imaging.ImageFormat.Png);
                }
                string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RecognizeSelection.ps1");
                // Add-Type compiles source in the PowerShell host; the script lives
                // alongside the source, not necessarily the PowerShell CWD.
                if (!File.Exists(script))
                {
                    string known = Path.Combine(Environment.CurrentDirectory, "RecognizeSelection.ps1");
                    if (File.Exists(known)) script = known;
                }
                if (!File.Exists(script)) throw new IOException("RecognizeSelection.ps1 が見つかりません。");
                string systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string powershell = Path.Combine(systemFolder, @"WindowsPowerShell\v1.0\powershell.exe");
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = powershell;
                start.Arguments = "-NoLogo -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File " + Quote(script)
                    + " -ImagePath " + Quote(imageFile) + " -TextPath " + Quote(resultFile)
                    + " -ErrorPath " + Quote(errorFile);
                start.WorkingDirectory = Path.GetDirectoryName(script);
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(start))
                {
                    if (!p.WaitForExit(18000))
                    {
                        try { p.Kill(); } catch { }
                        throw new TimeoutException("OCRが18秒以内に完了しなかったため停止しました。");
                    }
                    if (p.ExitCode != 0)
                    {
                        string message = File.Exists(errorFile) ? File.ReadAllText(errorFile, Encoding.UTF8) : "OCRの実行に失敗しました。";
                        throw new InvalidOperationException(message.Trim());
                    }
                }
                return File.Exists(resultFile) ? File.ReadAllText(resultFile, Encoding.UTF8) : "";
            }
            finally
            {
                try { if (File.Exists(imageFile)) File.Delete(imageFile); } catch { }
                try { if (File.Exists(resultFile)) File.Delete(resultFile); } catch { }
                try { if (File.Exists(errorFile)) File.Delete(errorFile); } catch { }
            }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            if (captureDelay != null) { captureDelay.Stop(); captureDelay.Dispose(); captureDelay = null; }
            refreshTimer.Stop();
            refreshTimer.Dispose();
            outline.Dispose();
            callback = null;
        }
    }

    // Transparent, non-activating, click-through outline across the virtual desktop.
    // It never takes focus from the underlying app or the dedicated magnifier.
    internal sealed class SelectionOutline : Form
    {
        internal Rectangle Selection = Rectangle.Empty;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= 0x20 /*WS_EX_TRANSPARENT*/ | 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/;
                return p;
            }
        }
        internal SelectionOutline()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Bounds = SystemInformation.VirtualScreen;
            DoubleBuffered = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Selection.IsEmpty) return;
            Rectangle r = Selection;
            r.Offset(-Left, -Top);
            if (r.Width <= 1 || r.Height <= 1) return;
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using (Pen p = new Pen(Color.LimeGreen, 5))
            {
                p.Alignment = PenAlignment.Inset;
                e.Graphics.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            }
        }
    }
}
