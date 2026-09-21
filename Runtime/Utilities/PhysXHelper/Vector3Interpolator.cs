using Horcrux.Runtime.Utilities.PhysXHelper;
using UnityEngine;

namespace Horcrux.Runtime.Horcrux.Runtime.Utilities.PhysXHelper
{
    public static class Vector3Interpolator
    {
        public static Vector3 ExpDecayHalfLifePrecomputed(Vector3 from, Vector3 to,
            float invHalfLife, float dt)
        {
            return new Vector3(
                Interpolator.ExpDecayHalfLifePrecomputed(from.x, to.x, invHalfLife, dt),
                Interpolator.ExpDecayHalfLifePrecomputed(from.y, to.y, invHalfLife, dt),
                Interpolator.ExpDecayHalfLifePrecomputed(from.z, to.z, invHalfLife, dt)
            );
        }
    }
}