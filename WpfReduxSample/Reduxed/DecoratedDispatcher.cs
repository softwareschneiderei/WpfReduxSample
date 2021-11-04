using Microsoft.Extensions.Logging;
using ReduxSimple;

namespace WpfReduxSample.Reduxed
{
    public class DecoratedDispatcher<TState> : GuardingDispatcher where TState : class, new()
    {
        public DecoratedDispatcher(ReduxStore<TState> store, ILogger<DecoratedDispatcher<TState>> logger)
            : base(new StoreDispatcher<TState>(store), logger)
        {
        }
    }
}
