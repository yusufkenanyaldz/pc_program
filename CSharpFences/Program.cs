using System;
using System.Windows.Forms;

namespace CSharpFences
{
    internal static class Program
    {
        // Uygulama giriş noktası. Tek bir pencere yerine, birden çok bağımsız
        // "fence" penceresini ve tepsi (tray) simgesini yöneten bir
        // ApplicationContext çalıştırıyoruz.
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FenceAppContext());
        }
    }
}
