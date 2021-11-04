using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace WpfReduxSample.Reduxed
{
    public class LensedReducer<TState, TSlicedState, TBaseAction>
        where TState : class, new()
        where TBaseAction : class
    {
        private readonly Func<TState, TBaseAction, TSlicedState> slicer;
        private readonly Func<TState, TSlicedState, TBaseAction, TState> joiner;
        private readonly ImmutableList<On<TState>> reducers = ImmutableList<On<TState>>.Empty;

        public LensedReducer(Func<TState, TBaseAction, TSlicedState> slicer, Func<TState, TSlicedState, TBaseAction, TState> joiner)
            : this(slicer, joiner, ImmutableList<On<TState>>.Empty)
        {
        }

        private LensedReducer(Func<TState, TBaseAction, TSlicedState> slicer, Func<TState, TSlicedState, TBaseAction, TState> joiner, ImmutableList<On<TState>> previous)
        {
            this.slicer = slicer;
            this.joiner = joiner;
            reducers = previous;
        }

        /// <summary>
        /// Normal sliced variant
        /// </summary>
        /// <typeparam name="TAction"></typeparam>
        /// <param name="handler"></param>
        /// <returns></returns>
        public LensedReducer<TState, TSlicedState, TBaseAction> On<TAction>(Func<TSlicedState, TAction, TSlicedState> handler)
            where TAction : class, TBaseAction
        {
            TState Reduce(TState state, TAction action)
            {
                var oldSlicedState = slicer(state, action);
                var newSlicedState = handler(oldSlicedState, action);
                return joiner(state, newSlicedState, action);
            }

            var withNewReducer = reducers.Add(Reducers.On<TAction, TState>(Reduce));
            return new LensedReducer<TState, TSlicedState, TBaseAction>(slicer, joiner, withNewReducer);
        }

        /// <summary>
        /// Context sliced variant
        /// </summary>
        /// <typeparam name="TAction"></typeparam>
        /// <param name="handler"></param>
        /// <returns></returns>
        public LensedReducer<TState, TSlicedState, TBaseAction> On<TAction>(Func<TState, TSlicedState, TAction, TSlicedState> handler)
            where TAction : class, TBaseAction
        {
            TState Reduce(TState state, TAction action)
            {
                var oldSlicedState = slicer(state, action);
                var newSlicedState = handler(state, oldSlicedState, action);
                return joiner(state, newSlicedState, action);
            }

            var withNewReducer = reducers.Add(Reducers.On<TAction, TState>(Reduce));
            return new LensedReducer<TState, TSlicedState, TBaseAction>(slicer, joiner, withNewReducer);
        }

        public IEnumerable<On<TState>> ListMethodsOf<TReducer>(TReducer reducer)
        {
            var methods = reducer.GetType().GetMethods();
            foreach (var method in methods)
            {
                foreach (var on in CreateOnFor(reducer, method))
                {
                    yield return on;
                }
            }
        }

        private IEnumerable<On<TState>> CreateOnFor<TReducer>(TReducer reducer, MethodInfo method)
        {
            if (method.ReturnType != typeof(TSlicedState))
                yield break;

            var parameters = method.GetParameters();
            if (parameters.Length == 2)
            {
                if (parameters[0].ParameterType != typeof(TSlicedState))
                    yield break;

                if (!parameters[1].ParameterType.IsAssignableTo(typeof(TBaseAction)))
                    yield break;

                TSlicedState SlicedReduce(TSlicedState state, TBaseAction action)
                {
                    return (TSlicedState)method.Invoke(reducer, new object[] { state, action });
                }

                TState Reduce(TState state, TBaseAction action)
                {
                    var oldSlicedState = slicer(state, action);
                    var newSlicedState = SlicedReduce(oldSlicedState, action);
                    return joiner(state, newSlicedState, action);
                }

                yield return new On<TState>
                {
                    Reduce = (state, action) => Reduce(state, (TBaseAction)action),
                    Types = new string[] { parameters[1].ParameterType.FullName }
                };
            }
            else if (parameters.Length == 3)
            {
                if (parameters[0].ParameterType != typeof(TState))
                    yield break;

                if (parameters[1].ParameterType != typeof(TSlicedState))
                    yield break;

                if (!parameters[2].ParameterType.IsAssignableTo(typeof(TBaseAction)))
                    yield break;

                TSlicedState SlicedReduce(TState fullState, TSlicedState state, TBaseAction action)
                {
                    return (TSlicedState)method.Invoke(reducer, new object[] { fullState, state, action });
                }

                TState Reduce(TState state, TBaseAction action)
                {
                    var oldSlicedState = slicer(state, action);
                    var newSlicedState = SlicedReduce(state, oldSlicedState, action);
                    return joiner(state, newSlicedState, action);
                }

                yield return new On<TState>
                {
                    Reduce = (state, action) => Reduce(state, (TBaseAction)action),
                    Types = new string[] { parameters[2].ParameterType.FullName }
                };
            }
        }

        public IReadOnlyList<On<TState>> ToList()
        {
            return reducers;
        }
    }
    public class ReducerBuilder
    {
        /// <summary>
        /// Create a lense that always focuses the same part of the state
        /// </summary>
        public static LensedReducer<TState, TSlicedState, object> StaticLens<TState, TSlicedState>(
            Func<TState, TSlicedState> slicer, Func<TState, TSlicedState, TState> joiner)
            where TState : class, new()
        {
            return new LensedReducer<TState, TSlicedState, object>(
                (state, _) => slicer(state),
                (state, sliced, _) => joiner(state, sliced));
        }

        /// <summary>
        /// Create a lense that can use the action to focus specific parts of the state
        /// </summary>
        public static LensedReducer<TState, TSlicedState, TBaseAction> ActionDependentLens<TState, TSlicedState, TBaseAction>(
            Func<TState, TBaseAction, TSlicedState> slicer,
            Func<TState, TSlicedState, TBaseAction, TState> joiner
            )
            where TState : class, new()
            where TBaseAction : class
        {
            return new LensedReducer<TState, TSlicedState, TBaseAction>(
                slicer, joiner);
        }

        public static IEnumerable<On<TState>> FromMethods<TState, TReducer>(TReducer reducer) where TState : class
        {
            var result = new List<On<TState>>();
            var methods = reducer.GetType().GetMethods();
            foreach (var method in methods)
            {
                if (method.ReturnType != typeof(TState))
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 2)
                    continue;

                if (parameters[0].ParameterType != typeof(TState))
                    continue;

                result.Add(new On<TState>
                {
                    Reduce = (state, action) => (TState)method.Invoke(reducer, new object[] { state, action }),
                    Types = new string[] { parameters[1].ParameterType.FullName }
                });
            }

            return result;
        }
    }
}
