using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: System.Reflection.AssemblyTitle("WinClicker")]
[assembly: System.Reflection.AssemblyProduct("WinClicker")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

static class Native
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    public const uint INPUT_MOUSE = 0;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

    // window targeting
    public const uint GW_HWNDNEXT = 2, CWP_SKIPINVISIBLE = 1, CWP_SKIPDISABLED = 2, CWP_SKIPTRANSPARENT = 4;
    public const int WM_MOUSEMOVE = 0x200;
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr h, POINT p, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

    public static bool Cloaked(IntPtr h)
    {
        int v;
        return DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out v, 4) == 0 && v != 0;
    }

    // Top-level window under a screen point, walking the z-order below `skip` (our overlay).
    public static IntPtr TopLevelAt(POINT pt, IntPtr skip)
    {
        for (IntPtr h = GetWindow(skip, GW_HWNDNEXT); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
        {
            if (!IsWindowVisible(h) || IsIconic(h) || Cloaked(h)) continue;
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            if ((ex & WS_EX_TRANSPARENT) != 0 || (ex & WS_EX_TOOLWINDOW) != 0) continue;
            RECT r;
            if (!GetWindowRect(h, out r) || r.Right <= r.Left || r.Bottom <= r.Top) continue;
            if (pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom) return h;
        }
        return IntPtr.Zero;
    }

    public static string Describe(IntPtr h)
    {
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(h, sb, 256);
        string title = sb.ToString();
        string app = "";
        try
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            app = Process.GetProcessById((int)pid).ProcessName;
            if (app.Length > 0) app = char.ToUpper(app[0]) + app.Substring(1);
        }
        catch (Exception) { }
        if (title.Length == 0) return app.Length > 0 ? app : Loc.T("untitled");
        if (app.Length == 0 || title.IndexOf(app, StringComparison.OrdinalIgnoreCase) >= 0) return title;
        return app + " - " + title;
    }
}

sealed class Hotkey
{
    public const int Ctrl = 1, Alt = 2, Shift = 4, Win = 8;
    public readonly int Vk, Mods;
    public Hotkey(int vk, int mods) { Vk = vk; Mods = mods; }

    public string Serialize() { return Mods + ":" + Vk; }

    public static Hotkey Parse(string s, Hotkey fallback)
    {
        string[] p = (s ?? "").Split(':');
        int m, v;
        if (p.Length == 2 && int.TryParse(p[0], out m) && int.TryParse(p[1], out v) && v > 0) return new Hotkey(v, m);
        return fallback;
    }

    public static bool IsModifier(int vk)
    {
        return vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C;
    }

    static string KeyName(int vk)
    {
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);
        switch (vk)
        {
            case 0x08: return "Backspace";
            case 0x0D: return "Enter";
            case 0x14: return "CapsLock";
            case 0x1B: return "Esc";
            case 0x20: return Loc.T("space");
            case 0x21: return "PageUp";
            case 0x22: return "PageDown";
            case 0x2C: return "PrintScreen";
            case 0xBA: return ";";
            case 0xBB: return "=";
            case 0xBC: return ",";
            case 0xBD: return "-";
            case 0xBE: return ".";
            case 0xBF: return "/";
            case 0xC0: return "`";
            case 0xDB: return "[";
            case 0xDC: return "\\";
            case 0xDD: return "]";
            case 0xDE: return "'";
        }
        Key k = KeyInterop.KeyFromVirtualKey(vk);
        return k == Key.None ? "Key " + vk : k.ToString();
    }

    public override string ToString()
    {
        string s = "";
        if ((Mods & Ctrl) != 0) s += "Ctrl + ";
        if ((Mods & Alt) != 0) s += "Alt + ";
        if ((Mods & Shift) != 0) s += "Shift + ";
        if ((Mods & Win) != 0) s += "Win + ";
        return s + KeyName(Vk);
    }
}

enum ClickMode { Fixed, Cursor, Window }

static class Loc
{
    public static string Lang = "en";

    static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        {"minimize", "Minimize"}, {"close", "Close"}, {"langTooltip", "Switch language"},
        {"target", "TARGET"}, {"click", "CLICK"}, {"hotkeys", "HOTKEYS"},
        {"modeFixed", "Fixed location"}, {"modeCursor", "Follow cursor"}, {"modeWindow", "In a window"},
        {"pickScreen", "Pick on screen"},
        {"cursorNote", "Clicks wherever your pointer currently is."},
        {"windowNote", "Clicks are sent straight to that window, so your mouse stays free and you can use other apps meanwhile. The window can be behind others, but not minimized. Only the stop hotkey stops this mode."},
        {"intervalLabel", "Interval between clicks"},
        {"left", "Left"}, {"right", "Right"}, {"middle", "Middle"}, {"single", "Single"}, {"double", "Double"},
        {"start", "Start"}, {"stop", "Stop"}, {"hotkeyTip", "Click, then press the new hotkey"},
        {"anyKey", "Any key press stops clicking"}, {"clicksRun", "clicks this run"},
        {"ready", "READY"}, {"clicking", "CLICKING"},
        {"anyNoteAny", "Off = only the stop hotkey stops it"},
        {"anyNoteWin", "Off in window mode - typing goes to other apps"},
        {"footerOther", "Moving the mouse never interrupts it - only your keyboard does."},
        {"footerWin", "Your mouse and keyboard stay free while it clicks in the background."},
        {"noWindowYet", "No window picked yet"}, {"untitled", "Untitled window"},
        {"clicksSep", "clicks  ·  "},
        {"invalidInterval", "Invalid interval"}, {"invalidIntervalMsg", "Enter a delay of 1 ms or more"},
        {"noTargetLoc", "No target location"}, {"noTargetLocMsg", "Use “Pick on screen” to choose where to click"},
        {"noWindowPick", "No window picked"}, {"noWindowPickMsg", "Use “Pick on screen” to choose a spot inside a window"},
        {"minimized", "Window is minimized"}, {"minimizedMsg", "Restore the target window first - it may sit behind others"},
        {"clickingIn", "Clicking in "}, {"started", "Clicking started"},
        {"press", "Press "}, {"orAny", " or any key"}, {"toStop", " to stop"},
        {"stopped", "Clicking stopped"}, {"clicksIn", " clicks in "},
        {"winClosed", "The target window was closed"}, {"winMinimized", "The target window was minimized"},
        {"hotkeysUnavail", "Hotkeys unavailable"}, {"hotkeysUnavailMsg", "Could not install the keyboard hook"},
        {"pressAKey", "Press a key…"},
        {"pickWinHint", "Click a spot inside the window to target  ·  Esc to cancel"},
        {"pickPointHint", "Click to set target  ·  Esc to cancel"},
        {"noWindowHere", "No window here"}, {"f7Demo", "Press F7 or any key to stop"},
        {"toastTitle", "WinClicker notification"}, {"space", "Space"},
    };

    static readonly Dictionary<string, string> Pt = new Dictionary<string, string>
    {
        {"minimize", "Minimizar"}, {"close", "Fechar"}, {"langTooltip", "Mudar idioma"},
        {"target", "ALVO"}, {"click", "CLIQUE"}, {"hotkeys", "ATALHOS"},
        {"modeFixed", "Local fixo"}, {"modeCursor", "Seguir o cursor"}, {"modeWindow", "Numa janela"},
        {"pickScreen", "Escolher na tela"},
        {"cursorNote", "Clica onde o seu ponteiro estiver no momento."},
        {"windowNote", "Os cliques são enviados direto para essa janela, então o seu mouse fica livre e você pode usar outros aplicativos enquanto isso. A janela pode ficar atrás das outras, mas não pode ser minimizada. Neste modo, apenas o atalho de parar interrompe."},
        {"intervalLabel", "Intervalo entre cliques"},
        {"left", "Esquerdo"}, {"right", "Direito"}, {"middle", "Meio"}, {"single", "Simples"}, {"double", "Duplo"},
        {"start", "Iniciar"}, {"stop", "Parar"}, {"hotkeyTip", "Clique e depois pressione o novo atalho"},
        {"anyKey", "Parar com qualquer tecla"}, {"clicksRun", "cliques nesta execução"},
        {"ready", "PRONTO"}, {"clicking", "CLICANDO"},
        {"anyNoteAny", "Desligado = só o atalho de parar interrompe"},
        {"anyNoteWin", "Desligado no modo janela — o que você digita vai para os outros apps"},
        {"footerOther", "Mexer o mouse nunca interrompe — só o teclado interrompe."},
        {"footerWin", "Seu mouse e seu teclado ficam livres enquanto ele clica em segundo plano."},
        {"noWindowYet", "Nenhuma janela escolhida"}, {"untitled", "Janela sem título"},
        {"clicksSep", "cliques  ·  "},
        {"invalidInterval", "Intervalo inválido"}, {"invalidIntervalMsg", "Digite um intervalo de 1 ms ou mais"},
        {"noTargetLoc", "Nenhum local definido"}, {"noTargetLocMsg", "Use “Escolher na tela” para definir onde clicar"},
        {"noWindowPick", "Nenhuma janela escolhida"}, {"noWindowPickMsg", "Use “Escolher na tela” para marcar um ponto dentro de uma janela"},
        {"minimized", "Janela minimizada"}, {"minimizedMsg", "Restaure a janela de destino primeiro — ela pode estar atrás das outras"},
        {"clickingIn", "Clicando em "}, {"started", "Clique iniciado"},
        {"press", "Pressione "}, {"orAny", " ou qualquer tecla"}, {"toStop", " para parar"},
        {"stopped", "Cliques parados"}, {"clicksIn", " cliques em "},
        {"winClosed", "A janela de destino foi fechada"}, {"winMinimized", "A janela de destino foi minimizada"},
        {"hotkeysUnavail", "Atalhos indisponíveis"}, {"hotkeysUnavailMsg", "Não foi possível instalar o hook de teclado"},
        {"pressAKey", "Pressione uma tecla…"},
        {"pickWinHint", "Clique em um ponto dentro da janela para mirar  ·  Esc para cancelar"},
        {"pickPointHint", "Clique para definir o alvo  ·  Esc para cancelar"},
        {"noWindowHere", "Nenhuma janela aqui"}, {"f7Demo", "Pressione F7 ou qualquer tecla para parar"},
        {"toastTitle", "Notificação do WinClicker"}, {"space", "Espaço"},
    };

    public static string T(string k)
    {
        string v;
        var d = Lang == "pt" ? Pt : En;
        if (d.TryGetValue(k, out v)) return v;
        if (En.TryGetValue(k, out v)) return v;
        return k;
    }
}


// One clicking session; runs on its own thread until StopEvent is set.
sealed class ClickRun
{
    public readonly ManualResetEvent StopEvent = new ManualResetEvent(false);
    public readonly Stopwatch Clock = new Stopwatch();
    public long Count;
    public ClickMode Mode;
    public bool Double;
    public int X, Y, Interval;
    public uint DownFlag, UpFlag;
    // Window mode: target top-level window and the click point relative to its top-left corner
    public IntPtr Root;
    public int DownMsg, UpMsg, DblMsg, MkFlag;
    public volatile string Error;

    public void Loop()
    {
        int n = Double ? 4 : 2;
        var inputs = new Native.INPUT[n];
        for (int i = 0; i < n; i++)
        {
            inputs[i].type = Native.INPUT_MOUSE;
            inputs[i].mi.dwFlags = (i % 2 == 0) ? DownFlag : UpFlag;
        }
        int size = Marshal.SizeOf(typeof(Native.INPUT));

        Native.timeBeginPeriod(1);
        try
        {
            Clock.Start();
            long next = 0;
            while (true)
            {
                if (Mode == ClickMode.Window)
                {
                    if (!ClickWindow()) break;
                }
                else
                {
                    if (Mode == ClickMode.Fixed) Native.SetCursorPos(X, Y);
                    Native.SendInput((uint)n, inputs, size);
                }
                Interlocked.Increment(ref Count);

                next += Interval;
                long wait = next - Clock.ElapsedMilliseconds;
                if (wait < 0) { next = Clock.ElapsedMilliseconds; wait = 0; }
                if (StopEvent.WaitOne((int)wait)) break;
            }
        }
        finally { Native.timeEndPeriod(1); }
    }

    // Posts the click to the deepest child window at the target point, so the real
    // cursor is never touched and the window needn't be in front.
    bool ClickWindow()
    {
        if (!Native.IsWindow(Root)) { Error = Loc.T("winClosed"); return false; }
        if (Native.IsIconic(Root)) { Error = Loc.T("winMinimized"); return false; }

        Native.RECT rc;
        Native.GetWindowRect(Root, out rc);
        var pt = new Native.POINT { X = rc.Left + X, Y = rc.Top + Y };

        IntPtr h = Root;
        for (int depth = 0; depth < 32; depth++)
        {
            Native.POINT c = pt;
            Native.ScreenToClient(h, ref c);
            IntPtr child = Native.ChildWindowFromPointEx(h, c, Native.CWP_SKIPINVISIBLE | Native.CWP_SKIPDISABLED | Native.CWP_SKIPTRANSPARENT);
            if (child == IntPtr.Zero || child == h) break;
            h = child;
        }

        Native.POINT cp = pt;
        Native.ScreenToClient(h, ref cp);
        IntPtr lp = (IntPtr)((cp.Y << 16) | (cp.X & 0xFFFF));
        Native.PostMessage(h, Native.WM_MOUSEMOVE, IntPtr.Zero, lp);
        Native.PostMessage(h, DownMsg, (IntPtr)MkFlag, lp);
        Native.PostMessage(h, UpMsg, IntPtr.Zero, lp);
        if (Double)
        {
            Native.PostMessage(h, DblMsg, (IntPtr)MkFlag, lp);
            Native.PostMessage(h, UpMsg, IntPtr.Zero, lp);
        }
        return true;
    }
}

sealed class Controller
{
    public readonly Window Win;

    readonly TextBox xBox, yBox, intervalBox;
    readonly RadioButton modeFixed, modeCursor, modeWindow, btnLeft, btnRight, btnMiddle, typeSingle, typeDouble;
    readonly CheckBox anyKey;
    readonly Button mainBtn, startKeyBtn, stopKeyBtn;
    readonly TextBlock statusText, countText, subText, mainBtnText, mainBtnHint, windowName, windowPos, anyKeyNote, footerNote;
    readonly System.Windows.Shapes.Ellipse statusDot;
    readonly Border windowDot;
    readonly FrameworkElement settingsPanel, fixedPanel, windowPanel;
    readonly TextBlock targetHead, clickHead, hotkeysHead, pickBtnText, pickWinBtnText, intervalLabel, startLabel, stopLabel, anyKeyText, windowNote, cursorNote;
    readonly Button langBtn;

    // Window mode target (not persisted: window handles don't survive a restart)
    IntPtr targetWin;
    int targetX, targetY;

    static readonly Brush Green = Frozen(0xA6, 0xE3, 0xA1), Red = Frozen(0xF3, 0x8B, 0xA8),
                          Amber = Frozen(0xF9, 0xE2, 0xAF), Muted = Frozen(0xA6, 0xAD, 0xC8), Idle = Frozen(0x58, 0x5B, 0x70);

    Hotkey startHk = new Hotkey(0x75, 0), stopHk = new Hotkey(0x76, 0);   // F6 / F7
    Button recording;
    bool picking;
    ClickRun run;

    IntPtr hook;
    Native.HookProc hookProc;   // kept in a field so the GC doesn't collect the callback
    readonly HashSet<int> pressed = new HashSet<int>();

    Window toast;
    FrameworkElement toastRoot;
    readonly DispatcherTimer uiTimer;
    readonly string settingsPath;
    readonly string shotDir;

    public Controller(string[] args)
    {
        if (args.Length == 2 && args[0] == "--shot") shotDir = args[1];

        Win = Load<Window>("Main.xaml");
        Win.Icon = MakeIcon();

        xBox = Find<TextBox>("XBox"); yBox = Find<TextBox>("YBox"); intervalBox = Find<TextBox>("IntervalBox");
        modeFixed = Find<RadioButton>("ModeFixed"); modeCursor = Find<RadioButton>("ModeCursor"); modeWindow = Find<RadioButton>("ModeWindow");
        windowName = Find<TextBlock>("WindowName"); windowPos = Find<TextBlock>("WindowPos"); windowDot = Find<Border>("WindowDot");
        windowPanel = Find<FrameworkElement>("WindowPanel");
        anyKeyNote = Find<TextBlock>("AnyKeyNote"); footerNote = Find<TextBlock>("FooterNote");
        btnLeft = Find<RadioButton>("BtnLeft"); btnRight = Find<RadioButton>("BtnRight"); btnMiddle = Find<RadioButton>("BtnMiddle");
        typeSingle = Find<RadioButton>("TypeSingle"); typeDouble = Find<RadioButton>("TypeDouble");
        anyKey = Find<CheckBox>("AnyKey");
        mainBtn = Find<Button>("MainBtn"); startKeyBtn = Find<Button>("StartKeyBtn"); stopKeyBtn = Find<Button>("StopKeyBtn");
        statusText = Find<TextBlock>("StatusText"); countText = Find<TextBlock>("CountText"); subText = Find<TextBlock>("SubText");
        mainBtnText = Find<TextBlock>("MainBtnText"); mainBtnHint = Find<TextBlock>("MainBtnHint");
        statusDot = Find<System.Windows.Shapes.Ellipse>("StatusDot");
        settingsPanel = Find<FrameworkElement>("SettingsPanel");
        fixedPanel = Find<FrameworkElement>("FixedPanel"); cursorNote = Find<TextBlock>("CursorNote");
        targetHead = Find<TextBlock>("TargetHead"); clickHead = Find<TextBlock>("ClickHead"); hotkeysHead = Find<TextBlock>("HotkeysHead");
        pickBtnText = Find<TextBlock>("PickBtnText"); pickWinBtnText = Find<TextBlock>("PickWinBtnText");
        intervalLabel = Find<TextBlock>("IntervalLabel"); windowNote = Find<TextBlock>("WindowNote");
        startLabel = Find<TextBlock>("StartLabel"); stopLabel = Find<TextBlock>("StopLabel");
        anyKeyText = Find<TextBlock>("AnyKeyText"); langBtn = Find<Button>("LangBtn");

        settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinClicker", "settings.ini");
        if (shotDir == null) LoadSettings();
        else { xBox.Text = "960"; yBox.Text = "540"; }

        DigitsOnly(xBox, true); DigitsOnly(yBox, true); DigitsOnly(intervalBox, false);

        Find<FrameworkElement>("TitleBar").MouseLeftButtonDown += delegate { Win.DragMove(); };
        Find<Button>("MinBtn").Click += delegate { Win.WindowState = WindowState.Minimized; };
        Find<Button>("CloseBtn").Click += delegate { Win.Close(); };
        langBtn.Click += delegate { Loc.Lang = Loc.Lang == "pt" ? "en" : "pt"; SaveSettings(); ApplyLanguage(); };
        Find<Button>("PickBtn").Click += delegate { Pick(false); };
        Find<Button>("PickWinBtn").Click += delegate { Pick(true); };
        mainBtn.Click += delegate { if (run != null) Stop(); else Start(); };
        startKeyBtn.Click += delegate { BeginRecording(startKeyBtn); };
        stopKeyBtn.Click += delegate { BeginRecording(stopKeyBtn); };
        modeFixed.Checked += delegate { UpdateMode(); };
        modeCursor.Checked += delegate { UpdateMode(); };
        modeWindow.Checked += delegate { UpdateMode(); };
        anyKey.Click += delegate { SaveSettings(); };
        Win.Deactivated += delegate { if (recording != null) EndRecording(null); };

        uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        uiTimer.Tick += delegate
        {
            ClickRun r = run;
            if (r != null && r.Error != null) { Stop(false); ShowToast("warn", Loc.T("stopped"), r.Error); return; }
            ShowProgress(r);
        };

        ApplyLanguage();
        UpdateMode();
        UpdateHotkeyTexts();

        Win.Loaded += delegate
        {
            if (shotDir != null) { TakeShots(); return; }
            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) ShowToast("warn", Loc.T("hotkeysUnavail"), Loc.T("hotkeysUnavailMsg"));
        };
        Win.Closed += delegate
        {
            Stop(false);
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            if (shotDir == null) SaveSettings();
        };
    }

    // ---------- helpers ----------

    static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    static T Load<T>(string name)
    {
        using (Stream s = typeof(Controller).Assembly.GetManifestResourceStream(name))
            return (T)XamlReader.Load(s);
    }

    T Find<T>(string name) where T : class { return (T)Win.FindName(name); }

    static void DigitsOnly(TextBox tb, bool allowNegative)
    {
        tb.PreviewTextInput += delegate(object s, TextCompositionEventArgs e)
        {
            foreach (char ch in e.Text)
                if (!(char.IsDigit(ch) || (allowNegative && ch == '-'))) { e.Handled = true; break; }
        };
    }

    static ImageSource MakeIcon()
    {
        var dv = new DrawingVisual();
        using (DrawingContext dc = dv.RenderOpen())
        {
            var g = new LinearGradientBrush(Color.FromRgb(0xCB, 0xA6, 0xF7), Color.FromRgb(0x89, 0xB4, 0xFA), 45);
            dc.DrawRoundedRectangle(g, null, new Rect(0, 0, 64, 64), 16, 16);
            dc.PushTransform(new TranslateTransform(21, 14));
            dc.PushTransform(new ScaleTransform(2.2, 2.2));
            dc.DrawGeometry(Frozen(0x11, 0x11, 0x1B), null, Geometry.Parse("M0,0 L0,14 L4,10.5 L6.5,16 L8.5,15 L6,9.8 L11,9.8 Z"));
        }
        var bmp = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        return BitmapFrame.Create(bmp);
    }

    // ---------- settings ----------

    void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            var d = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(settingsPath))
            {
                int i = line.IndexOf('=');
                if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
            }
            string v;
            if (d.TryGetValue("x", out v)) xBox.Text = v;
            if (d.TryGetValue("y", out v)) yBox.Text = v;
            if (d.TryGetValue("interval", out v)) intervalBox.Text = v;
            if (d.TryGetValue("mode", out v)) { if (v == "cursor") modeCursor.IsChecked = true; else if (v == "window") modeWindow.IsChecked = true; }
            if (d.TryGetValue("button", out v)) { if (v == "right") btnRight.IsChecked = true; else if (v == "middle") btnMiddle.IsChecked = true; }
            if (d.TryGetValue("double", out v) && v == "1") typeDouble.IsChecked = true;
            if (d.TryGetValue("anykey", out v)) anyKey.IsChecked = v == "1";
            if (d.TryGetValue("start", out v)) startHk = Hotkey.Parse(v, startHk);
            if (d.TryGetValue("stop", out v)) stopHk = Hotkey.Parse(v, stopHk);
            if (d.TryGetValue("lang", out v) && (v == "pt" || v == "en")) Loc.Lang = v;
        }
        catch (Exception) { }
    }

    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllLines(settingsPath, new[]
            {
                "x=" + xBox.Text.Trim(),
                "y=" + yBox.Text.Trim(),
                "interval=" + intervalBox.Text.Trim(),
                "mode=" + (modeCursor.IsChecked == true ? "cursor" : modeWindow.IsChecked == true ? "window" : "fixed"),
                "button=" + (btnRight.IsChecked == true ? "right" : btnMiddle.IsChecked == true ? "middle" : "left"),
                "double=" + (typeDouble.IsChecked == true ? "1" : "0"),
                "anykey=" + (anyKey.IsChecked == true ? "1" : "0"),
                "start=" + startHk.Serialize(),
                "stop=" + stopHk.Serialize(),
                "lang=" + Loc.Lang
            });
        }
        catch (Exception) { }
    }

    // ---------- idioma ----------

    void ApplyLanguage()
    {
        Find<Button>("MinBtn").ToolTip = Loc.T("minimize");
        Find<Button>("CloseBtn").ToolTip = Loc.T("close");
        langBtn.ToolTip = Loc.T("langTooltip");
        langBtn.Content = Loc.Lang == "pt" ? "PT-BR" : "EN";
        targetHead.Text = Loc.T("target");
        clickHead.Text = Loc.T("click");
        hotkeysHead.Text = Loc.T("hotkeys");
        modeFixed.Content = Loc.T("modeFixed");
        modeCursor.Content = Loc.T("modeCursor");
        modeWindow.Content = Loc.T("modeWindow");
        pickBtnText.Text = Loc.T("pickScreen");
        pickWinBtnText.Text = Loc.T("pickScreen");
        cursorNote.Text = Loc.T("cursorNote");
        windowNote.Text = Loc.T("windowNote");
        intervalLabel.Text = Loc.T("intervalLabel");
        btnLeft.Content = Loc.T("left");
        btnRight.Content = Loc.T("right");
        btnMiddle.Content = Loc.T("middle");
        typeSingle.Content = Loc.T("single");
        typeDouble.Content = Loc.T("double");
        startLabel.Text = Loc.T("start");
        stopLabel.Text = Loc.T("stop");
        startKeyBtn.ToolTip = Loc.T("hotkeyTip");
        stopKeyBtn.ToolTip = Loc.T("hotkeyTip");
        anyKeyText.Text = Loc.T("anyKey");
        subText.Text = Loc.T("clicksRun");
        UpdateRunningUi();
        UpdateMode();
    }

    // ---------- UI state ----------

    ClickMode CurrentMode()
    {
        return modeWindow.IsChecked == true ? ClickMode.Window : modeCursor.IsChecked == true ? ClickMode.Cursor : ClickMode.Fixed;
    }

    void UpdateMode()
    {
        ClickMode m = CurrentMode();
        fixedPanel.Visibility = m == ClickMode.Fixed ? Visibility.Visible : Visibility.Collapsed;
        cursorNote.Visibility = m == ClickMode.Cursor ? Visibility.Visible : Visibility.Collapsed;
        windowPanel.Visibility = m == ClickMode.Window ? Visibility.Visible : Visibility.Collapsed;

        // In window mode the keyboard is yours to use, so only the stop hotkey stops it
        bool win = m == ClickMode.Window;
        anyKey.IsEnabled = !win;
        anyKey.Opacity = win ? 0.45 : 1;
        anyKeyNote.Text = win ? Loc.T("anyNoteWin") : Loc.T("anyNoteAny");
        footerNote.Text = win ? Loc.T("footerWin")
                              : Loc.T("footerOther");
        UpdateWindowLabel();
    }

    void UpdateWindowLabel()
    {
        bool ok = targetWin != IntPtr.Zero && Native.IsWindow(targetWin);
        if (!ok && targetWin != IntPtr.Zero) targetWin = IntPtr.Zero;
        windowName.Text = ok ? Native.Describe(targetWin) : Loc.T("noWindowYet");
        windowName.Foreground = ok ? (Brush)Win.FindResource("Fg") : Muted;
        windowPos.Text = ok ? "@ " + targetX + ", " + targetY : "";
        windowDot.Background = ok ? Green : Idle;
    }

    void UpdateHotkeyTexts()
    {
        startKeyBtn.Content = startHk.ToString();
        stopKeyBtn.Content = stopHk.ToString();
        mainBtnHint.Text = run != null ? stopHk.ToString() : startHk.ToString();
    }

    void UpdateRunningUi()
    {
        bool on = run != null;
        statusText.Text = on ? Loc.T("clicking") : Loc.T("ready");
        statusText.Foreground = on ? Green : Muted;
        statusDot.Fill = on ? Green : Idle;
        if (on)
            statusDot.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0.25, TimeSpan.FromSeconds(0.55)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        else
            statusDot.BeginAnimation(UIElement.OpacityProperty, null);
        mainBtnText.Text = on ? Loc.T("stop") : Loc.T("start");
        mainBtn.Background = (Brush)Win.FindResource(on ? "StopBrush" : "Accent");
        settingsPanel.IsEnabled = !on;
        settingsPanel.Opacity = on ? 0.5 : 1;
        UpdateHotkeyTexts();
    }

    static string Elapsed(ClickRun r)
    {
        TimeSpan t = r.Clock.Elapsed;
        return string.Format("{0:00}:{1:00}", (int)t.TotalMinutes, t.Seconds);
    }

    void ShowProgress(ClickRun r)
    {
        if (r == null) return;
        countText.Text = Interlocked.Read(ref r.Count).ToString("N0");
        subText.Text = Loc.T("clicksSep") + Elapsed(r);
    }

    // ---------- start / stop ----------

    void Start()
    {
        if (run != null || picking) return;
        if (recording != null) EndRecording(null);

        var r = new ClickRun();
        if (!int.TryParse(intervalBox.Text.Trim(), out r.Interval) || r.Interval < 1)
        {
            ShowToast("warn", Loc.T("invalidInterval"), Loc.T("invalidIntervalMsg"));
            return;
        }
        r.Mode = CurrentMode();
        if (r.Mode == ClickMode.Fixed && (!int.TryParse(xBox.Text.Trim(), out r.X) || !int.TryParse(yBox.Text.Trim(), out r.Y)))
        {
            ShowToast("warn", Loc.T("noTargetLoc"), Loc.T("noTargetLocMsg"));
            return;
        }
        if (r.Mode == ClickMode.Window)
        {
            UpdateWindowLabel();
            if (targetWin == IntPtr.Zero)
            {
                ShowToast("warn", Loc.T("noWindowPick"), Loc.T("noWindowPickMsg"));
                return;
            }
            if (Native.IsIconic(targetWin))
            {
                ShowToast("warn", Loc.T("minimized"), Loc.T("minimizedMsg"));
                return;
            }
            r.Root = targetWin; r.X = targetX; r.Y = targetY;
        }
        r.Double = typeDouble.IsChecked == true;
        if (btnRight.IsChecked == true) { r.DownFlag = 0x0008; r.UpFlag = 0x0010; r.DownMsg = 0x204; r.UpMsg = 0x205; r.DblMsg = 0x206; r.MkFlag = 0x02; }
        else if (btnMiddle.IsChecked == true) { r.DownFlag = 0x0020; r.UpFlag = 0x0040; r.DownMsg = 0x207; r.UpMsg = 0x208; r.DblMsg = 0x209; r.MkFlag = 0x10; }
        else { r.DownFlag = 0x0002; r.UpFlag = 0x0004; r.DownMsg = 0x201; r.UpMsg = 0x202; r.DblMsg = 0x203; r.MkFlag = 0x01; }

        run = r;
        new Thread(r.Loop) { IsBackground = true }.Start();
        uiTimer.Start();
        UpdateRunningUi();
        SaveSettings();
        bool anyStops = r.Mode != ClickMode.Window && anyKey.IsChecked == true;
        ShowToast("start", r.Mode == ClickMode.Window ? Loc.T("clickingIn") + Native.Describe(r.Root) : Loc.T("started"),
            Loc.T("press") + stopHk + (anyStops ? Loc.T("orAny") : "") + Loc.T("toStop"));
    }

    void Stop() { Stop(true); }

    void Stop(bool notify)
    {
        ClickRun r = run;
        if (r == null) return;
        run = null;
        r.StopEvent.Set();
        r.Clock.Stop();
        uiTimer.Stop();
        ShowProgress(r);
        UpdateRunningUi();
        if (notify)
            ShowToast("stop", Loc.T("stopped"), Interlocked.Read(ref r.Count).ToString("N0") + Loc.T("clicksIn") + Elapsed(r));
    }

    // ---------- keyboard hook ----------

    static bool IsDown(int vk) { return (Native.GetAsyncKeyState(vk) & 0x8000) != 0; }

    static int CurrentMods()
    {
        int m = 0;
        if (IsDown(0x11)) m |= Hotkey.Ctrl;
        if (IsDown(0x12)) m |= Hotkey.Alt;
        if (IsDown(0x10)) m |= Hotkey.Shift;
        if (IsDown(0x5B) || IsDown(0x5C)) m |= Hotkey.Win;
        return m;
    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            int vk = (int)((Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT))).vkCode;
            if (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN)
            {
                // pressed.Add is false for auto-repeat while a key is held
                if (pressed.Add(vk) && HandleKeyDown(vk)) return (IntPtr)1;
            }
            else if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP)
            {
                pressed.Remove(vk);
            }
        }
        return Native.CallNextHookEx(hook, nCode, wParam, lParam);
    }

    // Returns true to swallow the key. Heavy work is posted so the hook returns quickly.
    bool HandleKeyDown(int vk)
    {
        if (recording != null)
        {
            if (Hotkey.IsModifier(vk)) return false;
            EndRecording(vk == 0x1B ? null : new Hotkey(vk, CurrentMods()));
            return true;
        }
        if (picking) return false;

        int mods = CurrentMods();
        bool mod = Hotkey.IsModifier(vk);
        bool isStart = !mod && vk == startHk.Vk && mods == startHk.Mods;
        bool isStop = !mod && vk == stopHk.Vk && mods == stopHk.Mods;

        ClickRun r = run;
        if (r != null)
        {
            bool any = r.Mode != ClickMode.Window && anyKey.IsChecked == true;
            if (isStop || any)
            {
                Win.Dispatcher.BeginInvoke(new Action(Stop));
                return isStop || isStart || Win.IsActive;
            }
            return isStart;
        }
        if (isStart)
        {
            Win.Dispatcher.BeginInvoke(new Action(Start));
            return true;
        }
        return false;
    }

    void BeginRecording(Button b)
    {
        if (recording != null) EndRecording(null);
        recording = b;
        b.Tag = "rec";
        b.Content = Loc.T("pressAKey");
    }

    void EndRecording(Hotkey hk)
    {
        Button b = recording;
        recording = null;
        if (b == null) return;
        b.Tag = null;
        if (hk != null)
        {
            if (b == startKeyBtn) startHk = hk; else stopHk = hk;
            SaveSettings();
        }
        UpdateHotkeyTexts();
    }

    // ---------- location picker ----------

    void Pick(bool pickWindow)
    {
        if (recording != null) EndRecording(null);

        var coord = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Frozen(0xCD, 0xD6, 0xF4) };
        var winLine = new TextBlock { FontSize = 12, Foreground = Frozen(0xB4, 0xBE, 0xFE), FontWeight = FontWeights.SemiBold,
                                      MaxWidth = 360, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0),
                                      Visibility = pickWindow ? Visibility.Visible : Visibility.Collapsed };
        var hint = new TextBlock { Text = pickWindow ? Loc.T("pickWinHint") : Loc.T("pickPointHint"),
                                   FontSize = 11.5, Foreground = Muted, Margin = new Thickness(0, 2, 0, 0) };
        var sp = new StackPanel();
        sp.Children.Add(coord);
        sp.Children.Add(winLine);
        sp.Children.Add(hint);
        var label = new Border
        {
            Background = Frozen(0x18, 0x18, 0x25), BorderBrush = Frozen(0xCB, 0xA6, 0xF7), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Child = sp, IsHitTestVisible = false
        };
        var canvas = new Canvas();
        canvas.Children.Add(label);

        var w = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0)), Topmost = true, ShowInTaskbar = false,
            Cursor = Cursors.Cross, FontFamily = new FontFamily("Segoe UI"), Content = canvas,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = SystemParameters.VirtualScreenLeft, Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth, Height = SystemParameters.VirtualScreenHeight
        };

        IntPtr overlay = IntPtr.Zero, hoverWin = IntPtr.Zero;
        Action<Point> place = delegate(Point p)
        {
            Native.POINT c;
            Native.GetCursorPos(out c);
            coord.Text = "X " + c.X + "    Y " + c.Y;
            if (pickWindow && overlay != IntPtr.Zero)
            {
                hoverWin = Native.TopLevelAt(c, overlay);
                winLine.Text = hoverWin == IntPtr.Zero ? Loc.T("noWindowHere") : Native.Describe(hoverWin);
            }
            label.UpdateLayout();
            double lx = p.X + 20, ly = p.Y + 20;
            if (lx + label.ActualWidth > canvas.ActualWidth) lx = p.X - 20 - label.ActualWidth;
            if (ly + label.ActualHeight > canvas.ActualHeight) ly = p.Y - 20 - label.ActualHeight;
            Canvas.SetLeft(label, lx);
            Canvas.SetTop(label, ly);
        };

        bool picked = false;
        Native.POINT target = new Native.POINT();
        w.SourceInitialized += delegate
        {
            // cover every monitor in physical pixels, whatever their individual scaling
            overlay = new WindowInteropHelper(w).Handle;
            Native.SetWindowPos(overlay, IntPtr.Zero,
                Native.GetSystemMetrics(76), Native.GetSystemMetrics(77), Native.GetSystemMetrics(78), Native.GetSystemMetrics(79), 0x0004 | 0x0010);
        };
        w.Loaded += delegate { place(Mouse.GetPosition(canvas)); };
        w.MouseMove += delegate(object s, MouseEventArgs e) { place(e.GetPosition(canvas)); };
        w.MouseLeftButtonDown += delegate
        {
            Native.GetCursorPos(out target);
            if (pickWindow)
            {
                hoverWin = Native.TopLevelAt(target, overlay);
                if (hoverWin == IntPtr.Zero) return;   // clicked on bare desktop; keep picking
            }
            picked = true;
            w.Close();
        };
        w.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) w.Close(); };

        picking = true;
        Win.Hide();
        try { w.ShowDialog(); }
        finally
        {
            picking = false;
            Win.Show();
            Win.Activate();
        }
        if (!picked) return;
        if (pickWindow)
        {
            Native.RECT rc;
            Native.GetWindowRect(hoverWin, out rc);
            targetWin = hoverWin;
            targetX = target.X - rc.Left;
            targetY = target.Y - rc.Top;
            UpdateWindowLabel();
        }
        else
        {
            xBox.Text = target.X.ToString();
            yBox.Text = target.Y.ToString();
            SaveSettings();
        }
    }

    // ---------- toast popup ----------

    void ShowToast(string kind, string title, string sub)
    {
        if (toast != null) toast.Close();

        Window t = Load<Window>("Toast.xaml");
        var root = (FrameworkElement)t.FindName("Root");
        t.Title = Loc.T("toastTitle");
        ((TextBlock)t.FindName("TitleText")).Text = title;
        ((TextBlock)t.FindName("SubText")).Text = sub;
        var icon = (Border)t.FindName("IconBox");
        var glyph = (System.Windows.Shapes.Path)t.FindName("Glyph");
        if (kind == "start") { icon.Background = Green; glyph.Data = Geometry.Parse("M0,0 L12,7.5 L0,15 Z"); glyph.Margin = new Thickness(3, 0, 0, 0); }
        else if (kind == "stop") { icon.Background = Red; glyph.Data = Geometry.Parse("M0,0 H12 V12 H0 Z"); }
        else { icon.Background = Amber; glyph.Data = Geometry.Parse("M0,0 H3 V10 H0 Z M0,12.5 H3 V15.5 H0 Z"); }

        var slide = new TranslateTransform(0, 14);
        root.RenderTransform = slide;

        t.SourceInitialized += delegate
        {
            // never takes focus and lets clicks pass straight through
            IntPtr h = new WindowInteropHelper(t).Handle;
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, Native.GetWindowLong(h, Native.GWL_EXSTYLE)
                | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW);
        };
        t.Loaded += delegate
        {
            Rect wa = SystemParameters.WorkArea;
            t.Left = wa.Left + (wa.Width - t.ActualWidth) / 2;
            t.Top = wa.Bottom - t.ActualHeight - 40;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        timer.Tick += delegate
        {
            timer.Stop();
            if (toast != t) return;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
            fade.Completed += delegate { if (toast == t) { toast = null; t.Close(); } };
            root.BeginAnimation(UIElement.OpacityProperty, fade);
        };
        t.Closed += delegate { timer.Stop(); };

        toast = t;
        toastRoot = root;
        t.Show();
        timer.Start();
    }

    // ---------- design preview (WinClicker.exe --shot <dir>) ----------

    static void Shot(FrameworkElement el, string path)
    {
        const double pad = 24, scale = 2;
        double w = el.ActualWidth + pad * 2, h = el.ActualHeight + pad * 2;
        var dv = new DrawingVisual();
        using (DrawingContext dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Frozen(0x31, 0x32, 0x44), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(el) { Stretch = Stretch.None }, null, new Rect(pad, pad, el.ActualWidth, el.ActualHeight));
        }
        var bmp = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using (FileStream fs = File.Create(path)) enc.Save(fs);
    }

    void TakeShots()
    {
        var steps = new Queue<Action>();
        steps.Enqueue(delegate { Shot((FrameworkElement)Win.Content, Path.Combine(shotDir, "main.png")); ShowToast("start", Loc.T("started"), Loc.T("f7Demo")); });
        steps.Enqueue(delegate { Shot(toastRoot, Path.Combine(shotDir, "toast.png")); });
        steps.Enqueue(delegate
        {
            targetWin = new WindowInteropHelper(Win).Handle; targetX = 412; targetY = 318;
            modeWindow.IsChecked = true;
            Win.UpdateLayout();
            Shot((FrameworkElement)Win.Content, Path.Combine(shotDir, "window.png"));
            Win.Close();
        });
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += delegate { steps.Dequeue()(); if (steps.Count == 0) timer.Stop(); };
        timer.Start();
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool created;
        using (new Mutex(true, "WinClicker.SingleInstance.7c5cff", out created))
        {
            if (!created && args.Length == 0) return;
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var c = new Controller(args);
            app.MainWindow = c.Win;
            app.Run(c.Win);
        }
    }
}
