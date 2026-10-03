using System.Runtime.InteropServices;

namespace DinamikAda.Servisler;

/// CPU ve RAM kullanımı; ek paket gerektirmez (kernel32).
public sealed class SistemServisi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Low, High; public ulong Deger => ((ulong)High << 32) | Low; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buf);

    private ulong _oncekiBos, _oncekiToplam;

    public int CpuYuzde { get; private set; }
    public int RamYuzde { get; private set; }
    public double RamKullanilanGb { get; private set; }
    public double RamToplamGb { get; private set; }

    /// Saniyede bir çağrılır; CPU yüzdesi iki çağrı arasındaki farktan hesaplanır.
    public void Tik()
    {
        if (GetSystemTimes(out var bos, out var cekirdek, out var kullanici))
        {
            ulong toplam = cekirdek.Deger + kullanici.Deger;   // kernel süresi boş süreyi de kapsar
            ulong dToplam = toplam - _oncekiToplam, dBos = bos.Deger - _oncekiBos;
            if (_oncekiToplam != 0 && dToplam > 0)
                CpuYuzde = (int)Math.Clamp(100.0 * (dToplam - dBos) / dToplam, 0, 100);
            _oncekiToplam = toplam; _oncekiBos = bos.Deger;
        }

        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref m))
        {
            RamYuzde = (int)m.dwMemoryLoad;
            RamToplamGb = m.ullTotalPhys / 1073741824.0;
            RamKullanilanGb = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
        }
    }
}
