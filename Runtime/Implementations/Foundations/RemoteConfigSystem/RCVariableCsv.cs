#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.RemoteConfigSystem
{
    /// <summary>CSV text to and from an RCVariable value: scalars, one flat object, or a list of them.</summary>
    /// <remarks>Editor authoring only. JSON stays the wire format with Remote Config.</remarks>
    internal static class RCVariableCsv
    {
        private const string CommentPrefix = "#";
        private const string AltCommentPrefix = "//";

        /// <summary>Reads CSV into a value of <paramref name="type"/>.</summary>
        /// <param name="report">Row counts when it succeeds, the reason when it fails.</param>
        /// <returns>False leaves <paramref name="parsed"/> untouched and nothing should be written.</returns>
        public static bool TryParse(Type type, string csv, out object parsed, out string report)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(csv))
            {
                report = "nothing pasted";
                return false;
            }

            if (!TryDescribe(type, out Type rowType, out bool isList, out FieldInfo[] columns, out report))
                return false;

            var dataLines = new List<(int line, string text)>();
            string[] rawLines = csv.Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                string line = rawLines[i].Trim();
                if (line.Length == 0 || line.StartsWith(CommentPrefix) || line.StartsWith(AltCommentPrefix))
                    continue;
                dataLines.Add((i + 1, line));
            }

            if (dataLines.Count == 0)
            {
                report = "no data rows";
                return false;
            }

            if (columns != null && TryReadHeader(dataLines[0].text, columns, out FieldInfo[] ordered))
            {
                columns = ordered;
                dataLines.RemoveAt(0);
            }

            if (dataLines.Count == 0)
            {
                report = "header only, no data rows";
                return false;
            }

            if (!isList && dataLines.Count > 1)
            {
                report = $"{Describe(type)} holds one row, got {dataLines.Count}";
                return false;
            }

            var rows = new List<object>();
            var skippedLines = new List<int>();
            foreach ((int line, string text) in dataLines)
            {
                if (!TryParseRow(text, rowType, columns, out object row, out bool notApplicable, out string rowError))
                {
                    report = $"line {line}: {rowError}";
                    return false;
                }

                if (notApplicable)
                    skippedLines.Add(line);
                else
                    rows.Add(row);
            }

            if (!isList)
            {
                if (rows.Count == 0)
                {
                    report = $"line {skippedLines[0]}: the only row reads as not applicable";
                    return false;
                }
                parsed = rows[0];
                report = "1 row";
                return true;
            }

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
            foreach (object row in rows)
                list.Add(row);
            parsed = list;
            report = skippedLines.Count == 0
                ? $"{rows.Count} rows"
                : $"{rows.Count} rows, {skippedLines.Count} skipped as not applicable (line {string.Join(", ", skippedLines)})";
            return true;
        }

        /// <summary>Writes a value back out as CSV, header first.</summary>
        /// <param name="error">Why the type has no CSV shape; empty when it succeeds.</param>
        public static bool TryFormat(Type type, object value, out string csv, out string error)
        {
            csv = null;
            if (!TryDescribe(type, out _, out bool isList, out FieldInfo[] columns, out error))
                return false;

            var builder = new StringBuilder();
            if (columns != null)
            {
                for (int i = 0; i < columns.Length; i++)
                    builder.Append(i == 0 ? string.Empty : ",").Append(columns[i].Name);
                builder.Append('\n');
            }

            if (!isList)
            {
                AppendRow(builder, value, columns);
                csv = builder.ToString();
                return true;
            }

            if (value is IEnumerable rows)
            {
                foreach (object row in rows)
                    AppendRow(builder, row, columns);
            }
            csv = builder.ToString();
            return true;
        }

        /// <summary>Splits the type into "one row" plus its columns, or explains why it has no CSV shape.</summary>
        private static bool TryDescribe(Type type, out Type rowType, out bool isList, out FieldInfo[] columns,
            out string error)
        {
            rowType = type;
            isList = false;
            columns = null;
            error = null;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                rowType = type.GetGenericArguments()[0];
                isList = true;
            }

            if (IsCellType(rowType))
                return true;

            if (!rowType.IsValueType && rowType.GetConstructor(Type.EmptyTypes) == null)
            {
                error = $"{rowType.Name} has no parameterless constructor to build rows with";
                return false;
            }

            var fields = new List<FieldInfo>();
            foreach (FieldInfo field in rowType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!IsSerializedByUnity(field))
                    continue;
                if (!IsCellType(field.FieldType))
                {
                    error = $"{rowType.Name}.{field.Name} is {Describe(field.FieldType)} — a CSV row holds flat "
                            + "cells only, so this type needs the JSON import";
                    return false;
                }
                fields.Add(field);
            }

            if (fields.Count == 0)
            {
                error = $"{rowType.Name} has no serialized field to map columns onto";
                return false;
            }

            columns = fields.ToArray();
            return true;
        }

        /// <summary>Reorders columns to match a header line, or reports that the line carries data instead.</summary>
        private static bool TryReadHeader(string line, FieldInfo[] columns, out FieldInfo[] ordered)
        {
            ordered = null;
            string[] cells = line.Split(',');
            if (cells.Length != columns.Length)
                return false;

            var byHeader = new FieldInfo[cells.Length];
            var taken = new bool[columns.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                string cell = cells[i].Trim();
                for (int c = 0; c < columns.Length; c++)
                {
                    if (taken[c] || !string.Equals(columns[c].Name, cell, StringComparison.OrdinalIgnoreCase))
                        continue;
                    byHeader[i] = columns[c];
                    taken[c] = true;
                    break;
                }
                if (byHeader[i] == null)
                    return false;
            }

            ordered = byHeader;
            return true;
        }

        private static bool TryParseRow(string line, Type rowType, FieldInfo[] columns, out object row,
            out bool notApplicable, out string error)
        {
            row = null;
            notApplicable = false;
            error = null;
            string[] cells = line.Split(',');

            if (columns == null)
            {
                if (cells.Length != 1)
                {
                    error = $"expected a single {rowType.Name} value, got {cells.Length} cells";
                    return false;
                }
                return TryReadCell(cells[0], rowType, out row, out notApplicable, out error);
            }

            if (cells.Length != columns.Length)
            {
                error = $"expected {columns.Length} columns ({string.Join(",", Names(columns))}), got {cells.Length}";
                return false;
            }

            object instance = Activator.CreateInstance(rowType);
            for (int i = 0; i < columns.Length; i++)
            {
                if (!TryReadCell(cells[i], columns[i].FieldType, out object cellValue, out bool cellNotApplicable,
                        out string cellError))
                {
                    error = $"column {columns[i].Name}: {cellError}";
                    return false;
                }

                if (cellNotApplicable)
                {
                    notApplicable = true;
                    return true;
                }
                columns[i].SetValue(instance, cellValue);
            }

            row = instance;
            return true;
        }

        /// <summary>
        /// Reads one cell. A blank, "-" or "None" cell the target type cannot hold means the row does
        /// not apply — the caller drops it instead of inventing a value.
        /// </summary>
        private static bool TryReadCell(string raw, Type type, out object value, out bool notApplicable,
            out string error)
        {
            value = null;
            notApplicable = false;
            error = null;
            string cell = raw.Trim();

            if (type == typeof(string))
            {
                value = cell;
                return true;
            }

            if (type.IsEnum)
            {
                if (Enum.TryParse(type, cell, true, out object parsed) && IsRepresentable(type, parsed))
                {
                    value = parsed;
                    return true;
                }
                if (IsNotApplicableToken(cell))
                {
                    notApplicable = true;
                    return true;
                }
                error = $"\"{cell}\" is not a {type.Name} — use {string.Join(", ", Enum.GetNames(type))}, "
                        + "or None to drop the row";
                return false;
            }

            if (IsNotApplicableToken(cell))
            {
                notApplicable = true;
                return true;
            }

            var culture = CultureInfo.InvariantCulture;
            bool ok;
            if (type == typeof(int)) { ok = int.TryParse(cell, NumberStyles.Integer, culture, out int v); value = v; }
            else if (type == typeof(long)) { ok = long.TryParse(cell, NumberStyles.Integer, culture, out long v); value = v; }
            else if (type == typeof(float)) { ok = float.TryParse(cell, NumberStyles.Float, culture, out float v); value = v; }
            else if (type == typeof(double)) { ok = double.TryParse(cell, NumberStyles.Float, culture, out double v); value = v; }
            else if (type == typeof(bool)) { ok = bool.TryParse(cell, out bool v); value = v; }
            else { error = $"{type.Name} has no CSV cell form"; return false; }

            if (!ok)
                error = $"\"{cell}\" is not a {type.Name}";
            return ok;
        }

        private static void AppendRow(StringBuilder builder, object row, FieldInfo[] columns)
        {
            if (columns == null)
            {
                builder.Append(Cell(row)).Append('\n');
                return;
            }

            for (int i = 0; i < columns.Length; i++)
                builder.Append(i == 0 ? string.Empty : ",").Append(Cell(columns[i].GetValue(row)));
            builder.Append('\n');
        }

        private static string Cell(object value)
            => value switch
            {
                null => string.Empty,
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };

        private static IEnumerable<string> Names(FieldInfo[] columns)
        {
            foreach (FieldInfo column in columns)
                yield return column.Name;
        }

        /// <summary>Enum.TryParse takes any number; only a declared one is a real value.</summary>
        private static bool IsRepresentable(Type enumType, object value)
            => Attribute.IsDefined(enumType, typeof(FlagsAttribute)) || Enum.IsDefined(enumType, value);

        private static bool IsNotApplicableToken(string cell)
            => cell.Length == 0 || cell == "-" || string.Equals(cell, "None", StringComparison.OrdinalIgnoreCase);

        /// <summary>Type name a reader recognises: List<DebtCurvePoint>, not List`1.</summary>
        private static string Describe(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            Type[] arguments = type.GetGenericArguments();
            var names = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
                names[i] = Describe(arguments[i]);
            return type.Name.Substring(0, type.Name.IndexOf('`')) + "<" + string.Join(", ", names) + ">";
        }

        private static bool IsCellType(Type type)
            => type.IsEnum || type == typeof(string) || type == typeof(int) || type == typeof(long)
               || type == typeof(float) || type == typeof(double) || type == typeof(bool);

        private static bool IsSerializedByUnity(FieldInfo field)
        {
            if (field.IsNotSerialized || field.IsStatic)
                return false;
            return field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
        }
    }
}
#endif
