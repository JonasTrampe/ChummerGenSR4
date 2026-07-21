using System;

namespace Chummer.Core
{
    public static class RuntimeInfo
    {
        public static bool IsMono { get; } = Type.GetType("Mono.Runtime") != null;

        public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;
    }
}