using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NeoDebug
{
    class DebugSession : IDebugSession, IVariableContainerSession
    {
        private readonly IExecutionEngine engine;
        private readonly Contract contract;
        private readonly Action<DebugEvent> sendEvent;
        private readonly ImmutableArray<string> returnTypes;
        private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, IVariableContainer> variableContainers = new Dictionary<int, IVariableContainer>();

        private DebugSession(IExecutionEngine engine, Contract contract, Action<DebugEvent> sendEvent, ContractArgument[] arguments, ImmutableArray<string> returnTypes)
        {
            this.engine = engine;
            this.sendEvent = sendEvent;
            this.contract = contract;
            this.returnTypes = returnTypes;

            using var builder = contract.BuildInvokeScript(arguments);
            engine.LoadScript(builder.ToArray());
        }

        static ContractArgument ConvertArgument(JToken arg)
        {
            switch (arg.Type)
            {
                case JTokenType.Integer:
                    return new ContractArgument(ContractParameterType.Integer, new BigInteger(arg.Value<int>()));
                case JTokenType.String:
                    var value = arg.Value<string>();
                    if (value.TryParseBigInteger(out var bigInteger))
                    {
                        return new ContractArgument(ContractParameterType.Integer, bigInteger);
                    }
                    else
                    {
                        return new ContractArgument(ContractParameterType.String, value);
                    }
                default:
                    throw new NotImplementedException($"DebugAdapter.ConvertArgument {arg.Type}");
            }
        }

        static object ConvertArgumentToObject(ContractParameterType paramType, JToken? arg)
        {
            if (arg == null)
            {
                return paramType switch
                {
                    ContractParameterType.Boolean => false,
                    ContractParameterType.String => string.Empty,
                    ContractParameterType.Array => Array.Empty<ContractArgument>(),
                    _ => BigInteger.Zero,
                };
            }

            switch (paramType)
            {
                case ContractParameterType.Boolean:
                    return arg.Value<bool>();
                case ContractParameterType.Integer:
                    return arg.Type == JTokenType.Integer
                        ? new BigInteger(arg.Value<int>())
                        : BigInteger.Parse(arg.ToString());
                case ContractParameterType.String:
                    return arg.ToString();
                case ContractParameterType.Array:
                    return arg.Select(ConvertArgument).ToArray();
                case ContractParameterType.ByteArray:
                    {
                        var value = arg.ToString();
                        if (value.TryParseBigInteger(out var bigInteger))
                        {
                            return bigInteger;
                        }

                        var byteCount = Encoding.UTF8.GetByteCount(value);
                        using var owner = MemoryPool<byte>.Shared.Rent(byteCount);
                        var span = owner.Memory.Span.Slice(0, byteCount);
                        Encoding.UTF8.GetBytes(value, span);
                        return new BigInteger(span);
                    }
            }
            throw new NotImplementedException($"DebugAdapter.ConvertArgument {paramType} {arg}");
        }

        static ContractArgument ConvertArgument((string name, string type) param, JToken? arg)
        {
            var type = param.type switch
            {
                "Integer" => ContractParameterType.Integer,
                "String" => ContractParameterType.String,
                "Array" => ContractParameterType.Array,
                "Boolean" => ContractParameterType.Boolean,
                "ByteArray" => ContractParameterType.ByteArray,
                "" => ContractParameterType.ByteArray,
                _ => throw new NotImplementedException(),
            };

            return new ContractArgument(type, ConvertArgumentToObject(type, arg));
        }

        static public DebugSession Create(Contract contract, LaunchArguments arguments, Action<DebugEvent> sendEvent)
        {
            var returnTypes = arguments.GetReturnTypes();
            var contractArgs = GetArguments(contract.EntryPoint).ToArray();

            var engine = DebugExecutionEngine.Create(contract, arguments, outputEvent => sendEvent(outputEvent));
            return new DebugSession(engine, contract, sendEvent, contractArgs, returnTypes);

            JArray GetArgsConfig()
            {
                if (arguments.ConfigurationProperties.TryGetValue("args", out var args))
                {
                    if (args is JArray jArray)
                    {
                        return jArray;
                    }

                    return new JArray(args);
                }

                return new JArray();
            }

            IEnumerable<ContractArgument> GetArguments(DebugInfo.Method method)
            {
                var args = GetArgsConfig();
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    yield return ConvertArgument(
                        method.Parameters[i],
                        i < args.Count ? args[i] : null);
                }
            }
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
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

        const VMState HALT_OR_FAULT = VMState.HALT | VMState.FAULT;

        bool CheckBreakpoint()
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.CurrentContext;

                if (contract.ScriptHash.AsSpan().SequenceEqual(context.ScriptHash))
                {
                    var ip = context.InstructionPointer;
                    foreach (var kvp in breakPoints)
                    {
                        if (kvp.Value.Contains(ip))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            ClearVariableContainers();
            SessionUtility.FireStoppedEvent(reasonValue, engine.State, sendEvent, GetResults);
        }

        void Step(Func<int, int, bool> compare)
        {
            var c = engine.InvocationStack.Count;
            var stopReason = StoppedEvent.ReasonValue.Step;
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteNext();

                if ((engine.State & HALT_OR_FAULT) != 0)
                {
                    break;
                }

                if (CheckBreakpoint())
                {
                    stopReason = StoppedEvent.ReasonValue.Breakpoint;
                    break;
                }

                if (compare(engine.InvocationStack.Count, c) && contract.CheckSequencePoint(engine.CurrentContext.ScriptHash, engine.CurrentContext.InstructionPointer))
                {
                    break;
                }
            }

            FireStoppedEvent(stopReason);
        }

        public void Continue()
        {
            while ((engine.State & HALT_OR_FAULT) == 0)
            {
                engine.ExecuteNext();

                if (CheckBreakpoint())
                {
                    break;
                }
            }

            FireStoppedEvent(StoppedEvent.ReasonValue.Breakpoint);
        }

        public void ReverseContinue()
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public IEnumerable<Thread> GetThreads()
        {
            yield return new Thread(1, "main thread");
        }

        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {

            return SessionUtility.GetStackFrames(args, contract, engine.State, engine.InvocationStack.Count, i =>
                {
                    var context = engine.InvocationStack.Peek(i);
                    return (context.ScriptHash.AsMemory(), context.InstructionPointer);
                });
        }

        void ClearVariableContainers()
        {
            variableContainers.Clear();
        }

        public int AddVariableContainer(IVariableContainer container)
        {
            var id = container.GetHashCode();
            if (variableContainers.TryAdd(id, container))
            {
                return id;
            }

            throw new Exception();
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                var context = engine.InvocationStack.Peek(args.FrameId);
                var contextID = AddVariableContainer(
                    new ExecutionContextContainer(this, context, contract));
                yield return new Scope("Locals", contextID, false);

                var storageID = AddVariableContainer(engine.GetStorageContainer(this));
                yield return new Scope("Storage", storageID, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) == 0)
            {
                if (variableContainers.TryGetValue(args.VariablesReference, out var container))
                {
                    return container.GetVariables();
                }
            }

            return Enumerable.Empty<Variable>();
        }

        private string GetResult(NeoArrayContainer container)
        {
            var array = new Newtonsoft.Json.Linq.JArray();
            foreach (var x in container.GetVariables())
            {
                array.Add(GetResult(x));
            }
            return array.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private string GetResult(ByteArrayContainer container)
        {
            return container.Span.ToHexString();
        }

        private string GetResult(Variable variable)
        {
            if (variable.VariablesReference == 0)
            {
                return variable.Value;
            }

            if (variableContainers.TryGetValue(variable.VariablesReference, out var container))
            {
                switch (container)
                {
                    case NeoArrayContainer arrayContainer:
                        return GetResult(arrayContainer);
                    case ByteArrayContainer byteArrayContainer:
                        return GetResult(byteArrayContainer);
                    default:
                        return $"{container.GetType().Name} unsupported container";
                }
            }

            return string.Empty;
        }

        string GetResult(StackItem item, string? typeHint = null)
        {
            if (typeHint == "ByteArray")
            {
                return Helpers.ToHexString(item.GetByteArray());
            }

            return GetResult(item.GetVariable(this, string.Empty, typeHint));
        }

        IEnumerable<string> GetResults()
        {
            foreach (var (item, index) in engine.ResultStack.Select((_item, index) => (_item, index)))
            {
                var returnType = index < returnTypes.Length
                    ? returnTypes[index] : null;
                yield return GetResult(item, returnType);
            }
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if ((engine.State & HALT_OR_FAULT) != 0)
                return DebugAdapter.FailedEvaluation;

            var (typeHint, index, variableName) = Helpers.ParseEvalExpression(args.Expression);

            if (variableName.StartsWith("$storage"))
            {
                return engine.EvaluateStorageExpression(this, args);
            }

            Variable? GetVariable(StackItem item, (string name, string type) local)
            {
                if (index.HasValue)
                {
                    if (item is Neo.VM.Types.Array neoArray
                        && index.Value < neoArray.Count)
                    {
                        return neoArray[index.Value].GetVariable(this, local.name + $"[{index.Value}]", typeHint);
                    }
                }
                else
                {
                    return item.GetVariable(this, local.name, typeHint ?? local.type);
                }

                return null;
            }

            for (var stackIndex = 0; stackIndex < engine.InvocationStack.Count; stackIndex++)
            {
                var context = engine.InvocationStack.Peek(stackIndex);
                if (context.AltStack.Count <= 0)
                    continue;

                var method = contract.GetMethod(context.ScriptHash, context.InstructionPointer);
                if (method == null)
                    continue;

                var locals = method.GetLocals().ToArray();
                var variables = (Neo.VM.Types.Array)context.AltStack.Peek(0);

                for (int varIndex = 0; varIndex < Math.Min(variables.Count, locals.Length); varIndex++)
                {
                    var local = locals[varIndex];
                    if (local.name == variableName)
                    {
                        var variable = GetVariable(variables[varIndex], local);
                        if (variable != null)
                        {
                            return new EvaluateResponse()
                            {
                                Result = variable.Value,
                                VariablesReference = variable.VariablesReference,
                                Type = variable.Type
                            };
                        }
                    }
                }
            }

            return DebugAdapter.FailedEvaluation;
        }
    }
}
