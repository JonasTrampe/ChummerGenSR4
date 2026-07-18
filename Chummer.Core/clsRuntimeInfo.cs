using System;

namespace Chummer
{
	public static class RuntimeInfo
	{
		private static readonly bool _isMono = Type.GetType("Mono.Runtime") != null;

		public static bool IsMono => _isMono;

		public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;
	}
}
