using System;

namespace C_;

/// <summary>
/// When <c>c_.default_scope = exempt</c> is set for a file (or globally), marks this type or member as
/// C_ hot path so analyzer rules apply. Optional <see cref="Reason"/> for review (like
/// <see cref="ExemptAttribute.Reason"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class HotPathAttribute : Attribute
{
    public string Reason { get; set; } = "";
}
