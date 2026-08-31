using System.Windows;
using ShogoLauncher.Services;
using ShogoLauncher.ViewModels;

namespace ShogoLauncher.Views;

/// <summary>
/// Game Setup modal: compat fixes + the ShogoFRESH overlay + DirectPlay.
/// Shown automatically at startup while anything still needs attention,
/// and on demand from Settings.
/// </summary>
public partial class SetupWindow : Window
{
    private readonly MainViewModel _vm;

    public SetupWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        UpdateDirectPlayUi();
    }

    private void ApplyFix_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MainViewModel.FixRow row)
            _vm.ApplyFix(row);
    }

    private void UndoFix_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not MainViewModel.FixRow row) return;
        var answer = MessageBox.Show(
            $"Remove {row.Title} and restore the previous files?",
            "Undo fix", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) _vm.UndoFix(row);
    }

    private void EnableDirectPlay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GameSetupService.EnableDirectPlay();
            _vm.Status = "DISM launched (UAC prompt) - click Re-check when it finishes.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _vm.Warn("Elevation was declined - DirectPlay was not enabled.");
        }
    }

    private void EnableAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _vm.Fixes.Where(f => f.CanApply).ToList())
            _vm.ApplyFix(row);

        if (!GameSetupService.IsDirectPlayEnabled())
        {
            try { GameSetupService.EnableDirectPlay(); }
            catch (System.ComponentModel.Win32Exception) { _vm.Warn("Elevation declined - DirectPlay not enabled."); }
        }
        UpdateDirectPlayUi();
    }

    private void RefreshSetup_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshSetup();
        UpdateDirectPlayUi();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ //
    //  Shogo not detected
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Point at the install by hand. Stays open on a wrong folder rather than
    /// closing and making them find this window again - the message says what
    /// was wrong with the last attempt and the button is still under it.
    /// </summary>
    private void LocateGame_Click(object sender, RoutedEventArgs e)
    {
        var pick = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Where is Shogo installed?",
        };

        if (pick.ShowDialog(this) != true) return;

        if (_vm.TryAdoptGameDir(pick.FolderName))
        {
            // Found. The rest of the window is now meaningful, so re-read
            // DirectPlay too and let them carry straight on rather than
            // closing something they will immediately reopen.
            UpdateDirectPlayUi();
        }
    }

    private void RetryDetect_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.RetryDetect()) UpdateDirectPlayUi();
    }

    /// <summary>
    /// The honest exit. Every other door in this launcher leads to something
    /// that needs a game folder, so with no game there is nothing to fall
    /// back to and pretending otherwise wastes the person's time.
    ///
    /// <para>
    /// Confirmed rather than immediate, because "Close launcher" sitting
    /// beside two buttons that are trying to help is easy to hit by mistake -
    /// and this is the one of the three that cannot be undone by clicking
    /// again.
    /// </para>
    /// </summary>
    private void QuitLauncher_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Close ShogoFRESH?\n\n" +
            "Nothing has been changed on this machine. When Shogo is installed, " +
            "start the launcher again and it will pick it up.",
            "Close launcher", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes) Application.Current.Shutdown();
    }

    private void UpdateDirectPlayUi()
    {
        bool enabled = GameSetupService.IsDirectPlayEnabled();
        DirectPlayStatus.Text = enabled ? "Enabled" : "Not enabled";
        EnableDirectPlayButton.IsEnabled = !enabled;
    }
}
