using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SunPet;
public sealed class PetWindow : Window
{
    public Settings Settings { get; }
    public PlaybackSelection Selection { get; }
    public System.Collections.Generic.List<Clip> Clips { get; }
    private readonly SettingsStore store;
    private readonly AudioEngine audio = new();
    private readonly PetBridge bridge = new(Profile.Role);
    private readonly Image face;
    private readonly Border caption;
    private readonly TextBlock captionText;
    private readonly RotateTransform rotation = new();
    private readonly TranslateTransform translation = new();
    private readonly ScaleTransform scale = new(1, 1);
    private readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromSeconds(1.0 / 30) };
    private readonly DispatcherTimer captionTimer = new();
    private Forms.NotifyIcon? tray;
    private CancellationTokenSource? pending;
    private double phase;
    private bool dragging, pressed, disposed;
    private Native.Point downCursor;
    private Native.Rect downWindow;
    private readonly bool testMode;
    public bool Speaking { get; private set; }
    public bool IsPlaying => audio.IsPlaying;
    public int ActiveOutputs => audio.ActiveOutputs;
    public bool AtRest => !Speaking && rotation.Angle == 0 && translation.Y == 0 && scale.ScaleY == 1;
    private double WindowHeight => Profile.VerticalBounce && face?.Source is BitmapSource bitmap ? (Settings.Size - 16) * bitmap.PixelHeight / bitmap.PixelWidth + 52 : Settings.Size + 48;

    public PetWindow(bool testMode = false, SettingsStore? settingsStore = null)
    {
        this.testMode = testMode;
        Clips = Catalog.Load(); store = settingsStore ?? new(); Settings = store.Load(Clips); Selection = new(Clips, Settings);
        Title = Profile.Title; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = Program.UiCheck; ShowActivated = false;
        if (testMode || Program.IntegrationTest) IsHitTestVisible = false;
        Width = Settings.Size; Height = Settings.Size + 48;
        var grid = new Grid { Background = null };
        face = new Image { Source = new BitmapImage(new Uri(Path.Combine(Catalog.Assets, "face.png"))), Stretch = Stretch.Uniform,
            Margin = new Thickness(12, 40, 12, 12), RenderTransformOrigin = new Point(.5, .5), ToolTip = "点击播放 · 拖动移动 · 右键设置" };
        if (Profile.VerticalBounce) { face.Margin = new Thickness(8, 44, 8, 8); face.RenderTransformOrigin = new Point(.5, 1); }
        Height = WindowHeight;
        var transforms = new TransformGroup(); transforms.Children.Add(rotation); transforms.Children.Add(translation); transforms.Children.Add(scale); face.RenderTransform = transforms;
        AutomationProperties.SetName(face, Profile.DisplayName + "，点击播放，拖动移动，右键设置");
        grid.Children.Add(face);
        captionText = new TextBlock { Foreground = Brushes.White, FontSize = 12, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis };
        caption = new Border { Background = new SolidColorBrush(Color.FromArgb(184, 0, 0, 0)), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Top,
            Child = captionText, Visibility = Visibility.Hidden, IsHitTestVisible = false };
        grid.Children.Add(caption); Content = grid;
        face.MouseLeftButtonDown += (_, e) =>
        {
            pressed = true; dragging = false; Native.GetCursorPos(out downCursor); Native.GetWindowRect(Native.Handle(this), out downWindow); face.CaptureMouse(); e.Handled = true;
        };
        face.MouseMove += (_, _) =>
        {
            if (!pressed) return;
            Native.GetCursorPos(out var p); var dpi = VisualTreeHelper.GetDpi(this);
            var dx = p.X - downCursor.X; var dy = p.Y - downCursor.Y;
            if (Math.Sqrt(dx * dx + dy * dy) > 4 * dpi.DpiScaleX) dragging = true;
            if (dragging) Native.Move(this, downWindow.Left + dx, downWindow.Top + dy);
        };
        face.MouseLeftButtonUp += (_, e) =>
        {
            if (!pressed) return;
            pressed = false; face.ReleaseMouseCapture();
            if (dragging) { Native.EnsureVisible(this); SavePosition(); Diagnostics.Log("drag saved"); } else PlayNext();
            e.Handled = true;
        };
        face.LostMouseCapture += (_, _) => { pressed = false; };
        face.MouseRightButtonUp += (_, e) => { OpenMenu(); e.Handled = true; };
        animation.Tick += (_, _) =>
        {
            if (Profile.VerticalBounce) { phase += .60; scale.ScaleY = 1 - .12 * (1 - Math.Cos(phase)) / 2; }
            else { phase += .38; rotation.Angle = Math.Sin(phase) * .055 * 180 / Math.PI; translation.Y = -Math.Abs(Math.Sin(phase)) * 3; }
        };
        captionTimer.Tick += (_, _) => { captionTimer.Stop(); caption.Visibility = Visibility.Hidden; };
        audio.Finished += error => Dispatcher.BeginInvoke(() =>
        {
            // A queued completion from an interrupted clip must never stop its successor's animation.
            if (audio.IsPlaying || disposed) return;
            Animate(false); if (error != null) ShowCaption("播放失败：" + error.Message, 5);
        });
        SourceInitialized += (_, _) =>
        {
            Native.NonActivating(this);
            HwndSource.FromHwnd(Native.Handle(this)).AddHook(WindowMessage);
        };
        Loaded += (_, _) =>
        {
            Diagnostics.Log("loaded");
            if (Settings.X is int x && Settings.Y is int y) Native.Move(this, x, y); else Native.EnsureVisible(this, true);
            Native.EnsureVisible(this); SavePosition();
            Diagnostics.Log("position ready");
            audio.Preload(Clips);
            Diagnostics.Log("preload ready");
            SetupTray();
            Diagnostics.Log("tray ready");
            if (!testMode)
            {
                bridge.Spoke += m => Dispatcher.BeginInvoke(() => Schedule(m));
                bridge.Activate += () => Dispatcher.BeginInvoke(() => { Native.EnsureVisible(this); ShowCaption("桌宠已经在这里 · 右键设置", 3); });
                bridge.Quit += () => Dispatcher.BeginInvoke(Close);
                bridge.Start();
                ShowCaption(store.Warning ?? "点我一下 · 右键设置", 5);
            }
        };
        Closed += (_, _) => Cleanup();
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (msg == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (msg == 0x7E || msg == 0x2E0 || msg == 0x1A)
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => { if (!disposed) { Native.EnsureVisible(this); SavePosition(); } });
        return IntPtr.Zero;
    }
    public void ShowCaption(string text, double seconds = 3)
    {
        captionTimer.Stop(); captionText.Text = text; caption.Visibility = Visibility.Visible;
        captionTimer.Interval = TimeSpan.FromSeconds(Math.Max(.1, seconds)); captionTimer.Start();
    }
    private void Animate(bool enabled)
    {
        animation.Stop(); phase = 0; rotation.Angle = 0; translation.Y = 0; scale.ScaleY = 1; Speaking = enabled;
        if (enabled && (!Profile.VerticalBounce || SystemParameters.ClientAreaAnimation)) animation.Start();
        Diagnostics.Log("animation=" + enabled);
    }
    public void PlayNext(bool forceRandom = false) => Play(Selection.Next(forceRandom));
    public void Play(Clip clip, int hop = 0, string? root = null)
    {
        CancelPending();
        try
        {
            var duration = audio.Play(clip, Settings.Volume); Selection.Played(clip); Animate(true); ShowCaption(clip.Label, duration + .5);
            Diagnostics.Log($"play clip={clip.Id} duration={duration:F3} hop={hop} root={root ?? Profile.Role} outputs={audio.ActiveOutputs}");
            if (!testMode && Settings.BridgeEnabled) _ = bridge.Announce(new(Profile.Role, clip.Id, duration, clip.Label, hop, root ?? Profile.Role));
        }
        catch (Exception e) { Animate(false); ShowCaption("播放失败：" + e.Message, 5); Diagnostics.Log("play-error " + e); }
    }
    private async void Schedule(BridgeMessage message)
    {
        if (!Settings.BridgeEnabled || !BridgeRules.Respond(Profile.Role, message)) return;
        CancelPending(); var clip = Selection.Companion(); if (clip == null) return;
        var cancellation = pending = new();
        try
        {
            var delay = BridgeRules.Delay(message.Duration, message.Hop, audio.Decode(clip).LeadingSilence);
            Diagnostics.Log($"schedule hop={message.Hop} delay={delay:F3} clip={clip.Id}");
            await Task.Delay(TimeSpan.FromSeconds(delay), cancellation.Token);
            if (!disposed && Settings.BridgeEnabled && !audio.IsPlaying && !Settings.ExcludedIds.Contains(clip.Id)) Play(clip, message.Hop + 1, message.Root);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Diagnostics.Log("companion-error " + e.Message); }
    }
    private void CancelPending() { pending?.Cancel(); pending?.Dispose(); pending = null; }
    public void StopPlayback() { CancelPending(); audio.Stop(); Animate(false); captionTimer.Stop(); caption.Visibility = Visibility.Hidden; }
    public void Save() { if (!store.Save(Settings)) ShowCaption(store.Warning!, 5); }
    public void SavePosition()
    {
        if (Native.Handle(this) == IntPtr.Zero) return;
        Native.GetWindowRect(Native.Handle(this), out var r); Settings.X = r.Left; Settings.Y = r.Top; Save();
    }
    private void SetupTray()
    {
        using var bitmap = new System.Drawing.Bitmap(Path.Combine(Catalog.Assets, "face.png"));
        using var small = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(32, 32));
        var handle = small.GetHicon();
        using var borrowed = System.Drawing.Icon.FromHandle(handle);
        var icon = (System.Drawing.Icon)borrowed.Clone(); Native.DestroyIcon(handle);
        tray = new Forms.NotifyIcon { Icon = icon, Text = Profile.DisplayName, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right) Dispatcher.Invoke(OpenMenu); };
    }
    public ContextMenu MakeMenu()
    {
        var menu = new ContextMenu { MaxHeight = SystemParameters.WorkArea.Height - 20 };
        MenuItem Item(string title, Action? action = null, bool check = false)
        {
            var item = new MenuItem { Header = title, IsChecked = check };
            if (action != null) item.Click += (_, _) => action(); return item;
        }
        menu.Items.Add(new MenuItem { Header = Profile.DisplayName + $"（{Clips.Count}句）", IsEnabled = false });
        var mode = Item("播放方式");
        mode.Items.Add(Item("随机一句（一轮不重复）", () => { Settings.RandomMode = true; Save(); ShowCaption("播放方式：随机一句"); }, Settings.RandomMode));
        var pinned = Clips.FirstOrDefault(c => c.Id == Settings.PinnedId);
        mode.Items.Add(Item("固定播一句" + (pinned == null ? "" : $"（{pinned.Id:00} {pinned.Label}）"), () =>
        { Settings.RandomMode = false; Settings.PinnedId ??= Selection.LastId ?? Clips[0].Id; Save(); ShowCaption("已固定：" + Clips.First(c => c.Id == Settings.PinnedId).Label); }, !Settings.RandomMode));
        menu.Items.Add(mode);
        var choose = Item("挑一句");
        foreach (var clip in Clips) choose.Items.Add(Item($"{clip.Id:00}  {clip.Label}", () => { if (!Settings.RandomMode) { Settings.PinnedId = clip.Id; Save(); } Play(clip); }, !Settings.RandomMode && clip.Id == Settings.PinnedId));
        menu.Items.Add(choose);
        var filter = Item("轮播内容");
        filter.Items.Add(new MenuItem { Header = "取消勾选＝不参与随机播（至少一句）", IsEnabled = false });
        foreach (var clip in Clips)
        {
            var item = Item($"{clip.Id:00}  {clip.Label}"); item.IsCheckable = true; item.IsChecked = !Settings.ExcludedIds.Contains(clip.Id); item.StaysOpenOnClick = true;
            item.Click += (_, _) => { CancelPending(); if (!Selection.Toggle(clip.Id)) ShowCaption("至少留一句在轮播里"); item.IsChecked = !Settings.ExcludedIds.Contains(clip.Id); Save(); };
            filter.Items.Add(item);
        }
        filter.Items.Add(new Separator());
        filter.Items.Add(Item("全部恢复", () => { CancelPending(); Selection.Restore(); Save(); ShowCaption("轮播已恢复全部"); })); menu.Items.Add(filter);
        menu.Items.Add(Item("随机播一次", () => PlayNext(true))); menu.Items.Add(Item("停止播放", StopPlayback)); menu.Items.Add(new Separator());
        menu.Items.Add(Item(Settings.BridgeEnabled ? "关闭捧哏联动" : "开启捧哏联动", () => { Settings.BridgeEnabled = !Settings.BridgeEnabled; CancelPending(); Save(); }, Settings.BridgeEnabled));
        menu.Items.Add(new MenuItem { Header = "捧哏：" + bridge.Summary, IsEnabled = false });
        if (!Profile.VerticalBounce) menu.Items.Add(new MenuItem { Header = "机身摇晃：此 Windows 版本暂不支持", IsEnabled = false });
        var volume = Item("音量");
        foreach (var n in new[] { 0, 25, 50, 65, 80, 100 }) volume.Items.Add(Item(n == 0 ? "0%（静音）" : n + "%", () => { Settings.Volume = n / 100.0; audio.SetVolume(Settings.Volume); Save(); }, Math.Abs(Settings.Volume - n / 100.0) < .01));
        menu.Items.Add(volume);
        var size = Item("头像大小");
        foreach (var entry in new[] { ("小", Profile.Sizes[0]), ("中", Profile.Sizes[1]), ("大", Profile.Sizes[2]) }) size.Items.Add(Item(entry.Item1, () =>
        { Settings.Size = entry.Item2; Width = Settings.Size; Height = WindowHeight; Dispatcher.BeginInvoke(() => { Native.EnsureVisible(this); SavePosition(); }); }, Settings.Size == entry.Item2));
        menu.Items.Add(size);
        menu.Items.Add(Item("移回屏幕右下角", () => { Native.EnsureVisible(this, true); SavePosition(); })); menu.Items.Add(new Separator());
        menu.Items.Add(Item("退出桌宠", Close)); return menu;
    }
    public void OpenMenu()
    {
        var menu = MakeMenu(); ContextMenu = menu; menu.PlacementTarget = face;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint; menu.IsOpen = true;
    }
    private void Cleanup()
    {
        if (disposed) return;
        SavePosition(); disposed = true; CancelPending(); animation.Stop(); captionTimer.Stop(); audio.Dispose(); bridge.Dispose();
        if (tray != null) { tray.Visible = false; var icon = tray.Icon; tray.Dispose(); icon?.Dispose(); tray = null; }
        Diagnostics.Log("closed: audio, tray, ipc disposed");
    }
}

