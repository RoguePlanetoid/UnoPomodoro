using System.Linq;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PomodoroApp
{
    public sealed partial class MainPage : Page
    {
        private readonly Library _library;

        public MainPage()
        {
#if NETFX_CORE
            var notifier = ToastNotificationManager.CreateToastNotifier();
            var scheduled = notifier.GetScheduledToastNotifications().FirstOrDefault();
            var notification = scheduled != null ? 
                new Notification(scheduled.Id, scheduled.DeliveryTime.UtcDateTime) : null;
            _library = new Library(notification);
            _library.Timer.Added += (object sender, AlertEventArgs e) =>
            {
                var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
                xml.GetElementsByTagName("text")[0].InnerText = $"{e.Alert.Item}";
                xml.GetElementsByTagName("text")[1].InnerText = $"{e.Alert}";
                var toast = new ScheduledToastNotification(xml, e.Alert.Finish)
                {
                    Id = e.Alert.Item.Id
                };
                notifier.AddToSchedule(toast);
            };
            _library.Timer.Removed += (object sender, AlertEventArgs e) =>
            {
                foreach (var toast in notifier.GetScheduledToastNotifications())
                    notifier.RemoveFromSchedule(toast);
            };
#else
        _library = new Library();
#endif
            this.InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e) =>
            _library.Loaded(Display, Command);
    }
}
