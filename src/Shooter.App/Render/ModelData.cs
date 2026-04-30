using System.Numerics;
using SharpGLTF.Schema2;

namespace Shooter.Render;

/// <summary>Raw, GPU-agnostic primitive data extracted from a glTF/GLB file.</summary>
public sealed class PrimitiveData
{
    /// <summary>Interleaved pos(3) + normal(3) + uv(2) = 8 floats per vertex.</summary>
    public required float[] Vertices { get; init; }
    public required uint[] Indices { get; init; }
    /// <summary>Raw image bytes (PNG/JPEG) for the baseColor texture, or null if none.</summary>
    public byte[]? BaseColorImage { get; init; }
    /// <summary>Per-material baseColor multiplier (linear RGBA, defaults to white).</summary>
    public Vector4 BaseColorFactor { get; init; } = Vector4.One;
}

/// <summary>A loaded glTF/GLB, possibly composed of multiple textured primitives.</summary>
public sealed class ModelData
{
    public required List<PrimitiveData> Primitives { get; init; }

    /// <summary>Loads a .glb (or .gltf) and returns flat primitive data ready for upload.
    /// Returns null on failure.</summary>
    public static ModelData? TryLoad(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"[Glb] Not found: {path}"); return null; }
        try
        {
            var model = ModelRoot.Load(path);
            var prims = new List<PrimitiveData>();
            foreach (var mesh in model.LogicalMeshes)
            {
                foreach (var prim in mesh.Primitives)
                {
                    var pos = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                    if (pos is null || pos.Count == 0) continue;
                    var nrm = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var uv0 = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                    var indexAcc = prim.GetIndexAccessor();
                    if (indexAcc is null) continue;
                    var idx = indexAcc.AsIndicesArray();

                    var verts = new float[pos.Count * 8];
                    for (int i = 0; i < pos.Count; i++)
                    {
                        var p = pos[i];
                        var n = nrm is not null && i < nrm.Count ? nrm[i] : Vector3.UnitY;
                        var t = uv0 is not null && i < uv0.Count ? uv0[i] : Vector2.Zero;
                        int o = i * 8;
                        verts[o + 0] = p.X; verts[o + 1] = p.Y; verts[o + 2] = p.Z;
                        verts[o + 3] = n.X; verts[o + 4] = n.Y; verts[o + 5] = n.Z;
                        verts[o + 6] = t.X; verts[o + 7] = t.Y;
                    }

                    var indices = new uint[idx.Count];
                    for (int i = 0; i < idx.Count; i++) indices[i] = idx[i];

                    byte[]? imgBytes = null;
                    var factor = Vector4.One;
                    if (prim.Material is { } mat)
                    {
                        var bc = mat.FindChannel("BaseColor");
                        if (bc.HasValue)
                        {
                            var ch = bc.Value;
                            factor = ch.Color;
                            var img = ch.Texture?.PrimaryImage;
                            if (img is not null)
                            {
                                var content = img.Content;
                                if (!content.Content.IsEmpty)
                                    imgBytes = content.Content.ToArray();
                            }
                        }
                    }

                    prims.Add(new PrimitiveData
                    {
                        Vertices = verts,
                        Indices = indices,
                        BaseColorImage = imgBytes,
                        BaseColorFactor = factor,
                    });
                }
            }
            if (prims.Count == 0) { Console.WriteLine($"[Glb] No primitives in {path}"); return null; }
            return new ModelData { Primitives = prims };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Glb] Failed to load {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Reorients all primitives so the model's longest axis aligns with <c>-Z</c>
    /// (camera forward), the front face of that axis lands at the model origin, the two short axes
    /// are centered on zero, and the longest extent is uniformly scaled to <paramref name="targetForwardLength"/>.
    /// Anchoring the model at a world point therefore places the barrel tip at that point.</summary>
    public ModelData AlignBarrelToForward(float targetForwardLength = 0.55f, bool flipForward = false)
    {
        // Combined bbox across all primitives.
        Vector3 min = new(float.PositiveInfinity), max = new(float.NegativeInfinity);
        foreach (var pr in Primitives)
        {
            for (int i = 0; i < pr.Vertices.Length; i += 8)
            {
                var p = new Vector3(pr.Vertices[i], pr.Vertices[i + 1], pr.Vertices[i + 2]);
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
        }
        var ext = max - min;
        int fAxis = ext.X >= ext.Y && ext.X >= ext.Z ? 0 : (ext.Y >= ext.Z ? 1 : 2);
        int uAxis;
        if (fAxis == 0) uAxis = ext.Y >= ext.Z ? 1 : 2;
        else if (fAxis == 1) uAxis = ext.X >= ext.Z ? 0 : 2;
        else uAxis = ext.X >= ext.Y ? 0 : 1;

        Vector3 forward = AxisVec(fAxis) * (flipForward ? -1f : 1f);
        Vector3 up = AxisVec(uAxis);
        Vector3 right = Vector3.Cross(up, forward);
        if (right.LengthSquared() < 1e-6f) right = AxisVec(3 - fAxis - uAxis);
        right = Vector3.Normalize(right);
        up = Vector3.Normalize(Vector3.Cross(forward, right));

        // First pass: rotate, recompute bbox to derive translation+scale.
        var rotatedPrims = new List<Vector3[]>(Primitives.Count);
        Vector3 nMin = new(float.PositiveInfinity), nMax = new(float.NegativeInfinity);
        foreach (var pr in Primitives)
        {
            var rot = new Vector3[pr.Vertices.Length / 8];
            for (int i = 0, vi = 0; i < pr.Vertices.Length; i += 8, vi++)
            {
                var p = new Vector3(pr.Vertices[i], pr.Vertices[i + 1], pr.Vertices[i + 2]);
                var q = new Vector3(Vector3.Dot(right, p), Vector3.Dot(up, p), -Vector3.Dot(forward, p));
                rot[vi] = q;
                nMin = Vector3.Min(nMin, q); nMax = Vector3.Max(nMax, q);
            }
            rotatedPrims.Add(rot);
        }
        float ncx = (nMin.X + nMax.X) * 0.5f;
        float ncy = (nMin.Y + nMax.Y) * 0.5f;
        float ncz = nMin.Z;                  // most-negative z = front face
        float length = nMax.Z - nMin.Z;
        if (length < 1e-6f) return this;
        float s = targetForwardLength / length;

        // Second pass: write transformed positions + rotated normals.
        var outPrims = new List<PrimitiveData>(Primitives.Count);
        for (int p = 0; p < Primitives.Count; p++)
        {
            var src = Primitives[p];
            var rot = rotatedPrims[p];
            var dst = new float[src.Vertices.Length];
            Array.Copy(src.Vertices, dst, src.Vertices.Length);
            for (int i = 0, vi = 0; i < dst.Length; i += 8, vi++)
            {
                var q = rot[vi];
                dst[i + 0] = (q.X - ncx) * s;
                dst[i + 1] = (q.Y - ncy) * s;
                dst[i + 2] = (q.Z - ncz) * s;

                var n = new Vector3(src.Vertices[i + 3], src.Vertices[i + 4], src.Vertices[i + 5]);
                var nr = new Vector3(Vector3.Dot(right, n), Vector3.Dot(up, n), -Vector3.Dot(forward, n));
                dst[i + 3] = nr.X; dst[i + 4] = nr.Y; dst[i + 5] = nr.Z;
            }
            outPrims.Add(new PrimitiveData
            {
                Vertices = dst,
                Indices = src.Indices,
                BaseColorImage = src.BaseColorImage,
                BaseColorFactor = src.BaseColorFactor,
            });
        }
        return new ModelData { Primitives = outPrims };
    }

    private static Vector3 AxisVec(int axis) => axis switch
    {
        0 => Vector3.UnitX,
        1 => Vector3.UnitY,
        _ => Vector3.UnitZ,
    };
}
