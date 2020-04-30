using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoFx.Models;
using System;
using System.Collections.Generic;

namespace NeoDebug.VariableContainers
{
    class EmulatedStorageContainer : IVariableContainer
    {
        internal class KvpContainer : IVariableContainer
        {
            private readonly IVariableContainerSession session;
            private readonly string hashCode;
            private readonly ReadOnlyMemory<byte> key;
            private readonly ReadOnlyMemory<byte> value;
            private readonly bool constant;

            public KvpContainer(IVariableContainerSession session, int hashCode, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, bool constant)
            {
                this.session = session;
                this.hashCode = hashCode.ToString("x");
                this.key = key;
                this.value = value;
                this.constant = constant;
            }

            public IEnumerable<Variable> GetVariables()
            {
                yield return new Variable()
                {
                    Name = "key",
                    Value = key.Span.ToHexString(),
                    EvaluateName = $"$storage[{hashCode}].key",
                };

                yield return new Variable()
                {
                    Name = "value",
                    Value = value.Span.ToHexString(),
                    EvaluateName = $"$storage[{hashCode}].value",
                };

                yield return new Variable()
                {
                    Name = "constant",
                    Value = constant.ToString(),
                    Type = "Boolean"
                };
            }
        }

        private readonly IVariableContainerSession session;
        private readonly Func<IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)>> enumerateStorage;

        public EmulatedStorageContainer(IVariableContainerSession session, Func<IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)>> enumerateStorage)
        {
            this.session = session;
            this.enumerateStorage = enumerateStorage;
        }

        public IEnumerable<Variable> GetVariables()
        {
            foreach (var (key, item) in enumerateStorage())
            {
                var keyHashCode = key.Span.GetSequenceHashCode();
                yield return new Variable()
                {
                    Name = keyHashCode.ToString("x"),
                    Value = string.Empty,
                    VariablesReference = session.AddVariableContainer(
                        new KvpContainer(session, keyHashCode, key, item.Value, item.IsConstant)),
                    NamedVariables = 3
                };
            }
        }
    }
}
