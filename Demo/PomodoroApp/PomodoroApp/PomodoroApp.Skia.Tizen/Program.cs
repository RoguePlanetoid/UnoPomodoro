using Tizen.Applications;
using Uno.UI.Runtime.Skia;

namespace PomodoroApp.Skia.Tizen
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var host = new TizenHost(() => new PomodoroApp.App(), args);
            host.Run();
        }
    }
}
