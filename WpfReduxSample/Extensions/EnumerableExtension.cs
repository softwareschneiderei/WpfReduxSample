using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WpfReduxSample.Extensions
{
    public static class EnumerableExtension
    {
        /// <summary>
        /// In contrast to ToDictionary, this only uses the first occurence of each key and places it in the result Dictionary
        /// </summary>
        public static Dictionary<TKey, TValue> ToSafeDictionary<TElement, TKey, TValue>(
            this IEnumerable<TElement> elements, Func<TElement, TKey> keySelector, Func<TElement, TValue> valueSelector)
        {
            var result = new Dictionary<TKey, TValue>();
            foreach (var each in elements)
            {
                var key = keySelector(each);
                if (result.ContainsKey(key))
                    continue;

                result.Add(key, valueSelector(each));
            }
            return result;
        }

        /// <summary>
        /// In contrast to ToDictionary, this only uses the first occurence of each key and places it in the result Dictionary
        /// </summary>
        public static Dictionary<TKey, TElement> ToSafeDictionary<TElement, TKey>(
            this IEnumerable<TElement> elements, Func<TElement, TKey> keySelector)
        {
            return elements.ToSafeDictionary(keySelector, each => each);
        }

        public static HashSet<TElement> ToSafeHashSet<TElement>(this IEnumerable<TElement> elements)
        {
            var result = new HashSet<TElement>();
            foreach (var each in elements)
            {
                if (result.Contains(each))
                    continue;

                result.Add(each);
            }
            return result;
        }

        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> list)
        {
            var result = new ObservableCollection<T>();
            foreach (T item in list)
                result.Add(item);
            return result;
        }

        public static void ToObservableCollection<T>(this IEnumerable<T> list, ObservableCollection<T> result)
        {
            result.Clear();
            foreach (T item in list)
                result.Add(item);
        }

        public static T FirstOr<T>(this IEnumerable<T> list, T fallback)
        {
            var position = list.GetEnumerator();
            if (!position.MoveNext())
            {
                return fallback;
            }
            return position.Current;
        }

        public static IEnumerable<T> DistinctAdjacent<T>(this IEnumerable<T> list)
        {
            if (!list.HasContent())
                yield break;

            var last = list.First();
            yield return last;

            foreach (var each in list.Skip(1))
            {
                if (each.Equals(last))
                {
                    continue;
                }
                yield return each;
                last = each;
            }
        }

        public static bool HasContent<T>(this IEnumerable<T> list)
        {
            var position = list.GetEnumerator();
            return position.MoveNext();
        }

        public static IEnumerable<(T value, int index)> WithIndex<T>(this IEnumerable<T> list)
        {
            return list.Select((value, index) => (value, index));
        }

        public static void UpdateObservable<T, R>(this IEnumerable<T> list, ObservableCollection<T> observable, Func<T, R> keySelector)
            where T : IEquatable<T>
        {
            list.UpdateObservable(observable, keySelector, (left, right) => left.Equals(right));
        }

        public static void UpdateObservable<T, R>(this IEnumerable<T> list, ObservableCollection<T> observable, Func<T, R> keySelector, Func<T, T, bool> equals)
        {
            var oldKeyToIndex = observable
                .Select((x, index) => (Value: x, Index: index))
                .ToSafeDictionary(x => keySelector(x.Value), x => x.Index);
            var newKeys = list.Select(keySelector).ToHashSet();

            var indicesToRemove = oldKeyToIndex
                .Where(tuple => !newKeys.Contains(tuple.Key))
                .Select(tuple => tuple.Value)
                .OrderByDescending(x => x);

            // Remove elements in backwards order to not invalidate the lower indices
            foreach (var index in indicesToRemove)
            {
                observable.RemoveAt(index);
            }

            // Now insert new elements
            var target = 0;
            foreach (var each in list.OrderBy(keySelector))
            {
                // Invariant: all indices below index are synchronized
                if (target >= observable.Count)
                {
                    observable.Add(each);
                }
                else if (!keySelector(each).Equals(keySelector(observable[target])))
                {
                    observable.Insert(target, each);
                }
                else if (!equals(each, observable[target]))
                {
                    observable[target] = each;
                }

                ++target;
            }
        }
    }
}
