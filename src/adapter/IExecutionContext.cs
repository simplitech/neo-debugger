using System;
using System.Collections.Generic;
using Neo.VM;
using NeoFx;

namespace NeoDebug
{
    public interface IExecutionContext
    {
        ReadOnlyMemory<byte> Script { get; }
        UInt160 ScriptHash { get; }
        int InstructionPointer { get; }
        IReadOnlyList<StackItem> EvaluationStack { get; }
        IReadOnlyList<StackItem> AltStack { get; }
    }
}
