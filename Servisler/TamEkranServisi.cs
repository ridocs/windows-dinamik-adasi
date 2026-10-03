using System.Runtime.InteropServices;
using System.Text;

namespace DinamikAda.Servisler;

/// Ön plandaki pencere, verilen monitörü tamamen kaplıyor mu (oyun, video)?
public static class TamEkranServisi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// kendiPencere: kapsülün tutamacı (kendisini yok saymak için).
    /// kapsulMonitoru: kapsülün bulunduğu monitör tutamacı; yalnız o monitördeki tam ekran sayılır.
    public static bool OnPlanTamEkranMi(IntPtr kendiPencere, IntPtr kapsulMonitoru)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == kendiPencere) return false;
        if (fg == GetShellWindow() || fg == GetDesktopWindow()) return false;

        var sb = new StringBuilder(64);
        GetClassName(fg, sb, sb.Capacity);
        var sinif = sb.ToString();
        if (sinif is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;

        // Ekranı kaplayan ama başlık çubuklu/maksimize pencere (görev çubuğu gizliyken VS Code gibi) tam ekran değildir
        if (IsZoomed(fg)) return false;
        if ((GetWindowLong(fg, GWL_STYLE) & WS_CAPTION) == WS_CAPTION) return false;

        var mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        if (mon != kapsulMonitoru) return false;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi)) return false;
        if (!GetWindowRect(fg, out var r)) return false;

        return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top
            && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
    }

    public static IntPtr MonitorTutamaci(IntPtr hwnd) => MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
}
