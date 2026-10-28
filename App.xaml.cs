using System.Threading;
using System.Windows;
namespace EdgeTime;
public partial class App : Application {
 private Mutex? mutex;
 protected override void OnStartup(StartupEventArgs e) { mutex = new Mutex(true, "Local\\EdgeTime.SingleInstance", out bool first); if (!first) { Shutdown(); return; } base.OnStartup(e); }
 protected override void OnExit(ExitEventArgs e) { mutex?.Dispose(); base.OnExit(e); }
}
