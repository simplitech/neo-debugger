using System.Collections.Immutable;
using Neo.VM;
using NeoFx.Models;

namespace NeoDebug.Models
{
    // TODO: add TraceNotification and TraceLog

    public readonly struct TraceResult
    {
        public readonly Neo.VM.VMState State;
        public readonly NeoFx.Fixed8 GasConsumed;
        public readonly ImmutableArray<string> Results;

        public TraceResult(VMState state, NeoFx.Fixed8 gasConsumed, ImmutableArray<string> results)
        {
            State = state;
            GasConsumed = gasConsumed;
            Results = results;
        }
    }

    public readonly struct TracePoint
    {
        public readonly struct StackFrame
        {
            public readonly int Index;
            public readonly ImmutableArray<byte> ScriptHash;
            public readonly int InstructionPointer;
            public readonly ImmutableArray<(ImmutableArray<byte> key, StorageItem item)> Storages;
            // TODO: add variables

            public StackFrame(int index, ImmutableArray<byte> scriptHash, int instructionPointer, ImmutableArray<(ImmutableArray<byte> key, StorageItem item)> storages)
            {
                Storages = storages;
                Index = index;
                ScriptHash = scriptHash;
                InstructionPointer = instructionPointer;
            }
        }

        public readonly Neo.VM.VMState State;
        public readonly ImmutableArray<StackFrame> StackFrames;

        public TracePoint(VMState state, ImmutableArray<StackFrame> stackFrames)
        {
            State = state;
            StackFrames = stackFrames;
        }
    }
}
