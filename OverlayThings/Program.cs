using System;
using System.Windows.Forms;

namespace OverlayThings
{
    internal static class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (await OverlayThings.IsServerRunning())
            {
                // Act as a client
                await OverlayThings.RunAsClient(args);
            }
            else
            {
                // Act as a server
                var overlay = new OverlayThings();
                await overlay.RunAsServer();
            }
        }
    }
}
