using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>
    /// One row of the keyword table: a campaign containing any of <see cref="Keywords"/> selects <see cref="SegmentId"/>.
    /// Row order is priority.
    /// </summary>
    public interface IUserSegmentKeywordRule
    {
        int SegmentId { get; }
        IReadOnlyList<string> Keywords { get; }
    }
}