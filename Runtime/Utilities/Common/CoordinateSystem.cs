namespace Horcrux.Runtime.Implementations.Utilities.Common
{
    public enum CoordinateSystem
    {
        XY, XZ, YZ, XYZ
    }

    public static class CoordinateSystemExtensions
    {
        public static bool Is2D(this CoordinateSystem coordinateSystem)
            => coordinateSystem is CoordinateSystem.XY 
                or CoordinateSystem.XZ or CoordinateSystem.YZ;
    }
}