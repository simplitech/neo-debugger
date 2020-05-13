using System;
using System.Collections.Generic;
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
            Debug.Assert(token.Value<string>("type") == "trace-point");

            var state = Enum.Parse<VMState>(token.Value<string>("vmstate"));
            var contexts = token["contexts"]?.Select(ParseContext).ToImmutableArray()
                ?? ImmutableArray<TracePoint.Context>.Empty;
            var storages = token["storages"].SelectMany(ParseStorage).ToImmutableDictionary(t => t.key, t => t.item)
                ?? ImmutableDictionary<StorageKey, StorageItem>.Empty;

            return new TracePoint(state, contexts, storages);

            static IEnumerable<(StorageKey key, StorageItem item)> ParseStorage(JToken token)
            {
                var scriptHash = UInt160.Parse(token.Value<string>("script-hash"));
                foreach (var item in token["items"] ?? Enumerable.Empty<JToken>())
                {
                    var key = item["key"]?.ToObject<byte[]>().ToImmutableArray() ?? ImmutableArray<byte>.Empty;
                    var value = item["value"]?.ToObject<byte[]>().ToImmutableArray() ?? ImmutableArray<byte>.Empty;
                    var constant = item.Value<bool>("constant");

                    yield return (new StorageKey(scriptHash, key.AsMemory()), new StorageItem(value.AsMemory(), constant));
                }
            }

            static TracePoint.Context ParseContext(JToken token)
            {
                var scriptHash = UInt160.Parse(token.Value<string>("script-hash"));
                var ip = token.Value<int>("instruction-pointer");
                // TODO: read eval/alt stacks

                return new TracePoint.Context(scriptHash, ip, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
            }

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
