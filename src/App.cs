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

// アセンブリ情報・バージョニング (v1.1.6)
[assembly: AssemblyTitle("Timer Overlay")]
[assembly: AssemblyDescription("Lightweight, ultra-low-latency timer overlay")]
[assembly: AssemblyProduct("TimerOverlay")]
[assembly: AssemblyVersion("1.1.6.0")]
[assembly: AssemblyFileVersion("1.1.6.0")]
[assembly: AssemblyInformationalVersion("v1.1.6")]

namespace TimerOverlay
{
    // --- 設定データクラス (C# 5 準拠) ---
    public class Config
    {
        public const string CurrentVersion = "v1.1.6";

        // メイン（青枠）
        public int CaptureX { get; set; }
        public int CaptureY { get; set; }
        public int CaptureW { get; set; }
        public int CaptureH { get; set; }
        public int OverlayX { get; set; }
        public int OverlayY { get; set; }
        public double Scale { get; set; }
        public int TargetFps { get; set; }

        // 黄枠用の独立キャプチャ座標
        public int YellowCaptureX { get; set; }
        public int YellowCaptureY { get; set; }
        public int YellowCaptureW { get; set; }
        public int YellowCaptureH { get; set; }

        // 赤枠用の独立キャプチャ座標
        public int RedCaptureX { get; set; }
        public int RedCaptureY { get; set; }
        public int RedCaptureW { get; set; }
        public int RedCaptureH { get; set; }

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

            YellowCaptureX = -1;
            YellowCaptureY = -1;
            YellowCaptureW = -1;
            YellowCaptureH = -1;

            RedCaptureX = -1;
            RedCaptureY = -1;
            RedCaptureW = -1;
            RedCaptureH = -1;
        }

        public bool HasCaptureRect
        {
            get { return CaptureW > 0 && CaptureH > 0 && CaptureX >= 0 && CaptureY >= 0; }
        }

        public bool HasCaptureRectFor(OverlayColor color)
        {
            if (color == OverlayColor.Yellow)
                return YellowCaptureW > 0 && YellowCaptureH > 0 && YellowCaptureX >= 0 && YellowCaptureY >= 0;
            if (color == OverlayColor.Red)
                return RedCaptureW > 0 && RedCaptureH > 0 && RedCaptureX >= 0 && RedCaptureY >= 0;
            return HasCaptureRect;
        }

        public void GetCaptureRect(OverlayColor color, out int x, out int y, out int w, out int h)
        {
            if (color == OverlayColor.Yellow && HasCaptureRectFor(OverlayColor.Yellow))
            {
                x = YellowCaptureX; y = YellowCaptureY; w = YellowCaptureW; h = YellowCaptureH;
                return;
            }
            if (color == OverlayColor.Red && HasCaptureRectFor(OverlayColor.Red))
            {
                x = RedCaptureX; y = RedCaptureY; w = RedCaptureW; h = RedCaptureH;
                return;
            }
            x = CaptureX; y = CaptureY; w = CaptureW; h = CaptureH;
        }

        public void SetCaptureRect(OverlayColor color, int x, int y, int w, int h)
        {
            if (color == OverlayColor.Yellow)
            {
                YellowCaptureX = x; YellowCaptureY = y; YellowCaptureW = w; YellowCaptureH = h;
            }
            else if (color == OverlayColor.Red)
            {
                RedCaptureX = x; RedCaptureY = y; RedCaptureW = w; RedCaptureH = h;
            }
            else
            {
                CaptureX = x; CaptureY = y; CaptureW = w; CaptureH = h;
            }
            Save();
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

                    cfg.YellowCaptureX = ExtractInt(json, "YellowCaptureX", -1);
                    cfg.YellowCaptureY = ExtractInt(json, "YellowCaptureY", -1);
                    cfg.YellowCaptureW = ExtractInt(json, "YellowCaptureW", -1);
                    cfg.YellowCaptureH = ExtractInt(json, "YellowCaptureH", -1);

                    cfg.RedCaptureX = ExtractInt(json, "RedCaptureX", -1);
                    cfg.RedCaptureY = ExtractInt(json, "RedCaptureY", -1);
                    cfg.RedCaptureW = ExtractInt(json, "RedCaptureW", -1);
                    cfg.RedCaptureH = ExtractInt(json, "RedCaptureH", -1);

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
                sb.AppendLine(string.Format("  \"TargetFps\": {0},", TargetFps));
                sb.AppendLine(string.Format("  \"YellowCaptureX\": {0},", YellowCaptureX));
                sb.AppendLine(string.Format("  \"YellowCaptureY\": {0},", YellowCaptureY));
                sb.AppendLine(string.Format("  \"YellowCaptureW\": {0},", YellowCaptureW));
                sb.AppendLine(string.Format("  \"YellowCaptureH\": {0},", YellowCaptureH));
                sb.AppendLine(string.Format("  \"RedCaptureX\": {0},", RedCaptureX));
                sb.AppendLine(string.Format("  \"RedCaptureY\": {0},", RedCaptureY));
                sb.AppendLine(string.Format("  \"RedCaptureW\": {0},", RedCaptureW));
                sb.AppendLine(string.Format("  \"RedCaptureH\": {0}", RedCaptureH));
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

    public enum OverlayColor
    {
        Blue,
        Yellow,
        Red
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

        private Border _closeBtn;
        private Border _reselectBtn;

        public OverlayColor ColorType { get; private set; }
        public bool IsPrimary { get { return ColorType == OverlayColor.Blue; } }
        private OverlayWindow _parentOverlay;

        public OverlayWindow YellowOverlay { get; set; }
        public OverlayWindow RedOverlay { get; set; }

        public event Action<OverlayColor> RequestReselectFor;

        public OverlayWindow(Config config, OverlayColor color = OverlayColor.Blue, OverlayWindow parentOverlay = null)
        {
            _config = config;
            ColorType = color;
            _parentOverlay = parentOverlay;

            if (IsPrimary)
            {
                _capture = new ScreenCapture();
            }

            switch (ColorType)
            {
                case OverlayColor.Yellow:
                    Title = "タイマーオーバーレイ (黄枠)";
                    break;
                case OverlayColor.Red:
                    Title = "タイマーオーバーレイ (赤枠)";
                    break;
                default:
                    Title = "タイマーオーバーレイ";
                    break;
            }

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent; // ウィンドウ自体の四隅を完全透過
            Topmost = true;
            ShowInTaskbar = true;

            // 枠色・背景色の設定
            System.Windows.Media.Color borderColor;
            System.Windows.Media.Color bgColor;
            System.Windows.Media.Color reselectBtnColor;

            if (ColorType == OverlayColor.Yellow)
            {
                borderColor = System.Windows.Media.Color.FromArgb(230, 250, 204, 21); // 黄色枠
                bgColor = System.Windows.Media.Color.FromArgb(190, 24, 22, 14);
                reselectBtnColor = System.Windows.Media.Color.FromArgb(210, 202, 138, 4);
            }
            else if (ColorType == OverlayColor.Red)
            {
                borderColor = System.Windows.Media.Color.FromArgb(230, 248, 113, 113); // 赤色枠
                bgColor = System.Windows.Media.Color.FromArgb(190, 26, 17, 17);
                reselectBtnColor = System.Windows.Media.Color.FromArgb(210, 220, 38, 38);
            }
            else
            {
                borderColor = System.Windows.Media.Color.FromArgb(180, 0, 210, 255); // 青枠（デフォルト）
                bgColor = System.Windows.Media.Color.FromArgb(190, 15, 23, 42);
                reselectBtnColor = System.Windows.Media.Color.FromArgb(200, 2, 132, 199);
            }

            Border mainBorder = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
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

            // 右上の [×] 閉じるボタン（アクティブ時のみ表示）
            _closeBtn = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 239, 68, 68)),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
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
            _closeBtn.Child = closeText;
            _closeBtn.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (IsPrimary)
                {
                    Application.Current.Shutdown();
                }
                else
                {
                    Close();
                }
            };

            // 左上の [📐] 再設定ボタン（アクティブ時のみ表示）
            _reselectBtn = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 2, 0, 0),
                Background = new SolidColorBrush(reselectBtnColor),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            TextBlock reselectText = new TextBlock
            {
                Text = "📐",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _reselectBtn.Child = reselectText;
            _reselectBtn.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                TriggerReselect();
            };

            innerGrid.Children.Add(imgContainer);
            innerGrid.Children.Add(_closeBtn);
            innerGrid.Children.Add(_reselectBtn);

            mainBorder.Child = innerGrid;
            Content = mainBorder;

            // アクティブ・非アクティブの切り替えでボタンの表示/非表示を自動更新
            Activated += delegate { UpdateButtonsVisibility(); };
            Deactivated += delegate { UpdateButtonsVisibility(); };
            Loaded += delegate { UpdateButtonsVisibility(); };

            PreviewMouseDown += delegate
            {
                if (!IsActive)
                {
                    Activate();
                    UpdateButtonsVisibility();
                }
            };

            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                    if (IsPrimary)
                    {
                        _config.OverlayX = (int)Left;
                        _config.OverlayY = (int)Top;
                        _config.Save();
                    }
                }
            };

            ContextMenu = CreateContextMenu();

            ApplyLayout();

            if (IsPrimary)
            {
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
        }

        private void UpdateButtonsVisibility()
        {
            Visibility v = IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (_closeBtn != null) _closeBtn.Visibility = v;
            if (_reselectBtn != null) _reselectBtn.Visibility = v;
        }

        public void SetFrame(BitmapSource bs)
        {
            if (_displayImage != null)
            {
                _displayImage.Source = bs;
            }
        }

        public void ApplyLayout()
        {
            int cx, cy, cw, ch;
            _config.GetCaptureRect(ColorType, out cx, out cy, out cw, out ch);
            if (cw <= 0 || ch <= 0) return;

            int pad = 8;
            Width = (cw * _config.Scale) + pad;
            Height = (ch * _config.Scale) + pad;

            if (IsPrimary)
            {
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
        }

        private void UpdateFrame()
        {
            if (IsPrimary)
            {
                // 1. 青枠（メイン）のキャプチャ
                int bx, by, bw, bh;
                _config.GetCaptureRect(OverlayColor.Blue, out bx, out by, out bw, out bh);
                BitmapSource bsBlue = null;
                if (bw > 0 && bh > 0 && bx >= 0 && by >= 0)
                {
                    bsBlue = _capture.Capture(bx, by, bw, bh);
                    if (bsBlue != null)
                    {
                        _displayImage.Source = bsBlue;
                    }
                }

                // 2. 黄枠のキャプチャ（独立領域）
                if (YellowOverlay != null && YellowOverlay.IsVisible)
                {
                    int yx, yy, yw, yh;
                    _config.GetCaptureRect(OverlayColor.Yellow, out yx, out yy, out yw, out yh);
                    if (yw > 0 && yh > 0 && yx >= 0 && yy >= 0)
                    {
                        if (yx == bx && yy == by && yw == bw && yh == bh && bsBlue != null)
                        {
                            YellowOverlay.SetFrame(bsBlue);
                        }
                        else
                        {
                            BitmapSource bsYellow = _capture.Capture(yx, yy, yw, yh);
                            if (bsYellow != null)
                            {
                                YellowOverlay.SetFrame(bsYellow);
                            }
                        }
                    }
                }

                // 3. 赤枠のキャプチャ（独立領域）
                if (RedOverlay != null && RedOverlay.IsVisible)
                {
                    int rx, ry, rw, rh;
                    _config.GetCaptureRect(OverlayColor.Red, out rx, out ry, out rw, out rh);
                    if (rw > 0 && rh > 0 && rx >= 0 && ry >= 0)
                    {
                        if (rx == bx && ry == by && rw == bw && rh == bh && bsBlue != null)
                        {
                            RedOverlay.SetFrame(bsBlue);
                        }
                        else
                        {
                            BitmapSource bsRed = _capture.Capture(rx, ry, rw, rh);
                            if (bsRed != null)
                            {
                                RedOverlay.SetFrame(bsRed);
                            }
                        }
                    }
                }
            }
        }

        public void TriggerReselect()
        {
            OverlayWindow root = IsPrimary ? this : _parentOverlay;
            if (root != null && root.RequestReselectFor != null)
            {
                root.RequestReselectFor(this.ColorType);
            }
        }

        public void SetScale(double scale)
        {
            OverlayWindow root = IsPrimary ? this : _parentOverlay;
            if (root != null)
            {
                root._config.Scale = scale;
                root._config.Save();
                root.ApplyLayout();
                if (root.YellowOverlay != null) root.YellowOverlay.ApplyLayout();
                if (root.RedOverlay != null) root.RedOverlay.ApplyLayout();
            }
        }

        public void SetFps(int fps)
        {
            OverlayWindow root = IsPrimary ? this : _parentOverlay;
            if (root != null)
            {
                root._config.TargetFps = fps;
                root._config.Save();
                if (root._timer != null)
                {
                    root._timer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
                }
            }
        }

        public void ResetPosition()
        {
            if (IsPrimary)
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = SystemParameters.PrimaryScreenHeight - Height - 70;
                _config.OverlayX = (int)Left;
                _config.OverlayY = (int)Top;
                _config.Save();
                if (YellowOverlay != null)
                {
                    YellowOverlay.Left = Left;
                    YellowOverlay.Top = Top + Height + 10;
                }
                if (RedOverlay != null)
                {
                    RedOverlay.Left = Left;
                    RedOverlay.Top = (YellowOverlay != null ? YellowOverlay.Top + YellowOverlay.Height + 10 : Top + Height + 10);
                }
            }
            else
            {
                OverlayWindow root = _parentOverlay;
                if (root != null)
                {
                    Left = root.Left;
                    if (ColorType == OverlayColor.Yellow)
                    {
                        Top = root.Top + root.Height + 10;
                    }
                    else if (ColorType == OverlayColor.Red)
                    {
                        Top = (root.YellowOverlay != null ? root.YellowOverlay.Top + root.YellowOverlay.Height + 10 : root.Top + root.Height + 10);
                    }
                }
            }
        }

        public void ToggleYellowOverlay(bool enable)
        {
            OverlayWindow root = IsPrimary ? this : _parentOverlay;
            if (root == null) return;

            if (enable)
            {
                if (root.YellowOverlay == null)
                {
                    root.YellowOverlay = new OverlayWindow(_config, OverlayColor.Yellow, root);
                    root.YellowOverlay.Closed += delegate { root.YellowOverlay = null; };
                    root.YellowOverlay.Left = root.Left;
                    root.YellowOverlay.Top = root.Top + root.Height + 10;
                    root.YellowOverlay.Show();
                }
            }
            else
            {
                if (root.YellowOverlay != null)
                {
                    root.YellowOverlay.Close();
                    root.YellowOverlay = null;
                }
            }
        }

        public void ToggleRedOverlay(bool enable)
        {
            OverlayWindow root = IsPrimary ? this : _parentOverlay;
            if (root == null) return;

            if (enable)
            {
                if (root.RedOverlay == null)
                {
                    root.RedOverlay = new OverlayWindow(_config, OverlayColor.Red, root);
                    root.RedOverlay.Closed += delegate { root.RedOverlay = null; };
                    root.RedOverlay.Left = root.Left;
                    if (root.YellowOverlay != null && root.YellowOverlay.IsVisible)
                    {
                        root.RedOverlay.Top = root.YellowOverlay.Top + root.YellowOverlay.Height + 10;
                    }
                    else
                    {
                        root.RedOverlay.Top = root.Top + root.Height + 10;
                    }
                    root.RedOverlay.Show();
                }
            }
            else
            {
                if (root.RedOverlay != null)
                {
                    root.RedOverlay.Close();
                    root.RedOverlay = null;
                }
            }
        }

        private ContextMenu CreateContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            string reselectText = "📐 トリミング範囲を再設定 (F9)";
            if (ColorType == OverlayColor.Yellow) reselectText = "📐 黄枠のトリミング範囲を再設定";
            else if (ColorType == OverlayColor.Red) reselectText = "📐 赤枠のトリミング範囲を再設定";

            MenuItem reselectItem = new MenuItem { Header = reselectText };
            reselectItem.Click += delegate { TriggerReselect(); };
            menu.Items.Add(reselectItem);

            // 多重起動メニュー
            MenuItem multiMenu = new MenuItem { Header = "多重起動" };

            MenuItem yellowItem = new MenuItem { Header = "黄枠", IsCheckable = true };
            yellowItem.Click += delegate(object sender, RoutedEventArgs e)
            {
                ToggleYellowOverlay(yellowItem.IsChecked);
            };
            multiMenu.Items.Add(yellowItem);

            MenuItem redItem = new MenuItem { Header = "赤枠", IsCheckable = true };
            redItem.Click += delegate(object sender, RoutedEventArgs e)
            {
                ToggleRedOverlay(redItem.IsChecked);
            };
            multiMenu.Items.Add(redItem);

            menu.Items.Add(multiMenu);

            menu.Items.Add(new Separator());

            MenuItem scaleMenu = new MenuItem { Header = "🔍 表示倍率" };
            double[] scales = new double[] { 1.0, 1.25, 1.5, 1.75, 2.0 };
            foreach (double sc in scales)
            {
                MenuItem m = new MenuItem { Header = string.Format("{0}%", (int)(sc * 100)), IsCheckable = true, Tag = sc };
                double localScale = sc;
                m.Click += delegate
                {
                    SetScale(localScale);
                };
                scaleMenu.Items.Add(m);
            }
            menu.Items.Add(scaleMenu);

            MenuItem fpsMenu = new MenuItem { Header = "⚡ 更新レート (FPS)" };
            int[] fpsList = new int[] { 30, 60, 90, 120 };
            foreach (int f in fpsList)
            {
                MenuItem m = new MenuItem { Header = string.Format("{0} FPS", f), IsCheckable = true, Tag = f };
                int localFps = f;
                m.Click += delegate
                {
                    SetFps(localFps);
                };
                fpsMenu.Items.Add(m);
            }
            menu.Items.Add(fpsMenu);

            menu.Items.Add(new Separator());

            MenuItem resetPosItem = new MenuItem { Header = "📍 位置を画面中央下にリセット" };
            resetPosItem.Click += delegate
            {
                ResetPosition();
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

            // メニューが開く直前にチェック状態を最新化
            menu.Opened += delegate
            {
                OverlayWindow root = IsPrimary ? this : _parentOverlay;
                if (root != null)
                {
                    yellowItem.IsChecked = (root.YellowOverlay != null && root.YellowOverlay.IsVisible);
                    redItem.IsChecked = (root.RedOverlay != null && root.RedOverlay.IsVisible);
                }
                foreach (object item in scaleMenu.Items)
                {
                    MenuItem mi = item as MenuItem;
                    if (mi != null && mi.Tag is double)
                    {
                        mi.IsChecked = Math.Abs(_config.Scale - (double)mi.Tag) < 0.01;
                    }
                }
                foreach (object item in fpsMenu.Items)
                {
                    MenuItem mi = item as MenuItem;
                    if (mi != null && mi.Tag is int)
                    {
                        mi.IsChecked = (_config.TargetFps == (int)mi.Tag);
                    }
                }
            };

            return menu;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_F9)
            {
                OverlayColor target = OverlayColor.Blue;
                if (YellowOverlay != null && YellowOverlay.IsActive) target = OverlayColor.Yellow;
                else if (RedOverlay != null && RedOverlay.IsActive) target = OverlayColor.Red;

                if (RequestReselectFor != null)
                {
                    RequestReselectFor(target);
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (IsPrimary)
            {
                if (_timer != null) _timer.Stop();
                if (_hwndSource != null)
                {
                    NativeMethods.UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID_F9);
                    _hwndSource.RemoveHook(HwndHook);
                }
                if (_capture != null) _capture.Dispose();
                if (YellowOverlay != null) { YellowOverlay.Close(); YellowOverlay = null; }
                if (RedOverlay != null) { RedOverlay.Close(); RedOverlay = null; }
                base.OnClosed(e);
                Application.Current.Shutdown();
            }
            else
            {
                base.OnClosed(e);
            }
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
                _mutex = new Mutex(true, "Local\\TimerOverlayNativeMutex", out createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("「タイマーオーバーレイ」は既に起動しています。\nタスクバーまたは画面上のウィンドウをご確認ください。", "多重起動の警告", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                NativeMethods.SetProcessDPIAware();

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Config config = Config.Load();
                OverlayWindow overlay = null;

                Action<OverlayColor> startSelection = null;
                startSelection = delegate(OverlayColor targetColor)
                {
                    if (overlay != null)
                    {
                        overlay.Hide();
                        if (overlay.YellowOverlay != null) overlay.YellowOverlay.Hide();
                        if (overlay.RedOverlay != null) overlay.RedOverlay.Hide();
                    }

                    AreaSelectorWindow selector = new AreaSelectorWindow();
                    selector.AreaSelected += delegate(int x, int y, int w, int h)
                    {
                        config.SetCaptureRect(targetColor, x, y, w, h);

                        if (targetColor == OverlayColor.Blue)
                        {
                            if (overlay == null)
                            {
                                overlay = new OverlayWindow(config);
                                overlay.RequestReselectFor += delegate(OverlayColor c) { startSelection(c); };
                                app.MainWindow = overlay;
                            }
                            else
                            {
                                overlay.ApplyLayout();
                            }
                        }
                        else if (targetColor == OverlayColor.Yellow)
                        {
                            if (overlay != null && overlay.YellowOverlay != null)
                            {
                                overlay.YellowOverlay.ApplyLayout();
                            }
                        }
                        else if (targetColor == OverlayColor.Red)
                        {
                            if (overlay != null && overlay.RedOverlay != null)
                            {
                                overlay.RedOverlay.ApplyLayout();
                            }
                        }

                        if (overlay != null) overlay.Show();
                        if (overlay != null && overlay.YellowOverlay != null) overlay.YellowOverlay.Show();
                        if (overlay != null && overlay.RedOverlay != null) overlay.RedOverlay.Show();
                    };

                    selector.SelectionCancelled += delegate
                    {
                        if (config.HasCaptureRect && overlay != null)
                        {
                            overlay.Show();
                            if (overlay.YellowOverlay != null) overlay.YellowOverlay.Show();
                            if (overlay.RedOverlay != null) overlay.RedOverlay.Show();
                        }
                        else
                        {
                            MessageBoxResult res = MessageBox.Show("範囲が選択されていません。\nもう一度選択しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                startSelection(targetColor);
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
                    overlay.RequestReselectFor += delegate(OverlayColor c) { startSelection(c); };
                    app.MainWindow = overlay;
                    overlay.Show();
                }
                else
                {
                    startSelection(OverlayColor.Blue);
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
