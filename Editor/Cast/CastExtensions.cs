using CastVector2 = CastImporter.Editor.Cast.Vector2;
using CastVector3 = CastImporter.Editor.Cast.Vector3;
using CastVector4 = CastImporter.Editor.Cast.Vector4;

namespace CastImporter.Editor.Cast
{
    public static class CastExtensions
    {
        public static UnityEngine.Vector3 ToUnityVector(this CastVector3 vec)
            => new(vec.X, vec.Y, vec.Z);

        public static UnityEngine.Vector4 ToUnityVector(this CastVector4 vec)
            => new(vec.X, vec.Y, vec.Z, vec.W);

        public static UnityEngine.Quaternion ToUnityQuaternion(this CastVector4 vec)
            => new(vec.X, vec.Y, vec.Z, vec.W);

        public static UnityEngine.Vector2 ToUnityVector(this CastVector2 vec)
            => new(vec.X, vec.Y);

    }
}