using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ShogoLauncher.Models;
using ShogoLauncher.Services;
using ShogoLauncher.ViewModels;

namespace ShogoLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (v is not null) Title = $"ShogoFRESH v{v.Major}.{v.Minor}.{v.Build}";
        Loaded += async (_, _) =>
        {
            ApplyServerSort("Name", ListSortDirection.Ascending);

            // Fresh-install flow: pop the Setup modal while anything still
            // needs attention (missing fix or DirectPlay off).
            if (_vm.SetupNeeded) ShowSetupModal();

            // After the Setup modal, because that is where a missing game
            // gets located - and there is nothing to migrate until there is
            // a game directory to migrate in.
            _vm.MigrateKeybindsOnce();

            _vm.RefreshServerProfiles();

            await _vm.RefreshServersAsync();

            // Last, and deliberately not awaited into anything that gates
            // the UI: an update check must never be the reason the launcher
            // is slow to appear or fails to open.
            await _vm.CheckForUpdatesAsync();
        };
    }

    // ----- Server list sorting (favorites always pinned on top) -----

    private void ServerGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var dir = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        foreach (var c in ServerGrid.Columns) c.SortDirection = null;
        e.Column.SortDirection = dir;
        ApplyServerSort(e.Column.SortMemberPath, dir);
    }

    private void ApplyServerSort(string key, ListSortDirection dir)
    {
        if (CollectionViewSource.GetDefaultView(_vm.Servers) is ListCollectionView view)
            view.CustomSort = new ServerComparer(key, dir);
    }

    /// <summary>Favorites first, then the chosen column; offline/unqueried (ping -1) always last on ping sorts.</summary>
    private sealed class ServerComparer : IComparer
    {
        private readonly string _key;
        private readonly int _sign;

        public ServerComparer(string key, ListSortDirection dir)
        {
            _key = key;
            _sign = dir == ListSortDirection.Ascending ? 1 : -1;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ServerInfo a || y is not ServerInfo b) return 0;

            if (a.IsFavorite != b.IsFavorite) return a.IsFavorite ? -1 : 1;

            int c = _key switch
            {
                "Name" => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                "DisplayAddress" => string.Compare(a.DisplayAddress, b.DisplayAddress, StringComparison.OrdinalIgnoreCase),
                "Map" => string.Compare(a.Map, b.Map, StringComparison.OrdinalIgnoreCase),
                "GameType" => string.Compare(a.GameType, b.GameType, StringComparison.OrdinalIgnoreCase),
                "Players" => a.Players.CompareTo(b.Players),
                "PingMs" => NormalizePing(a.PingMs).CompareTo(NormalizePing(b.PingMs)),
                "IsFavorite" => 0,
                _ => 0,
            };
            return c * _sign;
        }

        // Unqueried servers (-1) sort after real pings in either direction.
        private long NormalizePing(long ping) => ping < 0 ? long.MaxValue * Math.Sign(_sign) : ping;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await _vm.RefreshServersAsync();

    private void Join_Click(object sender, RoutedEventArgs e) => _vm.JoinSelected();

    private void ServerGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Clicks on the Fav checkbox (or anything outside a data row, like
        // the header) must never trigger a join.
        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null) return;
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is null) return;
        _vm.JoinSelected();
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T hit) return hit;
            node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    private void LaunchOnly_Click(object sender, RoutedEventArgs e) => _vm.LaunchGameOnly();

    private void AddServer_Click(object sender, RoutedEventArgs e)
    {
        var text = AddAddressBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (!int.TryParse(AddPortBox.Text.Trim(), out var port) || port <= 0 || port > 65535)
        {
            port = 27888;
        }

        // A pasted "ip:port" still works - the embedded port wins, since
        // typing it that way is a clear statement of intent.
        var parts = text.Split(':');
        if (parts.Length > 1 && int.TryParse(parts[1], out var embedded)) port = embedded;

        _vm.AddManualServer(parts[0], port);

        AddAddressBox.Clear();
        AddPortBox.Text = "27888";
    }

    private void RemoveServer_Click(object sender, RoutedEventArgs e) => _vm.RemoveSelectedServer();

    private void OpenUpdate_Click(object sender, RoutedEventArgs e) => _vm.OpenUpdatePage();

    private async void CheckUpdatesNow_Click(object sender, RoutedEventArgs e) =>
        await _vm.CheckForUpdatesAsync(force: true);

    /// <summary>Hide the banner for this session; the next launch re-checks.</summary>
    private void DismissUpdate_Click(object sender, RoutedEventArgs e) => _vm.DismissUpdate();

    private void SaveSettings_Click(object sender, RoutedEventArgs e) => _vm.SaveSettings();

    // ----- Unsaved-changes guard -----

    private TabItem? _lastTab;
    private bool _switchingTabs;

    /// <summary>Leaving Settings with unsaved changes: offer to save, discard, or stay.</summary>
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The masked box holds a copy rather than a binding, so it has to be
        // refilled from the view model whenever the Host tab is shown - the
        // saved password arrives from ShogoSrv.cfg long after this window
        // was built, and without this it would look blank and a Save would
        // then make it blank for real.
        if ((MainTabs.SelectedItem as TabItem)?.Header as string == "Host" &&
            RconPasswordPlain.Visibility != Visibility.Visible)
        {
            _syncingRcon = true;
            RconPasswordMasked.Password = _vm.HostRconPassword ?? "";
            _syncingRcon = false;
        }

        if (_switchingTabs || e.OriginalSource != MainTabs) return;

        var leaving = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as TabItem : null;
        var tab = leaving?.Header as string;

        bool dirty = tab switch
        {
            "Settings" => _vm.SettingsDirty,
            "Keybinds" => _vm.BindingsDirty,
            "Host" => _vm.HostDirty,
            _ => false,
        };

        if (!dirty)
        {
            _lastTab = MainTabs.SelectedItem as TabItem;
            return;
        }

        var answer = MessageBox.Show(
            $"You have unsaved {tab!.ToLower()} changes.\n\nSave them before leaving?",
            "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            switch (tab)
            {
                case "Settings": _vm.SaveSettings(); break;
                case "Keybinds": _vm.Bindings?.Save(); _vm.BindingsDirty = false; break;
                case "Host": _vm.SaveHostSettings(); break;
            }
        }
        else if (answer == MessageBoxResult.Cancel)
        {
            // Go back without re-triggering this handler.
            _switchingTabs = true;
            MainTabs.SelectedItem = leaving;
            _switchingTabs = false;
            return;
        }
        else
        {
            // Discard: re-read from disk.
            if (tab == "Keybinds") _vm.ReloadBindings();
            else _vm.LoadFromGameDir();
        }

        _lastTab = MainTabs.SelectedItem as TabItem;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_vm.SettingsDirty || _vm.BindingsDirty || _vm.HostDirty)
        {
            var answer = MessageBox.Show(
                "You have unsaved changes.\n\nSave before closing?",
                "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (answer == MessageBoxResult.Yes)
            {
                if (_vm.SettingsDirty) _vm.SaveSettings();
                if (_vm.BindingsDirty) { _vm.Bindings?.Save(); _vm.BindingsDirty = false; }
                if (_vm.HostDirty) _vm.SaveHostSettings();
            }
        }
        base.OnClosing(e);
    }

    private void RandomName_Click(object sender, RoutedEventArgs e) =>
        _vm.PlayerName = PilotNameGenerator.Generate();

    // --- rcon password: masked by default, revealed on request ---------- //
    //
    // A PasswordBox cannot be data-bound - WPF leaves Password off the
    // dependency-property system deliberately, so that a password is not
    // sitting in a binding engine's caches. That is why this is two
    // controls over one view-model string rather than a binding and a
    // Visibility trigger. _syncingRcon stops the two from echoing each
    // other: writing the box raises PasswordChanged, which would write the
    // view model, which would write the box.

    private bool _syncingRcon;

    private void RconPasswordMasked_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingRcon) return;

        _syncingRcon = true;
        _vm.HostRconPassword = RconPasswordMasked.Password;
        _syncingRcon = false;
    }

    private void ToggleRconPassword_Click(object sender, RoutedEventArgs e)
    {
        bool revealing = RconPasswordPlain.Visibility != Visibility.Visible;

        _syncingRcon = true;

        if (revealing)
        {
            // The view model is the truth in both directions; copy it into
            // whichever control is about to be looked at.
            RconPasswordPlain.Text = _vm.HostRconPassword ?? "";
        }
        else
        {
            RconPasswordMasked.Password = _vm.HostRconPassword ?? "";
        }

        _syncingRcon = false;

        RconPasswordPlain.Visibility  = revealing ? Visibility.Visible : Visibility.Collapsed;
        RconPasswordMasked.Visibility = revealing ? Visibility.Collapsed : Visibility.Visible;

        RconRevealButton.Content = revealing ? "Hide" : "Show";
    }

    private void StartHost_Click(object sender, RoutedEventArgs e)
    {
        // The warning is only true for a DEDICATED server, and since 0.10.19
        // it has been actively false for a listen one.
        //
        // "Two servers on the same port will fight over it" was accurate when
        // both paths took the port straight from ShogoSrv.cfg. A listen server
        // now asks the network stack which ports are bound and moves to a free
        // one, reporting where it went - so this dialog was offering to let the
        // player proceed into a disaster the launcher had already prevented.
        //
        // Warning about a solved problem is not harmless. It teaches people to
        // click through the dialog, which is exactly the habit you do not want
        // when the dedicated path raises the same box and means it.
        //
        // Dedicated still binds what the config says, so it still asks.

        int nRunning = _vm.RunningServerCount();

        if (nRunning > 0 && !_vm.HostIsListen)
        {
            var answer = MessageBox.Show(
                $"{nRunning} Shogo server{(nRunning == 1 ? " is" : "s are")} already running.\n\n" +
                "Two servers on the same port will fight over it and neither will be reachable.\n\n" +
                "Start another anyway?",
                "Server already running", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes) return;
        }

        _vm.StartHosting();
    }

    private void SaveHost_Click(object sender, RoutedEventArgs e) => _vm.SaveHostSettings();

    // ----- Host tab: server profiles -----

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = _vm.NewProfileName.Trim();
        if (string.IsNullOrEmpty(name)) { _vm.Warn("Give the profile a name first."); return; }

        if (ServerProfiles.Exists(name) &&
            MessageBox.Show($"Overwrite the existing \"{name}\" profile?", "Profile exists",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _vm.SaveServerProfile(name);
        _vm.NewProfileName = "";
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedServerProfile is not string name) return;

        if (_vm.HostDirty &&
            MessageBox.Show("You have unsaved server changes. Loading a profile discards them.\n\nContinue?",
                            "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _vm.LoadServerProfile(name);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedServerProfile is not string name) return;

        if (MessageBox.Show($"Delete the \"{name}\" profile?", "Delete profile",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        ServerProfiles.Delete(name);
        _vm.RefreshServerProfiles();
        _vm.Status = $"Deleted server profile \"{name}\".";
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedServerProfile is not string name) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export server profile",
            FileName = name + ".cfg",
            Filter = "Shogo server config (*.cfg)|*.cfg|All files (*.*)|*.*",
        };

        if (dlg.ShowDialog() != true) return;

        ServerProfiles.Export(name, dlg.FileName);
        _vm.Status = $"Exported \"{name}\" to {dlg.FileName}.";
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import server profile",
            Filter = "Shogo server config (*.cfg)|*.cfg|All files (*.*)|*.*",
        };

        if (dlg.ShowDialog() != true) return;

        var name = ServerProfiles.Import(dlg.FileName);
        _vm.RefreshServerProfiles(name);
        _vm.Status = $"Imported server profile \"{name}\".";
    }

    private void PickNightColor_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.ColorPickerDialog(_vm.HostNightColor) { Owner = this };
        if (dlg.ShowDialog() == true) _vm.HostNightColor = dlg.ColorString;
    }

    private void RestoreSettingsDefaults_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Load ShogoFRESH's recommended settings?\n\nNothing is written until you click Save Settings.",
            "Restore defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) _vm.RestoreSettingsDefaults();
    }

    private void FirewallRule_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HostService.AddFirewallRule(_vm.HostPort);
            _vm.Status = $"Firewall rule requested for UDP {_vm.HostPort} and {_vm.HostPort + 149} (UAC prompt). Router port-forwarding is still up to you.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _vm.Warn("Elevation declined - no firewall rule was added.");
        }
    }

    /// <summary>
    /// Fill the peers box from the servers currently responding in the browser.
    ///
    /// Closes the discovery loop: the launcher already knows a set of live
    /// addresses, and a new server needs exactly that to join the peer network.
    /// Without this a host has to go and copy addresses out of the Play tab by
    /// hand, which is the kind of friction that means nobody bothers.
    ///
    /// Only servers that actually answered are offered - handing a new server a
    /// list of dead addresses would have it announcing into the void.
    /// </summary>
    private void SeedPeers_Click(object sender, RoutedEventArgs e)
    {
        // PingMs >= 0, not Online: master-scraped rows keep Online even when
        // they never answer a query, and peers written here are PERSISTENT -
        // the server never ages a configured peer out, so a stale master row
        // would sit in the config for ever. Only what WE verified goes in.

        var live = _vm.Servers
            .Where(s => s.Online && s.PingMs >= 0)
            .Select(s => $"{s.Address}:{s.Port}")
            .ToList();

        if (live.Count == 0)
        {
            _vm.Warn("No servers have answered a query in the Play tab - refresh it first, or type an address in by hand.");
            return;
        }

        // Merge with what is already typed rather than replacing it. A host
        // who hand-entered an address they know is right should not lose it
        // to a button that was meant to save them typing.

        var merged = (_vm.HostPeers ?? "")
            .Split(new[] { ' ', ',', '	' }, StringSplitOptions.RemoveEmptyEntries)
            .Concat(live)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();

        int added = merged.Count - ((_vm.HostPeers ?? "")
            .Split(new[] { ' ', ',', '	' }, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());

        _vm.HostPeers = string.Join(' ', merged);
        _vm.Status = added > 0
            ? $"Added {added} verified peer(s) from the browser. Save to write them into ShogoSrv.cfg."
            : "No new peers - everything responding is already in the list.";
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveFavoritesNow();
        // Deferred re-sort so the row moves to/from the pinned block without
        // refreshing the view from inside the checkbox's own event.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            () => CollectionViewSource.GetDefaultView(_vm.Servers)?.Refresh());
    }

    private void DetailAdvanced_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.GameFound) return;
        var dlg = new Views.DetailDialog(_vm.GetDetailValues().Select(v => (v.Name, v.Value))) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _vm.SaveDetailValues(dlg.Rows.Select(r => new MainViewModel.DetailVar(r.Name, r.Value)));
    }

    private void EditBlocklist_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.BlockedPickupsDialog(_vm.HostBlockedWeapons, _vm.HostBlockedItems) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _vm.HostBlockedWeapons = dlg.BlockedWeapons;
        _vm.HostBlockedItems = dlg.BlockedItems;
    }

    private void RefreshMods_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshMods();
        _vm.Status = $"Mods rescanned - {_vm.Mods.Count} found.";
    }

    private void ViewRez_Click(object sender, RoutedEventArgs e) => ShowRezContents();

    /// <summary>
    /// The viewer, on any archive rather than only on an installed mod.
    ///
    /// <para>
    /// The extractor was always capable of this; it just had no door. The
    /// only route in was a row of the Mods grid, and that grid scans
    /// <c>Custom\</c> - so the game's own SHOGO.REZ and SOUND.REZ, sitting
    /// in the game folder, could not be reached at all. Those two are
    /// precisely what a texture or sound mod starts from, which made the
    /// modding guide send people to Monolith's <c>lithrez.exe</c> for a job
    /// the launcher could already do.
    /// </para>
    /// </summary>
    private void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        var picked = Views.RezViewerDialog.PickArchive(this, _vm.GameDir);
        if (picked is null) return;

        new Views.RezViewerDialog(picked, _vm.GameDir) { Owner = this }.ShowDialog();
    }

    private void ModsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // The Enabled column is a checkbox; a double-click there is two
        // toggles and opening a window on top of that would be a surprise.
        if (e.OriginalSource is System.Windows.DependencyObject d &&
            FindParent<System.Windows.Controls.CheckBox>(d) is not null) return;

        ShowRezContents();
    }

    /// <summary>
    /// Opens the archive viewer for the selected mod. A .dat is a single
    /// level rather than an archive, so there is nothing inside it to list.
    /// </summary>
    private void ShowRezContents()
    {
        if (ModsGrid.SelectedItem is not ViewModels.MainViewModel.ModRow row)
        {
            _vm.Status = "Select a mod first.";
            return;
        }

        if (!row.Name.EndsWith(".rez", StringComparison.OrdinalIgnoreCase))
        {
            _vm.Status = $"{row.Name} is a single level file, not an archive - nothing inside to list.";
            return;
        }

        new Views.RezViewerDialog(row.Path, _vm.GameDir) { Owner = this }.ShowDialog();
    }

    private static T? FindParent<T>(System.Windows.DependencyObject from) where T : System.Windows.DependencyObject
    {
        for (var at = from; at is not null; at = System.Windows.Media.VisualTreeHelper.GetParent(at))
            if (at is T hit) return hit;

        return null;
    }

    private void OpenCustom_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.GameDir is null) return;
        var custom = System.IO.Path.Combine(_vm.GameDir, "Custom");
        if (System.IO.Directory.Exists(custom))
            Process.Start(new ProcessStartInfo("explorer.exe", custom) { UseShellExecute = true });
    }

    // ----- Keybinds tab -----

    /// <summary>Clicking a Primary (col 1) or Secondary (col 2) cell opens the capture dialog for that slot.</summary>
    private void KeybindCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridCell cell) return;
        int slot = cell.Column.DisplayIndex switch { 1 => 0, 2 => 1, _ => -1 };
        if (slot < 0) return;
        if (cell.DataContext is not MainViewModel.BindingRow row || _vm.Bindings is null) return;

        e.Handled = true;
        var dlg = new Views.RebindDialog(row.Label, slot == 0 ? "primary" : "secondary") { Owner = this };
        if (dlg.ShowDialog() != true) return;

        if (dlg.Kind == Views.RebindDialog.CaptureKind.Clear)
        {
            _vm.Bindings.ClearBinding(row.Action, slot);
            _vm.BindingsDirty = true;
            RefreshBindRows();
            _vm.Status = $"Cleared the {(slot == 0 ? "primary" : "secondary")} binding for {row.Label}. Click 'Save Bindings' to write.";
            return;
        }

        var (device, trigger) = dlg.Kind switch
        {
            Views.RebindDialog.CaptureKind.MouseButton => ("##mouse", _vm.Bindings.MouseButtonName(dlg.MouseButtonIndex)),
            Views.RebindDialog.CaptureKind.WheelUp => ("##mouse", BindingStore.WheelUp),
            Views.RebindDialog.CaptureKind.WheelDown => ("##mouse", BindingStore.WheelDown),
            _ => ("##keyboard", $"##{dlg.ScanCode}"),
        };

        var stolen = _vm.Bindings.SetBinding(row.Action, slot, device, trigger);
        _vm.BindingsDirty = true;
        RefreshBindRows();

        if (stolen.Count > 0)
        {
            var labels = string.Join(", ", stolen.Select(a => _vm.Layout.LabelFor(a)));
            _vm.Warn($"Bound {row.Label} to {BindingStore.TriggerDisplay(device, trigger)} — unbound it from: {labels}. Click 'Save Bindings' to write.");
        }
        else
        {
            _vm.Status = $"Bound {row.Label} to {BindingStore.TriggerDisplay(device, trigger)}. Click 'Save Bindings' to write.";
        }
    }

    private void RefreshBindRows()
    {
        var selected = KeybindsGrid.SelectedIndex;
        _vm.RebuildBindRows();
        KeybindsGrid.SelectedIndex = selected;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Reset all live bindings to the defkeybd.cfg defaults?\nNothing is written until you click 'Save Bindings'.",
            "Restore default bindings", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        if (_vm.RestoreDefaultBindings())
        {
            _vm.BindingsDirty = true;
            _vm.Status = "Bindings reset to defaults (in memory). Click 'Save Bindings' to write.";
        }
        else
            _vm.Warn("Could not restore defaults (defkeybd.cfg missing, unreadable, or you are editing the defaults themselves).");
    }

    private void ApplyBindLayout_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedBindLayout is null)
        {
            _vm.Warn("Pick a layout first.");
            return;
        }

        // Same confirmation shape as Restore Defaults: this replaces every
        // binding, so it should not be one stray click away.
        var answer = MessageBox.Show(
            $"Replace all current bindings with the \"{_vm.SelectedBindLayout.Title}\" layout?\n" +
            "Nothing is written until you click 'Save Bindings'.",
            "Apply keybind layout", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        if (_vm.ApplySelectedBindLayout(out var error))
        {
            _vm.BindingsDirty = true;
            _vm.Status = $"{_vm.SelectedBindLayout.Title} applied (in memory). Click 'Save Bindings' to write.";
        }
        else
            _vm.Warn($"Could not apply layout: {error}");
    }

    private void SaveBindings_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Bindings is null) return;
        if (!_vm.Bindings.Loaded)
        {
            MessageBox.Show(
                "This config has no binding block yet. Launch the game once so the engine writes its full config, then rebind here.",
                "No bindings found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _vm.Bindings.Save();
        _vm.BindingsDirty = false;
        _vm.Status = $"Bindings saved to {System.IO.Path.GetFileName(_vm.Bindings.ConfigPath)}.";
    }

    private void ReloadBindings_Click(object sender, RoutedEventArgs e)
    {
        _vm.ReloadBindings();
        _vm.Status = "Bindings reloaded.";
    }

    private void EditLayout_Click(object sender, RoutedEventArgs e)
    {
        // Materialized on first load, so the file always exists.
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{KeybindLayout.LayoutPath}\"") { UseShellExecute = true });
        _vm.Status = "Edit labels/order/hidden in the JSON, save, then click Reload Layout.";
    }

    private void ReloadLayout_Click(object sender, RoutedEventArgs e)
    {
        _vm.ReloadLayout();
        _vm.Status = "Keybind layout reloaded.";
    }

    // ----- Setup modal -----

    private void OpenSetup_Click(object sender, RoutedEventArgs e) => ShowSetupModal();

    /// <summary>
    /// Open %AppData%\ShogoFRESH\Logs, where the crash handler writes.
    ///
    /// Created on demand rather than assumed to exist: the game only makes it
    /// when something actually crashes, so on a healthy install there is
    /// nothing there - and a button that errors because you have never
    /// crashed is a worse experience than an empty folder.
    /// </summary>
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = System.IO.Path.Combine(Services.AppPaths.Root, "Logs");
            System.IO.Directory.CreateDirectory(dir);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            // Status alone: StatusIsError has a private setter and the
            // viewmodel raises it from its own Warn path.
            _vm.Status = "Could not open the logs folder: " + ex.Message;
        }
    }

    /// <summary>
    /// Open Save\screenshots under the game folder, where FreshShot.cpp writes
    /// F8 captures (SHOT_FOLDER). Unlike the logs folder this lives beside the
    /// game, not in AppData, so it needs a known game directory - guarded
    /// rather than assumed. Created on demand for the same reason the logs
    /// folder is: someone who has never taken a screenshot should get an empty
    /// folder, not an error.
    /// </summary>
    private void OpenScreenshots_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_vm.GameDir))
            {
                _vm.Status = "No game folder yet - detect the install first.";
                return;
            }

            var dir = System.IO.Path.Combine(_vm.GameDir, "Save", "screenshots");
            System.IO.Directory.CreateDirectory(dir);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _vm.Status = "Could not open the screenshots folder: " + ex.Message;
        }
    }

    private void ShowSetupModal()
    {
        _vm.RefreshSetup();
        new Views.SetupWindow(_vm) { Owner = this }.ShowDialog();
        _vm.RefreshSetup();
    }

    // ----- Host tab: map rotation -----

    /// <summary>
    /// Re-scan the Available list. Deliberately does NOT reload host state:
    /// the rotation on the right is the operator's working set and rebuilding
    /// it from the saved cfg would throw away unsaved arranging - a refresh
    /// that costs you your work is worse than no refresh.
    /// </summary>
    private void RefreshMaps_Click(object sender, RoutedEventArgs e)
    {
        // What was picked before, so a re-scan does not lose the selection.
        var picked = AvailableMapsList.SelectedItems.Cast<string>().ToList();

        int nBefore = _vm.AvailableMaps.Count;

        _vm.ScanAvailableMaps();

        foreach (var map in picked)
        {
            if (_vm.AvailableMaps.Contains(map))
                AvailableMapsList.SelectedItems.Add(map);
        }

        int nAfter = _vm.AvailableMaps.Count;
        int nNew   = nAfter - nBefore;

        // Say what changed rather than just that something happened. "Nothing
        // new" is the answer a mapper most needs, because it means the map did
        // not land where the launcher looks - not that the button failed.
        _vm.MapScanSummary = nNew switch
        {
            > 0 => $"+{nNew} new ({nAfter} total)",
            < 0 => $"{nNew} gone ({nAfter} total)",
            _   => $"no change ({nAfter} total)",
        };
    }

    /// <summary>
    /// Open Custom\maps\mp in Explorer - where multiplayer maps go.
    ///
    /// CREATES IT IF ABSENT, which is a side effect worth defending: it is a
    /// folder the launcher already scans and already documents, and a button
    /// that opens nothing teaches a mapper that maps\mp is not a real place.
    /// Making it is the answer they wanted anyway.
    ///
    /// maps\mp specifically, not Custom\. The folder a map lands in decides
    /// which LISTS it appears in - maps\mp feeds the rotation and is kept out
    /// of the single-player menu - so opening the parent would be pointing at
    /// the wrong answer for the list this button sits under.
    /// </summary>
    private void OpenMapsFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.GameFound)
        {
            _vm.Warn("Find the game first - the maps folder lives inside the install.");
            return;
        }

        var dir = System.IO.Path.Combine(_vm.GameDir!, "Custom", "maps", "mp");

        try
        {
            System.IO.Directory.CreateDirectory(dir);

            // explorer.exe /n, NOT ShellExecute on the directory.
            //
            // Windows 11's Explorer is tabbed, and asking the shell to "open"
            // a folder can simply ACTIVATE an already-open Explorer window -
            // leaving it on whatever tab it was showing and never going to the
            // path at all. Reported from play exactly that way: the button
            // worked, but only when no Explorer window was already open, which
            // is the worst kind of intermittent because it looks like the
            // button is flaky rather than the shell being clever.
            //
            // "/n," forces a new single-pane window at the path, so it always
            // lands where it says. The cost, stated because it is real: press
            // it twice and you get two windows. That is a better failure than
            // pressing it once and getting nothing.
            //
            // TRAILING SEPARATOR STRIPPED FIRST. A path ending in a backslash
            // puts \" at the end of the argument, where the backslash escapes
            // the quote and Explorer receives a mangled path. Path.Combine
            // does not produce one here, but the day someone passes a drive
            // root this is the line that stops it.

            var arg = dir.TrimEnd(System.IO.Path.DirectorySeparatorChar,
                                  System.IO.Path.AltDirectorySeparatorChar);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"/n,\"{arg}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _vm.Warn($"Could not open {dir}: {ex.Message}");
        }
    }

    private void AddMap_Click(object sender, RoutedEventArgs e)
    {
        // Snapshot the selection: adding to the rotation can disturb it.
        var picked = AvailableMapsList.SelectedItems.Cast<string>().ToList();

        var added = new List<string>();

        foreach (var map in picked)
        {
            if (!_vm.MapRotation.Contains(map)) { _vm.MapRotation.Add(map); added.Add(map); }
        }

        // Select what was just added in the rotation list, so the eye lands on
        // where it went. Without this, adding several maps to a long rotation
        // gave no sign of what happened or where - the entries just appeared
        // somewhere below the fold. Selecting a map already in the rotation
        // (the skipped duplicates) is deliberately not done: only genuinely
        // new entries are highlighted, so the feedback means "this is new".
        if (added.Count > 0)
        {
            RotationList.SelectedItems.Clear();
            foreach (var map in added) RotationList.SelectedItems.Add(map);
            RotationList.ScrollIntoView(added[^1]);
        }
    }

    private void RemoveMap_Click(object sender, RoutedEventArgs e)
    {
        foreach (var map in RotationList.SelectedItems.Cast<string>().ToList())
        {
            _vm.MapRotation.Remove(map);
        }
    }

    // Up/Down/Top/Bottom act on the first selected entry, and the moved
    // entry keeps the selection so a run of clicks walks one map instead of
    // moving whatever happens to land under the cursor.
    private void MoveMapTo(int target)
    {
        int i = RotationList.SelectedIndex;
        if (i < 0 || _vm.MapRotation.Count == 0) return;

        if (target < 0) target = 0;
        if (target > _vm.MapRotation.Count - 1) target = _vm.MapRotation.Count - 1;
        if (target == i) return;

        _vm.MapRotation.Move(i, target);

        RotationList.SelectedIndex = target;
        RotationList.ScrollIntoView(RotationList.SelectedItem);
    }

    private void MapUp_Click(object sender, RoutedEventArgs e) =>
        MoveMapTo(RotationList.SelectedIndex - 1);

    private void MapDown_Click(object sender, RoutedEventArgs e) =>
        MoveMapTo(RotationList.SelectedIndex + 1);

    private void MapTop_Click(object sender, RoutedEventArgs e) => MoveMapTo(0);

    private void MapBottom_Click(object sender, RoutedEventArgs e) =>
        MoveMapTo(_vm.MapRotation.Count - 1);

    private void DetectGame_Click(object sender, RoutedEventArgs e)
    {
        _vm.GameDir = GameLocator.Locate() ?? _vm.GameDir;
        _vm.LoadFromGameDir();
    }

    private void ReloadGame_Click(object sender, RoutedEventArgs e) => _vm.LoadFromGameDir();
}
