using System.Runtime.InteropServices;

namespace DinamikAda.Servisler;

/// Uygulama başına çıkış aygıtı: Windows'un "Uygulama ses ve cihaz tercihleri" sayfasının kullandığı,
/// belgelenmemiş Windows.Media.Internal.AudioPolicyConfig arayüzü (EarTrumpet ve SoundSwitch de bunu kullanır).
/// .NET 8'de WinRT marshaling olmadığı için vtable'dan ham fonksiyon işaretçileriyle çağrılır.
internal static class AudioPolicyConfig
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetFn(IntPtr self, uint pid, int flow, int role, IntPtr deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetFn(IntPtr self, uint pid, int flow, int role, out IntPtr deviceId);

    private const int ERender = 0, RoleConsole = 0, RoleMultimedia = 1;
    private const string MmdevapiOnek = @"\\?\SWD#MMDEVAPI#", RenderSonek = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    private static IntPtr _fabrika;
    private static SetFn? _set;
    private static GetFn? _get;
    private static bool _denendi;
    public static string SonHata { get; private set; } = "";

    private static bool Hazirla()
    {
        if (_set != null) return true;
        if (_denendi) return false;
        _denendi = true;
        IntPtr hs = IntPtr.Zero;
        try
        {
            const string sinif = "Windows.Media.Internal.AudioPolicyConfig";
            WindowsCreateString(sinif, sinif.Length, out hs);
            // 21H2 ve sonrası yeni IID; eski sürümler için önceki IID
            foreach (var iidMetin in new[] { "ab3d4648-e242-459f-b02f-541c70306324", "2a59116d-6c4f-45e0-a74f-707e3fef9258" })
            {
                var iid = new Guid(iidMetin);
                int hr = RoGetActivationFactory(hs, ref iid, out var f);
                if (hr == 0 && f != IntPtr.Zero) { _fabrika = f; break; }
            }
            if (_fabrika == IntPtr.Zero) { SonHata = "AudioPolicyConfig fabrikası alınamadı"; return false; }
            // IInspectable 6 slot + 19 kullanılmayan yöntem → 25: SetPersistedDefaultAudioEndpoint, 26: GetPersistedDefaultAudioEndpoint
            IntPtr vtbl = Marshal.ReadIntPtr(_fabrika);
            _set = Marshal.GetDelegateForFunctionPointer<SetFn>(Marshal.ReadIntPtr(vtbl, 25 * IntPtr.Size));
            _get = Marshal.GetDelegateForFunctionPointer<GetFn>(Marshal.ReadIntPtr(vtbl, 26 * IntPtr.Size));
            return true;
        }
        catch (Exception e) { SonHata = e.Message; return false; }
        finally { if (hs != IntPtr.Zero) WindowsDeleteString(hs); }
    }

    /// mmDeviceId: MMDevice.ID ("{0.0.0.00000000}.{guid}"); null = sistem varsayılanına dön
    public static bool Ayarla(uint pid, string? mmDeviceId)
    {
        if (!Hazirla()) return false;
        IntPtr hs = IntPtr.Zero;
        try
        {
            if (mmDeviceId != null)
            {
                string tam = MmdevapiOnek + mmDeviceId + RenderSonek;
                WindowsCreateString(tam, tam.Length, out hs);
            }
            int h1 = _set!(_fabrika, pid, ERender, RoleMultimedia, hs);
            int h2 = _set!(_fabrika, pid, ERender, RoleConsole, hs);
            if (h1 != 0 || h2 != 0) { SonHata = $"HRESULT 0x{(uint)(h1 != 0 ? h1 : h2):X8}"; return false; }
            return true;
        }
        catch (Exception e) { SonHata = e.Message; return false; }
        finally { if (hs != IntPtr.Zero) WindowsDeleteString(hs); }
    }

    /// Uygulamaya kalıcı atanmış çıkış aygıtı (MMDevice.ID biçiminde); yoksa null (sistem varsayılanı)
    public static string? Oku(uint pid)
    {
        if (!Hazirla()) return null;
        try
        {
            int hr = _get!(_fabrika, pid, ERender, RoleMultimedia, out var hs);
            if (hr != 0 || hs == IntPtr.Zero) return null;
            try
            {
                IntPtr buf = WindowsGetStringRawBuffer(hs, out int len);
                string s = Marshal.PtrToStringUni(buf, len) ?? "";
                int a = s.IndexOf("MMDEVAPI#", StringComparison.OrdinalIgnoreCase);
                if (a < 0) return s.Length > 0 ? s : null;
                a += "MMDEVAPI#".Length;
                int b = s.IndexOf("#{", a, StringComparison.Ordinal);
                return b > a ? s[a..b] : s[a..];
            }
            finally { WindowsDeleteString(hs); }
        }
        catch { return null; }
    }

    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr hstring);
    [DllImport("combase.dll")] private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out int length);
}
