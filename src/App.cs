using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Drawing;
using System.Drawing.Imaging;

// アセンブリ情報・バージョニング (v1.1.4)
[assembly: AssemblyTitle("Blue Archive Timer Overlay")]
[assembly: AssemblyDescription("Lightweight, ultra-low-latency timer overlay for Blue Archive")]
[assembly: AssemblyProduct("BlueArchiveTimerOverlay")]
[assembly: AssemblyVersion("1.1.4.0")]
[assembly: AssemblyFileVersion("1.1.4.0")]
[assembly: AssemblyInformationalVersion("v1.1.4")]

namespace BATimerOverlay
{
    // --- 設定データクラス (C# 5 準拠) ---
    public class Config
    {
        public const string CurrentVersion = "v1.1.4";

        public int CaptureX { get; set; }
        public int CaptureY { get; set; }
        public int CaptureW { get; set; }
        public int CaptureH { get; set; }
        public int OverlayX { get; set; }
        public int OverlayY { get; set; }
        public double Scale { get; set; }
        public int TargetFps { get; set; }

        public Config()
        {
            CaptureX = -1;
            CaptureY = -1;
            CaptureW = -1;
            CaptureH = -1;
            OverlayX = -1;
            OverlayY = -1;
            Scale = 1.25;
            TargetFps = 60;
        }

        public bool HasCaptureRect
        {
            get { return CaptureW > 0 && CaptureH > 0 && CaptureX >= 0 && CaptureY >= 0; }
        }

        private static string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"); }
        }

        public static Config Load()
        {
            Config cfg = new Config();
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    cfg.CaptureX = ExtractInt(json, "CaptureX", -1);
                    cfg.CaptureY = ExtractInt(json, "CaptureY", -1);
                    cfg.CaptureW = ExtractInt(json, "CaptureW", -1);
                    cfg.CaptureH = ExtractInt(json, "CaptureH", -1);
                    cfg.OverlayX = ExtractInt(json, "OverlayX", -1);
                    cfg.OverlayY = ExtractInt(json, "OverlayY", -1);
                    cfg.Scale = ExtractDouble(json, "Scale", 1.25);
                    cfg.TargetFps = ExtractInt(json, "TargetFps", 60);

                    // 互換性パース
                    if (cfg.CaptureW <= 0)
                    {
                        int rectIdx = json.IndexOf("\"capture_rect\"");
                        if (rectIdx != -1)
                        {
                            cfg.CaptureX = ExtractIntFromSection(json, rectIdx, "x", -1);
                            cfg.CaptureY = ExtractIntFromSection(json, rectIdx, "y", -1);
                            cfg.CaptureW = ExtractIntFromSection(json, rectIdx, "width", -1);
                            cfg.CaptureH = ExtractIntFromSection(json, rectIdx, "height", -1);
                        }
                    }
                    if (cfg.OverlayX < 0)
                    {
                        int posIdx = json.IndexOf("\"overlay_pos\"");
                        if (posIdx != -1)
                        {
                            cfg.OverlayX = ExtractIntFromSection(json, posIdx, "x", -1);
                            cfg.OverlayY = ExtractIntFromSection(json, posIdx, "y", -1);
                        }
                    }
                    if (Math.Abs(cfg.Scale - 1.25) < 0.001)
                    {
                        cfg.Scale = ExtractDouble(json, "scale", 1.25);
                    }
                    if (cfg.TargetFps == 60)
                    {
                        cfg.TargetFps = ExtractInt(json, "target_fps", 60);
                    }
                }
                catch { }
            }
            return cfg;
        }

        private static int ExtractIntFromSection(string json, int startIdx, string key, int def)
        {
            int idx = json.IndexOf("\"" + key + "\"", startIdx);
            if (idx == -1) return def;
            int colon = json.IndexOf(':', idx);
            if (colon == -1) return def;
            int end = json.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end == -1) end = json.Length;
            string val = json.Substring(colon + 1, end - colon - 1).Trim();
            int res;
            return int.TryParse(val, out res) ? res : def;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"version\": \"{0}\",", CurrentVersion));
                sb.AppendLine(string.Format("  \"CaptureX\": {0},", CaptureX));
                sb.AppendLine(string.Format("  \"CaptureY\": {0},", CaptureY));
                sb.AppendLine(string.Format("  \"CaptureW\": {0},", CaptureW));
                sb.AppendLine(string.Format("  \"CaptureH\": {0},", CaptureH));
                sb.AppendLine(string.Format("  \"OverlayX\": {0},", OverlayX));
                sb.AppendLine(string.Format("  \"OverlayY\": {0},", OverlayY));
                sb.AppendLine(string.Format("  \"Scale\": {0},", Scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
                sb.AppendLine(string.Format("  \"TargetFps\": {0}", TargetFps));
                sb.AppendLine("}");
                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch { }
        }

        private static int ExtractInt(string json, string key, int def)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx == -1) return def;
            int colon = json.IndexOf(':', idx);
            if (colon == -1) return def;
            int end = json.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end == -1) end = json.Length;
            string val = json.Substring(colon + 1, end - colon - 1).Trim();
            int res;
            return int.TryParse(val, out res) ? res : def;
        }

        private static double ExtractDouble(string json, string key, double def)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx == -1) return def;
            int colon = json.IndexOf(':', idx);
            if (colon == -1) return def;
            int end = json.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end == -1) end = json.Length;
            string val = json.Substring(colon + 1, end - colon - 1).Trim();
            double res;
            return double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out res) ? res : def;
        }
    }

    // --- Win32 ネイティブ API ---
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        public const int SRCCOPY = 0x00CC0020;
        public const int CAPTUREBLT = 0x40000000;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    // --- 超低遅延 GDI キャプチャ ---
    public class ScreenCapture : IDisposable
    {
        private IntPtr _hDesktopDc = IntPtr.Zero;
        private IntPtr _hMemDc = IntPtr.Zero;
        private IntPtr _hBitmap = IntPtr.Zero;
        private IntPtr _hOldBitmap = IntPtr.Zero;
        private int _w = 0, _h = 0;

        public void Init(int width, int height)
        {
            Cleanup();
            _w = width;
            _h = height;
            _hDesktopDc = NativeMethods.GetDC(IntPtr.Zero);
            _hMemDc = NativeMethods.CreateCompatibleDC(_hDesktopDc);
            _hBitmap = NativeMethods.CreateCompatibleBitmap(_hDesktopDc, width, height);
            _hOldBitmap = NativeMethods.SelectObject(_hMemDc, _hBitmap);
        }

        public BitmapSource Capture(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            if (_w != width || _h != height || _hMemDc == IntPtr.Zero)
            {
                Init(width, height);
            }

            NativeMethods.BitBlt(_hMemDc, 0, 0, width, height, _hDesktopDc, x, y, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);

            BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(
                _hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }

        private void Cleanup()
        {
            if (_hMemDc != IntPtr.Zero && _hOldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(_hMemDc, _hOldBitmap);
            }
            if (_hBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(_hBitmap);
                _hBitmap = IntPtr.Zero;
            }
            if (_hMemDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(_hMemDc);
                _hMemDc = IntPtr.Zero;
            }
            if (_hDesktopDc != IntPtr.Zero)
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, _hDesktopDc);
                _hDesktopDc = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Cleanup();
        }
    }

    // --- 範囲選択キャンバス (Snipping Tool風 + 4倍ルーペ + リアルタイムシアン枠) ---
    public class SelectionCanvas : FrameworkElement
    {
        private BitmapSource _screenBmpSource;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _currentPoint;
        private bool _isSelecting = false;
        private System.Windows.Rect _currentRect = System.Windows.Rect.Empty;

        public event Action<int, int, int, int> AreaSelected;
        public event Action SelectionCancelled;

        public SelectionCanvas(BitmapSource screenBmpSource)
        {
            _screenBmpSource = screenBmpSource;
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.Cross;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(this);
                _currentPoint = _startPoint;
                _isSelecting = true;
                _currentRect = System.Windows.Rect.Empty;
                CaptureMouse();
                InvalidateVisual();
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                Cancel();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _currentPoint = e.GetPosition(this);
            if (_isSelecting)
            {
                double x = Math.Min(_startPoint.X, _currentPoint.X);
                double y = Math.Min(_startPoint.Y, _currentPoint.Y);
                double w = Math.Abs(_startPoint.X - _currentPoint.X);
                double h = Math.Abs(_startPoint.Y - _currentPoint.Y);
                _currentRect = new System.Windows.Rect(x, y, w, h);
            }
            InvalidateVisual();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Released)
            {
                _isSelecting = false;
                ReleaseMouseCapture();
                if (_currentRect.Width > 10 && _currentRect.Height > 10)
                {
                    if (AreaSelected != null)
                    {
                        AreaSelected((int)_currentRect.X, (int)_currentRect.Y, (int)_currentRect.Width, (int)_currentRect.Height);
                    }
                }
                else
                {
                    _currentRect = System.Windows.Rect.Empty;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
            }
        }

        private void Cancel()
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                ReleaseMouseCapture();
            }
            if (SelectionCancelled != null)
            {
                SelectionCancelled();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // 1. スクリーンショット全体の描画
            dc.DrawImage(_screenBmpSource, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));

            // 2. 画面全体を暗い半透明ダークマスク (RGBA: 0, 0, 0, 150)
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 0, 0)), null, new System.Windows.Rect(0, 0, ActualWidth, ActualHeight));

            // 3. 選択範囲のくり抜き表示 & 枠線
            if (_currentRect.Width > 0 && _currentRect.Height > 0)
            {
                try
                {
                    Int32Rect cropRect = new Int32Rect((int)_currentRect.X, (int)_currentRect.Y, (int)_currentRect.Width, (int)_currentRect.Height);
                    CroppedBitmap crop = new CroppedBitmap(_screenBmpSource, cropRect);
                    dc.DrawImage(crop, _currentRect);
                }
                catch { }

                // 鮮やかなシアン枠線 (2px)
                System.Windows.Media.Pen cyanPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 255)), 2);
                dc.DrawRectangle(null, cyanPen, _currentRect);

                // サイズ情報バッジ (例: 205 × 54 px)
                string sizeText = string.Format("{0} × {1} px", (int)_currentRect.Width, (int)_currentRect.Height);
                FormattedText ft = new FormattedText(
                    sizeText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    12,
                    System.Windows.Media.Brushes.White);

                double bW = ft.Width + 16;
                double bH = ft.Height + 6;
                double bX = _currentRect.Left;
                double bY = Math.Max(10, _currentRect.Top - bH - 4);
                System.Windows.Rect badgeRect = new System.Windows.Rect(bX, bY, bW, bH);
                dc.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 15, 23, 42)), null, badgeRect, 4, 4);
                dc.DrawText(ft, new System.Windows.Point(bX + 8, bY + 3));
            }

            // 4. 上部中央のガイダンスバナー
            string guideText = "【トリミング範囲の指定】ブルアカの「残り時間（0.00s）」をマウスの左ドラッグで囲んでください (Escでキャンセル)";
            FormattedText guideFt = new FormattedText(
                guideText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                13,
                System.Windows.Media.Brushes.White);

            double gw = guideFt.Width + 36;
            double gh = guideFt.Height + 16;
            double gx = (ActualWidth - gw) / 2;
            double gy = 25;
            System.Windows.Rect gRect = new System.Windows.Rect(gx, gy, gw, gh);
            dc.DrawRoundedRectangle(
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 15, 23, 42)),
                new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 255)), 1.5),
                gRect,
                8,
                8);
            dc.DrawText(guideFt, new System.Windows.Point(gx + 18, gy + 8));

            // 5. 4倍拡大ルーペ
            DrawMagnifier(dc, _currentPoint);
        }

        private void DrawMagnifier(DrawingContext dc, System.Windows.Point pos)
        {
            int magSize = 130;
            int zoom = 4;
            int srcSize = magSize / zoom;

            double mx = pos.X + 25;
            double my = pos.Y + 25;
            if (mx + magSize > ActualWidth) mx = pos.X - magSize - 25;
            if (my + magSize > ActualHeight) my = pos.Y - magSize - 25;

            int sx = (int)pos.X - srcSize / 2;
            int sy = (int)pos.Y - srcSize / 2;
            if (sx < 0) sx = 0;
            if (sy < 0) sy = 0;
            if (sx + srcSize > _screenBmpSource.PixelWidth) sx = _screenBmpSource.PixelWidth - srcSize;
            if (sy + srcSize > _screenBmpSource.PixelHeight) sy = _screenBmpSource.PixelHeight - srcSize;

            System.Windows.Rect magRect = new System.Windows.Rect(mx, my, magSize, magSize);
            try
            {
                CroppedBitmap crop = new CroppedBitmap(_screenBmpSource, new Int32Rect(sx, sy, srcSize, srcSize));
                dc.DrawImage(crop, magRect);
            }
            catch { }

            // 枠線 (シアン 2px)
            dc.DrawRectangle(null, new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 255)), 2), magRect);

            // 十字クロスヘア (赤 1px)
            double cx = magRect.Left + magSize / 2;
            double cy = magRect.Top + magSize / 2;
            System.Windows.Media.Pen redPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 60, 60)), 1);
            dc.DrawLine(redPen, new System.Windows.Point(cx, magRect.Top), new System.Windows.Point(cx, magRect.Bottom));
            dc.DrawLine(redPen, new System.Windows.Point(magRect.Left, cy), new System.Windows.Point(magRect.Right, cy));
        }
    }

    // --- 範囲選択ウィンドウ ---
    public class AreaSelectorWindow : Window
    {
        public event Action<int, int, int, int> AreaSelected;
        public event Action SelectionCancelled;

        public AreaSelectorWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            int w = (int)Width;
            int h = (int)Height;
            Bitmap screenBmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(screenBmp))
            {
                g.CopyFromScreen((int)Left, (int)Top, 0, 0, new System.Drawing.Size(w, h));
            }
            IntPtr hBitmap = screenBmp.GetHbitmap();
            BitmapSource screenBmpSource = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            screenBmpSource.Freeze();
            NativeMethods.DeleteObject(hBitmap);
            screenBmp.Dispose();

            Background = System.Windows.Media.Brushes.Transparent;

            SelectionCanvas canvas = new SelectionCanvas(screenBmpSource);
            canvas.AreaSelected += delegate(int x, int y, int sw, int sh)
            {
                int gx = (int)Left + x;
                int gy = (int)Top + y;
                if (AreaSelected != null) AreaSelected(gx, gy, sw, sh);
                Close();
            };
            canvas.SelectionCancelled += delegate
            {
                if (SelectionCancelled != null) SelectionCancelled();
                Close();
            };

            Content = canvas;

            Loaded += delegate { canvas.Focus(); };
        }
    }

    // --- 最前面オーバーレイウィンドウ ---
    public class OverlayWindow : Window
    {
        private Config _config;
        private ScreenCapture _capture;
        private DispatcherTimer _timer;
        private System.Windows.Controls.Image _displayImage;
        private HwndSource _hwndSource;
        private const int HOTKEY_ID_F9 = 9001;

        public event Action RequestReselect;

        public OverlayWindow(Config config)
        {
            _config = config;
            _capture = new ScreenCapture();

            Title = "ブルアカ 残り時間オーバーレイ";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent; // ウィンドウ自体の四隅を完全透過
            Topmost = true;
            ShowInTaskbar = true;

            // 角丸メインボーダー (シアン枠線と角丸半透明背景)
            Border mainBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 210, 255)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                ClipToBounds = true
            };

            Grid innerGrid = new Grid();
            innerGrid.Background = System.Windows.Media.Brushes.Transparent;

            // キャプチャ画像用コンテナ (内側も角丸にフィット)
            Border imgContainer = new Border
            {
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(2),
                ClipToBounds = true
            };

            _displayImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Fill
            };
            RenderOptions.SetBitmapScalingMode(_displayImage, BitmapScalingMode.HighQuality);
            imgContainer.Child = _displayImage;

            // 右上の [×] 閉じるボタン
            Border closeBtn = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 239, 68, 68)),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand
            };
            TextBlock closeText = new TextBlock
            {
                Text = "×",
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeText;
            closeBtn.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                Application.Current.Shutdown();
            };

            // 左上の [📐] 再設定ボタン
            Border reselectBtn = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 2, 0, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 2, 132, 199)),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand
            };
            TextBlock reselectText = new TextBlock
            {
                Text = "📐",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            reselectBtn.Child = reselectText;
            reselectBtn.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (RequestReselect != null) RequestReselect();
            };

            innerGrid.Children.Add(imgContainer);
            innerGrid.Children.Add(closeBtn);
            innerGrid.Children.Add(reselectBtn);

            mainBorder.Child = innerGrid;
            Content = mainBorder;

            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                    _config.OverlayX = (int)Left;
                    _config.OverlayY = (int)Top;
                    _config.Save();
                }
            };

            ContextMenu = CreateContextMenu();

            ApplyLayout();

            _timer = new DispatcherTimer(DispatcherPriority.Render);
            int fps = Math.Max(30, Math.Min(144, _config.TargetFps));
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            _timer.Tick += delegate { UpdateFrame(); };

            Loaded += delegate
            {
                _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                _hwndSource.AddHook(HwndHook);
                NativeMethods.RegisterHotKey(_hwndSource.Handle, HOTKEY_ID_F9, 0, 0x78); // F9
                _timer.Start();
            };
        }

        public void ApplyLayout()
        {
            if (!_config.HasCaptureRect) return;

            int pad = 8;
            Width = (_config.CaptureW * _config.Scale) + pad;
            Height = (_config.CaptureH * _config.Scale) + pad;

            if (_config.OverlayX >= 0 && _config.OverlayY >= 0)
            {
                Left = _config.OverlayX;
                Top = _config.OverlayY;
            }
            else
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = SystemParameters.PrimaryScreenHeight - Height - 70;
                _config.OverlayX = (int)Left;
                _config.OverlayY = (int)Top;
                _config.Save();
            }
        }

        private void UpdateFrame()
        {
            if (!_config.HasCaptureRect) return;
            BitmapSource bs = _capture.Capture(_config.CaptureX, _config.CaptureY, _config.CaptureW, _config.CaptureH);
            if (bs != null)
            {
                _displayImage.Source = bs;
            }
        }

        private ContextMenu CreateContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem reselectItem = new MenuItem { Header = "📐 トリミング範囲を再設定 (F9)" };
            reselectItem.Click += delegate { if (RequestReselect != null) RequestReselect(); };
            menu.Items.Add(reselectItem);

            MenuItem scaleMenu = new MenuItem { Header = "🔍 表示倍率" };
            double[] scales = new double[] { 1.0, 1.25, 1.5, 1.75, 2.0 };
            foreach (double sc in scales)
            {
                MenuItem m = new MenuItem { Header = string.Format("{0}%", (int)(sc * 100)), IsCheckable = true, IsChecked = Math.Abs(_config.Scale - sc) < 0.01 };
                double localScale = sc;
                m.Click += delegate
                {
                    _config.Scale = localScale;
                    _config.Save();
                    ApplyLayout();
                };
                scaleMenu.Items.Add(m);
            }
            menu.Items.Add(scaleMenu);

            MenuItem fpsMenu = new MenuItem { Header = "⚡ 更新レート (FPS)" };
            int[] fpsList = new int[] { 30, 60, 90, 120 };
            foreach (int f in fpsList)
            {
                MenuItem m = new MenuItem { Header = string.Format("{0} FPS", f), IsCheckable = true, IsChecked = _config.TargetFps == f };
                int localFps = f;
                m.Click += delegate
                {
                    _config.TargetFps = localFps;
                    _config.Save();
                    _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / localFps);
                };
                fpsMenu.Items.Add(m);
            }
            menu.Items.Add(fpsMenu);

            menu.Items.Add(new Separator());

            MenuItem resetPosItem = new MenuItem { Header = "📍 位置を画面中央下にリセット" };
            resetPosItem.Click += delegate
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = SystemParameters.PrimaryScreenHeight - Height - 70;
                _config.OverlayX = (int)Left;
                _config.OverlayY = (int)Top;
                _config.Save();
            };
            menu.Items.Add(resetPosItem);

            menu.Items.Add(new Separator());

            // バージョン情報表示
            MenuItem verItem = new MenuItem
            {
                Header = string.Format("ℹ️ バージョン: {0}", Config.CurrentVersion),
                IsEnabled = false
            };
            menu.Items.Add(verItem);

            menu.Items.Add(new Separator());

            MenuItem exitItem = new MenuItem { Header = "❌ アプリを終了" };
            exitItem.Click += delegate { Application.Current.Shutdown(); };
            menu.Items.Add(exitItem);

            return menu;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_F9)
            {
                if (RequestReselect != null) RequestReselect();
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_timer != null) _timer.Stop();
            if (_hwndSource != null)
            {
                NativeMethods.UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID_F9);
                _hwndSource.RemoveHook(HwndHook);
            }
            if (_capture != null) _capture.Dispose();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }

    // --- メインアプリケーション制御 ---
    public class Program
    {
        private static Mutex _mutex;

        [STAThread]
        public static void Main()
        {
            try
            {
                bool createdNew;
                _mutex = new Mutex(true, "Local\\BATimerOverlayNativeMutex", out createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("「ブルアカ 残り時間オーバーレイ」は既に起動しています。\nタスクバーまたは画面上のウィンドウをご確認ください。", "多重起動の警告", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                NativeMethods.SetProcessDPIAware();

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Config config = Config.Load();
                OverlayWindow overlay = null;

                Action startSelection = null;
                startSelection = delegate
                {
                    if (overlay != null)
                    {
                        overlay.Hide();
                    }

                    AreaSelectorWindow selector = new AreaSelectorWindow();
                    selector.AreaSelected += delegate(int x, int y, int w, int h)
                    {
                        config.CaptureX = x;
                        config.CaptureY = y;
                        config.CaptureW = w;
                        config.CaptureH = h;
                        config.Save();

                        if (overlay == null)
                        {
                            overlay = new OverlayWindow(config);
                            overlay.RequestReselect += delegate { startSelection(); };
                            app.MainWindow = overlay;
                        }
                        else
                        {
                            overlay.ApplyLayout();
                        }
                        overlay.Show();
                    };

                    selector.SelectionCancelled += delegate
                    {
                        if (config.HasCaptureRect && overlay != null)
                        {
                            overlay.Show();
                        }
                        else
                        {
                            MessageBoxResult res = MessageBox.Show("範囲が選択されていません。\nもう一度選択しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                startSelection();
                            }
                            else
                            {
                                app.Shutdown();
                            }
                        }
                    };

                    selector.Show();
                };

                if (config.HasCaptureRect)
                {
                    overlay = new OverlayWindow(config);
                    overlay.RequestReselect += delegate { startSelection(); };
                    app.MainWindow = overlay;
                    overlay.Show();
                }
                else
                {
                    startSelection();
                }

                app.Run();

                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
                }
                catch { }
            }
        }
    }
}
