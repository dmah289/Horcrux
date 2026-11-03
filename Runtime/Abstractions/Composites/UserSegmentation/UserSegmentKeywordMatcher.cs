using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    public static class UserSegmentKeywordMatcher
    {
        /// <summary>First row with a keyword the campaign contains (ordinal, ignore case) wins; <paramref name="keyword"/> is that row's first matching keyword.</summary>
        public static bool TryMatch(string campaign, IReadOnlyList<IUserSegmentKeywordRule> rules, 
            out int segmentId, out string keyword, out bool isAmbiguous)
        {
            segmentId = 0;
            keyword = null;
            isAmbiguous = false;
            
            if(string.IsNullOrEmpty(campaign) || rules == null)
                return false;

            bool found = false;

            for (int i = 0; i < rules.Count; i++)
            {
                IUserSegmentKeywordRule rule = rules[i];

                if (rule == null || !TryMatchRule(campaign, rule.Keywords, out string matchedKeyword))
                    continue;

                if (!found)
                {
                    found = true;
                    segmentId = rule.SegmentId;
                    keyword = matchedKeyword;
                    continue;
                }

                if (rule.SegmentId != segmentId)
                {
                    isAmbiguous = true;
                    Debug.LogError($"[UserSegmentKeywordMatcher] Ambiguous segment ID: Campaign {campaign} matches the same segment ID {segmentId} and {rule.SegmentId}");
                    break;
                }
            }

            return found;
        }

        /// <summary>Empty keywords are skipped; a row without keywords never matches.</summary>
        private static bool TryMatchRule(string campaign, IReadOnlyList<string> keywords, out string matchedKeyword)
        {
            matchedKeyword = null;
            
            for (int i = 0; keywords != null && i < keywords.Count; i++)
            {
                string candidate = keywords[i];

                if (string.IsNullOrEmpty(candidate) ||
                    campaign.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                
                matchedKeyword = candidate;
                return true;
            }

            return false;
        }
    }
}