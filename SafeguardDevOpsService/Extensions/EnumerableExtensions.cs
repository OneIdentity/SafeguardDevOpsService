using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneIdentity.DevOps.Extensions
{
    public static class EnumerableExtensions
    {
        public static List<T> EnumerateAsList<T>(this IEnumerable<T> value)
        {
            return value is List<T> list 
                ? list 
                : (value ?? Enumerable.Empty<T>()).ToList();
        }
        public static HashSet<T> EnumerateAsSet<T>(this IEnumerable<T> value)
        {
            return value is HashSet<T> set
                ? set
                : (value ?? Enumerable.Empty<T>()).ToHashSet();
        }

        public static List<T> EnsureNonNull<T>(this IEnumerable<T> value)
        {
            return value.EnumerateAsList().Where(x => x != null).ToList();
        }

        public static List<string> EnsureNonEmpty(this IEnumerable<string> value)
        {
            return value.EnumerateAsList().Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public static IEnumerable<IEnumerable<T>> Page<T>(this IEnumerable<T> values, int pageSize)
        {
            if (values == null) yield break;

            var items = values.EnumerateAsList();
            var count = Convert.ToInt32(Math.Ceiling(items.Count / (double) pageSize));
            for (var i = 0; i < count; i++)
            {
                yield return items.Skip(pageSize * i).Take(pageSize);
            }
        }

        public static void Page<T>(this IEnumerable<T> values, int pageSize, Action<int, List<T>> action)
        {
            var page = 0;

            var myValues = values.EnumerateAsList();
            do
            {
                var curValues = myValues.Skip(page * pageSize).Take(pageSize).ToList();
                if (curValues.Count == 0) break;

                action(page, curValues);

                page++;
            } while (true);
        }

        public static async Task PageAsync<T>(this IEnumerable<T> values, int pageSize, Func<int, List<T>, Task> action)
        {
            var page = 0;

            var myValues = values.EnumerateAsList();
            do
            {
                var curValues = myValues.Skip(page * pageSize).Take(pageSize).ToList();
                if (curValues.Count == 0) break;

                await action(page, curValues);

                page++;
            } while (true);
        }

        public static async Task<IEnumerable<TS>> PageAsync<T,TS>(this IEnumerable<T> values, int pageSize, Func<int, List<T>, Task<IEnumerable<TS>>> action)
        {
            var pagedResults = new List<TS>();
            var page = 0;

            var myValues = values.EnumerateAsList();
            do
            {
                var curValues = myValues.Skip(page * pageSize).Take(pageSize).ToList();
                if (curValues.Count == 0) break;

                var result = await action(page, curValues);
                pagedResults.AddRange(result);

                page++;
            } while (true);

            return pagedResults;
        }

        public static IEnumerable<T> Yield<T>(this T item)
        {
            yield return item;
        }
    }
}
