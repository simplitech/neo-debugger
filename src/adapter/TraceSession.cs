using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NeoDebug
{
    class TraceSession : IDebugSession
    {
        private readonly Contract contract;
        private readonly Action<DebugEvent> sendEvent;
        private readonly Dictionary<int, HashSet<int>> breakPoints = new Dictionary<int, HashSet<int>>();

        private TraceSession(Contract contract, Action<DebugEvent> sendEvent)
        {
            this.contract = contract;
            this.sendEvent = sendEvent;

        }

        static public async Task<TraceSession> Create(Contract contract, LaunchArguments arguments, Action<DebugEvent> sendEvent)
        {
            static async Task<JArray> LoadJArray(string fileName)
            {
                using var stream = File.OpenRead(fileName);
                using var streamReader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                return await JArray.LoadAsync(jsonReader);
            } 

            var traceFileName = arguments.ConfigurationProperties["trace-file"].Value<string>();
            var traceItems = await LoadJArray(traceFileName);
            var returnTypes = arguments.GetReturnTypes();

            throw new NotImplementedException();
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Thread> GetThreads()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            throw new NotImplementedException();
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            throw new NotImplementedException();
        }

        public void Continue()
        {
            throw new NotImplementedException();
        }

        public void ReverseContinue()
        {
            throw new NotImplementedException();
        }

        public void StepBack()
        {
            throw new NotImplementedException();
        }

        public void StepIn()
        {
            throw new NotImplementedException();
        }

        public void StepOut()
        {
            throw new NotImplementedException();
        }

        public void StepOver()
        {
            throw new NotImplementedException();
        }
    }
}
