namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>
    /// One row of the segment pool: the project DTO carries the fields, this only names the row.
    /// Ids start at 1; 0 means undecided.
    /// </summary>
    public interface IUserSegmentConfig
    {
        int Id { get; }
    }

    /// <summary>
    /// The project DTO implements the merge itself.
    /// Takes the value of <paramref name="prev"/>, everything else is copied.
    /// </summary>
    public interface IUserSegmentConfig<TSelf> : IUserSegmentConfig where TSelf : class, IUserSegmentConfig<TSelf>
    {
        TSelf InheritFrom(TSelf prev);
    }
}