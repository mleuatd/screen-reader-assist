# Windows 11 Magnifier Base v0.1
# Runs with Windows PowerShell 5.1. Uses the real Windows Magnifier.
# The future extension point is Invoke-MagnifierCommand.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Use SendInput to send the same official keyboard shortcuts as Windows Magnifier.
# No UIAccess, driver, admin rights, or screen capture is required here.
$nativeCode = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class WinKeyBridge
{
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public int type; public INPUTUNION U; }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    private static INPUT Key(ushort vk, bool up)
    {
        INPUT input = new INPUT();
        input.type = 1;
        input.U.ki = new KEYBDINPUT();
        input.U.ki.wVk = vk;
        input.U.ki.dwFlags = up ? 2u : 0u;
        return input;
    }

    public static void Combo(params ushort[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        List<INPUT> sequence = new List<INPUT>();
        foreach (ushort key in keys) sequence.Add(Key(key, false));
        for (int i = keys.Length - 1; i >= 0; i--) sequence.Add(Key(keys[i], true));
        INPUT[] array = sequence.ToArray();
        uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf(typeof(INPUT)));
        if (sent != (uint)array.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "キーボード操作を送信できませんでした。");
    }

    public static IntPtr Foreground() { return GetForegroundWindow(); }
    public static bool Focus(IntPtr h) { return h != IntPtr.Zero && IsWindow(h) && SetForegroundWindow(h); }
}
'@
Add-Type -TypeDefinition $nativeCode -Language CSharp

# Official Windows virtual-key codes.
$VK = @{
    Win   = [ushort]0x5B
    Ctrl  = [ushort]0x11
    Alt   = [ushort]0x12
    Add   = [ushort]0x6B
    Sub   = [ushort]0x6D
    Esc   = [ushort]0x1B
    Enter = [ushort]0x0D
    F     = [ushort]0x46
    L     = [ushort]0x4C
    D     = [ushort]0x44
    I     = [ushort]0x49
    H     = [ushort]0x48
    K     = [ushort]0x4B
}

function Send-MagnifierKeys {
    param([ushort[]] $Keys)
    [WinKeyBridge]::Combo($Keys)
}

function Start-WindowsMagnifier {
    $magnifierPath = Join-Path $env:WINDIR 'System32\Magnify.exe'
    if (-not (Test-Path -LiteralPath $magnifierPath)) {
        throw 'Windows標準の拡大鏡が見つかりません。'
    }
    if (-not (Get-Process -Name 'Magnify' -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath $magnifierPath
        Start-Sleep -Milliseconds 250
    }
}

function Open-WindowsPage {
    param([string]$Uri)
    Start-Process -FilePath 'explorer.exe' -ArgumentList $Uri
}

function Restore-WorkWindow {
    if ($script:LastWorkWindow -ne [IntPtr]::Zero) {
        [void][WinKeyBridge]::Focus($script:LastWorkWindow)
        Start-Sleep -Milliseconds 120
    }
}

function Invoke-MagnifierCommand {
    param([string] $Name)
    try {
        switch ($Name) {
            'start'  { Start-WindowsMagnifier; $message = '拡大鏡を開始しました。' }
            'stop'   { Send-MagnifierKeys @($VK.Win, $VK.Esc); $message = '拡大鏡を終了しました。' }
            'plus'   { Start-WindowsMagnifier; Send-MagnifierKeys @($VK.Win, $VK.Add); $message = '拡大しました。' }
            'minus'  { Start-WindowsMagnifier; Send-MagnifierKeys @($VK.Win, $VK.Sub); $message = '縮小しました。' }
            'full'   { Start-WindowsMagnifier; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.F); $message = '全画面表示に切り替えました。' }
            'lens'   { Start-WindowsMagnifier; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.L); $message = 'レンズ表示に切り替えました。' }
            'dock'   { Start-WindowsMagnifier; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.D); $message = '固定表示に切り替えました。' }
            'invert' { Start-WindowsMagnifier; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.I); $message = '色反転を切り替えました。' }
            'read'   { Start-WindowsMagnifier; Restore-WorkWindow; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.Enter); $message = '読み上げ / 一時停止を実行しました。' }
            'prev'   { Start-WindowsMagnifier; Restore-WorkWindow; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.H); $message = '前の文の読み上げを実行しました。' }
            'next'   { Start-WindowsMagnifier; Restore-WorkWindow; Send-MagnifierKeys @($VK.Ctrl, $VK.Alt, $VK.K); $message = '次の文の読み上げを実行しました。' }
            'settings' { Open-WindowsPage 'ms-settings:easeofaccess-magnifier'; $message = 'Windowsの拡大鏡設定を開きました。' }
            'text'     { Open-WindowsPage 'ms-settings:easeofaccess-display'; $message = 'Windowsの文字サイズ設定を開きました。' }
            'pointer'  { Open-WindowsPage 'ms-settings:easeofaccess-mousepointer'; $message = 'Windowsのマウスポインター設定を開きました。' }
            'colors'   { Open-WindowsPage 'ms-settings:easeofaccess-highcontrast'; $message = 'Windowsのコントラスト設定を開きました。' }
            default { throw "不明な操作: $Name" }
        }
        $script:statusLabel.Text = $message
    }
    catch {
        $script:statusLabel.Text = '操作に失敗しました。'
        [void][System.Windows.Forms.MessageBox]::Show(
            ("操作できませんでした。`r`n" + $_.Exception.Message),
            'Win11拡大鏡ベース',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
    }
}

[System.Windows.Forms.Application]::EnableVisualStyles()
[System.Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)

$script:LastWorkWindow = [IntPtr]::Zero
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Win11拡大鏡ベース v0.1'
$form.Width = 940
$form.Height = 760
$form.MinimumSize = New-Object System.Drawing.Size(570, 490)
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = [System.Drawing.Color]::FromArgb(16, 20, 27)
$form.ForeColor = [System.Drawing.Color]::White
$form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::Dpi
$form.Font = New-Object System.Drawing.Font('Yu Gothic UI', 14, [System.Drawing.FontStyle]::Regular)

$root = New-Object System.Windows.Forms.FlowLayoutPanel
$root.Dock = [System.Windows.Forms.DockStyle]::Fill
$root.FlowDirection = [System.Windows.Forms.FlowDirection]::TopDown
$root.WrapContents = $false
$root.AutoScroll = $true
$root.Padding = New-Object System.Windows.Forms.Padding(16, 14, 16, 16)
$root.BackColor = $form.BackColor
[void]$form.Controls.Add($root)

function New-TextLabel {
    param([string]$Text, [int]$Height, [int]$FontSize, [bool]$IsBold)
    $item = New-Object System.Windows.Forms.Label
    $item.Text = $Text
    $item.Width = 840
    $item.Height = $Height
    $item.Margin = New-Object System.Windows.Forms.Padding(3, 3, 3, 7)
    $item.ForeColor = [System.Drawing.Color]::White
    $item.Font = New-Object System.Drawing.Font('Yu Gothic UI', $FontSize, $(if ($IsBold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }))
    return $item
}

function New-ActionButton {
    param([string]$Text, [string]$Command, [string]$Help, [bool]$Critical)
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $Text
    $b.Tag = $Command
    $b.AccessibleName = $Text
    $b.AccessibleDescription = $Help
    $b.Dock = [System.Windows.Forms.DockStyle]::Fill
    $b.Margin = New-Object System.Windows.Forms.Padding(4, 4, 4, 4)
    $b.Height = 69
    $b.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $b.FlatAppearance.BorderSize = 2
    $b.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(140, 166, 195)
    $b.BackColor = $(if ($Critical) { [System.Drawing.Color]::FromArgb(97, 36, 48) } else { [System.Drawing.Color]::FromArgb(32, 46, 66) })
    $b.ForeColor = [System.Drawing.Color]::White
    $b.Font = New-Object System.Drawing.Font('Yu Gothic UI', 15, [System.Drawing.FontStyle]::Bold)
    $b.UseVisualStyleBackColor = $false
    $b.Add_Click({ param($sender, $eventArgs) Invoke-MagnifierCommand -Name ([string]$sender.Tag) })
    return $b
}

$widthAware = New-Object 'System.Collections.Generic.List[System.Windows.Forms.Control]'

function New-Section {
    param([string]$Heading, [object[]]$Buttons)
    $label = New-TextLabel $Heading 38 17 $true
    [void]$root.Controls.Add($label)
    [void]$widthAware.Add($label)

    $rows = [int][Math]::Ceiling($Buttons.Count / 2)
    $table = New-Object System.Windows.Forms.TableLayoutPanel
    $table.Width = 840
    $table.Height = 78 * $rows
    $table.RowCount = $rows
    $table.ColumnCount = 2
    $table.GrowStyle = [System.Windows.Forms.TableLayoutPanelGrowStyle]::FixedSize
    $table.Margin = New-Object System.Windows.Forms.Padding(0, 0, 0, 14)
    $table.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 50))) | Out-Null
    $table.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 50))) | Out-Null
    for ($i = 0; $i -lt $rows; $i++) {
        $table.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 78))) | Out-Null
        for ($j = 0; $j -lt 2; $j++) {
            $index = ($i * 2) + $j
            if ($index -lt $Buttons.Count) {
                $entry = $Buttons[$index]
                $button = New-ActionButton $entry.Text $entry.Command $entry.Help ([bool]$entry.Critical)
                $table.Controls.Add($button, $j, $i)
            }
        }
    }
    [void]$root.Controls.Add($table)
    [void]$widthAware.Add($table)
}

$heading = New-TextLabel 'Windows 11 拡大鏡｜操作パネル' 55 23 $true
[void]$root.Controls.Add($heading)
[void]$widthAware.Add($heading)

$lead = New-TextLabel 'Windows標準の拡大鏡をそのまま使う、無料の日本語コントローラーです。' 40 12 $false
$lead.ForeColor = [System.Drawing.Color]::FromArgb(202, 220, 238)
[void]$root.Controls.Add($lead)
[void]$widthAware.Add($lead)

New-Section '1. 拡大鏡の起動・倍率' @(
    @{ Text='拡大鏡を起動'; Command='start'; Help='Windows標準の拡大鏡を起動'; Critical=$false }
    @{ Text='拡大鏡を終了'; Command='stop'; Help='WindowsキーとEscapeキーで終了'; Critical=$true }
    @{ Text='拡大する ＋'; Command='plus'; Help='Windows標準拡大鏡の倍率を上げる'; Critical=$false }
    @{ Text='縮小する －'; Command='minus'; Help='Windows標準拡大鏡の倍率を下げる'; Critical=$false }
)

New-Section '2. 表示方法・配色' @(
    @{ Text='全画面表示'; Command='full'; Help='全画面拡大にする'; Critical=$false }
    @{ Text='レンズ表示'; Command='lens'; Help='拡大レンズにする'; Critical=$false }
    @{ Text='固定表示'; Command='dock'; Help='固定された拡大領域にする'; Critical=$false }
    @{ Text='色を反転'; Command='invert'; Help='画面の色を反転/元に戻す'; Critical=$false }
)

New-Section '3. 日本語読み上げ（Windows標準）' @(
    @{ Text='読み上げ／一時停止'; Command='read'; Help='作業中の画面の読み上げを切り替える'; Critical=$false }
    @{ Text='前の文を読む'; Command='prev'; Help='直前の文を読み上げる'; Critical=$false }
    @{ Text='次の文を読む'; Command='next'; Help='次の文を読み上げる'; Critical=$false }
    @{ Text='拡大鏡の読み上げ設定'; Command='settings'; Help='音声/読み上げキーの設定画面'; Critical=$false }
)

$readHint = New-TextLabel '場所を指定して読む：Ctrl＋Altを押しながら、読みたい文章を左クリック。' 42 12 $false
$readHint.ForeColor = [System.Drawing.Color]::FromArgb(227, 225, 168)
[void]$root.Controls.Add($readHint)
[void]$widthAware.Add($readHint)

New-Section '4. Windowsの表示設定' @(
    @{ Text='標準の拡大鏡設定'; Command='settings'; Help='Windowsアクセシビリティの拡大鏡設定'; Critical=$false }
    @{ Text='文字サイズ設定'; Command='text'; Help='Windowsの文字サイズ変更画面'; Critical=$false }
    @{ Text='マウスポインター設定'; Command='pointer'; Help='マウスポインターの大きさなどを変更'; Critical=$false }
    @{ Text='コントラスト設定'; Command='colors'; Help='Windowsのコントラストテーマの設定'; Critical=$false }
)

$script:statusLabel = New-TextLabel '準備完了。まず「拡大鏡を起動」を押してください。' 54 13 $true
$script:statusLabel.ForeColor = [System.Drawing.Color]::FromArgb(165, 226, 180)
[void]$root.Controls.Add($script:statusLabel)
[void]$widthAware.Add($script:statusLabel)

$foot = New-TextLabel 'いつでも Windows＋Esc で拡大鏡を終了できます。画面が見にくい場合はこの操作を使ってください。' 60 12 $false
$foot.ForeColor = [System.Drawing.Color]::FromArgb(208, 214, 224)
[void]$root.Controls.Add($foot)
[void]$widthAware.Add($foot)

$root.Add_Resize({
    $targetWidth = [Math]::Max(460, $root.ClientSize.Width - 42)
    foreach ($ctrl in $widthAware) { $ctrl.Width = $targetWidth }
})

# Remember the last foreground app so the reading buttons have a better chance
# of operating on its caret instead of reading this control panel. Best-effort.
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 350
$timer.Add_Tick({
    $hwnd = [WinKeyBridge]::Foreground()
    if ($hwnd -ne [IntPtr]::Zero -and $hwnd -ne $form.Handle) {
        $script:LastWorkWindow = $hwnd
    }
})
$form.Add_Shown({ $timer.Start(); $root.PerformLayout() })
$form.Add_FormClosed({ $timer.Stop(); $timer.Dispose() })

[void][System.Windows.Forms.Application]::Run($form)
