using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace PomodoroApp
{
    public enum PomodoroType
    {
        [Description("Tomato")]
        TaskTimer = 25,
        [Description("HotBeverage")]
        ShortBreak = 5,
        [Description("GreenApple")]
        LongBreak = 20
    }

    public abstract class ObservableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<TItem>(ref TItem field, TItem value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<TItem>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ActionCommandHandler : ICommand
    {
        private readonly Action<object> _action;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public ActionCommandHandler(Action<object> action, Func<object, bool> canExecute = null) =>
            (_action, _canExecute) = (action, canExecute);

        public bool CanExecute(object parameter) =>
            _canExecute == null || parameter == null || _canExecute.Invoke(parameter);

        public void Execute(object parameter) =>
            _action(parameter);

        public void UpdateCanExecute() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class Item : ObservableBase
    {
        private const string space = " ";
        private static readonly Regex regex = new Regex(@"\p{Lu}\p{Ll}*");

        private PomodoroType _type;
        private Color _upper;
        private Color _lower;

        public PomodoroType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public Color Upper
        {
            get => _upper;
            set => SetProperty(ref _upper, value);
        }

        public Color Lower
        {
            get => _lower;
            set => SetProperty(ref _lower, value);
        }

        public string Id =>
            Enum.GetName(typeof(PomodoroType), Type);

        public TimeSpan TimeSpan =>
            TimeSpan.FromMinutes((double)Type);

        public string Resource => 
            (Type.GetType()
            .GetField(Type.ToString())
            .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .First() as DescriptionAttribute)
                    .Description;
        public override string ToString() =>
            string.Join(space, regex.Matches(Id)
                .Cast<Match>().Select(s => s.Value));

        public Item(PomodoroType type, Color upper, Color lower) =>
            (Type, Upper, Lower) = (type, upper, lower);
    }

    public class Alert
    {
        public DateTime Start { get; }

        public DateTime Finish { get; }

        public Item Item { get; }

        public override string ToString() =>
            $"Started {Start:HH:mm} Finished {Finish:HH:mm}";

        public Alert(DateTime start, DateTime finish, Item item) =>
           (Start, Finish, Item) = (start, finish, item);
    }

    public class AlertEventArgs : EventArgs
    {
        public Alert Alert { get; }

        public AlertEventArgs(Alert alert) =>
            Alert = alert;
    }

    public class Notification
    {
        public string Id { get; }

        public DateTime Completed { get; }

        public Notification(string id, DateTime completed) =>
            (Id, Completed) = (id, completed);
    }

    public class Timer : ObservableBase
    {
        private const int timer_interval_ms = 100;
        private const string display_format = @"mm\:ss";

        private readonly List<Item> _items = new List<Item>()
        {
            new Item(PomodoroType.TaskTimer,
            Color.FromArgb(255, 240, 58, 23),
            Color.FromArgb(255, 239, 105, 80)),
            new Item(PomodoroType.ShortBreak,
            Color.FromArgb(255, 131, 190, 236),
            Color.FromArgb(255, 179, 219, 212)),
            new Item(PomodoroType.LongBreak,
            Color.FromArgb(255, 186, 216, 10),
            Color.FromArgb(255, 228, 245, 119)),
        };

        private bool _started;
        private string _display;
        private Item _item;
        private Alert _alert;
        private DateTime _start;
        private DateTime _finish;
        private TimeSpan _current;
        private DispatcherTimer _timer;

        public event EventHandler<AlertEventArgs> Added;
        public event EventHandler<AlertEventArgs> Removed;
        public event EventHandler<AlertEventArgs> Triggered;

        public bool Started => _started;
        public List<Item> Items => _items;

        public string Display
        {
            get => _display;
            set => SetProperty(ref _display, value);
        }

        public Item Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        private string GetDisplay(TimeSpan timer) =>
            timer.ToString(display_format);

        private void Set(Item item)
        {
            Item = item;
            _current = item.TimeSpan;
            Display = GetDisplay(_current);
        }

        private void Reset()
        {
            _alert = null;
            _timer?.Stop();
            _started = false;
            Set(Item);
        }

        private void Tick()
        {
            var display = GetDisplay(_current + (_start - DateTime.UtcNow));
            if (_started && display != GetDisplay(TimeSpan.Zero))
                Display = display;
            else
            {
                Triggered?.Invoke(this, new AlertEventArgs(_alert));
                Reset();
            }
        }

        private void Start()
        {
            if (_alert == null)
            {
                _start = DateTime.UtcNow;
                _finish = _start.Add(Item.TimeSpan);
                _alert = new Alert(_start, _finish, Item);
                Added?.Invoke(this, new AlertEventArgs(_alert));
            }
            Set(Item);
            if (_timer == null)
            {
                _timer = new DispatcherTimer()
                {
                    Interval = TimeSpan.FromMilliseconds(timer_interval_ms)
                };
                _timer.Tick += (object sender, object e) => Tick();
            }
            _timer.Start();
            _started = true;
        }

        private void Stop()
        {
            Removed?.Invoke(this, new AlertEventArgs(_alert));
            Reset();
        }

        public Timer(Notification notification)
        {
            if(notification != null)
            {
                var item = Items.First(f => f.Id == notification.Id);
                _start = notification.Completed - item.TimeSpan;
                _finish = notification.Completed;
                _alert = new Alert(_start, _finish, item);
                Set(item);
                Start();
            }
            else
                Set(Items.First());
        }
            
        public void Toggle()
        {
            if (_started)
                Stop();
            else
                Start();
        }

        public void Select(Item item) => Set(item);
    }

    public class ItemToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            value is Item item ? new BitmapImage(new Uri($"ms-appx:///Assets/{item.Resource}.png")) : null;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class Library
    {
        private const string title = "Pomodoro App";
        private const string toggle_label = "Toggle";
        private const string toggle_resource = "TimerClock";

        private readonly Timer _timer;
        private ContentDialog _dialog;

        private async Task<bool> ConfirmAsync(object content,
        string primaryButtonText = "Ok", string secondaryButtonText = "")
        {
            try
            {
                if (_dialog != null)
                    _dialog.Hide();
                _dialog = new ContentDialog()
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = primaryButtonText,
                    SecondaryButtonText = secondaryButtonText
                };
                return await _dialog.ShowAsync() == ContentDialogResult.Primary;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        private Uri GetResource(string resource) =>
            new Uri($"ms-appx:///Assets/{resource}64.png");

        private async void Show(Item item, params string[] messages)
        {
            var content = new StackPanel()
            {
                Orientation = Orientation.Vertical
            };
            foreach (var message in messages)
            {
                content.Children.Add(new TextBlock()
                {
                    Text = message,
                    HorizontalTextAlignment = TextAlignment.Center
                });
            }
            var image = new Image()
            {
                Width = 64,
                Height = 64,
                Margin = new Thickness(5),
                Source = new BitmapImage(GetResource(item.Resource))
            };
            content.Children.Add(image);
            await ConfirmAsync(content);
        }

        private void Choose(Item item)
        {
            if (_timer.Started)
                Show(_timer.Item, "To Switch you need to Toggle", $"{_timer.Item}");
            else
                _timer.Select(item);
        }

        private void Triggered(Alert alert) =>
            Show(alert.Item, "Completed", $"{alert.Item}", $"{alert}");

        private void Add(CommandBar commandBar, string label, string resource, ICommand command, object parameter = null) =>
            commandBar.PrimaryCommands.Add(new AppBarButton()
            {
                Label = label,
                Command = command,
                CommandParameter = parameter,
                Icon = new BitmapIcon()
                {
                    ShowAsMonochrome = false,
                    UriSource = GetResource(resource)
                },
            });

        public Timer Timer => 
            _timer;

        public Library(Notification notification = null)
        {
            _timer = new Timer(notification);
            _timer.Triggered += (object sender, AlertEventArgs e) =>
                Triggered(e.Alert);
        }

        public void Loaded(Grid display, CommandBar commandBar)
        {
            Add(commandBar, toggle_label, toggle_resource, 
                new ActionCommandHandler((param) => _timer.Toggle()));
            foreach (var item in _timer.Items)
                Add(commandBar, $"{item}", item.Resource,
                    new ActionCommandHandler((param) => Choose(param as Item)), item);
            display.DataContext = _timer;
        }   
    }
}