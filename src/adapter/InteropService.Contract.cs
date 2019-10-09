﻿using Neo.VM;
using Neo.VM.Types;
using NeoDebug.Adapter.ModelAdapters;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public static void RegisterContract(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Contract.Create", Contract_Create, 0);
            register("Neo.Contract.Migrate", Contract_Migrate, 0);
            register("Neo.Contract.Destroy", Contract_Destroy, 1);
            register("Neo.Contract.GetScript", Contract_GetScript, 1);
            register("Neo.Contract.IsPayable", Contract_IsPayable, 1);
            register("Neo.Contract.GetStorageContext", Contract_GetStorageContext, 1);

            register("System.Contract.Destroy", Contract_Destroy, 1);
            register("System.Contract.GetStorageContext", Contract_GetStorageContext, 1);

            register("AntShares.Contract.Create", Contract_Create, 0);
            register("AntShares.Contract.Migrate", Contract_Migrate, 0);
            register("AntShares.Contract.Destroy", Contract_Destroy, 1);
            register("AntShares.Contract.GetScript", Contract_GetScript, 1);
            register("AntShares.Contract.GetStorageContext", Contract_GetStorageContext, 1);
        }

        private static bool Contract_IsPayable(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<DeployedContractAdapter>(adapter => adapter.IsPayable(engine));
        }

        private static bool Contract_GetScript(ExecutionEngine engine)
        {
            return engine.TryAdapterOperation<DeployedContractAdapter>(adapter => adapter.GetScript(engine));
        }

        private static bool Contract_GetStorageContext(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private static bool Contract_Destroy(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private static bool Contract_Migrate(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }

        private static bool Contract_Create(ExecutionEngine engine)
        {
            throw new NotImplementedException();
        }
    }
}
