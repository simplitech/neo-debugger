using System.Collections.Immutable;
using Neo.VM;

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
        public readonly struct Storage
        {
            public readonly ImmutableArray<byte> Key; 
            public readonly ImmutableArray<byte> Value;
            public readonly bool Constant;

            public Storage(ImmutableArray<byte> key, ImmutableArray<byte> value, bool constant)
            {
                Constant = constant;
                Key = key;
                Value = value;
            }
        }

        public readonly struct StackFrame
        {
            public readonly int Index;
            public readonly ImmutableArray<byte> ScriptHash;
            public readonly int InstructionPointer;
            public readonly ImmutableArray<Storage> Storages;
            // TODO: add variables

            public StackFrame(int index, ImmutableArray<byte> scriptHash, int instructionPointer, ImmutableArray<Storage> storages)
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
