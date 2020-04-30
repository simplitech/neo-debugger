using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Neo.VM;
using NeoFx;
using NeoFx.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoDebug.Models
{
    static class TraceDebugJsonParser
    {
        static TraceResult ParseTraceResult(JToken token)
        {
            Debug.Assert(token.Value<string>("type") == "results");

            var state = Enum.Parse<VMState>(token.Value<string>("vmstate"));
            var gasConsumed = Fixed8.Parse(token.Value<string>("gas-consumed"));
            var results = token["results"].Select(t => t.Value<string>()).ToImmutableArray();

            return new TraceResult(state, gasConsumed, results);
        }

        static TracePoint ParseTracePoint(JToken token)
        {
            static (ImmutableArray<byte> key, StorageItem item) ParseStorage(JToken token)
            {
                var key = token["key"]?.ToObject<byte[]>().ToImmutableArray() ?? ImmutableArray<byte>.Empty;
                var value = token["value"]?.ToObject<byte[]>().ToImmutableArray() ?? ImmutableArray<byte>.Empty;
                var constant = token.Value<bool>("constant");

                var item = new StorageItem(value.AsMemory(), constant);

                return (key, item);
            }

            static TracePoint.StackFrame ParseStackFrame(JToken token)
            {
                var scriptHash = UInt160.Parse(token.Value<string>("script-hash"));
                var scriptHashArray= new byte[UInt160.Size];
                scriptHash.Write(scriptHashArray);
                var scriptHashImmutableArray = Unsafe.As<byte[], ImmutableArray<byte>>(ref scriptHashArray);

                var index = token.Value<int>("index");
                var ip = token.Value<int>("instruction-pointer");
                var storages = token["storages"]?.Select(ParseStorage).ToImmutableArray()
                    ?? ImmutableArray<(ImmutableArray<byte>, StorageItem)>.Empty;

                return new TracePoint.StackFrame(index, scriptHashImmutableArray, ip, storages);
            }

            Debug.Assert(token.Value<string>("type") == "trace-point");

            var state = Enum.Parse<VMState>(token.Value<string>("vmstate"));
            var stackFrames = token["stack-frames"]?.Select(ParseStackFrame).ToImmutableArray()
                ?? ImmutableArray<TracePoint.StackFrame>.Empty;
            
            return new TracePoint(state, stackFrames);
        }

        // TODO: convert this to async method. JArray.LoadAsync failing for some reason
        public static (ImmutableArray<TracePoint> points, TraceResult results) Load(string traceDebugFile)
        {
            
            JArray LoadJArray()
            {
                using var stream = File.OpenRead(traceDebugFile);
                using var streamReader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                return JArray.Load(jsonReader);
            } 

            var traceItems = LoadJArray();
            var tracePoints = traceItems.Take(traceItems.Count - 1).Select(ParseTracePoint).ToImmutableArray();
            var traceResults = traceItems.TakeLast(1).Select(ParseTraceResult).Single();

            return (tracePoints, traceResults);
        }
    }
}
