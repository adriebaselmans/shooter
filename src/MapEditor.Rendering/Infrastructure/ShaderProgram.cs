using Silk.NET.OpenGL;
using System.Numerics;
using System.Reflection;

namespace MapEditor.Rendering.Infrastructure;

/// <summary>
/// Compiles a GLSL vertex + fragment shader pair and exposes typed uniform setters.
/// GLSL source is loaded from embedded resources.
/// </summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private bool _disposed;

    private ShaderProgram(GL gl, uint handle)
    {
        _gl = gl;
        _handle = handle;
    }

    /// <summary>Loads and compiles a shader from embedded GLSL resources.</summary>
    /// <param name="vertResourceName">Embedded resource name ending in .vert.glsl</param>
    /// <param name="fragResourceName">Embedded resource name ending in .frag.glsl</param>
    public static ShaderProgram FromEmbeddedResources(GL gl, string vertResourceName, string fragResourceName)
    {
        var vert = LoadEmbeddedResource(vertResourceName);
        var frag = LoadEmbeddedResource(fragResourceName);
        return Compile(gl, vert, frag);
    }

    public static ShaderProgram Compile(GL gl, string vertSource, string fragSource)
    {
        uint vert = CompileShader(gl, ShaderType.VertexShader,   vertSource);
        uint frag = CompileShader(gl, ShaderType.FragmentShader, fragSource);

        uint program = gl.CreateProgram();
        gl.AttachShader(program, vert);
        gl.AttachShader(program, frag);
        gl.LinkProgram(program);

        gl.GetProgram(program, GLEnum.LinkStatus, out int status);
        if (status == 0)
        {
            string log = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"Shader link error: {log}");
        }

        gl.DeleteShader(vert);
        gl.DeleteShader(frag);

        return new ShaderProgram(gl, program);
    }

    public void Use() => _gl.UseProgram(_handle);

    public void SetUniform(string name, int value)
    {
        int loc = _gl.GetUniformLocation(_handle, name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, float value)
    {
        int loc = _gl.GetUniformLocation(_handle, name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, bool value) => SetUniform(name, value ? 1 : 0);

    public void SetUniform(string name, Vector3 value)
    {
        int loc = _gl.GetUniformLocation(_handle, name);
        if (loc >= 0) _gl.Uniform3(loc, value.X, value.Y, value.Z);
    }

    public void SetUniform(string name, Vector4 value)
    {
        int loc = _gl.GetUniformLocation(_handle, name);
        if (loc >= 0) _gl.Uniform4(loc, value.X, value.Y, value.Z, value.W);
    }

    public unsafe void SetUniform(string name, Matrix4x4 value)
    {
        int loc = _gl.GetUniformLocation(_handle, name);
        if (loc >= 0)
            _gl.UniformMatrix4(loc, 1, false, (float*)&value);
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Shader compile error ({type}): {log}");
        }

        return shader;
    }

    private static string LoadEmbeddedResource(string name)
    {
        var assembly = typeof(ShaderProgram).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Embedded shader resource not found: {name}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Could not open embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _gl.DeleteProgram(_handle);
        _disposed = true;
    }
}
