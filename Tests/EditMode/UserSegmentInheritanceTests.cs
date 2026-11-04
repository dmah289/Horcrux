using Horcrux.Runtime.Abstractions.UserSegmentation;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers <see cref="UserSegmentInheritance.Pick"/>; the project's DTO InheritFrom is tested in the project.</summary>
    public sealed class UserSegmentInheritanceTests
    {
        [TestCase(-1, 5, ExpectedResult = 5)]
        [TestCase(3, 5, ExpectedResult = 3)]
        [TestCase(-1, -1, ExpectedResult = -1)]
        [TestCase(0, 5, ExpectedResult = 0)]   // 0 is a real value (e.g. free booster), never inherited
        public int Pick_ReturnsPrevOnlyForInheritValue(int target, int prev)
            => UserSegmentInheritance.Pick(target, prev);
    }
}
