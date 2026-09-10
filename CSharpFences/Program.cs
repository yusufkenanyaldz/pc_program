using System;
using System.Windows.Forms;

namespace CSharpFences
{
    internal static class Program
    {
        // Uygulamanın giriş noktası. Bu olmadan proje ÇALIŞMAZ.
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
