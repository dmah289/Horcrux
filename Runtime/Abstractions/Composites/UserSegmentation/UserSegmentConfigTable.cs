using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Lookup and validation over the segment pool and the keyword table. Pure: the host logs, the Editor simulator displays, tests call directly.</summary>
    public static class UserSegmentConfigTable
    {
        /// <summary>Row whose id equals <paramref name="id"/>; the last one when ids repeat (Validate reports that). Null when absent.</summary>
        public static TConfig Find<TConfig>(IReadOnlyList<TConfig> configs, int id)
            where TConfig : class, IUserSegmentConfig
        {
            TConfig found = null;

            if (configs == null)
                return null;

            for (int i = 0; i < configs.Count; i++)
            {
                if(configs[i] != null && configs[i].Id == id)
                    found = configs[i];
            }
            
            return found;
        }

        /// <summary>Appends one line per invalid row to <paramref name="errors"/>. Checks the data only: the default row is the one row every project needs; other roles are the project's. Never fixes the data. True when nothing was appended.</summary>
        public static bool Validate<TConfig>(IReadOnlyList<TConfig> configs, IReadOnlyList<IUserSegmentKeywordRule> rules,
            int defaultSegmentId, List<string> errors) where TConfig : class, IUserSegmentConfig
        {
            int before = errors.Count;
            
            if(configs == null || configs.Count == 0)
                errors.Add("segment pool is empty");

            for (int i = 0; configs != null && i < configs.Count; i++)
            {
                TConfig row = configs[i];

                if (row == null)
                {
                    errors.Add($"pool row {i} is null");
                    continue;
                }

                if (row.Id < 1)
                    errors.Add($"pool row {i} has segment id {row.Id}; ids start at 1, 0 means undecided");

                for (int j = 0; j < i; j++)
                {
                    if (configs[j] != null && configs[j].Id == row.Id)
                    {
                        errors.Add($"pool rows {j} and {i} share segment id {row.Id}; the last one wins");
                        break;
                    }
                }
            }
            
            if (Find(configs, defaultSegmentId) == null)
                errors.Add($"no pool row for the default segment {defaultSegmentId}");

            if (rules == null || rules.Count == 0)
            {
                errors.Add("rules is empty");
            }

            for (int i = 0; rules != null && i < rules.Count; i++)
            {
                IUserSegmentKeywordRule rule = rules[i];

                if (rule == null)
                {
                    errors.Add($"keyword row {i} is null and will be skipped");
                    continue;
                }

                if (rule.Keywords == null || rule.Keywords.Count == 0)
                    errors.Add($"keyword row {i} (segment {rule.SegmentId}) has no keyword and never matches");
                
                for (int k = 0; rule.Keywords != null && k < rule.Keywords.Count; k++)
                {
                    if (string.IsNullOrEmpty(rule.Keywords[k]))
                        errors.Add($"keyword row {i} (segment {rule.SegmentId}) has an empty keyword at {k}; it is skipped");
                }
                
                if (Find(configs, rule.SegmentId) == null)
                    errors.Add($"keyword row {i} points at segment {rule.SegmentId}, which has no pool row");
            }

            return before == errors.Count;
        }
    }
}