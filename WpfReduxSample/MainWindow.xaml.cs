using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using WpfReduxSample.Reduxed;

namespace WpfReduxSample
{
    public partial class MainWindow : Window
    {
        private readonly IActionDispatcher dispatcher;
        private UIReadBinder binder = new();

        public MainWindow(Selectors selectors, IActionDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            InitializeComponent();

            _ = binder
                .BindText(Counter, selectors.Counter.Select(x => x.ToString()))
                .BindText(Primes, selectors.Primes.Select(list => string.Join("*", list)));

            // Make sure the view is only updated when we are visible
            Loaded += (s, e) => binder.Arm();
            Unloaded += (s, e) => binder.Disarm();
        }

        private void Decrement_Click(object sender, RoutedEventArgs e)
        {
            dispatcher.Dispatch(new DecrementAction { });
        }

        private void Increment_Click(object sender, RoutedEventArgs e)
        {
            dispatcher.Dispatch(new IncrementAction { });
        }
    }
}
