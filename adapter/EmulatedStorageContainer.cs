﻿using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.DebugAdapter
{
    internal class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly NeoDebugSession session;
            private readonly byte[] key;
            private readonly byte[] value;

            public KvpContainer(NeoDebugSession session, (byte[] key, byte[] value) kvp)
            {
                this.session = session;
                this.key = kvp.key;
                this.value = kvp.value;
            }

            public IEnumerable<Variable> GetVariables(VariablesArguments args)
            {
                yield return ByteArrayContainer.GetVariable(key, session, "key");
                yield return ByteArrayContainer.GetVariable(value, session, "value");
            }
        }

        private readonly NeoDebugSession session;
        private readonly EmulatedStorage storage;

        public EmulatedStorageContainer(NeoDebugSession session, EmulatedStorage storage)
        {
            this.storage = storage;
            this.session = session;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            foreach (var kvp in storage.Storage)
            {
                yield return new Variable()
                {
                    Name = new BigInteger(kvp.Value.key).ToString(),
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(session, kvp.Value)),
                    NamedVariables = 2
                };
            }
        }
    }
}
