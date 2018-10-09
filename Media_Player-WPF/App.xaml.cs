using System.Windows;

namespace Media_Player_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string DirectOpenPath { get; set; }

        void App_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args != null && e.Args.Length > 0)
                DirectOpenPath = e.Args[0].ToString();
        }
    }
}
