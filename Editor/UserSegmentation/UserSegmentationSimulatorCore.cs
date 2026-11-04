using System;
using System.Collections.Generic;
using System.Reflection;
using Horcrux.Runtime.Abstractions.UserSegmentation;

namespace Horcrux.Editor.UserSegmentation
{
    /// <summary>
    /// Reflection over the project's pool row type and the checks only an Editor tool can make: they need the columns or the
    /// purchased role, which the runtime guard deliberately ignores. Pure: appends lines, never logs.
    /// </summary>
    public static class UserSegmentationSimulatorCore
    {
        private const int IdProbeValue = 271828;

        private static readonly MethodInfo ValidateDefinition = typeof(UserSegmentConfigTable).GetMethod(nameof(UserSegmentConfigTable.Validate));

        /// <summary>Public instance int fields: the columns a -1 may inherit, plus the id column (<see cref="FindIdField"/>).</summary>
        public static FieldInfo[] IntFieldsOf(Type configType)
        {
            FieldInfo[] all = configType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var ints = new List<FieldInfo>(all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].FieldType == typeof(int))
                    ints.Add(all[i]);
            }

            return ints.ToArray();
        }

        /// <summary>The int field that <see cref="IUserSegmentConfig.Id"/> reads; null when Id is not backed by a public int field.</summary>
        public static FieldInfo FindIdField(Type configType, FieldInfo[] intFields)
        {
            for (int i = 0; i < intFields.Length; i++)
            {
                var probe = (IUserSegmentConfig)Activator.CreateInstance(configType, true);
                intFields[i].SetValue(probe, IdProbeValue);

                if (probe.Id == IdProbeValue)
                    return intFields[i];
            }

            return null;
        }

        /// <summary>Calls the DTO's own <c>InheritFrom</c>: the tool shows the merge the host computes, never its own copy.</summary>
        public static IUserSegmentConfig Inherit(Type configType, IUserSegmentConfig target, IUserSegmentConfig prev)
        {
            MethodInfo inheritFrom = typeof(IUserSegmentConfig<>).MakeGenericType(configType).GetMethod("InheritFrom");
            return (IUserSegmentConfig)inheritFrom.Invoke(target, new object[] { prev });
        }

        /// <summary>
        /// Runs the host's boot guard (<see cref="UserSegmentConfigTable.Validate{TConfig}"/>), then the three Editor-only checks:
        /// -1 outside the purchased row, a column InheritFrom forgets, a keyword row pointing at the purchased segment.
        /// </summary>
        /// <param name="configs">The host's pool; its runtime type must be a list of <paramref name="configType"/>.</param>
        public static void Check(Type configType, IReadOnlyList<IUserSegmentConfig> configs, IReadOnlyList<IUserSegmentKeywordRule> rules,
            int defaultSegmentId, int purchasedSegmentId, List<string> errors)
        {
            ValidateDefinition.MakeGenericMethod(configType).Invoke(null, new object[] { configs, rules, defaultSegmentId, errors });

            FieldInfo[] intFields = IntFieldsOf(configType);
            FieldInfo idField = FindIdField(configType, intFields);

            CheckInheritOutsidePurchased(configs, intFields, idField, purchasedSegmentId, errors);
            CheckInheritFromCoversColumns(configType, configs, intFields, idField, defaultSegmentId, errors);
            CheckKeywordsAvoidPurchased(rules, purchasedSegmentId, errors);
        }

        private static void CheckInheritOutsidePurchased(IReadOnlyList<IUserSegmentConfig> configs, FieldInfo[] intFields,
            FieldInfo idField, int purchasedSegmentId, List<string> errors)
        {
            for (int i = 0; configs != null && i < configs.Count; i++)
            {
                IUserSegmentConfig row = configs[i];

                if (row == null || row.Id == purchasedSegmentId)
                    continue;

                for (int f = 0; f < intFields.Length; f++)
                {
                    if (intFields[f] != idField && (int)intFields[f].GetValue(row) == UserSegmentInheritance.InheritValue)
                        errors.Add($"pool row {i} (segment {row.Id}) has {UserSegmentInheritance.InheritValue} in {intFields[f].Name}; only the purchased segment {purchasedSegmentId} inherits");
                }
            }
        }

        // A target with every column at -1 merged onto the default row: a column still at -1 while the default row has a value is one InheritFrom skips.
        private static void CheckInheritFromCoversColumns(Type configType, IReadOnlyList<IUserSegmentConfig> configs, FieldInfo[] intFields,
            FieldInfo idField, int defaultSegmentId, List<string> errors)
        {
            IUserSegmentConfig prev = UserSegmentConfigTable.Find(configs, defaultSegmentId);

            if (prev == null)
                return;

            var target = (IUserSegmentConfig)Activator.CreateInstance(configType, true);

            for (int f = 0; f < intFields.Length; f++)
            {
                if (intFields[f] != idField)
                    intFields[f].SetValue(target, UserSegmentInheritance.InheritValue);
            }

            IUserSegmentConfig merged = Inherit(configType, target, prev);

            for (int f = 0; f < intFields.Length; f++)
            {
                if (intFields[f] == idField)
                    continue;

                if ((int)intFields[f].GetValue(merged) == UserSegmentInheritance.InheritValue
                    && (int)intFields[f].GetValue(prev) != UserSegmentInheritance.InheritValue)
                    errors.Add($"{configType.Name}.InheritFrom does not take {intFields[f].Name} from prev; add its Pick line");
            }
        }

        private static void CheckKeywordsAvoidPurchased(IReadOnlyList<IUserSegmentKeywordRule> rules, int purchasedSegmentId, List<string> errors)
        {
            for (int i = 0; rules != null && i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].SegmentId == purchasedSegmentId)
                    errors.Add($"keyword row {i} points at the purchased segment {purchasedSegmentId}; a campaign matching it falls to the default segment silently, purchase is the only way in");
            }
        }
    }
}
