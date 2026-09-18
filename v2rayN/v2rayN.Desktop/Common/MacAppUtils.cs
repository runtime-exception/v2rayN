using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace v2rayN.Desktop.Common;

internal static class MacAppUtils
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";
    private const nint ActivationPolicyAccessory = 1;
    private const nint NSViewWidthSizable = 2;
    private const nint NSViewHeightSizable = 16;
    private const nint NSWindowBelow = -1;
    private const nint NSVisualEffectMaterialWindowBackground = 12;
    private const nint NSVisualEffectBlendingModeBehindWindow = 0;
    private const nint NSVisualEffectStateFollowsWindowActiveState = 0;
    private static readonly ConditionalWeakTable<Window, object> ConfiguredWindows = new();
    private static readonly ConditionalWeakTable<Window, object> MaterialWindows = new();

    public static void SetActivationPolicyAccessory()
        => objc_msgSend(
            objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication")),
            sel_registerName("setActivationPolicy:"),
            ActivationPolicyAccessory);

    public static bool IsWindowMiniaturized(Window window)
        => window.TryGetPlatformHandle() is IMacOSTopLevelPlatformHandle { NSWindow: not 0 } handle
            && objc_msgSend_bool(handle.NSWindow, sel_registerName("isMiniaturized"));

    public static void ConfigureWindow(Window window)
    {
        if (!OperatingSystem.IsMacOS() || ConfiguredWindows.TryGetValue(window, out _))
        {
            return;
        }

        ConfiguredWindows.Add(window, new object());
        window.Classes.Add("macos");
        window.TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        window.Opened += (_, _) => RefreshAppearance(window);
        window.Activated += (_, _) => RefreshAppearance(window);
    }

    private static void RefreshAppearance(Window window)
    {
        var workspace = objc_msgSend(
            objc_getClass("NSWorkspace"),
            sel_registerName("sharedWorkspace"));

        var reduceTransparency = workspace != 0
                                 && objc_msgSend_bool(workspace, sel_registerName("accessibilityDisplayShouldReduceTransparency"));
        var reduceMotion = workspace != 0
                           && objc_msgSend_bool(workspace, sel_registerName("accessibilityDisplayShouldReduceMotion"));
        var increaseContrast = workspace != 0
                               && objc_msgSend_bool(workspace, sel_registerName("accessibilityDisplayShouldIncreaseContrast"));

        SetClass(window, "reduce-transparency", reduceTransparency);
        SetClass(window, "reduce-motion", reduceMotion);
        SetClass(window, "increase-contrast", increaseContrast);

        if (!reduceTransparency)
        {
            AttachVisualEffect(window);
        }
    }

    private static void AttachVisualEffect(Window window)
    {
        if (MaterialWindows.TryGetValue(window, out _)
            || window.TryGetPlatformHandle() is not IMacOSTopLevelPlatformHandle { NSWindow: not 0 } handle)
        {
            return;
        }

        try
        {
            var contentView = objc_msgSend(handle.NSWindow, sel_registerName("contentView"));
            if (contentView == 0)
            {
                return;
            }

            var effectClass = objc_getClass("NSVisualEffectView");
            var effect = objc_msgSend(effectClass, sel_registerName("alloc"));
            var frame = new NativeRect(0, 0, Math.Max(window.ClientSize.Width, 1), Math.Max(window.ClientSize.Height, 1));
            effect = objc_msgSend_rect(effect, sel_registerName("initWithFrame:"), frame);
            if (effect == 0)
            {
                return;
            }

            objc_msgSend_nint(effect, sel_registerName("setAutoresizingMask:"), NSViewWidthSizable | NSViewHeightSizable);
            objc_msgSend_nint(effect, sel_registerName("setMaterial:"), NSVisualEffectMaterialWindowBackground);
            objc_msgSend_nint(effect, sel_registerName("setBlendingMode:"), NSVisualEffectBlendingModeBehindWindow);
            objc_msgSend_nint(effect, sel_registerName("setState:"), NSVisualEffectStateFollowsWindowActiveState);
            objc_msgSend_three_nint(contentView, sel_registerName("addSubview:positioned:relativeTo:"), effect, NSWindowBelow, 0);
            objc_msgSend_void(effect, sel_registerName("release"));
            objc_msgSend_bool_arg(handle.NSWindow, sel_registerName("setTitlebarAppearsTransparent:"), true);

            MaterialWindows.Add(window, new object());
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Attach macOS visual effect", ex);
        }
    }

    private static void SetClass(Window window, string name, bool enabled)
    {
        if (enabled)
        {
            if (!window.Classes.Contains(name))
            {
                window.Classes.Add(name);
            }
        }
        else
        {
            window.Classes.Remove(name);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect(double x, double y, double width, double height)
    {
        private readonly double _x = x;
        private readonly double _y = y;
        private readonly double _width = width;
        private readonly double _height = height;
    }

    [DllImport(LibObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(LibObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool(nint receiver, nint selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(nint receiver, nint selector, nint argument);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_rect(nint receiver, nint selector, NativeRect argument);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_nint(nint receiver, nint selector, nint argument);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_three_nint(nint receiver, nint selector, nint argument1, nint argument2, nint argument3);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool_arg(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool argument);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(nint receiver, nint selector);
}
