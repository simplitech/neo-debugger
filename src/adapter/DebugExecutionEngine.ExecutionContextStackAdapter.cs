using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neo.VM;

namespace NeoDebug
{
    partial class DebugExecutionEngine
    {
        class ExecutionContextStackAdapter : IReadOnlyList<IExecutionContext>
        {
            private readonly RandomAccessStack<ExecutionContext> contexts;
            private readonly Dictionary<int, IExecutionContext> adapterCache = new Dictionary<int, IExecutionContext>();

            public ExecutionContextStackAdapter(RandomAccessStack<ExecutionContext> contexts)
            {
                this.contexts = contexts;
            }

            IExecutionContext GetContextAdapter(ExecutionContext context)
            {
                var key = context.GetHashCode();
                while (true)
                {
                    if (adapterCache.TryGetValue(key, out var value))
                    {
                        return value;
                    }

                    value = new ExecutionContextAdapter(context);
                    if (adapterCache.TryAdd(key, value))
                    {
                        return value;
                    }
                }
            }
            
            public IExecutionContext Current => ((IReadOnlyList<IExecutionContext>)this)[0];
            public IExecutionContext Entry => ((IReadOnlyList<IExecutionContext>)this)[contexts.Count - 1];

            public void Cleanup()
            {
                var currentKeys = contexts.Select(c => c.GetHashCode());
                foreach (var key in adapterCache.Keys.Except(currentKeys))
                {
                    adapterCache.Remove(key);
                }
            }

            IExecutionContext IReadOnlyList<IExecutionContext>.this[int index] => GetContextAdapter(contexts.Peek(index));

            int IReadOnlyCollection<IExecutionContext>.Count => contexts.Count;

            IEnumerator<IExecutionContext> IEnumerable<IExecutionContext>.GetEnumerator()
            {
                foreach (var c in contexts)
                {
                    yield return GetContextAdapter(c);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() 
                => ((IReadOnlyCollection<IExecutionContext>)this).GetEnumerator();
        }
    }
}
