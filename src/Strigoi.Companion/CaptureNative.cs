using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace Strigoi.Companion;

internal sealed record CaptureTarget(nint Handle, uint ProcessId, string Title, string ProcessName = "")
{
    public override string ToString() => Title;
}

internal static class CaptureNative
{
    private delegate bool EnumProc(nint hwnd, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int type, nint software, uint flags, nint levels, uint count, uint sdk, out nint device, out int level, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgi, out nint device);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out nint result);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name, in Guid iid, out nint factory);
    internal static List<CaptureTarget> Targets()
    {
        var result = new List<CaptureTarget>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || pid == Environment.ProcessId) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            var title = new StringBuilder(512); GetWindowText(hwnd, title, title.Capacity);
            if (title.Length > 0)
            {
                string process = "";
                try { process = Process.GetProcessById((int)pid).ProcessName; } catch (ArgumentException) { }
                result.Add(new(hwnd, pid, title.ToString(), process));
            }
            return true;
        }, 0);
        result.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Title, b.Title));
        return result;
    }
    internal static CaptureTarget? ForegroundEligible()
    {
        var foreground = Native.GetForegroundWindow();
        return Targets().FirstOrDefault(target => target.Handle == foreground && IsLikelyGame(target));
    }
    internal static bool IsLikelyGame(CaptureTarget target)
    {
        if (!Matches(target) || string.IsNullOrWhiteSpace(target.Title)) return false;
        return Core.FamiliarAutomation.IsEligible(new Core.ForegroundCandidate(target.ProcessId, target.ProcessName, target.Title, true, IsIconic(target.Handle), IsGameSized(target.Handle)), Environment.ProcessId);
    }
    internal static bool IsOwnWindow(nint handle) => handle != 0 && GetWindowThreadProcessId(handle, out uint pid) != 0 && pid == Environment.ProcessId;
    private static bool IsGameSized(nint handle)
    {
        if (!Native.GetWindowRect(handle, out var rect)) return false;
        var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        int width = Math.Max(0, rect.Right - rect.Left), height = Math.Max(0, rect.Bottom - rect.Top);
        return width >= area.Width * .70 && height >= area.Height * .65;
    }
    internal static bool Matches(CaptureTarget target) => IsWindow(target.Handle) && GetWindowThreadProcessId(target.Handle, out uint pid) != 0 && pid == target.ProcessId;
    internal static IDirect3DDevice CreateDevice()
    {
        nint device = 0, context = 0, dxgi = 0, abi = 0;
        try
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out device, out _, out context));
            var iid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out abi));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(abi);
        }
        finally { Release(abi); Release(dxgi); Release(context); Release(device); }
    }
    internal static unsafe GraphicsCaptureItem CreateItem(nint hwnd)
    {
        nint name = 0, factory = 0, item = 0;
        try
        {
            const string type = "Windows.Graphics.Capture.GraphicsCaptureItem";
            Marshal.ThrowExceptionForHR(WindowsCreateString(type, type.Length, out name));
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, new Guid("3628e81b-3cac-4c60-b7f4-23ce0e0c3356"), out factory));
            var iid = new Guid("79c3f95b-31f7-4ec2-a464-632ef5d30760");
            var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(nint**)factory)[3];
            Marshal.ThrowExceptionForHR(create(factory, hwnd, &iid, &item));
            return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
        }
        finally { Release(item); Release(factory); if (name != 0) WindowsDeleteString(name); }
    }
    internal static unsafe (byte[] Pixels, int Width, int Height) Reduce(SoftwareBitmap bitmap, int contentWidth, int contentHeight, int maxWidth = 640, int maxHeight = 360)
    {
        int sourceWidth = Math.Min(bitmap.PixelWidth, contentWidth), sourceHeight = Math.Min(bitmap.PixelHeight, contentHeight);
        double scale = Math.Min(1, Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight));
        int width = Math.Max(1, (int)(sourceWidth * scale)), height = Math.Max(1, (int)(sourceHeight * scale));
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        nint abi = WinRT.MarshalInterface<Windows.Foundation.IMemoryBufferReference>.FromManaged(reference), access = 0;
        try
        {
            var iid = new Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(abi, in iid, out access));
            byte* data; uint capacity;
            var get = (delegate* unmanaged[Stdcall]<nint, byte**, uint*, int>)(*(nint**)access)[3];
            Marshal.ThrowExceptionForHR(get(access, &data, &capacity));
            var plane = buffer.GetPlaneDescription(0);
            if (sourceWidth < 1 || sourceHeight < 1 || plane.StartIndex < 0 || plane.Stride < sourceWidth * 4 ||
                (long)plane.StartIndex + (sourceHeight - 1L) * plane.Stride + sourceWidth * 4L > capacity)
                throw new InvalidOperationException("Formato de imagem indisponível.");
            var pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    byte* p = data + plane.StartIndex + (y * sourceHeight / height) * plane.Stride + (x * sourceWidth / width) * 4;
                    int to = (y * width + x) * 4;
                    pixels[to] = p[0]; pixels[to + 1] = p[1]; pixels[to + 2] = p[2]; pixels[to + 3] = 255;
                }
            return (pixels, width, height);
        }
        finally { Release(access); Release(abi); }
    }
    private static void Release(nint pointer) { if (pointer != 0) Marshal.Release(pointer); }
}
