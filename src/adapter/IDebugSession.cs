using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Generic;

namespace NeoDebug
{
    interface IDebugSession
    {
        IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints);
        IEnumerable<Thread> GetThreads();
        IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args);
        IEnumerable<Scope> GetScopes(ScopesArguments args);
        IEnumerable<Variable> GetVariables(VariablesArguments args);
        EvaluateResponse Evaluate(EvaluateArguments args);
        void Continue();
        void StepIn();
        void StepOut();
        void StepOver();
    }
}
