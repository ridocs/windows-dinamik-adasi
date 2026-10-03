using System.Runtime.InteropServices;
using System.Text;

namespace DinamikAda.Servisler;

/// Windows'un kendi ses seviyesi göstergesini (OSD) gizler: kapsül zaten kendi göstergesini çiziyor.
/// OSD penceresi ShellExperienceHost sürecine ait, sınıfı "NativeHWNDHost". Ses değişince kısa süre bastırılır.
/// En iyi çaba: Windows sürümüne göre pencere değişebilir, bulunamazsa sessizce geçer.
public sealed class SesOsdServisi
{
    private IntPtr _osd = IntPtr.Zero;

    /// Ses değişiminde çağrılır; OSD penceresini bulup gizler
    public void Bastir()
    {
        var h = Pencere();
        if (h != IntPtr.Zero && IsWindowVisible(h)) ShowWindow(h, SW_HIDE);
    }

    private IntPtr Pencere()
    {
        if (_osd != IntPtr.Zero && IsWindow(_osd)) return _osd;
        _osd = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            var sb = new StringBuilder(64);
            GetClassName(h, sb, sb.Capacity);
            if (sb.ToString() != "NativeHWNDHost") return true;
            GetWindowThreadProcessId(h, out uint pid);
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (!p.ProcessName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { return true; }
            if (GetWindowRect(h, out var r))
            {
                int w = r.Right - r.Left, y = r.Bottom - r.Top;
                if (w > 600 || y > 300 || w < 50) return true;
            }
            _osd = h; return false;
        }, IntPtr.Zero);
        return _osd;
    }

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private delegate bool EnumProc(IntPtr h, IntPtr l);
    private const int SW_HIDE = 0;
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
}
