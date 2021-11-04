using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfReduxSample.Extensions;
using WpfReduxSample.Utils;

namespace WpfReduxSample.Reduxed
{
    /// <summary>
    /// This is for smart user-controls that only react to the store, but do not send out actions
    /// </summary>

    public class UIReadBinder : IDisposable
    {
        private readonly List<IDisposable> subscriptions = new();
        private readonly List<Action> binders = new();
        private bool armed;

        public UIReadBinder(bool armed = false)
        {
            this.armed = armed;
        }

        public UIReadBinder Record(Action callable)
        {
            binders.Add(callable);
            if (armed)
            {
                callable();
            }
            return this;
        }

        public void Arm()
        {
            if (armed)
            {
                return;
            }

            binders.ForEach(callable => { callable(); });
            armed = true;
        }

        public void Disarm()
        {
            if (!armed)
            {
                return;
            }

            subscriptions.ForEach(x => x.Dispose());
            subscriptions.Clear();
            armed = false;
        }

        public void Manage(IDisposable s)
        {
            subscriptions.Add(s);
        }

        public void OnDispose(Action a)
        {
            subscriptions.Add(Disposable.Create(a));
        }

        public void Dispose()
        {
            Disarm();
        }

        /// <summary>
        /// Reset this binder to an empty state
        /// </summary>
        public void Reset()
        {
            var wasArmed = armed;
            Disarm();
            binders.Clear();
            if (wasArmed)
            {
                Arm();
            }
        }

        public UIReadBinder Bind<T>(IObservable<T> observable, Action<T> subscriber)
        {
            return Record(() => Manage(observable.Subscribe(subscriber)));
        }

        public UIReadBinder BindEnabled(IReadOnlyList<UIElement> elements, IObservable<bool> observable)
        {
            return Record(() => Manage(observable.Subscribe(enabled =>
            {
                foreach (var each in elements)
                {
                    each.IsEnabled = enabled;
                }
            })));
        }
        public UIReadBinder BindEnabled(UIElement element, IObservable<bool> observable)
        {
            return Record(() => Manage(observable.Subscribe(enabled =>
            {
                element.IsEnabled = enabled;
            })));
        }

        public UIReadBinder BindText(TextBlock textBlock, IObservable<string> observable)
        {
            return Record(() => Manage(observable.Subscribe(text =>
            {
                textBlock.Text = text;
            })));
        }

        public UIReadBinder BindVisibility(UIElement element, IObservable<Visibility> observable)
        {
            return Record(() => Manage(observable.Subscribe(v =>
            {
                element.Visibility = v;
            })));
        }

        public UIReadBinder BindVisibility(IReadOnlyList<UIElement> elements, IObservable<Visibility> observable)
        {
            return Record(() => Manage(observable.Subscribe(v =>
            {
                foreach (var element in elements)
                {
                    element.Visibility = v;
                }
            })));
        }

        public UIReadBinder BindDataGrid<TData, TEnum>(DataGrid dataGrid, IObservable<(IEnumerable<TData> options, TEnum selected)> observable,
            Func<TData, TEnum> keySelector)
             where TData : IEquatable<TData>
        {
            return Record(() =>
            {
                var observableCollection = new ObservableCollection<TData>();

                // Update the value from the observable
                Manage(observable.Subscribe(info =>
                {
                    info.options
                        .UpdateObservable(observableCollection, keySelector);
                    dataGrid.SelectedIndex = observableCollection.FindIndex(x => keySelector(x).Equals(info.selected));
                }));

                // Setup the options for the listview - make sure this happens after the initial subscription
                dataGrid.ItemsSource = observableCollection;
            });
        }
        public UIReadBinder BindDataGrid<TData, TEnum>(DataGrid dataGrid, IObservable<IEnumerable<TData>> observable,
            Func<TData, TEnum> keySelector)
             where TData : IEquatable<TData>
        {
            return Record(() =>
            {
                var observableCollection = new ObservableCollection<TData>();

                // Update the value from the observable
                Manage(observable.Subscribe(options =>
                {
                    options.UpdateObservable(observableCollection, keySelector);
                }));

                // Setup the options for the listview - make sure this happens after the initial subscription
                dataGrid.ItemsSource = observableCollection;
            });
        }
    }

    /// <summary>
    /// This is meant as a helper class for "smart" user controls that
    /// subscribe to the store and send out actions
    /// </summary>
    public class UIDuplexBinder : IDisposable
    {
        private readonly UIReadBinder readBinder;

        public UIDuplexBinder(IActionDispatcher dispatcher, bool armed = false)
        {
            Dispatcher = dispatcher;
            EventFilter = new EventFilter();
            readBinder = new UIReadBinder(armed: armed);
        }

        public IActionDispatcher Dispatcher { get; }

        public EventFilter EventFilter { get; }

        private UIDuplexBinder Record(Action callable)
        {
            _ = readBinder.Record(callable);
            return this;
        }

        public void Arm()
        {
            readBinder.Arm();
        }

        public void Disarm()
        {
            readBinder.Disarm();
        }

        /// <summary>
        /// Reset this binder to an empty state
        /// </summary>
        public void Reset()
        {
            readBinder.Reset();
        }

        public void Dispose()
        {
            readBinder.Dispose();
        }

        public void Manage(IDisposable s)
        {
            readBinder.Manage(s);
        }

        private void OnDispose(Action a)
        {
            readBinder.OnDispose(a);
        }

        public void Dispatch(object action)
        {
            Dispatcher.Dispatch(action);
        }

        public UIDuplexBinder BindInput(TextBox textBox, IObservable<string> observable, Func<string, object> actionCreator)
        {
            return Record(() =>
            {
                Manage(observable.Subscribe(value =>
                {
                    using (EventFilter.Blocked(textBox))
                    {
                        textBox.Text = value;
                    }
                }));

                var handler = EventFilter.AttachTextBox(textBox, (sender, e) => { Dispatch(actionCreator(textBox.Text)); });
                textBox.TextChanged += handler;
                OnDispose(() => textBox.TextChanged -= handler);
            });
        }

        public UIDuplexBinder BindInput<TKey, TValue>(ListView listView, IObservable<(IEnumerable<TValue> elements, TKey selected)> observable, Func<TValue, TKey> selectKey, Func<TKey, object> actionCreator)
            where TValue : IEquatable<TValue>
        {
            return Record(() =>
            {
                var observableCollection = new ObservableCollection<TValue>();

                // Update the value from the observable
                Manage(observable.Subscribe(info =>
                {
                    using (EventFilter.Blocked(listView))
                    {
                        info.elements
                            .UpdateObservable(observableCollection, selectKey);
                        listView.SelectedIndex = observableCollection.FindIndex(x => selectKey(x).Equals(info.selected));
                    }
                }));

                // Setup the options for the listview - make sure this happens after the initial subscription
                listView.ItemsSource = observableCollection;

                // React to changes
                var handler = EventFilter.Attach(listView, (s, e) => Dispatch(actionCreator(selectKey(observableCollection[listView.SelectedIndex]))));
                listView.SelectionChanged += handler;
                OnDispose(() => listView.SelectionChanged -= handler);
            });
        }

        public UIDuplexBinder BindInput(CheckBox checkBox, IObservable<bool> observable, Func<bool, object> actionCreator)
        {
            return Record(() =>
            {
                Manage(observable.Subscribe(value =>
                {
                    using (EventFilter.Blocked(checkBox))
                    {
                        checkBox.IsChecked = value;
                    }
                }));

                var checkHandler = EventFilter.Attach(checkBox, (s, e) => Dispatch(actionCreator(true)));
                var uncheckHandler = EventFilter.Attach(checkBox, (s, e) => Dispatch(actionCreator(false)));

                checkBox.Checked += checkHandler;
                checkBox.Unchecked += uncheckHandler;

                OnDispose(() =>
                {
                    checkBox.Checked -= checkHandler;
                    checkBox.Unchecked -= uncheckHandler;
                });
            });
        }

        public UIDuplexBinder BindEnabled(IReadOnlyList<UIElement> elements, IObservable<bool> observable)
        {
            _ = readBinder.BindEnabled(elements, observable);
            return this;
        }

        public UIDuplexBinder BindEnabled(UIElement element, IObservable<bool> observable)
        {
            _ = readBinder.BindEnabled(element, observable);
            return this;
        }

        public UIDuplexBinder BindText(TextBlock textBlock, IObservable<string> observable)
        {
            _ = readBinder.BindText(textBlock, observable);
            return this;
        }

        public UIDuplexBinder BindVisibility(UIElement element, IObservable<Visibility> observable)
        {
            _ = readBinder.BindVisibility(element, observable);
            return this;
        }

        public UIDuplexBinder BindVisibility(IReadOnlyList<UIElement> elements, IObservable<Visibility> observable)
        {
            _ = readBinder.BindVisibility(elements, observable);
            return this;
        }

        public UIDuplexBinder Bind<T>(IObservable<T> observable, Action<T> subscriber)
        {
            _ = readBinder.Bind(observable, subscriber);
            return this;
        }
    }
}
