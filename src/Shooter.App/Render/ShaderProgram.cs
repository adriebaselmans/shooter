using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Compiles a vertex+fragment GLSL pair into an OpenGL program.</summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }

    public ShaderProgram(GL gl, string vertexSrc, string fragmentSrc)
    {
        _gl = gl;
        uint vs = Compile(ShaderType.VertexShader, vertexSrc);
        uint fs = Compile(ShaderType.FragmentShader, fragmentSrc);
        Handle = _gl.CreateProgram();
        _gl.AttachShader(Handle, vs);
        _gl.AttachShader(Handle, fs);
        _gl.LinkProgram(Handle);
        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(Handle);
            throw new InvalidOperationException("Program link failed: " + log);
        }
        _gl.DetachShader(Handle, vs); _gl.DetachShader(Handle, fs);
        _gl.DeleteShader(vs); _gl.DeleteShader(fs);
    }

    private uint Compile(ShaderType type, string src)
    {
        uint id = _gl.CreateShader(type);
        _gl.ShaderSource(id, src);
        _gl.CompileShader(id);
        _gl.GetShader(id, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            string log = _gl.GetShaderInfoLog(id);
            throw new InvalidOperationException($"{type} compile failed: {log}");
        }
        return id;
    }

    public void Use() => _gl.UseProgram(Handle);

    public int U(string name) => _gl.GetUniformLocation(Handle, name);

    public void Dispose() => _gl.DeleteProgram(Handle);
}
