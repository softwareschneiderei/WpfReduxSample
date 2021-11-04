using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;

namespace WpfReduxSample.Utils
{
    public class EventFilter
    {
        private readonly HashSet<FrameworkElement> set;

        public EventFilter()
        {
            set = new HashSet<FrameworkElement>();
        }

        public IDisposable Blocked(params FrameworkElement[] elements)
        {
            foreach (var each in elements)
            {
                set.Add(each);
            }
            return Disposable.Create(() =>
            {
                foreach (var each in elements)
                {
                    set.Remove(each);
                }
            });
        }


        public RoutedEventHandler Attach(CheckBox element, RoutedEventHandler handler)
        {
            return AttachRouted(element, handler);
        }

        public SelectionChangedEventHandler Attach(ComboBox element, SelectionChangedEventHandler handler)
        {
            return AttachSelectionChanged(element, handler);
        }

        public SelectionChangedEventHandler Attach(ListView listView, SelectionChangedEventHandler handler)
        {
            return AttachSelectionChanged(listView, handler);
        }

        public SelectionChangedEventHandler Attach(DataGrid dataGrid, SelectionChangedEventHandler handler)
        {
            return AttachSelectionChanged(dataGrid, handler);
        }

        public RoutedEventHandler AttachRouted(FrameworkElement element, RoutedEventHandler handler)
        {
            return (sender, message) =>
            {
                if (set.Contains(element))
                    return;
                handler(sender, message);
            };
        }

        public TextChangedEventHandler AttachTextBox(FrameworkElement element, TextChangedEventHandler handler)
        {
            return (sender, message) =>
            {
                if (set.Contains(element))
                    return;
                handler(sender, message);
            };
        }

        private SelectionChangedEventHandler AttachSelectionChanged(FrameworkElement element, SelectionChangedEventHandler handler)
        {
            return (sender, message) =>
            {
                if (set.Contains(element))
                    return;
                handler(sender, message);
            };
        }
    }
}
