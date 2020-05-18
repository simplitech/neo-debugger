using System.Collections;
using System.Collections.Generic;
using Neo.VM;

namespace NeoDebug
{
    partial class DebugExecutionEngine
    {
        class StackItemsAdapter : IReadOnlyList<StackItem>
    {
        private readonly RandomAccessStack<StackItem> stackItems;

        public StackItemsAdapter(RandomAccessStack<StackItem> stackItems)
        {
            this.stackItems = stackItems;
        }

        StackItem IReadOnlyList<StackItem>.this[int index] => stackItems.Peek(index);

        int IReadOnlyCollection<StackItem>.Count => stackItems.Count;

        IEnumerator<StackItem> IEnumerable<StackItem>.GetEnumerator() => stackItems.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<StackItem>)this).GetEnumerator();
    }
    }
}
