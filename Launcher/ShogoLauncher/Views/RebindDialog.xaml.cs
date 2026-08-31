using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShogoLauncher.Views;

/// <summary>
/// Modal capture dialog: waits for one key press, mouse button, or wheel
/// tick and reports it as an engine binding trigger — or an explicit
/// "clear this binding" request.
///
/// Keyboard: WPF Key -> Win32 virtual key -> DirectInput scancode via
/// MapVirtualKey, with the extended-key set offset by 0x80 (DIK_* codes).
/// Mouse: button index 0-4; wheel reported as up/down.
/// </summary>
public partial class RebindDialog : Window
{
    public enum CaptureKind { Key, MouseButton, WheelUp, WheelDown, Clear }

    public CaptureKind Kind { get; private set; }
    public int ScanCode { get; private set; }         // CaptureKind.Key
    public int MouseButtonIndex { get; private set; } // CaptureKind.MouseButton

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
    private const uint MAPVK_VK_TO_VSC = 0;

    private static readonly HashSet<Key> ExtendedKeys = new()
    {
        Key.Up, Key.Down, Key.Left, Key.Right,
        Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown,
        Key.RightCtrl, Key.RightAlt, Key.Divide, Key.NumLock,
    };

    public RebindDialog(string actionName, string slotName)
    {
        InitializeComponent();
        PromptText.Text = $"Press a key, mouse button, or scroll the wheel\nto set the {slotName} binding for \"{actionName}\"...";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.None) return;

        int vk = KeyInterop.VirtualKeyFromKey(key);
        int scan = (int)MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
        if (scan == 0) return;

        if (ExtendedKeys.Contains(key)) scan |= 0x80;

        Kind = CaptureKind.Key;
        ScanCode = scan;
        DialogResult = true;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        // Clicks on the dialog's own buttons are their own commands, not
        // capture input. Walk the visual tree - OriginalSource is usually a
        // TextBlock or Border inside the button template, not the Button.
        if (IsInsideDialogButton(e.OriginalSource as DependencyObject)) return;

        e.Handled = true;
        Kind = CaptureKind.MouseButton;
        MouseButtonIndex = e.ChangedButton switch
        {
            MouseButton.Left => 0,
            MouseButton.Right => 1,
            MouseButton.Middle => 2,
            MouseButton.XButton1 => 3,
            MouseButton.XButton2 => 4,
            _ => 0,
        };
        DialogResult = true;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        Kind = e.Delta > 0 ? CaptureKind.WheelUp : CaptureKind.WheelDown;
        DialogResult = true;
    }

    private bool IsInsideDialogButton(DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ClearButton) || ReferenceEquals(node, CancelButton)) return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Kind = CaptureKind.Clear;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
