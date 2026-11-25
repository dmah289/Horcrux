using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horcrux.Runtime.Implementations.RemoteConfigSystem;
using Horcrux.Runtime.Utilities.Common;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Line and cell splitting shared by the CSV importers, plus the remote config importer read through it.</summary>
    public sealed class DelimitedTextTests
    {
        [Test]
        public void ReadLines_SkipsBlankLines_KeepsSourceLineNumbers()
        {
            var lines = DelimitedText.ReadLines("a,b\n\n   \r\nc,d\r\n");

            CollectionAssert.AreEqual(new[] { 1, 4 }, lines.Select(line => line.Number));
            CollectionAssert.AreEqual(new[] { "c", "d" }, lines[1].Cells);
        }

        [Test]
        public void ReadLines_TrimsCells_KeepsTrailingEmptyCells()
        {
            var line = DelimitedText.ReadLines("  1 , Car ,,\n")[0];

            CollectionAssert.AreEqual(new[] { "1", "Car", "", "" }, line.Cells);
            Assert.AreEqual("1 , Car ,,", line.Text);
        }

        [Test]
        public void ReadLines_LineWithTab_SplitsOnTabOnly()
        {
            var line = DelimitedText.ReadLines("2\tBubble Tea, hot\tCar\r\n")[0];

            CollectionAssert.AreEqual(new[] { "2", "Bubble Tea, hot", "Car" }, line.Cells);
        }

        [Test]
        public void ReadLines_SeparatorChosenPerLine()
        {
            var lines = DelimitedText.ReadLines("a\tb\nc,d\n");

            CollectionAssert.AreEqual(new[] { "a", "b" }, lines[0].Cells);
            CollectionAssert.AreEqual(new[] { "c", "d" }, lines[1].Cells);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("\n \r\n\t\n")]
        public void ReadLines_NothingButWhitespace_ReturnsNoLine(string text)
        {
            Assert.IsEmpty(DelimitedText.ReadLines(text));
        }

        [Serializable]
        public class Row
        {
            public string name;
            public int amount;
        }

        [Test]
        public void RemoteConfigCsv_HeaderReordersColumns_SkipsCommentsAndBlankLines()
        {
            bool ok = ParseRemoteConfigCsv(typeof(List<Row>), "# note, with comma\n\nAmount,name\n3, a \n// x\n5,b\r\n",
                out object parsed, out string report);

            Assert.IsTrue(ok, report);
            var rows = (List<Row>)parsed;
            CollectionAssert.AreEqual(new[] { "a", "b" }, rows.Select(row => row.name));
            CollectionAssert.AreEqual(new[] { 3, 5 }, rows.Select(row => row.amount));
            Assert.AreEqual("2 rows", report);
        }

        [Test]
        public void RemoteConfigCsv_WrongColumnCount_ReportsSourceLine()
        {
            bool ok = ParseRemoteConfigCsv(typeof(List<Row>), "\nname,amount\na,1,2\n", out _, out string report);

            Assert.IsFalse(ok);
            Assert.AreEqual("line 3: expected 2 columns (name,amount), got 3", report);
        }

        [Test]
        public void RemoteConfigCsv_NotApplicableRow_IsSkippedWithItsLine()
        {
            bool ok = ParseRemoteConfigCsv(typeof(List<Row>), "name,amount\na,-\nb,4\n", out object parsed, out string report);

            Assert.IsTrue(ok, report);
            Assert.AreEqual(1, ((List<Row>)parsed).Count);
            Assert.AreEqual("1 rows, 1 skipped as not applicable (line 2)", report);
        }

        [Test]
        public void RemoteConfigCsv_SingleValue()
        {
            bool ok = ParseRemoteConfigCsv(typeof(int), " 42 \n", out object parsed, out string report);

            Assert.IsTrue(ok, report);
            Assert.AreEqual(42, parsed);
        }

        // Sheets copies cells tab-separated; before the shared reader this split on commas and failed on column count.
        [Test]
        public void RemoteConfigCsv_PastedFromSheet_ReadsTabSeparatedCells()
        {
            bool ok = ParseRemoteConfigCsv(typeof(List<Row>), "name\tamount\nBubble Tea, hot\t3\n", out object parsed,
                out string report);

            Assert.IsTrue(ok, report);
            Assert.AreEqual("Bubble Tea, hot", ((List<Row>)parsed)[0].name);
        }

        // The importer is internal to the runtime assembly; tests reach it the way RemoteConfig's Inspector button does.
        private static bool ParseRemoteConfigCsv(Type type, string csv, out object parsed, out string report)
        {
            var importer = typeof(RemoteConfig<int>).Assembly.GetType(
                "Horcrux.Runtime.Implementations.RemoteConfigSystem.RemoteConfigCsvImporter", true);
            var tryParse = importer.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static);
            var arguments = new object[] { type, csv, null, null };
            bool ok = (bool)tryParse.Invoke(null, arguments);
            parsed = arguments[2];
            report = (string)arguments[3];
            return ok;
        }
    }
}
