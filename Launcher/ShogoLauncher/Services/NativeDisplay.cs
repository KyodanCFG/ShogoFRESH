using System.Runtime.InteropServices;

namespace ShogoLauncher.Services;

/// <summary>
/// True pixel resolution of the primary display.
///
/// WPF's SystemParameters.PrimaryScreenWidth/Height return device-independent
/// units, which on a DPI-scaled display (any modern laptop at 125-150%) are
/// NOT the real resolution - that bug wrote e.g. 1707x1067 into autoexec.cfg
/// on a 2560x1600 panel. EnumDisplaySettings reports actual pixels regardless
/// of the process's DPI awareness.
/// </summary>
public static class NativeDisplay
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    private const int ENUM_CURRENT_SETTINGS = -1;

    public static (int Width, int Height) Primary()
    {
        var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmPelsWidth > 0)
            return ((int)dm.dmPelsWidth, (int)dm.dmPelsHeight);

        // Fallback: DIP-based (wrong under scaling, but better than nothing).
        return ((int)System.Windows.SystemParameters.PrimaryScreenWidth,
                (int)System.Windows.SystemParameters.PrimaryScreenHeight);
    }
}
