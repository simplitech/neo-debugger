using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using NeoFx;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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

        TracePoint CurrentTracePoint => tracePoints[tracePointIndex];

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

        static byte[] ToArray(UInt160 value)
        {
            if (value.TryToArray(out var array))
            {
                return array;
            }

            throw new Exception();
        }

        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {
            return SessionUtility.GetStackFrames(args, contract, CurrentTracePoint.State, CurrentTracePoint.Contexts.Length, i =>
                {
                    var context = CurrentTracePoint.Contexts[i];
                    var scriptHash = ToArray(context.ScriptHash);
                    return (scriptHash.AsMemory(), context.InstructionPointer);
                    
                    throw new Exception();
                });
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            if ((CurrentTracePoint.State & SessionUtility.HALT_OR_FAULT) == 0)
            {
                var tracePoint = CurrentTracePoint;
                var context = tracePoint.Contexts.First();
                var storages = tracePoint.Storages
                    .Where(s => s.Key.ScriptHash == context.ScriptHash)
                    .Select(s => (s.Key.Key, s.Value));

                // var context = engine.InvocationStack.Peek(args.FrameId);
                // var contextID = AddVariableContainer(
                //     new ExecutionContextContainer(this, context, contract));
                // yield return new Scope("Locals", contextID, false);
 
                var storageContainer = new EmulatedStorageContainer(this, 
                    () => storages);
                var storageID = variables.Add(storageContainer);
                yield return new Scope("Storage", storageID, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((CurrentTracePoint.State & SessionUtility.HALT_OR_FAULT) == 0)
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


        bool CheckBreakpoint() 
        {
            var tracePoint = CurrentTracePoint;
            var stackFrame = tracePoint.Contexts.First();
            var scriptHash = ToArray(stackFrame.ScriptHash);
            return breakPointManager.Check(tracePoint.State, scriptHash.AsSpan(), stackFrame.InstructionPointer);
        }
        
        void Continue(Direction direction)
        {
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((CurrentTracePoint.State & SessionUtility.HALT_OR_FAULT) == 0)
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

        void Step(Func<int, int, bool> compare, Direction direction)
        {
            var c = CurrentTracePoint.Contexts.Length;
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((CurrentTracePoint.State & SessionUtility.HALT_OR_FAULT) == 0)
            {
                tracePointIndex += direction == Direction.Forward ? 1 : -1;

                if (tracePointIndex < 0 || tracePointIndex >= tracePoints.Length)
                {
                    throw new InvalidOperationException();
                }

                if ((CurrentTracePoint.State & SessionUtility.HALT_OR_FAULT) != 0)
                {
                    break;
                }

                if (CheckBreakpoint())
                {
                   stopReason = StoppedEvent.ReasonValue.Breakpoint;
                   break;
                }

                var currentFrame = CurrentTracePoint.Contexts.First();
                var scriptHash = ToArray(currentFrame.ScriptHash);
                if (compare(CurrentTracePoint.Contexts.Length, c) 
                    && contract.CheckSequencePoint(scriptHash.AsSpan(), currentFrame.InstructionPointer))
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
            // step to the next sequence point, regardless of stack 
            Step((_, __) => true, Direction.Forward);
        }

        public void StepOut()
        {
            Step((currentStackSize, originalStackSize) => currentStackSize < originalStackSize, Direction.Forward);
        }

        public void StepBack()
        {
            // step to the previous sequence point, regardless of stack 
            Step((_, __) => true, Direction.Reverse);
        }

        int IVariableContainerSession.AddVariableContainer(IVariableContainer container)
        {
            return variables.Add(container);
        }
    }
}
