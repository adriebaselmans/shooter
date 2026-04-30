using System.Numerics;
using Shooter.Game;

namespace Shooter.Physics;

public readonly record struct RayHit(bool Hit, Vector3 Point, Vector3 Normal, float Distance);

/// <summary>Sphere/triangle collision and ray-vs-triangle queries against the world.</summary>
public sealed class CollisionWorld
{
    private readonly GameWorld _world;
    public CollisionWorld(GameWorld world) { _world = world; }

    /// <summary>Move a sphere by <paramref name="delta"/> with iterative push-out against world triangles.</summary>
    /// <returns>Final sphere center and last contact normal (zero if none).</returns>
    public (Vector3 position, Vector3 contactNormal) MoveSphere(Vector3 center, float radius, Vector3 delta)
    {
        var pos = center + delta;
        var contactNormal = Vector3.Zero;

        // 3 push-out passes to resolve corner cases.
        for (int pass = 0; pass < 4; pass++)
        {
            bool any = false;
            foreach (var wb in _world.Brushes)
            {
                if (!SphereAabbOverlap(pos, radius, wb.BoundsMin, wb.BoundsMax)) continue;
                foreach (var tri in wb.Triangles)
                {
                    if (TryResolveSphereTriangle(ref pos, radius, tri, out var n))
                    {
                        contactNormal = n;
                        any = true;
                    }
                }
            }
            if (!any) break;
        }
        return (pos, contactNormal);
    }

    public RayHit RayCast(Vector3 origin, Vector3 dir, float maxDist = 10000f)
    {
        float bestT = maxDist;
        Vector3 bestN = Vector3.Zero;
        bool hit = false;
        Vector3 bestPoint = origin;

        foreach (var wb in _world.Brushes)
        {
            if (!RayAabb(origin, dir, wb.BoundsMin, wb.BoundsMax, bestT)) continue;
            foreach (var tri in wb.Triangles)
            {
                if (RayTriangle(origin, dir, tri, out float t) && t < bestT && t > 0.0001f)
                {
                    bestT = t;
                    bestN = tri.Normal;
                    bestPoint = origin + dir * t;
                    hit = true;
                }
            }
        }
        return new RayHit(hit, bestPoint, bestN, bestT);
    }

    private static bool SphereAabbOverlap(Vector3 c, float r, Vector3 mn, Vector3 mx)
    {
        var clamped = Vector3.Clamp(c, mn, mx);
        return Vector3.DistanceSquared(c, clamped) <= r * r;
    }

    private static bool TryResolveSphereTriangle(ref Vector3 center, float radius, in WorldTriangle tri, out Vector3 normal)
    {
        var cp = ClosestPointOnTriangle(center, tri);
        var diff = center - cp;
        float distSq = diff.LengthSquared();
        if (distSq >= radius * radius)
        {
            normal = Vector3.Zero;
            return false;
        }
        float dist = MathF.Sqrt(distSq);
        Vector3 dir = dist > 1e-5f ? diff / dist : tri.Normal;
        center = cp + dir * (radius + 0.001f);
        normal = dir;
        return true;
    }

    private static Vector3 ClosestPointOnTriangle(Vector3 p, in WorldTriangle t)
    {
        // Standard Ericson "Real-Time Collision Detection" algorithm.
        var a = t.V0; var b = t.V1; var c = t.V2;
        var ab = b - a; var ac = c - a; var ap = p - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;

        var bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + (d1 / (d1 - d3)) * ab;

        var cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + (d2 / (d2 - d6)) * ac;

        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            return b + ((d4 - d3) / ((d4 - d3) + (d5 - d6))) * (c - b);

        float denom = 1f / (va + vb + vc);
        float v = vb * denom;
        float w = vc * denom;
        return a + ab * v + ac * w;
    }

    private static bool RayTriangle(Vector3 o, Vector3 d, in WorldTriangle tri, out float t)
    {
        // Möller-Trumbore.
        var e1 = tri.V1 - tri.V0;
        var e2 = tri.V2 - tri.V0;
        var pvec = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, pvec);
        if (MathF.Abs(det) < 1e-7f) { t = 0; return false; }
        float invDet = 1f / det;
        var tvec = o - tri.V0;
        float u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f) { t = 0; return false; }
        var qvec = Vector3.Cross(tvec, e1);
        float v = Vector3.Dot(d, qvec) * invDet;
        if (v < 0f || u + v > 1f) { t = 0; return false; }
        t = Vector3.Dot(e2, qvec) * invDet;
        return t > 0f;
    }

    private static bool RayAabb(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx, float maxT)
    {
        float tmin = 0f, tmax = maxT;
        for (int i = 0; i < 3; i++)
        {
            float od = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
            float oo = i == 0 ? o.X : i == 1 ? o.Y : o.Z;
            float a = i == 0 ? mn.X : i == 1 ? mn.Y : mn.Z;
            float b = i == 0 ? mx.X : i == 1 ? mx.Y : mx.Z;
            if (MathF.Abs(od) < 1e-7f)
            {
                if (oo < a || oo > b) return false;
            }
            else
            {
                float inv = 1f / od;
                float t1 = (a - oo) * inv;
                float t2 = (b - oo) * inv;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > tmin) tmin = t1;
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
        }
        return true;
    }
}
