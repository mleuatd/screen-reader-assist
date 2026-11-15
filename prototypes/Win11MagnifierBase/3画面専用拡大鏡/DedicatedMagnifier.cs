// Dedicated Magnifier proof of concept for Windows 11 (C# 5 / .NET Framework 4.x).
// Hosted by Windows PowerShell 5.1. No mouse hooks, key injection, admin or drivers.
// Uses the documented Microsoft Magnification API. Windows 11 physical-machine test pending.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Win11Magnifier
{
    // Same location and file format as ScreenReaderAssist.SharedSpeechRate.
    // Data is shared through a user-local file only; no network communication.
    internal static class SharedVoiceRate
    {
        internal static string PathName
        {
            get {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenReaderAssist", "voice-rate.shared");
            }
        }
        internal static bool TryRead(out int rate)
        {
            rate=0;
            try
            {
                int value;
                if(!int.TryParse(File.ReadAllText(PathName,Encoding.ASCII).Trim(),
                    NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)
                   || value < -10 || value > 10)return false;
                rate=value;
                return true;
            }
            catch(IOException) {return false;}
            catch(UnauthorizedAccessException) {return false;}
        }
        internal static bool Publish(int value)
        {
            string destination=PathName;
            string temp=null;
            try
            {
                string folder=Path.GetDirectoryName(destination);
                if(!Directory.Exists(folder))Directory.CreateDirectory(folder);
                temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllText(temp,Math.Max(-10,Math.Min(10,value)).ToString(CultureInfo.InvariantCulture),Encoding.ASCII);
                if(File.Exists(destination))
                    File.Replace(temp,destination,null);
                else File.Move(temp,destination);
                return true;
            }
            catch(IOException) {return false;}
            catch(UnauthorizedAccessException) {return false;}
            finally {if(temp!=null){try{File.Delete(temp);}catch(IOException){}}}
        }
    }

    internal static class WinMagApi
    {
        internal const int WS_EX_LAYERED = 0x00080000;
        internal const int LWA_ALPHA = 0x00000002;
        internal const int WS_CHILD = 0x40000000;
        internal const int WS_VISIBLE = 0x10000000;
        internal const int MS_SHOWMAGNIFIEDCURSOR = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        internal struct MagRect
        {
            public int left, top, right, bottom;
            public MagRect(int l, int t, int r, int b)
            { left = l; top = t; right = r; bottom = b; }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MagTransform
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
            public float[] matrix;
            public static MagTransform Scale(float z)
            {
                return new MagTransform { matrix = new float[] {
                    z, 0f, 0f,
                    0f, z, 0f,
                    0f, 0f, 1f
                } };
            }
        }

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagInitialize();

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagUninitialize();

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagSetWindowTransform(IntPtr hwnd, ref MagTransform transform);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagSetWindowSource(IntPtr hwnd, MagRect rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            int exStyle, string className, string windowName, int style,
            int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        // Captures input on our magnifier child only while the left button is held.
        // There is no system-wide mouse hook or input injection.
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetCapture(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetCapture();

        [DllImport("user32.dll")]
        internal static extern IntPtr SetCursor(IntPtr cursorHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        internal static string LastErrorMessage(string operation)
        {
            return operation + " (Win32 error: " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ")";
        }
    }

    public static class DedicatedMagnifierEntry
    {
        public static void Start()
        {
            // Must be called before creating any WinForms windows. It can fail if the
            // PowerShell host already set the process DPI mode; carry on in that case.
            try { WinMagApi.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException) { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!WinMagApi.MagInitialize())
                throw new InvalidOperationException(WinMagApi.LastErrorMessage("Windowsの拡大APIの初期化に失敗しました"));

            try
            {
                using (DedicatedViewer window = new DedicatedViewer())
                    Application.Run(window);
            }
            finally
            {
                WinMagApi.MagUninitialize();
            }
        }
    }

    internal sealed class DisplayChoice
    {
        public Screen Screen;
        public string Label;
        public override string ToString() { return Label; }
    }

    // Non-activating, mouse-click-through visual indicator shown over the
    // magnified content. It never changes the Windows desktop zoom.
    internal sealed class GestureHud : Form
    {
        private string title = "";
        private string details = "";
        private double ratio;
        private readonly DedicatedViewer viewer;

        internal GestureHud(DedicatedViewer owner)
        {
            viewer = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(12, 21, 38);
            ForeColor = Color.White;
            Opacity = 0.95;
            Width = 550;
            Height = 162;
            DoubleBuffered = true;
            AccessibleName = "拡大倍率または読み上げ速度を確認する中央表示";
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // No activation, click-through and no entry in Alt+Tab.
                cp.ExStyle |= 0x08000000 | 0x00000020 | 0x00000080;
                return cp;
            }
        }

        internal void Display(string caption, string value, double fraction, Rectangle screenArea)
        {
            title = caption;
            details = value;
            ratio = Math.Max(0, Math.Min(1, fraction));
            int w = Math.Min(550, Math.Max(280, screenArea.Width - 32));
            int h = Math.Min(162, Math.Max(120, screenArea.Height - 20));
            Bounds = new Rectangle(
                screenArea.Left + (screenArea.Width - w) / 2,
                screenArea.Top + (screenArea.Height - h) / 2, w, h);
            if (!Visible) Show(viewer);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(12, 21, 38));
            using (Pen edge = new Pen(Color.FromArgb(119, 241, 164), 3))
                g.DrawRectangle(edge, 2, 2, Width - 5, Height - 5);
            using (Font headingFont = new Font("Yu Gothic UI", 15f, FontStyle.Bold))
                TextRenderer.DrawText(g, title, headingFont,
                    new Rectangle(18, 12, Width - 36, 36), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Font numberFont = new Font("Yu Gothic UI", 18f, FontStyle.Bold))
                TextRenderer.DrawText(g, details, numberFont,
                    new Rectangle(18, 52, Width - 36, 38), Color.FromArgb(180, 255, 192),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int left = 32, barWidth = Math.Max(80, Width - 64), top = Height - 39;
            using (Brush bg = new SolidBrush(Color.FromArgb(66, 85, 106)))
                g.FillRectangle(bg, left, top, barWidth, 15);
            using (Brush active = new SolidBrush(Color.FromArgb(80, 232, 143)))
                g.FillRectangle(active, left, top, Math.Max(1, (int)(ratio * barWidth)), 15);
            int knob = left + (int)(ratio * barWidth);
            using (Brush indicator = new SolidBrush(Color.White))
                g.FillEllipse(indicator, knob - 10, top - 5, 20, 25);
        }
    }

    // Native magnifier windows may not deliver ordinary mouse messages.
    // The already-installed WH_MOUSE_LL listener forwards clicks inside the
    // preview to this controller. Native-window messages remain as a fallback.
    internal sealed class PreviewDragInput : NativeWindow
    {
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_SETCURSOR = 0x0020;
        private const int HTCLIENT = 1;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_CAPTURECHANGED = 0x0215;
        private const int WM_CANCELMODE = 0x001F;
        private const int MK_LBUTTON = 0x0001;
        private const int PIXELS_PER_STEP = 24;
        private const float ZOOM_PER_STEP = 0.10f;
        private const int MAX_CLICK_MOVEMENT = 16;

        private enum Gesture { None, Zoom, VoiceRate }
        private readonly DedicatedViewer viewer;
        private Gesture currentGesture = Gesture.None;
        private bool dragging;
        private bool hookOwned;
        private bool changed;
        private Point pressPoint;
        private float startZoom;
        private int startVoiceRate;
        private int appliedSteps;
        private bool firstClickCandidate;
        private Point firstClickPoint;
        private int pressTicks;
        private int firstClickPressedTicks;

        internal PreviewDragInput(DedicatedViewer owner, IntPtr childHwnd)
        {
            viewer = owner;
            AssignHandle(childHwnd);
        }

        private bool IsSecondClick(Point position)
        {
            if (!firstClickCandidate) return false;
            int elapsed = unchecked(Environment.TickCount - firstClickPressedTicks);
            return MagnifierGestureMath.IsSecondClick(elapsed,
                position.X - firstClickPoint.X, position.Y - firstClickPoint.Y,
                SystemInformation.DoubleClickTime, SystemInformation.DoubleClickSize);
        }

        internal bool IsHookGestureActive { get { return dragging && hookOwned; } }

        // Called only after a left down inside the native magnifier preview.
        internal bool StartFromHook(Point at)
        {
            if (dragging || !viewer.CanStartPreviewGesture(at)) return false;
            BeginGesture(at, true);
            return true;
        }

        internal bool MoveFromHook(Point at)
        {
            if (!IsHookGestureActive) return false;
            ApplyDragPosition(at);
            return true;
        }

        internal bool EndFromHook(Point at)
        {
            if (!IsHookGestureActive) return false;
            ApplyDragPosition(at);
            FinishGesture(true);
            return true;
        }

        private void BeginGesture(Point at, bool fromHook)
        {
            viewer.ActivateViewerFromPreview();
            bool second = IsSecondClick(at);
            firstClickCandidate = false;
            currentGesture = second && viewer.DragVoiceEnabled
                ? Gesture.VoiceRate
                : (viewer.DragZoomEnabled ? Gesture.Zoom : Gesture.None);
            pressPoint = at;
            pressTicks = Environment.TickCount;
            appliedSteps = 0;
            changed = false;
            startZoom = viewer.DragStartZoom;
            startVoiceRate = viewer.DragStartVoiceRate;
            hookOwned = fromHook;
            dragging = true;
            if (!fromHook) WinMagApi.SetCapture(Handle);
            if (currentGesture == Gesture.VoiceRate)
                viewer.BeginGestureVoicePreview();
        }

        protected override void WndProc(ref Message m)
        {
            // If the low-level hook owns a gesture, these messages are duplicates.
            if (hookOwned && (m.Msg == WM_MOUSEMOVE || m.Msg == WM_LBUTTONUP
                           || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK))
            {
                m.Result = IntPtr.Zero;
                return;
            }
            switch (m.Msg)
            {
                case WM_NCHITTEST:
                    m.Result = new IntPtr(HTCLIENT);
                    return;
                case WM_SETCURSOR:
                    if (viewer.DragZoomEnabled || viewer.DragVoiceEnabled)
                    {
                        WinMagApi.SetCursor(Cursors.SizeWE.Handle);
                        m.Result = new IntPtr(1);
                        return;
                    }
                    break;
                case WM_LBUTTONDOWN:
                case WM_LBUTTONDBLCLK:
                    if (!dragging)
                        BeginGesture(Cursor.Position, false);
                    m.Result = IntPtr.Zero;
                    return;
                case WM_MOUSEMOVE:
                    if (dragging)
                    {
                        if ((m.WParam.ToInt64() & MK_LBUTTON) == 0)
                            FinishGesture(false);
                        else
                            ApplyDragPosition(Cursor.Position);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                    break;
                case WM_LBUTTONUP:
                    if (dragging)
                    {
                        ApplyDragPosition(Cursor.Position);
                        FinishGesture(true);
                    }
                    m.Result = IntPtr.Zero;
                    return;
                case WM_CAPTURECHANGED:
                case WM_CANCELMODE:
                    if (dragging && !hookOwned) FinishGesture(false);
                    m.Result = IntPtr.Zero;
                    return;
            }
            base.WndProc(ref m);
        }

        private void ApplyDragPosition(Point at)
        {
            int steps = MagnifierGestureMath.HorizontalSteps(at.X - pressPoint.X);
            if (steps == appliedSteps) return;
            appliedSteps = steps;
            if (currentGesture == Gesture.Zoom)
            {
                if (viewer.ApplyPreviewDragZoom(startZoom + ZOOM_PER_STEP * steps))
                    changed = true;
            }
            else if (currentGesture == Gesture.VoiceRate)
            {
                if (viewer.ApplyPreviewDragVoiceRate(startVoiceRate + steps))
                    changed = true;
            }
        }

        private void FinishGesture(bool releasedNormally)
        {
            if (!dragging) return;
            Point at = Cursor.Position;
            bool clean = releasedNormally &&
                Math.Abs(at.X - pressPoint.X) <= MAX_CLICK_MOVEMENT &&
                Math.Abs(at.Y - pressPoint.Y) <= MAX_CLICK_MOVEMENT &&
                appliedSteps == 0 && !changed;
            Gesture finished = currentGesture;
            bool save = changed;
            dragging = false;
            changed = false;
            hookOwned = false;
            currentGesture = Gesture.None;
            if (WinMagApi.GetCapture() == Handle)
                WinMagApi.ReleaseCapture();
            firstClickCandidate = clean && finished != Gesture.VoiceRate;
            if (firstClickCandidate)
            {
                firstClickPoint = pressPoint;
                firstClickPressedTicks = pressTicks;
            }
            viewer.EndGesturePreview(finished == Gesture.VoiceRate);
            if (save) viewer.CommitPreviewDragGesture();
        }

        internal bool IsDragging { get { return dragging; } }

        internal void Detach()
        {
            FinishGesture(false);
            if (Handle != IntPtr.Zero) ReleaseHandle();
        }
    }

    internal static class MagnifierGestureMath
    {
        internal static int HorizontalSteps(int deltaPixels) { return deltaPixels / 24; }

        internal static bool IsSecondClick(int elapsedMs,int deltaX,int deltaY,int maxMilliseconds,Size doubleClickSize)
        {
            // A 3px system default is too strict for users with severely reduced
            // vision. Keep time bounded and allow modest positional uncertainty.
            int tolerance = Math.Max(18, Math.Max(doubleClickSize.Width,doubleClickSize.Height) / 2);
            return elapsedMs >= 0 && elapsedMs <= maxMilliseconds &&
                Math.Abs((long)deltaX) <= tolerance &&
                Math.Abs((long)deltaY) <= tolerance;
        }
    }

    internal sealed class DedicatedViewer : Form
    {
        private readonly Panel viewSpace;
        private readonly FlowLayoutPanel bar;
        private readonly ComboBox displaySelector;
        private readonly Button plusButton;
        private readonly Button minusButton;
        private readonly Button holdButton;
        private readonly Button maximizeButton;
        private readonly CheckBox dragToggle;
        private readonly CheckBox dragVoiceToggle;
        private readonly CheckBox globalOcrToggle;
        private readonly CheckBox altLeftToggle;
        private readonly FlowLayoutPanel speechBar;
        private readonly Panel speechInputBar;
        private readonly Label voiceRateLabel;
        private readonly Label voiceStatusLabel;
        private readonly TextBox speechText;
        private readonly Button speakButton;
        private readonly Button stopSpeakButton;
        private readonly Label zoomLabel;
        private readonly Label stateLabel;
        private readonly Timer followTimer;
        private readonly Timer voiceSyncTimer;
        private readonly Timer voicePreviewTimer;
        private readonly Timer hudHideTimer;
        private readonly GestureHud gestureHud;
        private Form largeControlsWindow;
        private bool voicePreviewActive;
        private const string VoicePreviewPhrase = "テスト再生";

        private IntPtr magHandle = IntPtr.Zero;
        private PreviewDragInput previewInput;
        private float zoom = 2.5f;
        private float appliedZoom = -1f;
        private bool frozen;
        private bool dragZoomEnabled = true;
        private bool dragVoiceEnabled = true;
        private int voiceRate = 0; // System.Speech: -10 (slow) .. +10 (fast).
        private SpeechSynthesizer speechSynth;
        private GlobalOcrSelection globalOcr;
        private bool globalOcrEnabled = true;
        private bool altLeftEnabled = false; // Alt+left often belongs to other apps
        private bool updatingDisplays;
        private bool closing;
        private string savedDevice = "";
        private WinMagApi.MagRect? lastSource;
        private Point? trackedPoint;
        private Rectangle trackedBounds = Rectangle.Empty;
        private string trackedDevice = "";
        private Rectangle lastView = Rectangle.Empty;
        private string lastState = "";
        private readonly string settingsPath;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WinMagApi.WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Required for Microsoft's native magnifier child window.
            if (!WinMagApi.SetLayeredWindowAttributes(Handle, 0, 255, WinMagApi.LWA_ALPHA))
                throw new InvalidOperationException(WinMagApi.LastErrorMessage("拡大用ウィンドウの作成に失敗しました"));
        }

        public DedicatedViewer()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            settingsPath = Path.Combine(appData, "Win11MagnifierBase", "third-monitor-settings.txt");
            LoadSettings();
            int syncedRate;
            if(SharedVoiceRate.TryRead(out syncedRate))voiceRate=syncedRate;
            else SharedVoiceRate.Publish(voiceRate);

            Text = "3画面専用拡大鏡 v0.5 — 1回ドラッグ：拡大／2回目ドラッグ：読み上げ速度";
            Font = new Font("Yu Gothic UI", 12f);
            BackColor = Color.FromArgb(17, 23, 35);
            ForeColor = Color.White;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            // Keep a real, resizable Windows caption/frame. Do not consume
            // Win+Arrow or Win+Z: Windows Snap handles these globally.
            FormBorderStyle = FormBorderStyle.Sizable;
            ControlBox = true;
            MinimizeBox = true;
            MaximizeBox = true;
            // Narrow enough for quarter layouts even at 1280x720, while allowing
            // the toolbars to scroll in a compact window.
            MinimumSize = new Size(400, 320);
            Size = new Size(1050, 750);
            KeyPreview = true;
            ShowInTaskbar = true;
            TopMost = false;

            bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 72;
            bar.Padding = new Padding(7, 7, 7, 7);
            bar.BackColor = Color.FromArgb(30, 42, 60);
            bar.AutoScroll = true;
            bar.WrapContents = false;

            displaySelector = new ComboBox();
            displaySelector.DropDownStyle = ComboBoxStyle.DropDownList;
            displaySelector.AccessibleName = "拡大映像を表示するモニター";
            displaySelector.Width = 220;
            displaySelector.Font = new Font("Yu Gothic UI", 11.5f);
            displaySelector.Margin = new Padding(3, 7, 8, 3);
            displaySelector.SelectedIndexChanged += delegate {
                if (updatingDisplays || !Visible) return;
                DisplayChoice choice = displaySelector.SelectedItem as DisplayChoice;
                if (choice == null) return;
                savedDevice = choice.Screen.DeviceName;
                SaveSettings();
                PutOnScreen(choice.Screen, true);
                lastSource = null;
            };
            bar.Controls.Add(displaySelector);

            minusButton = MakeButton("－", "倍率を下げる", 56);
            minusButton.Click += delegate { SetZoom(zoom >= 2f ? zoom - 0.5f : zoom - 0.25f); };
            bar.Controls.Add(minusButton);

            zoomLabel = new Label();
            zoomLabel.AutoSize = false;
            zoomLabel.Size = new Size(105, 46);
            zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            zoomLabel.Font = new Font("Yu Gothic UI", 14f, FontStyle.Bold);
            zoomLabel.AccessibleName = "現在の拡大倍率";
            bar.Controls.Add(zoomLabel);

            plusButton = MakeButton("＋", "倍率を上げる", 56);
            plusButton.Click += delegate { SetZoom(zoom < 2f ? zoom + 0.25f : zoom + 0.5f); };
            bar.Controls.Add(plusButton);

            holdButton = MakeButton("追従を停止", "マウス追従の一時停止と再開", 144);
            holdButton.Click += delegate { frozen = !frozen; holdButton.Text = frozen ? "追従を再開" : "追従を停止"; UpdateState(frozen ? "追従を停止中。表示位置は変わりません。" : "追従を再開しました。"); };
            bar.Controls.Add(holdButton);

            maximizeButton = MakeButton("最大化／復元", "現在のモニターで最大化または復元", 152);
            maximizeButton.Click += delegate {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            };
            bar.Controls.Add(maximizeButton);

            Button largeControlsButton = MakeButton("操作を大きく表示", "倍率と音声速度の大きい操作ボタンを開く", 196);
            largeControlsButton.Click += delegate { OpenLargeControlPanel(); };
            bar.Controls.Add(largeControlsButton);

            dragToggle = new CheckBox();
            dragToggle.Text = "1回ドラッグ：拡大";
            dragToggle.AccessibleName = "拡大映像上の左右ドラッグで倍率変更を有効にする";
            dragToggle.Checked = dragZoomEnabled;
            dragToggle.AutoSize = true;
            dragToggle.Font = new Font("Yu Gothic UI", 11.5f);
            dragToggle.ForeColor = Color.White;
            dragToggle.Margin = new Padding(12, 15, 4, 0);
            dragToggle.CheckedChanged += delegate {
                dragZoomEnabled = dragToggle.Checked;
                SaveSettings();
            };
            bar.Controls.Add(dragToggle);

            dragVoiceToggle = new CheckBox();
            dragVoiceToggle.Text = "2回目ドラッグ：音声";
            dragVoiceToggle.AccessibleName = "ダブルクリックの2回目を押し続けたまま左右にドラッグして読み上げ速度を調整";
            dragVoiceToggle.Checked = dragVoiceEnabled;
            dragVoiceToggle.AutoSize = true;
            dragVoiceToggle.Font = new Font("Yu Gothic UI", 11.5f);
            dragVoiceToggle.ForeColor = Color.White;
            dragVoiceToggle.Margin = new Padding(12, 15, 4, 0);
            dragVoiceToggle.CheckedChanged += delegate {
                dragVoiceEnabled = dragVoiceToggle.Checked;
                SaveSettings();
            };
            bar.Controls.Add(dragVoiceToggle);

            globalOcrToggle = new CheckBox();
            globalOcrToggle.Text = "Alt＋右ドラッグ：範囲読み上げ";
            globalOcrToggle.AccessibleName = "必要な時だけAltと右ドラッグによる範囲OCRを有効にします。他のアプリと競合する場合はオフにできます";
            globalOcrToggle.Checked = globalOcrEnabled;
            globalOcrToggle.AutoSize = true;
            globalOcrToggle.Font = new Font("Yu Gothic UI", 11.5f);
            globalOcrToggle.ForeColor = Color.White;
            globalOcrToggle.Margin = new Padding(12, 15, 4, 0);
            globalOcrToggle.CheckedChanged += delegate {
                globalOcrEnabled = globalOcrToggle.Checked;
                if (globalOcr != null) globalOcr.Enabled = globalOcrEnabled;
                SaveSettings();
            };
            bar.Controls.Add(globalOcrToggle);

            altLeftToggle = new CheckBox();
            altLeftToggle.Text = "Alt＋左も使用（競合注意・既定OFF）";
            altLeftToggle.AccessibleName = "他のアプリのAltと左クリック操作を妨げる恐れがあるため通常は無効。必要時だけ有効化";
            altLeftToggle.Checked = altLeftEnabled;
            altLeftToggle.AutoSize = true;
            altLeftToggle.Font = new Font("Meiryo", 11.5f, FontStyle.Bold);
            altLeftToggle.ForeColor = Color.White;
            altLeftToggle.Margin = new Padding(12, 15, 4, 0);
            altLeftToggle.CheckedChanged += delegate {
                altLeftEnabled = altLeftToggle.Checked;
                if (globalOcr != null) globalOcr.AllowAltLeft = altLeftEnabled;
                SaveSettings();
            };
            bar.Controls.Add(altLeftToggle);

            speechBar = new FlowLayoutPanel();
            speechBar.Dock = DockStyle.Bottom;
            speechBar.Height = 68;
            speechBar.Padding = new Padding(9, 7, 7, 4);
            speechBar.BackColor = Color.FromArgb(32, 49, 70);
            speechBar.WrapContents = false;
            speechBar.AutoScroll = true;

            Label voiceCaption = new Label();
            voiceCaption.Text = "読み上げ速度";
            voiceCaption.AccessibleName = "アプリ内読み上げ速度";
            voiceCaption.TextAlign = ContentAlignment.MiddleLeft;
            voiceCaption.Size = new Size(130, 45);
            voiceCaption.Font = new Font("Yu Gothic UI", 11.5f);
            speechBar.Controls.Add(voiceCaption);

            Button voiceMinus = MakeButton("遅く", "読み上げを1段階遅くする", 76);
            voiceMinus.Click += delegate { SetVoiceRate(voiceRate - 1); };
            speechBar.Controls.Add(voiceMinus);

            voiceRateLabel = new Label();
            voiceRateLabel.Size = new Size(108, 45);
            voiceRateLabel.TextAlign = ContentAlignment.MiddleCenter;
            voiceRateLabel.Font = new Font("Yu Gothic UI", 13f, FontStyle.Bold);
            voiceRateLabel.AccessibleName = "現在の読み上げ速度、マイナス10からプラス10";
            speechBar.Controls.Add(voiceRateLabel);

            Button voicePlus = MakeButton("速く", "読み上げを1段階速くする", 76);
            voicePlus.Click += delegate { SetVoiceRate(voiceRate + 1); };
            speechBar.Controls.Add(voicePlus);

            voiceStatusLabel = new Label();
            voiceStatusLabel.Size = new Size(385, 45);
            voiceStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            voiceStatusLabel.Font = new Font("Yu Gothic UI", 10.5f);
            voiceStatusLabel.Text = "日本語読み上げ機能を確認しています…";
            voiceStatusLabel.AccessibleName = "日本語音声の利用可否";
            speechBar.Controls.Add(voiceStatusLabel);

            speechInputBar = new Panel();
            speechInputBar.Dock = DockStyle.Bottom;
            speechInputBar.Height = 58;
            speechInputBar.Padding = new Padding(11, 6, 11, 6);
            speechInputBar.BackColor = Color.FromArgb(32, 49, 70);
            speechText = new TextBox();
            speechText.Dock = DockStyle.Fill;
            speechText.Font = new Font("Yu Gothic UI", 12f);
            speechText.Text = "読み上げの速度を確認します。左右に動かして速さを調整できます。";
            speechText.MaxLength = 400;
            speechText.AccessibleName = "読み上げ確認用の文章。自由に変更できます";
            stopSpeakButton = MakeButton("停止", "音声読み上げを停止", 90);
            stopSpeakButton.Dock = DockStyle.Right;
            stopSpeakButton.Click += delegate { StopSpeaking(); };
            speakButton = MakeButton("文章を読む", "入力欄の文章を日本語で読み上げる", 145);
            speakButton.Dock = DockStyle.Right;
            speakButton.Click += delegate { SpeakInputText(); };
            speechInputBar.Controls.Add(speechText);
            speechInputBar.Controls.Add(stopSpeakButton);
            speechInputBar.Controls.Add(speakButton);
            speakButton.Enabled = false;
            stopSpeakButton.Enabled = false;

            stateLabel = new Label();
            stateLabel.Dock = DockStyle.Bottom;
            stateLabel.Height = 51;
            stateLabel.Padding = new Padding(14, 10, 10, 8);
            stateLabel.Font = new Font("Yu Gothic UI", 11.5f);
            stateLabel.ForeColor = Color.FromArgb(217, 235, 255);
            stateLabel.BackColor = Color.FromArgb(25, 34, 48);
            stateLabel.Text = "準備中…";
            stateLabel.AccessibleName = "拡大鏡の状態";

            viewSpace = new Panel();
            viewSpace.Dock = DockStyle.Fill;
            viewSpace.BackColor = Color.Black;
            viewSpace.AccessibleName = "拡大画面。1回押したまま左右へドラッグで倍率変更。素早く2回クリックし2回目を押し続けて左右へドラッグで読み上げ速度変更。元画面は操作されません";
            viewSpace.Resize += delegate { ResizeMagnifier(); };

            Controls.Add(viewSpace);
            Controls.Add(stateLabel);
            Controls.Add(speechBar);
            Controls.Add(speechInputBar);
            Controls.Add(bar);

            gestureHud = new GestureHud(this);
            voicePreviewTimer = new Timer();
            voicePreviewTimer.Interval = 200;
            voicePreviewTimer.Tick += delegate { ContinueVoicePreview(); };
            hudHideTimer = new Timer();
            hudHideTimer.Interval = 650;
            hudHideTimer.Tick += delegate {
                hudHideTimer.Stop();
                if (!gestureHud.IsDisposed) gestureHud.Hide();
            };

            zoomLabel.Text = zoom.ToString("0.00", CultureInfo.InvariantCulture) + "倍";
            UpdateVoiceRateLabel();

            followTimer = new Timer();
            followTimer.Interval = 40; // 25 checks/sec, only update source on change.
            followTimer.Tick += delegate { TickFollow(); };
            voiceSyncTimer = new Timer();
            voiceSyncTimer.Interval = 350;
            voiceSyncTimer.Tick += delegate {
                int incoming;
                if(SharedVoiceRate.TryRead(out incoming) && incoming != voiceRate)
                    SetVoiceRate(incoming,false);
            };

            Shown += delegate { StartViewer(); };
            FormClosing += delegate { ShutdownViewer(); };
            SizeChanged += delegate { ResizeMagnifier(); };
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private Button MakeButton(string caption, string accessibleName, int width)
        {
            Button b = new Button();
            b.Text = caption;
            b.AccessibleName = accessibleName;
            b.Font = new Font("Meiryo", 11.5f, FontStyle.Bold);
            b.Size = new Size(width, 47);
            b.Margin = new Padding(4, 3, 4, 3);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(150, 185, 225);
            StyleAccessibleButton(b);
            return b;
        }

        // Pressed = vivid yellow/black; keyboard focus = thick yellow outline.
        // Use a Japanese bold face rather than artificially thick strokes that close kanji.
        private static void StyleAccessibleButton(Button button)
        {
            Color normal = Color.FromArgb(42, 64, 91);
            Color bright = Color.FromArgb(255, 224, 47);
            button.UseVisualStyleBackColor = false;
            button.BackColor = normal;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(180, 207, 242);
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 99, 136);
            button.FlatAppearance.MouseDownBackColor = bright;
            Action<bool> paint = delegate(bool pressed) {
                if (button.IsDisposed) return;
                button.BackColor = pressed ? bright : normal;
                button.ForeColor = pressed ? Color.Black : Color.White;
                button.FlatAppearance.BorderColor = pressed || button.Focused
                    ? bright : Color.FromArgb(180, 207, 242);
                button.FlatAppearance.BorderSize = pressed || button.Focused ? 4 : 2;
            };
            button.MouseDown += delegate(object sender, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) paint(true);
            };
            button.MouseUp += delegate { paint(false); };
            button.MouseLeave += delegate { paint(false); };
            button.GotFocus += delegate { paint(false); };
            button.LostFocus += delegate { paint(false); };
            button.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) paint(true);
            };
            button.KeyUp += delegate { paint(false); };
        }

        private void StartViewer()
        {
            try
            {
                InitializeJapaneseSpeech(); // Speech failure must not disable magnification.
                ReloadDisplays();
                if (displaySelector.SelectedItem == null)
                    throw new InvalidOperationException("利用できるモニターがありません。");

                // The native Magnifier child is placed directly in the layered Form,
                // NOT inside the unlayered placeholder Panel.
                Rectangle r = ViewRectangleInForm();
                magHandle = WinMagApi.CreateWindowEx(
                    0, "Magnifier", "MagnifierContent",
                    WinMagApi.WS_CHILD | WinMagApi.WS_VISIBLE | WinMagApi.MS_SHOWMAGNIFIEDCURSOR,
                    r.Left, r.Top, Math.Max(1, r.Width), Math.Max(1, r.Height),
                    Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (magHandle == IntPtr.Zero)
                    throw new InvalidOperationException(WinMagApi.LastErrorMessage("拡大描画領域を作成できませんでした"));

                // Native Magnifier is a separate child HWND, so ordinary Panel
                // MouseDown/MouseMove handlers would never receive its input.
                previewInput = new PreviewDragInput(this, magHandle);
                ApplyZoom(zoom);
                ResizeMagnifier();
                TickFollow();
                followTimer.Start();
                voiceSyncTimer.Start();
                // The optional global listener is independent of focus. A failure
                // to install it must never stop the magnifier or speech test UI.
                try
                {
                    globalOcr = new GlobalOcrSelection(this);
                    globalOcr.Enabled = globalOcrEnabled;
                    globalOcr.Start();
                }
                catch (Exception ocrEx)
                {
                    ReportOcrStatus("画面範囲読み上げを開始できません: " + ocrEx.Message);
                    if (globalOcr != null) { globalOcr.Dispose(); globalOcr = null; }
                }
            }
            catch (Exception ex) { FailAndStop(ex); }
        }

        private void ReloadDisplays()
        {
            updatingDisplays = true;
            try
            {
                Screen[] screens = Screen.AllScreens;
                List<DisplayChoice> choices = new List<DisplayChoice>();
                foreach (Screen s in screens)
                {
                    Rectangle b = s.Bounds;
                    choices.Add(new DisplayChoice {
                        Screen = s,
                        Label = s.DeviceName.Replace("\\\\.\\", "") +
                            " (" + b.Width + "×" + b.Height + ")"
                    });
                }

                displaySelector.Items.Clear();
                foreach (DisplayChoice c in choices) displaySelector.Items.Add(c);
                if (choices.Count == 0) return;

                DisplayChoice target = null;
                if (!String.IsNullOrEmpty(savedDevice))
                    target = choices.Find(delegate(DisplayChoice c) { return c.Screen.DeviceName == savedDevice; });
                if (target == null)
                    target = choices.Find(delegate(DisplayChoice c) { return c.Screen.DeviceName.EndsWith("DISPLAY3", StringComparison.OrdinalIgnoreCase); });
                if (target == null)
                    target = choices[choices.Count >= 3 ? 2 : choices.Count - 1];
                displaySelector.SelectedItem = target;
                savedDevice = target.Screen.DeviceName;
                PutOnScreen(target.Screen, true);
                if (choices.Count < 3)
                    UpdateState("現在" + choices.Count + "画面を検出。3画面接続後に表示先を選び直してください。");
            }
            finally { updatingDisplays = false; }
        }

        private void PutOnScreen(Screen selected, bool maximize)
        {
            if (selected == null) return;
            // Windows maximizes on the monitor that contains the largest portion
            // of the window. First MOVE in normal state, then maximize.
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            Rectangle w = selected.WorkingArea;
            int marginX = Math.Min(38, Math.Max(8, w.Width / 20));
            int marginY = Math.Min(35, Math.Max(8, w.Height / 20));
            Bounds = new Rectangle(w.Left + marginX, w.Top + marginY,
                Math.Max(MinimumSize.Width, w.Width - 2 * marginX),
                Math.Max(MinimumSize.Height, w.Height - 2 * marginY));
            if (maximize) WindowState = FormWindowState.Maximized;
        }

        private Rectangle ViewRectangleInForm()
        {
            if (!viewSpace.IsHandleCreated) return Rectangle.Empty;
            Rectangle screenRect = viewSpace.RectangleToScreen(viewSpace.ClientRectangle);
            return RectangleToClient(screenRect);
        }

        private void ResizeMagnifier()
        {
            if (magHandle == IntPtr.Zero || closing || IsDisposed) return;
            Rectangle r = ViewRectangleInForm();
            if (r.Width < 1 || r.Height < 1) return;
            if (r == lastView) return;
            lastView = r;
            if (!WinMagApi.MoveWindow(magHandle, r.X, r.Y, r.Width, r.Height, true))
            {
                FailAndStop(new InvalidOperationException(WinMagApi.LastErrorMessage("拡大画面のサイズを変更できませんでした")));
                return;
            }
            lastSource = null;
            TickFollow();
        }

        // Clicking the image (a native child HWND rather than a WinForms Panel)
        // must activate the real top-level window so Win+Left/Right/Up/Down and
        // Win+Z affect this viewer. Never synthesize Windows-key shortcuts.
        internal void ActivateViewerFromPreview()
        {
            if (!closing && !IsDisposed && !ContainsFocus)
                Activate();
        }
        internal bool CanStartPreviewGesture(Point screenPoint)
        {
            if (closing || IsDisposed || magHandle == IntPtr.Zero ||
                WindowState == FormWindowState.Minimized || !Visible ||
                !(dragZoomEnabled || dragVoiceEnabled)) return false;
            if (largeControlsWindow != null && largeControlsWindow.Visible &&
                largeControlsWindow.Bounds.Contains(screenPoint)) return false;
            return viewSpace.RectangleToScreen(viewSpace.ClientRectangle).Contains(screenPoint);
        }

        internal bool IsPreviewHookGestureActive {
            get { return previewInput != null && previewInput.IsHookGestureActive; }
        }

        internal bool ForwardPreviewMouse(int msg, Point screenPoint)
        {
            if (previewInput == null) return false;
            if (msg == 0x0201) return previewInput.StartFromHook(screenPoint);
            if (msg == 0x0200) return previewInput.MoveFromHook(screenPoint);
            if (msg == 0x0202) return previewInput.EndFromHook(screenPoint);
            return false;
        }


        // Gesture settings are persisted once on mouse release to avoid disk churn.
        internal float DragStartZoom { get { return zoom; } }
        internal int DragStartVoiceRate { get { return voiceRate; } }
        internal bool DragZoomEnabled { get { return dragZoomEnabled; } }
        internal bool DragVoiceEnabled { get { return dragVoiceEnabled; } }

        internal bool ApplyPreviewDragZoom(float next)
        {
            bool updated = SetZoom(next, false);
            if (updated) ShowGestureHud(false);
            return updated;
        }
        internal bool ApplyPreviewDragVoiceRate(int next)
        {
            bool updated = SetVoiceRate(next, false);
            if (updated)
            {
                ShowGestureHud(true);
                StartVoicePreviewSample();
            }
            return updated;
        }

        // The displayed image is not a recursive magnifier of its own toolbar.
        // Offer large real WinForms controls rather than magnifying pixels of
        // controls displayed on the same screen.
        private void OpenLargeControlPanel()
        {
            if (largeControlsWindow != null && !largeControlsWindow.IsDisposed)
            {
                largeControlsWindow.Activate();
                return;
            }
            Form dialog = new Form();
            largeControlsWindow = dialog;
            dialog.Text = "拡大鏡の大きい操作画面";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.Size = new Size(900, 525);
            dialog.MinimumSize = new Size(675, 420);
            dialog.BackColor = Color.FromArgb(17, 23, 35);
            dialog.ForeColor = Color.White;
            dialog.Font = new Font("Yu Gothic UI", 19f, FontStyle.Bold);
            dialog.AutoScaleMode = AutoScaleMode.Font;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);
            layout.ColumnCount = 3;
            layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 31f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 31f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 18f));
            dialog.Controls.Add(layout);

            Label help = new Label();
            help.Text = "倍率と日本語音声の速度を調整できます";
            help.TextAlign = ContentAlignment.MiddleCenter;
            help.Dock = DockStyle.Fill;
            help.AutoSize = false;
            layout.Controls.Add(help, 0, 0);
            layout.SetColumnSpan(help, 3);

            Label zoomValue = new Label();
            zoomValue.TextAlign = ContentAlignment.MiddleCenter;
            zoomValue.Dock = DockStyle.Fill;
            zoomValue.AccessibleName = "現在の拡大倍率";
            layout.Controls.Add(zoomValue, 1, 1);
            Label rateValue = new Label();
            rateValue.TextAlign = ContentAlignment.MiddleCenter;
            rateValue.Dock = DockStyle.Fill;
            rateValue.AccessibleName = "現在の読み上げ速度";
            layout.Controls.Add(rateValue, 1, 2);
            Action updateLabels = delegate {
                zoomValue.Text = zoom.ToString("0.00", CultureInfo.InvariantCulture) + " 倍";
                rateValue.Text = "速度 " + (voiceRate >= 0 ? "+" : "") +
                    voiceRate.ToString(CultureInfo.InvariantCulture);
            };
            Action<string, string, int, int, Action> addButton =
                delegate(string caption, string accessibility, int column, int row, Action action) {
                    Button button = new Button();
                    button.Text = caption;
                    button.AccessibleName = accessibility;
                    button.Dock = DockStyle.Fill;
                    button.Margin = new Padding(7);
                    button.FlatStyle = FlatStyle.Flat;
                    button.Font = new Font("Meiryo", 21f, FontStyle.Bold);
                    StyleAccessibleButton(button);
                    button.Click += delegate { action(); updateLabels(); };
                    layout.Controls.Add(button, column, row);
                };
            addButton("倍率 −", "拡大倍率を下げる", 0, 1,
                delegate { SetZoom(Math.Max(1f, zoom - 0.5f)); });
            addButton("倍率 ＋", "拡大倍率を上げる", 2, 1,
                delegate { SetZoom(Math.Min(16f, zoom + 0.5f)); });
            addButton("遅く", "音声を遅くする", 0, 2,
                delegate { SetVoiceRate(voiceRate - 1); });
            addButton("速く", "音声を速くする", 2, 2,
                delegate { SetVoiceRate(voiceRate + 1); });
            addButton("閉じる", "大きい操作画面を閉じる", 1, 3, delegate { dialog.Close(); });
            updateLabels();
            Timer refresh = new Timer();
            refresh.Interval = 350;
            refresh.Tick += delegate {
                if (!dialog.IsDisposed) updateLabels();
            };
            dialog.FormClosed += delegate {
                refresh.Stop();
                refresh.Dispose();
                if (largeControlsWindow == dialog) largeControlsWindow = null;
                dialog.Dispose();
            };
            refresh.Start();
            dialog.Show(this);
        }

        private void ShowGestureHud(bool voice)
        {
            if (closing || IsDisposed) return;
            hudHideTimer.Stop();
            Rectangle screen = viewSpace.RectangleToScreen(viewSpace.ClientRectangle);
            if (voice)
                gestureHud.Display("読み上げ速度", "速度 " + (voiceRate >= 0 ? "+" : "") +
                    voiceRate.ToString(CultureInfo.InvariantCulture) + "　（調整中：テスト再生）",
                    (voiceRate + 10.0) / 20.0, screen);
            else
                gestureHud.Display("拡大倍率", zoom.ToString("0.00", CultureInfo.InvariantCulture) + "倍",
                    (zoom - 1.0) / 15.0, screen);
        }

        internal void BeginGestureVoicePreview()
        {
            if (closing || IsDisposed) return;
            voicePreviewActive = true;
            ShowGestureHud(true);
            StartVoicePreviewSample();
            voicePreviewTimer.Start();
        }

        private void StartVoicePreviewSample()
        {
            if (!voicePreviewActive || speechSynth == null) return;
            try
            {
                speechSynth.SpeakAsyncCancelAll();
                speechSynth.Rate = voiceRate;
                speechSynth.SpeakAsync(VoicePreviewPhrase);
            }
            catch (InvalidOperationException) { /* Retry on next timer tick. */ }
        }

        private void ContinueVoicePreview()
        {
            if (!voicePreviewActive || speechSynth == null || closing)
            {
                voicePreviewTimer.Stop();
                return;
            }
            try
            {
                if (speechSynth.State == SynthesizerState.Ready)
                    StartVoicePreviewSample();
            }
            catch (InvalidOperationException) { voicePreviewTimer.Stop(); }
        }

        internal void EndGesturePreview(bool wasVoice)
        {
            if (wasVoice || voicePreviewActive)
            {
                voicePreviewActive = false;
                voicePreviewTimer.Stop();
                if (speechSynth != null)
                {
                    try { speechSynth.SpeakAsyncCancelAll(); }
                    catch (InvalidOperationException) { }
                }
            }
            if (!closing && !IsDisposed)
            {
                hudHideTimer.Stop();
                hudHideTimer.Start();
            }
        }
        internal void CommitPreviewDragGesture() {
            SaveSettings();
            SharedVoiceRate.Publish(voiceRate);
        }

        internal void ReportOcrStatus(string message)
        {
            if (closing || IsDisposed) return;
            voiceStatusLabel.Text = message.Length > 120 ? message.Substring(0, 120) : message;
        }

        internal void SpeakRecognizedText(string recognized)
        {
            if (closing || IsDisposed) return;
            string text = (recognized ?? "").Trim();
            if (text.Length == 0)
            {
                ReportOcrStatus("文字を認識できませんでした。範囲を広げてお試しください。");
                return;
            }
            if (text.Length > 2000) text = text.Substring(0, 2000);
            speechText.Text = text.Length > 400 ? text.Substring(0, 400) : text;
            if (speechSynth == null)
            {
                ReportOcrStatus("文字は認識しましたが、日本語SAPI音声が見つかりません。認識結果は入力欄に表示しました。");
                return;
            }
            try
            {
                speechSynth.SpeakAsyncCancelAll();
                speechSynth.Rate = voiceRate;
                speechSynth.SpeakAsync(text);
                ReportOcrStatus("選択範囲の文字を読み上げています。停止ボタンで中断できます。");
            }
            catch (Exception ex)
            {
                ReportOcrStatus("読み上げに失敗しました: " + ex.Message);
            }
        }

        private void UpdateVoiceRateLabel()
        {
            voiceRateLabel.Text = "速度 " + (voiceRate > 0 ? "+" : "") +
                voiceRate.ToString(CultureInfo.InvariantCulture);
        }

        private bool SetVoiceRate(int requested, bool persist = true)
        {
            int next = Math.Max(-10, Math.Min(10, requested));
            if (voiceRate == next) return false;
            voiceRate = next;
            UpdateVoiceRateLabel();
            // A voice engine may not change the pace of an already spoken
            // sentence. Rate is always explicitly applied before the next reading.
            if (speechSynth != null)
            {
                try { speechSynth.Rate = voiceRate; }
                catch (Exception e) { voiceStatusLabel.Text = "速度の適用に失敗: " + e.Message; }
            }
            if (persist)
            {
                SaveSettings();
                SharedVoiceRate.Publish(voiceRate);
            }
            return true;
        }

        private void InitializeJapaneseSpeech()
        {
            SpeechSynthesizer candidate = null;
            try
            {
                candidate = new SpeechSynthesizer();
                InstalledVoice japanese = null;
                foreach (InstalledVoice v in candidate.GetInstalledVoices())
                {
                    if (v.Enabled && v.VoiceInfo != null && v.VoiceInfo.Culture != null &&
                        String.Equals(v.VoiceInfo.Culture.Name, "ja-JP", StringComparison.OrdinalIgnoreCase))
                    {
                        japanese = v;
                        break;
                    }
                }
                if (japanese == null)
                {
                    voiceStatusLabel.Text = "日本語SAPI音声がありません（拡大機能は使用可）";
                    return;
                }
                candidate.SelectVoice(japanese.VoiceInfo.Name);
                candidate.SetOutputToDefaultAudioDevice();
                candidate.Rate = voiceRate;
                speechSynth = candidate;
                candidate = null; // The form now owns this synthesizer.
                speakButton.Enabled = true;
                stopSpeakButton.Enabled = true;
                voiceStatusLabel.Text = "日本語音声: " + japanese.VoiceInfo.Name + "（速度変更は次回読上げで確実に反映）";
            }
            catch (Exception e)
            {
                voiceStatusLabel.Text = "日本語音声を初期化できません: " + e.Message;
            }
            finally
            {
                if (candidate != null)
                {
                    try { candidate.Dispose(); } catch { }
                }
            }
        }

        private void SpeakInputText()
        {
            if (speechSynth == null) return;
            string sample = speechText.Text.Trim();
            if (sample.Length == 0) return;
            try
            {
                speechSynth.SpeakAsyncCancelAll();
                speechSynth.Rate = voiceRate;
                speechSynth.SpeakAsync(sample);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "読み上げを開始できませんでした: " + e.Message,
                    "日本語読み上げ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StopSpeaking()
        {
            if (speechSynth == null) return;
            try { speechSynth.SpeakAsyncCancelAll(); }
            catch (Exception e) { voiceStatusLabel.Text = "音声停止エラー: " + e.Message; }
        }

        private bool SetZoom(float next, bool persist = true)
        {
            float requested = (float)Math.Round(Math.Max(1f, Math.Min(16f, next)), 2);
            if (Math.Abs(zoom - requested) < 0.0001f) return false;
            float previous = zoom;
            zoom = requested;
            zoomLabel.Text = zoom.ToString("0.00", CultureInfo.InvariantCulture) + "倍";
            if (magHandle != IntPtr.Zero)
            {
                try { ApplyZoom(zoom); }
                catch (Exception e)
                {
                    zoom = previous;
                    zoomLabel.Text = zoom.ToString("0.00", CultureInfo.InvariantCulture) + "倍";
                    FailAndStop(e);
                    return false;
                }
            }
            if (persist) SaveSettings();
            lastSource = null;
            TickFollow();
            return true;
        }

        private void ApplyZoom(float scale)
        {
            if (Math.Abs(appliedZoom - scale) < 0.0001f) return;
            WinMagApi.MagTransform transform = WinMagApi.MagTransform.Scale(scale);
            if (!WinMagApi.MagSetWindowTransform(magHandle, ref transform))
                throw new InvalidOperationException(WinMagApi.LastErrorMessage("倍率を変更できませんでした"));
            appliedZoom = scale;
        }

        private void TickFollow()
        {
            if (closing || magHandle == IntPtr.Zero || WindowState == FormWindowState.Minimized) return;
            try
            {
                Point mouse = Cursor.Position;
                Screen sourceScreen = Screen.FromPoint(mouse);
                Screen viewingScreen = Screen.FromHandle(Handle);
                bool inViewerScreen = sourceScreen.DeviceName == viewingScreen.DeviceName;
                if (!frozen && !inViewerScreen && !(previewInput != null && previewInput.IsDragging))
                {
                    trackedPoint = mouse;
                    trackedBounds = sourceScreen.Bounds;
                    trackedDevice = sourceScreen.DeviceName;
                }
                if (!trackedPoint.HasValue)
                {
                    // If launched while the pointer is on the viewer display,
                    // use the first other monitor as a safe default source.
                    foreach (Screen s in Screen.AllScreens)
                    {
                        if (s.DeviceName == viewingScreen.DeviceName) continue;
                        trackedBounds = s.Bounds;
                        trackedPoint = new Point(s.Bounds.Left + s.Bounds.Width / 2,
                                                 s.Bounds.Top + s.Bounds.Height / 2);
                        trackedDevice = s.DeviceName;
                        break;
                    }
                }
                if (!trackedPoint.HasValue)
                {
                    UpdateState("別の作業画面がありません。Windowsの『拡張』ディスプレイ設定と接続数を確認してください。");
                    return;
                }

                Rectangle view = ViewRectangleInForm();
                if (view.Width < 1 || view.Height < 1) return;
                Rectangle bounds = trackedBounds;
                // Prevent capture from crossing into the viewer monitor when
                // the output is physically larger than the source monitor.
                // A minimum effective zoom is needed in that case.
                float minimumToFit = Math.Max((float)view.Width / Math.Max(1, bounds.Width),
                                              (float)view.Height / Math.Max(1, bounds.Height));
                float effectiveZoom = Math.Max(zoom, minimumToFit);
                ApplyZoom(effectiveZoom);
                int srcW = Math.Min(bounds.Width, Math.Max(1, (int)Math.Round(view.Width / effectiveZoom)));
                int srcH = Math.Min(bounds.Height, Math.Max(1, (int)Math.Round(view.Height / effectiveZoom)));
                int srcX = trackedPoint.Value.X - srcW / 2;
                int srcY = trackedPoint.Value.Y - srcH / 2;

                // Keep the source as far as possible inside its physical monitor,
                // preventing sudden jumps or a feedback loop on the viewer display.
                srcX = srcW >= bounds.Width ? bounds.Left : Math.Max(bounds.Left, Math.Min(srcX, bounds.Right - srcW));
                srcY = srcH >= bounds.Height ? bounds.Top : Math.Max(bounds.Top, Math.Min(srcY, bounds.Bottom - srcH));

                WinMagApi.MagRect rect = new WinMagApi.MagRect(srcX, srcY, srcX + srcW, srcY + srcH);
                if (!lastSource.HasValue || !EqualRect(lastSource.Value, rect))
                {
                    if (!WinMagApi.MagSetWindowSource(magHandle, rect))
                        throw new InvalidOperationException(WinMagApi.LastErrorMessage("拡大位置を更新できませんでした"));
                    lastSource = rect;
                }
                string status = frozen ? "追従停止中" : (inViewerScreen ? "表示位置を保持中" : "マウス追従中");
                UpdateState(trackedDevice.Replace("\\\\.\\", "") +
                    " を" + effectiveZoom.ToString("0.##", CultureInfo.InvariantCulture) +
                    "倍で表示・" + status +
                    (effectiveZoom > zoom + 0.01f ? "（画面サイズに合わせ倍率自動補正）" : "") +
                    "。1回ドラッグ＝拡大±10％ポイント／48px。2回目を押し続けてドラッグ＝音声速度±1段階／48px。元画面へのクリック送信なし。");
            }
            catch (Exception e) { FailAndStop(e); }
        }

        private static bool EqualRect(WinMagApi.MagRect a, WinMagApi.MagRect b)
        {
            return a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;
        }

        private void UpdateState(string message)
        {
            if (lastState == message) return;
            lastState = message;
            stateLabel.Text = message;
        }

        private void FailAndStop(Exception error)
        {
            if (closing || IsDisposed) return;
            followTimer.Stop();
            UpdateState("拡大鏡の処理を停止しました：" + error.Message);
            MessageBox.Show(this,
                error.Message + "\r\n\r\n通常の画面操作は変更されていません。アプリを閉じ、必要なら再起動してください。",
                "専用拡大鏡：動作を停止", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs args)
        {
            if (closing || !IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate {
                    if (closing || IsDisposed) return;
                    lastSource = null;
                    trackedPoint = null;
                    trackedBounds = Rectangle.Empty;
                    trackedDevice = "";
                    try { ReloadDisplays(); ResizeMagnifier(); }
                    catch (Exception e) { FailAndStop(e); }
                });
            }
            catch (InvalidOperationException) { }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;
                foreach (string line in File.ReadAllLines(settingsPath, Encoding.UTF8))
                {
                    if (line.StartsWith("device=")) savedDevice = line.Substring(7).Trim();
                    if (line.StartsWith("globalOcr="))
                        globalOcrEnabled = line.Substring(10).Trim() != "false";
                    if (line.StartsWith("altLeft="))
                        altLeftEnabled = line.Substring(8).Trim() == "true";
                    if (line.StartsWith("dragZoom="))
                        dragZoomEnabled = line.Substring(9).Trim() != "false";
                    if (line.StartsWith("dragVoice="))
                        dragVoiceEnabled = line.Substring(10).Trim() != "false";
                    if (line.StartsWith("voiceRate="))
                    {
                        int r;
                        if (int.TryParse(line.Substring(10), NumberStyles.Integer, CultureInfo.InvariantCulture, out r))
                            voiceRate = Math.Max(-10, Math.Min(10, r));
                    }
                    if (line.StartsWith("zoom="))
                    {
                        float v;
                        if (float.TryParse(line.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 1f && v <= 16f)
                            zoom = v;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void SaveSettings()
        {
            try
            {
                string folder = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllLines(settingsPath, new string[] {
                    "device=" + savedDevice,
                    "zoom=" + zoom.ToString("0.00", CultureInfo.InvariantCulture),
                    "dragZoom=" + (dragZoomEnabled ? "true" : "false"),
                    "dragVoice=" + (dragVoiceEnabled ? "true" : "false"),
                    "globalOcr=" + (globalOcrEnabled ? "true" : "false"),
                    "altLeft=" + (altLeftEnabled ? "true" : "false"),
                    "voiceRate=" + voiceRate.ToString(CultureInfo.InvariantCulture)
                }, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void ShutdownViewer()
        {
            if (closing) return;
            // The window may have moved with Win+Shift+Left/Right. Remember
            // its actual monitor without interfering with normal Snap moves.
            try
            {
                if (IsHandleCreated)
                    savedDevice = Screen.FromHandle(Handle).DeviceName;
                SaveSettings();
            }
            catch (Exception) { } // Shutdown must remain failure-safe.
            closing = true;
            if (globalOcr != null)
            {
                globalOcr.Dispose();
                globalOcr = null;
            }
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            followTimer.Stop();
            followTimer.Dispose();
            voiceSyncTimer.Stop();
            voiceSyncTimer.Dispose();
            voicePreviewActive = false;
            voicePreviewTimer.Stop();
            voicePreviewTimer.Dispose();
            hudHideTimer.Stop();
            hudHideTimer.Dispose();
            gestureHud.Close();
            gestureHud.Dispose();
            if (largeControlsWindow != null && !largeControlsWindow.IsDisposed)
                largeControlsWindow.Close();
            if (speechSynth != null)
            {
                try { speechSynth.SpeakAsyncCancelAll(); } catch { }
                try { speechSynth.Dispose(); } catch { }
                speechSynth = null;
            }
            if (previewInput != null)
            {
                previewInput.Detach();
                previewInput = null;
            }
            if (magHandle != IntPtr.Zero)
            {
                WinMagApi.DestroyWindow(magHandle);
                magHandle = IntPtr.Zero;
            }
        }
    }
}
