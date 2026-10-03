using System.Runtime.InteropServices;

namespace DinamikAda.Servisler;

/// Varsayılan posta istemcisinde ekli yeni ileti (Simple MAPI). Outlook klasik, Thunderbird vb. destekler.
/// mailto: ek taşıyamaz; MAPI yoksa çağıran taraf dosyayı panoya koyup mailto: açar.
public static class Posta
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MapiFileDesc
    {
        public int ulReserved, flFlags, nPosition;
        public string lpszPathName, lpszFileName;
        public IntPtr lpFileType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MapiMessage
    {
        public int ulReserved;
        public string? lpszSubject, lpszNoteText, lpszMessageType, lpszDateReceived, lpszConversationID;
        public int flFlags;
        public IntPtr lpOriginator;
        public int nRecipCount;
        public IntPtr lpRecips;
        public int nFileCount;
        public IntPtr lpFiles;
    }

    [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
    private static extern int MAPISendMail(IntPtr session, IntPtr uiParam, ref MapiMessage message, int flags, int reserved);

    private const int MAPI_LOGON_UI = 0x1, MAPI_DIALOG = 0x8;

    /// 0 = gönderildi, 1 = kullanıcı iptal etti, diğer = MAPI hata kodu; -1 = MAPI yok
    public static int EkliIletiAc(string[] dosyalar, string konu = "", string metin = "")
    {
        int n = dosyalar.Length;
        int boyut = Marshal.SizeOf<MapiFileDesc>();
        IntPtr blok = Marshal.AllocHGlobal(boyut * Math.Max(1, n));
        try
        {
            for (int i = 0; i < n; i++)
            {
                var fd = new MapiFileDesc { nPosition = -1, lpszPathName = dosyalar[i], lpszFileName = System.IO.Path.GetFileName(dosyalar[i]) };
                Marshal.StructureToPtr(fd, blok + i * boyut, false);
            }
            var msg = new MapiMessage { lpszSubject = konu, lpszNoteText = metin, nFileCount = n, lpFiles = n > 0 ? blok : IntPtr.Zero };
            return MAPISendMail(IntPtr.Zero, IntPtr.Zero, ref msg, MAPI_LOGON_UI | MAPI_DIALOG, 0);
        }
        catch (DllNotFoundException) { return -1; }
        catch (EntryPointNotFoundException) { return -1; }
        finally
        {
            for (int i = 0; i < n; i++) Marshal.DestroyStructure<MapiFileDesc>(blok + i * boyut);
            Marshal.FreeHGlobal(blok);
        }
    }
}
