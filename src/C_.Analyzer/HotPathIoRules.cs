using System;
using Microsoft.CodeAnalysis;

namespace C_.Analyzer;

/// <summary>Detects BCL entry points that perform console, network, filesystem, pipe, or serial I/O.</summary>
internal static class HotPathIoRules
{
    internal static bool IsDisallowedIo(IMethodSymbol method)
    {
        var type = method.ContainingType;
        if (type is null)
            return false;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        var name = type.Name;

        if (ns.StartsWith("System.Net.", StringComparison.Ordinal))
            return true;

        if (ns.StartsWith("System.IO.Pipes", StringComparison.Ordinal))
            return true;

        if (ns.StartsWith("System.IO.Ports", StringComparison.Ordinal))
            return true;

        if (ns.StartsWith("System.IO.IsolatedStorage", StringComparison.Ordinal))
            return true;

        if (ns.StartsWith("System.IO.MemoryMappedFiles", StringComparison.Ordinal))
            return true;

        if (name == "Console" && ns == "System")
            return IsConsoleIoMethod(method);

        if (ns == "System.IO")
            return name switch
            {
                "File" or "Directory" or "FileStream" or "FileInfo" or "DirectoryInfo" or "StreamReader" or
                    "StreamWriter" or "BinaryReader" or "BinaryWriter" or "MemoryMappedFile" or "RandomAccess" or
                    "FileSystemWatcher" or "DriveInfo" => true,
                _ => false,
            };

        return false;
    }

    private static bool IsConsoleIoMethod(IMethodSymbol method) =>
        method.Name switch
        {
            "Write" or "WriteLine" or "Read" or "ReadLine" or "ReadKey" or "OpenStandardOutput" or
                "OpenStandardError" or "OpenStandardInput" => true,
            _ => false,
        };
}
