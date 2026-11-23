using System;
using System.Collections.Generic;
using Horcrux.Editor.WireContract;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Horcrux.Tests
{
    [Serializable]
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class WireStrictRow
    {
        public int x;
    }

    [Serializable]
    public sealed class WireLooseRow
    {
        public int x;
    }

    [Serializable]
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class WireStrictRoot
    {
        public int a;
        public List<WireStrictRow> rows = new();
    }

    /// <summary>Requires its own keys but not the keys of its rows.</summary>
    [Serializable]
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class WireMixedRoot
    {
        public int a;
        public List<WireLooseRow> rows = new();
    }

    [Serializable]
    public sealed class WireLooseRoot
    {
        public int a;
        public int b;
    }

    /// <summary>Setter doubles the value, so a serialize → parse → serialize round trip does not return the same JSON.</summary>
    public sealed class WireLossy
    {
        private int _v;
        public int v { get => _v; set => _v = value * 2; }
    }

    public class JsonWireContractTests
    {
        [Test]
        public void EveryKeyRequired_NoViolation()
            => CollectionAssert.IsEmpty(JsonWireContract.RequiredKeyViolations<WireStrictRoot>("{\"a\":1,\"rows\":[{\"x\":1},{\"x\":2}]}"));

        [Test]
        public void RowKeyNotRequired_ReportsRowPath()
            => CollectionAssert.AreEqual(new[] { "rows[].x: parses without it" },
                JsonWireContract.RequiredKeyViolations<WireMixedRoot>("{\"a\":1,\"rows\":[{\"x\":1},{\"x\":2}]}"));

        [Test]
        public void RootArray_PathsStartWithBrackets()
        {
            CollectionAssert.IsEmpty(JsonWireContract.RequiredKeyViolations<List<WireStrictRow>>("[{\"x\":1}]"));
            CollectionAssert.AreEqual(new[] { "[].x: parses without it" },
                JsonWireContract.RequiredKeyViolations<List<WireLooseRow>>("[{\"x\":1}]"));
        }

        // A key the sample lacks is never removed, so the check would pass without testing it.
        [Test]
        public void SampleLacksKey_ReportsUntestedKey()
            => CollectionAssert.AreEqual(new[] { "b: the type writes it, the sample lacks it", "a: parses without it" },
                JsonWireContract.RequiredKeyViolations<WireLooseRoot>("{\"a\":1}"));

        [Test]
        public void SampleNotParsable_Throws()
            => Assert.Throws<JsonSerializationException>(() => JsonWireContract.RequiredKeyViolations<WireStrictRoot>("{\"a\":1}"));

        [Test]
        public void KeyPathsOf_NestedListMergedUnderBrackets()
        {
            var value = new WireStrictRoot { rows = { new WireStrictRow(), new WireStrictRow() } };

            CollectionAssert.AreEqual(new[] { "a", "rows", "rows[].x" }, JsonWireContract.KeyPathsOf(value));
        }

        [Test]
        public void RoundTrip_StableValue_Null()
            => Assert.IsNull(JsonWireContract.RoundTripDiff(new WireStrictRoot { a = 3, rows = { new WireStrictRow { x = 1 } } }));

        [Test]
        public void RoundTrip_LossyValue_ShowsBothJson()
            => Assert.AreEqual("{\"v\":2} -> {\"v\":4}", JsonWireContract.RoundTripDiff(new WireLossy { v = 1 }));
    }
}
