using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WhiteLabelLauncher
{
    public static class NativeIconHelper
    {
        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const int SHIL_JUMBO = 0x4; // 256x256
        private const int SHIL_EXTRALARGE = 0x2; // 48x48

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            [PreserveSig]
            int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig]
            int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig]
            int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig]
            int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig]
            int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig]
            int Draw(ref IMAGELISTDRAWPARAMS pimldp);
            [PreserveSig]
            int Remove(int i);
            [PreserveSig]
            int GetIcon(int i, int flags, ref IntPtr picon);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public int fStyle;
            public int dwRop;
            public int fState;
            public int Frame;
            public int crEffect;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("shell32.dll", EntryPoint = "#727")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, ref IImageList ppv);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Task<BitmapSource?> GetHighResIconAsync(string filePath)
        {
            var tcs = new TaskCompletionSource<BitmapSource?>();

            Thread thread = new Thread(() =>
            {
                try
                {
                    SHFILEINFO shinfo = new SHFILEINFO();
                    // Get the system icon index
                    SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX);

                    if (shinfo.iIcon == 0)
                    {
                        tcs.SetResult(null);
                        return;
                    }

                    Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                    IImageList? imageList = null;

                    // Try Jumbo first (256x256), fallback to Extra Large (48x48)
                    int hResult = SHGetImageList(SHIL_JUMBO, ref iidImageList, ref imageList);
                    if (hResult != 0 || imageList == null)
                    {
                        hResult = SHGetImageList(SHIL_EXTRALARGE, ref iidImageList, ref imageList);
                    }

                    if (hResult == 0 && imageList != null)
                    {
                        IntPtr hIcon = IntPtr.Zero;
                        // ILD_TRANSPARENT = 1
                        imageList.GetIcon(shinfo.iIcon, 1, ref hIcon);

                        if (hIcon != IntPtr.Zero)
                        {
                            try
                            {
                                var bitmap = Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var src = Imaging.CreateBitmapSourceFromHIcon(
                                        hIcon,
                                        Int32Rect.Empty,
                                        BitmapSizeOptions.FromEmptyOptions());
                                    src.Freeze(); // Make it cross-thread accessible
                                    return src;
                                });
                                tcs.SetResult(bitmap);
                                return;
                            }
                            finally
                            {
                                DestroyIcon(hIcon);
                            }
                        }
                    }
                    tcs.SetResult(null);
                }
                catch
                {
                    tcs.SetResult(null);
                }
            });

            // Shell operations (like SHGetImageList) require an STA thread to prevent Access Violations
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }
    }
}
