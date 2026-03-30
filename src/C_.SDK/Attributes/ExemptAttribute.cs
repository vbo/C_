using System;

namespace C_;

/// <summary>Marks a type or member as exempt from C_ hot-path analyzer rules (allocations, I/O, reflection, etc.).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ExemptAttribute : Attribute
{
    public string Reason { get; set; } = "";
}
