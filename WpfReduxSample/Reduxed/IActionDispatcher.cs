using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace WpfReduxSample.Reduxed
{
    public interface IActionDispatcher
    {
        void Dispatch(object action);
    }

    public sealed class NullDispatcher : IActionDispatcher
    {
        public void Dispatch(object action)
        {
            // sic! Do nothing!
        }
    }

    public interface IAsyncActionDispatcher
    {
        Task DispatchAsync(object action);
    }

    public sealed class NullAsyncActionDispatcher : IAsyncActionDispatcher
    {
        public Task DispatchAsync(object action)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingAsyncActionDispatcher : IAsyncActionDispatcher
    {
        private readonly ConcurrentQueue<object> recorded = new();

        public Task DispatchAsync(object action)
        {
            recorded.Enqueue(action);
            return Task.CompletedTask;
        }
        public IEnumerable<object> Recorded => recorded;
    }

    /// <summary>
    /// Marshall action dispatching back to the WPF UI dispatcher
    /// </summary>
    public sealed class WpfAsyncActionDispatcher : IAsyncActionDispatcher
    {
        private readonly IActionDispatcher dispatcher;

        public WpfAsyncActionDispatcher(IActionDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public async Task DispatchAsync(object action)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                dispatcher.Dispatch(action);
            });
        }
    }
}
