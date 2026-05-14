namespace Shooter;

internal static class MaterialMapConventions
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private static readonly string[] NormalSuffixes = ["_normal", "_nor", "_n", "-normal"];
    private static readonly string[] RoughnessSuffixes = ["_roughness", "_rough", "_r", "-roughness"];
    private static readonly string[] MetallicSuffixes = ["_metallic", "_metal", "_m", "-metallic"];
    private static readonly string[] AoSuffixes = ["_ao", "_occlusion", "_ambientocclusion", "-ao"];
    private static readonly string[] HeightSuffixes = ["_height", "_disp", "_displacement", "-height"];

    public static string? ResolveNormal(string? baseColorPath) => ResolveCompanion(baseColorPath, NormalSuffixes);
    public static string? ResolveRoughness(string? baseColorPath) => ResolveCompanion(baseColorPath, RoughnessSuffixes);
    public static string? ResolveMetallic(string? baseColorPath) => ResolveCompanion(baseColorPath, MetallicSuffixes);
    public static string? ResolveAo(string? baseColorPath) => ResolveCompanion(baseColorPath, AoSuffixes);
    public static string? ResolveHeight(string? baseColorPath) => ResolveCompanion(baseColorPath, HeightSuffixes);

    private static string? ResolveCompanion(string? baseColorPath, IReadOnlyList<string> suffixes)
    {
        if (string.IsNullOrWhiteSpace(baseColorPath) || !Path.IsPathRooted(baseColorPath) || !File.Exists(baseColorPath))
            return null;

        string dir = Path.GetDirectoryName(baseColorPath)!;
        string stem = Path.GetFileNameWithoutExtension(baseColorPath);
        string ext = Path.GetExtension(baseColorPath);

        foreach (string suffix in suffixes)
        {
            string sameExt = Path.Combine(dir, stem + suffix + ext);
            if (File.Exists(sameExt)) return sameExt;
            foreach (string candidateExt in ImageExtensions)
            {
                string path = Path.Combine(dir, stem + suffix + candidateExt);
                if (File.Exists(path)) return path;
            }
        }

        return null;
    }
}
