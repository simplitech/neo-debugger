using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
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
    class TraceSession : IDebugSession, IVariableContainerSession
    {
        private readonly ImmutableArray<TracePoint> tracePoints;
        private readonly TraceResult traceResult;
        private readonly Contract contract;
        private readonly Action<DebugEvent> sendEvent;
        private readonly ImmutableArray<string> returnTypes;
        private int tracePointIndex = 0;
        private readonly BreakpointManager breakPointManager;
        private readonly VariableContainerManager variables = new VariableContainerManager();

        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        TracePoint CurrentTracePoint => tracePoints[tracePointIndex];
        TracePoint.StackFrame CurrentStackFrame => CurrentTracePoint.StackFrames.First();


        private TraceSession(ImmutableArray<TracePoint> tracePoints, in TraceResult traceResult, Contract contract, Action<DebugEvent> sendEvent, ImmutableArray<string> returnTypes)
        {
            this.tracePoints = tracePoints;
            this.traceResult = traceResult;
            this.contract = contract;
            this.sendEvent = sendEvent;
            this.returnTypes = returnTypes;
            this.breakPointManager = new BreakpointManager(contract);
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
            return breakPointManager.Set(source, sourceBreakpoints);
        }

        public IEnumerable<Thread> GetThreads()
        {
            yield return new Thread(1, "main thread");
        }

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
            if ((CurrentTracePoint.State & HALT_OR_FAULT) == 0)
            {
                var currentFrame = CurrentStackFrame;

                // var context = engine.InvocationStack.Peek(args.FrameId);
                // var contextID = AddVariableContainer(
                //     new ExecutionContextContainer(this, context, contract));
                // yield return new Scope("Locals", contextID, false);
 
                var storageContainer = new EmulatedStorageContainer(this, 
                    () => currentFrame.Storages.Select(s => (s.key.AsMemory(), s.item)));
                var storageID = variables.Add(storageContainer);
                yield return new Scope("Storage", storageID, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((CurrentTracePoint.State & HALT_OR_FAULT) == 0)
            {
                if (variables.TryGetValue(args.VariablesReference, out var container))
                {
                    return container.GetVariables();
                }
            }

            return Enumerable.Empty<Variable>();
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            return DebugAdapter.FailedEvaluation;
        }

        enum Direction { Forward, Reverse };

        void Continue(Direction direction)
        {
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((CurrentTracePoint.State & HALT_OR_FAULT) == 0)
            {
                if (direction == Direction.Reverse && tracePointIndex == 0) break;
                    
                tracePointIndex += direction == Direction.Forward ? 1 : -1;

                if (CheckBreakpoint())
                {
                    stopReason = StoppedEvent.ReasonValue.Breakpoint;
                    break;
                }
            }

            SessionUtility.FireStoppedEvent(stopReason, CurrentTracePoint.State, sendEvent, () => traceResult.Results);
        }

        bool CheckBreakpoint() 
        {
            var tracePoint = CurrentTracePoint;
            var stackFrame = tracePoint.StackFrames.First();
            return breakPointManager.Check(tracePoint.State, stackFrame.ScriptHash.AsSpan(), stackFrame.InstructionPointer);
        }

        void Step(Func<int, int, bool> compare, Direction direction)
        {
            var c = CurrentTracePoint.StackFrames.Length;
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((CurrentTracePoint.State & HALT_OR_FAULT) == 0)
            {
                tracePointIndex += direction == Direction.Forward ? 1 : -1;

                if (tracePointIndex < 0 || tracePointIndex >= tracePoints.Length)
                {
                    throw new InvalidOperationException();
                }

                if ((CurrentTracePoint.State & HALT_OR_FAULT) != 0)
                {
                    break;
                }

                if (CheckBreakpoint())
                {
                   stopReason = StoppedEvent.ReasonValue.Breakpoint;
                   break;
                }

                var currentFrame = CurrentStackFrame;
                if (compare(CurrentTracePoint.StackFrames.Length, c) 
                    && contract.CheckSequencePoint(currentFrame.ScriptHash.AsSpan(), currentFrame.InstructionPointer))
                {
                    break;
                }
            }

            SessionUtility.FireStoppedEvent(stopReason, CurrentTracePoint.State, sendEvent, () => traceResult.Results);
        }
        
        public void Continue()
        {
            Continue(Direction.Forward);
        }

        public void ReverseContinue()
        {
            Continue(Direction.Reverse);
        }

        public void StepOver()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize <= originalStackSize, Direction.Forward);
        }

        public void StepIn()
        {
            Step((_, __) => true, Direction.Forward);
        }

        public void StepOut()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize < originalStackSize, Direction.Forward);
        }

        public void StepBack()
        {
            Step((_, __) => true, Direction.Reverse);
        }

        int IVariableContainerSession.AddVariableContainer(IVariableContainer container)
        {
            return variables.Add(container);
        }
    }
}
