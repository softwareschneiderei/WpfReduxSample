using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfReduxSample.Extensions
{
    public static class ReadOnlyListExtension
    {
        public static int GetSequenceHashCode<T>(this IReadOnlyList<T> list)
        {
            var hashCode = -1003278967;
            foreach (var value in list)
                hashCode = hashCode * -1521134295 + value.GetHashCode();
            return hashCode;
        }

        public static bool LazyEquals<T>(this IReadOnlyList<T> list, IReadOnlyList<T> other)
        {
            return list.Equals(other) || list.SequenceEqual(other);
        }

        public static int IndexOf<T>(this IReadOnlyList<T> self, T elementToFind)
        {
            int i = 0;
            foreach (T element in self)
            {
                if (Equals(element, elementToFind))
                    return i;
                i++;
            }
            return -1;
        }
        public static int FindIndex<T>(this IReadOnlyList<T> self, Func<T, bool> predicate)
        {
            int i = 0;
            foreach (T element in self)
            {
                if (predicate(element))
                    return i;
                i++;
            }
            return -1;
        }
        public static int FindIndex<T>(this IReadOnlyList<T> self, int startIndex, Func<T, bool> predicate)
        {
            for (int i = startIndex; i < self.Count; ++i)
            {
                if (predicate(self[i]))
                    return i;
            }
            return -1;
        }
    }
}
