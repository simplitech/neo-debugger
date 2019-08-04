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
    internal class EmulatedStorage
    {
        private class StorageContext
        {
            public byte[] ScriptHash;

            public int GetHashCode(ReadOnlySpan<byte> key)
            {
                var keyHash = Crypto.GetHashCode(key);
                var contextHash = Crypto.GetHashCode<byte>(ScriptHash.AsSpan());
                return HashCode.Combine(keyHash, contextHash);
            }
        }

        private readonly Dictionary<int, (byte[] key, byte[] value)> storage =
            new Dictionary<int, (byte[] key, byte[] value)>();

        public IReadOnlyDictionary<int, (byte[] key, byte[] value)> Storage => storage;

        static byte[] ConvertString(string value)
        {
            if (value.StartsWith("0x") 
                && BigInteger.TryParse(value.AsSpan().Slice(2), out var bigInt))
            {
                return bigInt.ToByteArray();
            }

            return Encoding.UTF8.GetBytes(value);
        }

        public void Populate(byte[] scriptHash, IEnumerable<(string key, string value)> items)
        {
            var storageContext = new StorageContext()
            {
                ScriptHash = scriptHash
            };

            foreach (var (key, value) in items)
            {
                Put(storageContext,
                    ConvertString(key),
                    ConvertString(value));
            }
        }

        static bool TryGetStorageContext(RandomAccessStack<StackItem> evalStack, out StorageContext context)
        {
            if (evalStack.Pop() is VM.Types.InteropInterface interop)
            {
                context = interop.GetInterface<StorageContext>();
                return true;
            }

            context = default;
            return false;
        }

        public bool Delete (ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var storageHash = storageContext.GetHashCode(key);

                storage.Remove(storageHash);
                return true;
            }

            return false;
        }

        private void Put(StorageContext storageContext, byte[] key, byte[] value)
        {
            var storageHash = storageContext.GetHashCode(key);
            storage.Add(storageHash, (key, value));
        }

        public bool Put(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var value = evalStack.Pop().GetByteArray();

                Put(storageContext, key, value);
                return true;
            }

            return false;
        }

        public bool Get(ExecutionEngine engine)
        {
            var evalStack = engine.CurrentContext.EvaluationStack;
            if (TryGetStorageContext(evalStack, out var storageContext))
            {
                var key = evalStack.Pop().GetByteArray();
                var storageHash = storageContext.GetHashCode(key);

                if (storage.TryGetValue(storageHash, out var kvp))
                {
                    evalStack.Push(kvp.value);
                }
                else
                {
                    evalStack.Push(new byte[0]);
                }

                return true;
            }

            return false;
        }

        public bool GetContext(ExecutionEngine engine)
        {
            var storageContext = new StorageContext()
            {
                ScriptHash = engine.CurrentContext.ScriptHash,
            };

            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(storageContext));
            return true;
        }
    }
}
