using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace NeoDebug
{
    static class SessionUtility
    {
        public const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        public static IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args, Contract contract, VMState state, int stackCount,
            Func<int, (ReadOnlyMemory<byte> scriptHash, int instructionPointer)> getFrame)
        {
            System.Diagnostics.Debug.Assert(args.ThreadId == 1);

            if ((state & HALT_OR_FAULT) == 0)
            {
                var start = args.StartFrame ?? 0;
                var count = args.Levels ?? int.MaxValue;
                var end = Math.Min(stackCount, start + count);

                for (var i = start; i < end; i++)
                {
                    var (scriptHash, instructionPointer) = getFrame(i);
                    var method = contract.GetMethod(scriptHash.Span, instructionPointer);
                    
                    var frame = new StackFrame()
                    {
                        Id = i,
                        Name = method?.Name ?? "<unknown>",
                        //ModuleId = context.ScriptHash,
                    };

                    var sequencePoint = method?.GetCurrentSequencePoint(instructionPointer);

                    if (sequencePoint != null)
                    {
                        frame.Source = new Source()
                        {
                            Name = Path.GetFileName(sequencePoint.Document),
                            Path = sequencePoint.Document
                        };
                        frame.Line = sequencePoint.Start.line;
                        frame.Column = sequencePoint.Start.column;

                        if (sequencePoint.Start != sequencePoint.End)
                        {
                            frame.EndLine = sequencePoint.End.line;
                            frame.EndColumn = sequencePoint.End.column;
                        }
                    }

                    yield return frame;
                }
            }
        }

        public static void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue, VMState state, Action<DebugEvent> sendEvent, Func<IEnumerable<string>> getResults)
        {
            if ((state & VMState.FAULT) != 0)
            {
                sendEvent(new OutputEvent()
                {
                    Category = OutputEvent.CategoryValue.Stderr,
                    Output = "Engine State Faulted\n",
                });
                sendEvent(new TerminatedEvent());
            }
            if ((state & VMState.HALT) != 0)
            {
                foreach (var result in getResults())
                {
                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stdout,
                        Output = $"Return: {result}\n",
                    });
                }
                sendEvent(new ExitedEvent());
                sendEvent(new TerminatedEvent());
            }
            else
            {
                sendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
            }
        }
    }
}
