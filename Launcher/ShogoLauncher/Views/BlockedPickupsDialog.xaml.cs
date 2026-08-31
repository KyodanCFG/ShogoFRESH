using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

using ShogoLauncher.Services;

namespace ShogoLauncher.Views;

/// <summary>
/// Ticks the pickups a host wants kept out of the level. Produces the two
/// lists the server reads at level start: "BlockWeapons" and "BlockItems".
/// Grouped by tier first, because that is the split that matters in play -
/// a mech level and an on-foot level draw from different pools.
/// </summary>
public partial class BlockedPickupsDialog : Window
{
    public class PickupRow : INotifyPropertyChanged
    {
        public BlockablePickups.Entry Entry { get; init; } = null!;
        public string Name => Entry.Name;
        public string Note => Entry.Note;

        /// <summary>Group header; also the unit the "whole pool is gone" warning works in.</summary>
        public string Group =>
            BlockablePickups.IsSpecial(Entry)
                ? "Ultra powerups and upgrades"
                : (Entry.Mech ? "Mech (MCA) " : "On-foot ") + (Entry.IsWeapon ? "weapons" : "items");

        private bool _blocked;
        public bool Blocked
        {
            get => _blocked;
            set
            {
                _blocked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Blocked)));
                Changed?.Invoke();
            }
        }

        public Action? Changed;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public List<PickupRow> Rows { get; }

    /// <summary>The resulting "BlockWeapons" value; only meaningful after OK.</summary>
    public string BlockedWeapons =>
        BlockablePickups.FormatWeapons(Rows.Where(r => r.Blocked && r.Entry.IsWeapon).Select(r => r.Entry.Id));

    /// <summary>The resulting "BlockItems" value; only meaningful after OK.</summary>
    public string BlockedItems =>
        BlockablePickups.FormatItems(Rows.Where(r => r.Blocked && !r.Entry.IsWeapon).Select(r => r.Entry.ClassName));

    public BlockedPickupsDialog(string currentWeapons, string currentItems)
    {
        InitializeComponent();

        var blockedIds = BlockablePickups.ParseWeaponIds(currentWeapons).ToHashSet();
        var blockedClasses = BlockablePickups.ParseItemClasses(currentItems)
                                             .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Rows = BlockablePickups.All.Select(e => new PickupRow
        {
            Entry = e,
            Blocked = e.IsWeapon ? blockedIds.Contains(e.Id) : blockedClasses.Contains(e.ClassName),
        }).ToList();

        foreach (var row in Rows) row.Changed = UpdateWarning;

        var view = new ListCollectionView(Rows);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PickupRow.Group)));
        PickupList.ItemsSource = view;

        UpdateWarning();
    }

    /// <summary>
    /// Blocking a whole pool leaves nothing to substitute in, so those
    /// pickups just disappear. Legal, but worth saying out loud.
    /// </summary>
    private void UpdateWarning()
    {
        // Only meaningful for pools that get substituted from. Ultras and
        // upgrades are removed outright when blocked, so "nothing left to
        // substitute in" says nothing about them.
        var emptied = Rows.Where(r => !BlockablePickups.IsSpecial(r.Entry))
                          .GroupBy(r => r.Group)
                          .Where(g => g.All(r => r.Blocked))
                          .Select(g => g.Key.ToLowerInvariant())
                          .ToList();

        if (emptied.Count == 0)
        {
            WarningText.Visibility = Visibility.Collapsed;
            return;
        }

        WarningText.Text = $"Every entry in {string.Join(" and ", emptied)} is blocked - there is "
                         + "nothing left to substitute in, so those pickups will be removed from "
                         + "the level entirely.";
        WarningText.Visibility = Visibility.Visible;
    }

    private void BlockAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Rows) row.Blocked = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Rows) row.Blocked = false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
