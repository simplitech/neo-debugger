using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeoDebug
{
    class TraceSession : IDebugSession
    {
        private readonly ImmutableArray<TracePoint> tracePoints;
        private readonly TraceResult traceResult;
        private readonly Contract contract;
        private readonly Action<DebugEvent> sendEvent;
        private readonly ImmutableArray<string> returnTypes;

        private int tracePointIndex = 0;

        //private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();

        private TraceSession(ImmutableArray<TracePoint> tracePoints, in TraceResult traceResult, Contract contract, Action<DebugEvent> sendEvent, ImmutableArray<string> returnTypes)
        {
            this.tracePoints = tracePoints;
            this.traceResult = traceResult;
            this.contract = contract;
            this.sendEvent = sendEvent;
            this.returnTypes = returnTypes;
        }

        static public TraceSession Create(Contract contract, LaunchArguments arguments, Action<DebugEvent> sendEvent)
        {
            var returnTypes = arguments.GetReturnTypes();
            var traceFileName = arguments.ConfigurationProperties["trace-file"].Value<string>();
            var (tracePoints, traceResult) = TraceDebugJsonParser.Load(traceFileName);

            return new TraceSession(tracePoints, traceResult, contract, sendEvent, returnTypes);
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            yield break;
        }

        public IEnumerable<Thread> GetThreads()
        {
            yield return new Thread(1, "main thread");
        }

        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        TracePoint CurrentTracePoint => tracePoints[tracePointIndex];
        TracePoint.StackFrame CurrentStackFrame => CurrentTracePoint.StackFrames.First();


        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {
            return SessionUtility.GetStackFrames(args, contract, CurrentTracePoint.State, CurrentTracePoint.StackFrames.Length, i =>
                {
                    var stackFrame = CurrentTracePoint.StackFrames[i];
                    return (stackFrame.ScriptHash.AsMemory(), stackFrame.InstructionPointer);
                });
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            yield break;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            yield break;
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            return DebugAdapter.FailedEvaluation;
        }

        public void Continue()
        {
            throw new NotImplementedException();
        }

        void Step(Func<int, int, bool> compare, bool decrement = false)
        {
            var c = CurrentTracePoint.StackFrames.Length;
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((CurrentTracePoint.State & HALT_OR_FAULT) == 0)
            {
                tracePointIndex += decrement ? -1 : 1;

                if (tracePointIndex < 0 || tracePointIndex >= tracePoints.Length)
                {
                    throw new InvalidOperationException();
                }

                if ((CurrentTracePoint.State & HALT_OR_FAULT) != 0)
                {
                    break;
                }

                //if (CheckBreakpoint())
                //{
                //    stopReason = StoppedEvent.ReasonValue.Breakpoint;
                //    break;
                //}

                var currentFrame = CurrentStackFrame;
                if (compare(CurrentTracePoint.StackFrames.Length, c) 
                    && contract.CheckSequencePoint(currentFrame.ScriptHash.AsSpan(), currentFrame.InstructionPointer))
                {
                    break;
                }
            }

            SessionUtility.FireStoppedEvent(stopReason, CurrentTracePoint.State, sendEvent, () => traceResult.Results);
        }

        
        public void ReverseContinue()
        {
            throw new NotImplementedException();
        }

        public void StepOver()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize <= originalStackSize);
        }

        public void StepIn()
        {
            Step((_, __) => true);
        }

        public void StepOut()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize < originalStackSize);
        }

        public void StepBack()
        {
            Step((_, __) => true, true);
        }
    }
}
