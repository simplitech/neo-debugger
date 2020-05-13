using System.Collections.Immutable;
using Neo.VM;
using NeoFx;
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
        public readonly struct Context
        {
            public readonly UInt160 ScriptHash;
            public readonly int InstructionPointer;
            public readonly ImmutableArray<string> EvalStack;
            public readonly ImmutableArray<string> AltStack;

            public Context(UInt160 scriptHash, int instructionPointer, ImmutableArray<string> evalStack, ImmutableArray<string> altStack)
            {
                ScriptHash = scriptHash;
                InstructionPointer = instructionPointer;
                EvalStack = evalStack;
                AltStack = altStack;
            }
        }

        public readonly Neo.VM.VMState State;
        public readonly ImmutableArray<Context> Contexts;
        public readonly ImmutableDictionary<StorageKey, StorageItem> Storages;

        public TracePoint(VMState state, ImmutableArray<Context> contexts, ImmutableDictionary<StorageKey, StorageItem> storages)
        {
            State = state;
            Contexts = contexts;
            Storages = storages;
        }
    }
}
