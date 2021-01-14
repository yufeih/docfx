// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal static class ParallelUtility
    {
        private static readonly int s_maxParallelism = Math.Max(8, Environment.ProcessorCount * 2);
        private static readonly ParallelOptions s_parallelOptions = new() { MaxDegreeOfParallelism = s_maxParallelism };
        private static readonly AsyncLocal<ImmutableStack<Sentinal>> s_sentinalStack = new();

        public static void ForEach<T>(LogScope scope, ErrorBuilder errors, IEnumerable<T> source, Action<T> action)
        {
            var sentinal = new Sentinal();
            var stack = s_sentinalStack.Value ?? ImmutableStack<Sentinal>.Empty;
            var parentSentinal = stack.IsEmpty ? null : stack.Peek();

            s_sentinalStack.Value = stack.Push(sentinal);
            parentSentinal?.Increment();

            try
            {
                ForEachCore(scope, errors, WaitItems(source, sentinal), action);
            }
            finally
            {
                parentSentinal?.Decrement();
                s_sentinalStack.Value = s_sentinalStack.Value.Pop();
            }
        }

        private static IEnumerable<T> WaitItems<T>(IEnumerable<T> items, Sentinal sentinal)
        {
            foreach (var item in items)
            {
                sentinal.Wait(500);
                yield return item;
            }
        }

        private static void ForEachCore<T>(LogScope scope, ErrorBuilder errors, IEnumerable<T> source, Action<T> action)
        {
            var done = 0;
            var total = source.Count();

            Parallel.ForEach(source, s_parallelOptions, item =>
            {
                try
                {
                    action(item);
                }
                catch (Exception ex) when (DocfxException.IsDocfxException(ex, out var dex))
                {
                    errors.AddRange(dex);
                }
                catch
                {
                    Console.WriteLine($"Error processing '{item}'");
                    throw;
                }

                Progress.Update(scope, Interlocked.Increment(ref done), total);
            });
        }

        private class Sentinal
        {
            private readonly ManualResetEventSlim _event = new ManualResetEventSlim(true);
            private int _counter;

            public void Decrement()
            {
                if (Interlocked.Decrement(ref _counter) <= 0)
                {
                    _event.Set();
                }
            }

            public void Increment()
            {
                if (Interlocked.Increment(ref _counter) > 0)
                {
                    _event.Reset();
                }
            }

            public void Wait(int timeout)
            {
                _event.Wait(timeout);
            }
        }
    }
}
