namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>The one rule of inheritance. The project DTO applies it per column inside <see cref="IUserSegmentConfig{TSelf}.InheritFrom"/>.</summary>
    public static class UserSegmentInheritance
    {
        /// <summary>An int cell holding this value on the purchased row takes the previous segment's value.</summary>
        public const int InheritValue = -1;

        /// <summary><paramref name="prev"/> when <paramref name="target"/> equals <see cref="InheritValue"/>, otherwise <paramref name="target"/>.</summary>
        public static int Pick(int target, int prev) => target == InheritValue ? prev : target;
    }
}