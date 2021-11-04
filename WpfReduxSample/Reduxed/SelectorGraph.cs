using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;

namespace WpfReduxSample.Reduxed
{
    public interface ISelectorNode
    {
        void Synchronize();
        void ForwardIfNeeded();
        void ForwardError(Exception error);
        void ForwardCompleted();
        long LastChanged { get; }
        ISelectorGraph Owner { get; }
    }

    public interface ISelector<out TValue> : IObservable<TValue>, ISelectorNode
    {
        public TValue Current { get; }
    }

    public interface ISelectorGraph
    {
        long CurrentTime { get; }
        void OnSubscribe(ISelectorNode node);
    }

    public class Selector<TValue> : ISelector<TValue>
    {
        record ObserverEntry
        {
            public IObserver<TValue> Observer { get; set; }
            public long UpdateTime { get; set; }
        }

        private readonly IReadOnlyList<ISelectorNode> dependencies;
        private readonly ISelectorGraph owner;
        private readonly Func<TValue> producer;
        private ImmutableList<ObserverEntry> observers = ImmutableList<ObserverEntry>.Empty;
        private TValue current;
        private long lastChanged;
        private long lastSynchronized;

        public Selector(ISelectorGraph owner, Func<TValue> producer, params ISelectorNode[] dependencies)
        {
            if (dependencies.Any(dependency => !ReferenceEquals(dependency.Owner, owner)))
            {
                throw new ArgumentException("Unable to add dependency of unrelated selector graph", nameof(dependencies));
            }

            this.dependencies = dependencies;
            this.owner = owner;
            this.producer = producer;
        }

        public ISelectorGraph Owner => owner;

        public TValue Current => current;

        public long LastChanged => lastChanged;

        public void Synchronize()
        {
            // We're guaranteed to be updated
            if (lastSynchronized == owner.CurrentTime)
                return;

            // Make sure all dependencies are synchronized
            foreach (var dependency in dependencies)
                dependency.Synchronize();

            // Either we are a root, we were never updated, or one of our dependencies changed this frame
            bool needsRecomputation = dependencies.Count == 0 || lastSynchronized == 0 ||
                dependencies.Any(dependency => dependency.LastChanged > lastSynchronized);

            if (needsRecomputation)
            {
                var newValue = producer();
                if (!EqualityComparer<TValue>.Default.Equals(newValue, current))
                {
                    lastChanged = owner.CurrentTime;
                    current = newValue;
                }
            }

            lastSynchronized = owner.CurrentTime;
        }

        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            owner.OnSubscribe(this);

            Synchronize();

            // all previous observers are up-to-date, so only update this one
            observers = observers.Add(new ObserverEntry { Observer = observer, UpdateTime = owner.CurrentTime });
            observer.OnNext(current);

            return Disposable.Create(() =>
            {
                var index = observers.FindIndex(x => x.Observer == observer);
                if (index != -1)
                {
                    observers = observers.RemoveAt(index);
                }
            });
        }

        public void ForwardIfNeeded()
        {
            var index = observers.FindIndex(x => x.UpdateTime < owner.CurrentTime);

            // No observers or already updated?
            if (index == -1)
            {
                return;
            }

            // Make sure the data here is current
            Synchronize();

            // Bump out if value was not changed
            if (lastChanged < owner.CurrentTime)
            {
                return;
            }

            for (int i = index; i < observers.Count; ++i)
            {
                var observer = observers[i];
                if (observer.UpdateTime < owner.CurrentTime)
                {
                    observer.UpdateTime = owner.CurrentTime;
                    observer.Observer.OnNext(Current);
                }
            }
        }

        public void ForwardError(Exception error)
        {
            foreach (var observer in observers)
                observer.Observer.OnError(error);

        }

        public void ForwardCompleted()
        {
            foreach (var observer in observers)
                observer.Observer.OnCompleted();
        }
    }

    public class SelectorGraph<TState> : IObserver<TState>, ISelectorGraph
    {
        private readonly List<WeakReference<ISelectorNode>> nodes = new();
        private readonly Func<TState> getState;
        private long currentTime = 1;
        private TState currentState;

        public long CurrentTime => currentTime;

        public SelectorGraph()
            : this((TState)default)
        {
        }

        public SelectorGraph(Func<TState> getState)
        {
            this.getState = getState ?? throw new ArgumentNullException(nameof(getState));
            currentState = getState();
        }

        public SelectorGraph(TState initial)
        {
            currentState = initial;
        }

        private TState ReadCurrent()
        {
            return currentState;
        }

        private ISelector<TOutput> Register<TOutput>(ISelector<TOutput> selector)
        {
            nodes.Add(new WeakReference<ISelectorNode>(selector));
            return selector;
        }

        public ISelector<TOutput> Create<TOutput>(Func<TState, TOutput> applier)
        {
            return Register(new Selector<TOutput>(this, () => applier(ReadCurrent())));
        }

        public ISelector<TOutput> Create<TInput, TOutput>(ISelector<TInput> previous, Func<TInput, TOutput> applier)
        {
            return Register(new Selector<TOutput>(this, () => applier(previous.Current), previous));
        }

        public ISelector<TOutput> Create<TLeft, TRight, TOutput>(ISelector<TLeft> left, ISelector<TRight> right, Func<TLeft, TRight, TOutput> applier)
        {
            return Register(new Selector<TOutput>(this, () => applier(left.Current, right.Current), left, right));
        }

        public ISelector<TOutput> Create<T1, T2, T3, TOutput>(ISelector<T1> s1, ISelector<T2> s2, ISelector<T3> s3, Func<T1, T2, T3, TOutput> applier)
        {
            return Register(new Selector<TOutput>(this, () => applier(s1.Current, s2.Current, s3.Current), s1, s2, s3));
        }

        public ISelector<TOutput> Create<T1, T2, T3, T4, TOutput>(ISelector<T1> s1, ISelector<T2> s2, ISelector<T3> s3, ISelector<T4> s4, Func<T1, T2, T3, T4, TOutput> applier)
        {
            return Register(new Selector<TOutput>(this, () => applier(s1.Current, s2.Current, s3.Current, s4.Current), s1, s2, s3, s4));
        }

        public void OnCompleted()
        {
            foreach (var node in LiveNodes())
                node.ForwardCompleted();
        }

        public void OnError(Exception error)
        {
            foreach (var node in LiveNodes())
                node.ForwardError(error);
        }

        public void OnNext(TState value)
        {
            UpdateIfChanged(value);
            ForwardAll();
        }

        private IEnumerable<ISelectorNode> LiveNodes()
        {
            var target = 0;
            for (int i = 0; i < nodes.Count; ++i)
            {
                var weakNode = nodes[i];
                if (!weakNode.TryGetTarget(out ISelectorNode node))
                {
                    // Node was collected, ignore it
                    continue;
                }

                // Trigger outside code, if needed
                yield return node;

                nodes[target++] = weakNode;
            }

            // Remove unused capacity
            nodes.RemoveRange(target, nodes.Count - target);
        }

        private void ForwardAll()
        {
            foreach (var node in LiveNodes())
            {
                node.ForwardIfNeeded();
            }
        }

        public void OnSubscribe(ISelectorNode node)
        {
            // Need this when subscribing while the graph has not been updated yet.
            // This happens when subscribing to an observable without the graph, and
            // subscribing to a graph node in that subscription, while the graph has
            // not been updated by the subject yet (e.g. if that happens after the first subscription)
            if (getState != null)
            {
                var next = getState();
                UpdateIfChanged(next);
            }
        }

        private void UpdateIfChanged(TState next)
        {
            if (!ReferenceEquals(next, currentState))
            {
                currentState = next;
                currentTime++;
            }
        }
    }

    public static class ReduxStoreExtension
    {
        private class IdentitySelector<TState> : ISelectorWithoutProps<TState, TState>
        {
            public TState Apply(TState input)
            {
                return input;
            }

            public IObservable<TState> Apply(IObservable<TState> input)
            {
                return input;
            }
        }

        public static SelectorGraph<TState> SelectorGraph<TState>(this ReduxStore<TState> store) where TState : class, new()
        {
            var graph = new SelectorGraph<TState>(() => store.State);
            var observeState = store.Select(new IdentitySelector<TState>());
            observeState.Subscribe(graph);
            return graph;
        }
    }
}
