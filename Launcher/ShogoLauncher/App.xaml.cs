using System.Windows;

namespace ShogoLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 0.2.0 renamed the AppData folder ShogoLauncher -> ShogoFRESH;
        // carry existing favorites/layouts/prefs/fix-manifests across.
        Services.AppPaths.MigrateIfNeeded();
        Services.AppPaths.SeedMotdIfAbsent();
        base.OnStartup(e);
    }
}
