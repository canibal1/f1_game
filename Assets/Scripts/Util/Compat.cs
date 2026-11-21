using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Small helpers that hide API renames between Unity 2022 LTS and Unity 6
    /// (Rigidbody.velocity -> linearVelocity, PhysicMaterial -> PhysicsMaterial, ...).
    /// </summary>
    public static class Compat
    {
        public static Vector3 GetVelocity(this Rigidbody rb)
        {
#if UNITY_2023_3_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(this Rigidbody rb, Vector3 v)
        {
#if UNITY_2023_3_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(this Rigidbody rb, float linear, float angular)
        {
#if UNITY_2023_3_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        /// <summary>Gives a collider a slippery, non-bouncy surface (cars sliding along walls).</summary>
        public static void SetSlippery(Collider c, float friction = 0.15f, float bounce = 0.05f)
        {
#if UNITY_2023_3_OR_NEWER
            var m = new PhysicsMaterial("CarBody")
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = bounce,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
#else
            var m = new PhysicMaterial("CarBody")
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = bounce,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounceCombine = PhysicMaterialCombine.Minimum
            };
#endif
            c.sharedMaterial = m;
        }

        public static T FindAny<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
