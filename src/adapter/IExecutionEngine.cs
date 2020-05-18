using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;

namespace NeoDebug
{
    interface IExecutionEngine
    {
        VMState State { get; }
        RandomAccessStack<StackItem> ResultStack { get; }
        ExecutionContext CurrentContext { get; }
        ExecutionContext EntryContext { get; }
        RandomAccessStack<ExecutionContext> InvocationStack { get; }
        void ExecuteInstruction();
        IVariableContainer GetStorageContainer(IVariableContainerSession session, in UInt160 scriptHash);
        EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, in UInt160 scriptHash, EvaluateArguments args);

        // TODO: figure out how to remove GetMethodName
        string GetMethodName(uint methodHash);

    }
}
