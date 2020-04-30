using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NeoDebug
{
    class VariableContainerManager
    {
        private readonly Dictionary<int, IVariableContainer> containers = new Dictionary<int, IVariableContainer>();

        public bool TryGetValue(int key, [NotNullWhen(true)] out IVariableContainer? container)
        {
            return containers.TryGetValue(key, out container);
        }

        public void Clear()
        {
            containers.Clear();
        }

        public int Add(IVariableContainer container)
        {
            var id = container.GetHashCode();
            if (containers.TryAdd(id, container))
            {
                return id;
            }

            throw new Exception();
        }

    }
}
