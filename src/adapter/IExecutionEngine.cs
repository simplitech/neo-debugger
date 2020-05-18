using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx;

namespace NeoDebug
{

    interface IExecutionEngine
    {
        VMState State { get; }
        IReadOnlyList<StackItem> ResultStack { get; }
        IExecutionContext CurrentContext { get; }
        IExecutionContext EntryContext { get; }
        IReadOnlyList<IExecutionContext> InvocationStack { get; }
        void ExecuteInstruction();
        IVariableContainer GetStorageContainer(IVariableContainerSession session, in UInt160 scriptHash);
        EvaluateResponse EvaluateStorageExpression(IVariableContainerSession session, in UInt160 scriptHash, EvaluateArguments args);

        // TODO: figure out how to move GetMethodName elsewhere
        string GetMethodName(uint methodHash);

    }
}
