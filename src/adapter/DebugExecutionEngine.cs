using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;

namespace NeoDebug
{
    class DebugExecutionEngine : ExecutionEngine, IExecutionEngine
    {
        private readonly InteropService interopService;

        public DebugExecutionEngine(IScriptContainer container, ScriptTable scriptTable, InteropService interopService)
            : base(container, new Crypto(), scriptTable, interopService)
        {
            this.interopService = interopService;
        }

        public void ExecuteInstruction() => ExecuteNext();

        public IVariableContainer GetStorageContainer(IVariableContainerSession session, in UInt160 scriptHash)
            => interopService.GetStorageContainer(session, scriptHash);

        public EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, in UInt160 scriptHash, EvaluateArguments args)
            => interopService.EvaluateStorageExpression(session, scriptHash, args);

        public string GetMethodName(uint methodHash) => interopService.GetMethodName(methodHash);
    }
}
