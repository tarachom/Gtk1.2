using Gtk;

namespace GtkTest
{
    class Program
    {
        public static void Main()
        {
            Application.Init();
            new WindowStart();
            Application.Run();
        }

        public static void Quit()
        {
            Application.Quit();
        }
    }
}