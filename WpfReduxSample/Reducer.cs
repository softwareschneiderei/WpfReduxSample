using System;

namespace WpfReduxSample
{
    public class Reducer
    {
        public static State OnIncrement(State state, IncrementAction _)
        {
            return state with
            {
                Counter = Math.Min(state.Counter + 1, 999),
            };
        }

        public static State OnDecrement(State state, DecrementAction _)
        {
            return state with
            {
                Counter = Math.Max(state.Counter - 1, 0),
            };
        }
    }
}
