using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeoDebug
{
    class BreakpointManager
    {
        private readonly Contract contract;
        private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();

        public BreakpointManager(Contract contract)
        {
            this.contract = contract;
        }

        public IEnumerable<Breakpoint> Set(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            var sourcePath = Path.GetFullPath(source.Path).ToLowerInvariant();
            var sourcePathHash = sourcePath.GetHashCode();

            breakPoints[sourcePathHash] = new HashSet<int>();

            if (sourceBreakpoints.Count == 0)
            {
                yield break;
            }

            var sequencePoints = contract.DebugInfo.Methods
                .SelectMany(m => m.SequencePoints)
                .Where(sp => sourcePath.Equals(Path.GetFullPath(sp.Document), StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            foreach (var sourceBreakPoint in sourceBreakpoints)
            {
                var sequencePoint = Array.Find(sequencePoints, sp => sp.Start.line == sourceBreakPoint.Line);

                if (sequencePoint != null)
                {
                    breakPoints[sourcePathHash].Add(sequencePoint.Address);

                    yield return new Breakpoint()
                    {
                        Verified = true,
                        Column = sequencePoint.Start.column,
                        EndColumn = sequencePoint.End.column,
                        Line = sequencePoint.Start.line,
                        EndLine = sequencePoint.End.line,
                        Source = source
                    };
                }
                else
                {
                    yield return new Breakpoint()
                    {
                        Verified = false,
                        Column = sourceBreakPoint.Column,
                        Line = sourceBreakPoint.Line,
                        Source = source
                    };
                }
            }
        }

        public bool Check(VMState state, ReadOnlySpan<byte> scriptHash, int instructionPointer)
        {
            if ((state & SessionUtility.HALT_OR_FAULT) == 0)
            {
                if (contract.ScriptHash.AsSpan().SequenceEqual(scriptHash))
                {
                    foreach (var kvp in breakPoints)
                    {
                        if (kvp.Value.Contains(instructionPointer))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
