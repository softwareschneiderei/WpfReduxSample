using Microsoft.Extensions.DependencyInjection;
using ReduxSimple;
using System.Windows;
using WpfReduxSample.Reduxed;

namespace WpfReduxSample
{
    public partial class App : Application
    {
        private static IServiceCollection ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(provider => new ReduxStore<State>(ReducerBuilder.FromMethods<State, Reducer>(new Reducer())))
                .AddSingleton<IActionDispatcher, StoreDispatcher<State>>()
                .AddSingleton<Selectors>()
                .AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = ConfigureServices();
            var provider = services.BuildServiceProvider();

            var window = provider.GetRequiredService<MainWindow>();
            window.Show();
        }
    }
}
