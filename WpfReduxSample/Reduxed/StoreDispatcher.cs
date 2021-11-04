using ReduxSimple;

namespace WpfReduxSample.Reduxed
{
    public class StoreDispatcher<TState> : IActionDispatcher where TState : class, new()
    {
        private readonly ReduxStore<TState> store;

        public StoreDispatcher(ReduxStore<TState> store)
        {
            this.store = store;
        }

        public void Dispatch(object action)
        {
            store.Dispatch(action);
        }
    }
}
