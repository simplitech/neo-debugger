using System;
using System.Collections.Generic;
using Neo.VM;
using NeoFx;

namespace NeoDebug
{
    partial class DebugExecutionEngine
    {
        class ExecutionContextAdapter : IExecutionContext
        {
            private readonly ExecutionContext context;
            private readonly UInt160 scriptHash;
            private readonly StackItemsAdapter evalStack;
            private readonly StackItemsAdapter altStack;

            public ExecutionContextAdapter(ExecutionContext context)
            {
                this.context = context;
                this.scriptHash = new UInt160(context.ScriptHash);
                this.evalStack = new StackItemsAdapter(context.EvaluationStack);
                this.altStack = new StackItemsAdapter(context.AltStack);
            }

            ReadOnlyMemory<byte> IExecutionContext.Script => ((byte[])context.Script).AsMemory();

            UInt160 IExecutionContext.ScriptHash => scriptHash;

            int IExecutionContext.InstructionPointer => context.InstructionPointer;

            IReadOnlyList<StackItem> IExecutionContext.EvaluationStack => evalStack;

            IReadOnlyList<StackItem> IExecutionContext.AltStack => altStack;
        }
    }
}
