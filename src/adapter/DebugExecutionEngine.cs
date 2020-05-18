using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;

namespace NeoDebug
{
    partial class DebugExecutionEngine : ExecutionEngine, IExecutionEngine
    {

        private readonly InteropService interopService;
        private readonly ExecutionContextStackAdapter invocationStack;
        private readonly StackItemsAdapter resultStack;

        IReadOnlyList<StackItem> IExecutionEngine.ResultStack => resultStack;

        IExecutionContext IExecutionEngine.CurrentContext => invocationStack.Current;

        IExecutionContext IExecutionEngine.EntryContext => invocationStack.Entry;

        IReadOnlyList<IExecutionContext> IExecutionEngine.InvocationStack => invocationStack;

        public DebugExecutionEngine(IScriptContainer container, ScriptTable scriptTable, InteropService interopService)
            : base(container, new Crypto(), scriptTable, interopService)
        {
            this.interopService = interopService;
            this.resultStack = new StackItemsAdapter(ResultStack);
            this.invocationStack = new ExecutionContextStackAdapter(InvocationStack);
        }

        public void ExecuteInstruction() => ExecuteNext();

        protected override bool PostExecuteInstruction(Instruction instruction)
        {
            if (instruction.OpCode == OpCode.RET)
            {
                invocationStack.Cleanup();
            }

            return true;
        }

        public IVariableContainer GetStorageContainer(IVariableContainerSession session, in UInt160 scriptHash)
            => interopService.GetStorageContainer(session, scriptHash);

        public EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, in UInt160 scriptHash, EvaluateArguments args)
            => interopService.EvaluateStorageExpression(session, scriptHash, args);

        public string GetMethodName(uint methodHash) => interopService.GetMethodName(methodHash);
    }
}
