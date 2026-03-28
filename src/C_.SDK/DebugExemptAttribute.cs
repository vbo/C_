using System;

namespace C_;

/// <summary>
/// Like <see cref="ExemptAttribute"/>, but only while the compilation defines the <c>DEBUG</c> preprocessor symbol.
/// In Release (typical for production/CI), code is analyzed as non-exempt.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DebugExemptAttribute : Attribute
{
    public string Reason { get; set; } = "";
}
