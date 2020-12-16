// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Threading;

namespace Microsoft.Docs.Build
{
    /// <summary>
    /// Represents a component whose life time is scoped, usually per build.
    /// </summary>
    internal class Scoped<T>
    {
        private readonly AsyncLocal<ImmutableStack<T>> _value = new();

        public T Value => _value.Value!.Peek();

        public Disposable BeginScope(T value)
        {
            var stack = _value.Value ?? ImmutableStack<T>.Empty;
            _value.Value = stack.Push(value);
            return new Disposable(_value);
        }

        public readonly struct Disposable : IDisposable
        {
            public readonly AsyncLocal<ImmutableStack<T>> _value;

            public Disposable(AsyncLocal<ImmutableStack<T>> value) => _value = value;

            public void Dispose() => _value.Value = _value.Value?.Pop() ?? ImmutableStack<T>.Empty;
        }
    }
}
