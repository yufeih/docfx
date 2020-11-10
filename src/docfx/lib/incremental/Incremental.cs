// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Docs.Build
{
    internal static class Incremental
    {
        private static readonly AsyncLocal<IFunction> t_functionGraph = new AsyncLocal<IFunction>();

        public static T Impure<T>(Func<T> func) => Impure(func, func);

        public static T Impure<T, TToken>(Func<T> func, Func<TToken> changeToken)
        {
            var node = new Function<T, TToken>(func, changeToken);

            try
            {
                return func();
            }
            finally
            {

            }
        }

        private interface IFunction
        {
            bool HasChanged();
        }

        private class Function<T, TToken> : IFunction
        {
            private readonly Func<T> _func;
            private readonly Func<TToken> _changeToken;
            private readonly IFunction[] _children;

            public Function(Func<T> func, Func<TToken> changeToken)
            {
                _func = func;
                _changeToken = changeToken;
            }

            public bool HasChanged()
            {
                return false;
            }
        }
    }
}
