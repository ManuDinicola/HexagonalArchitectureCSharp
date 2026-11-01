using System.Runtime.InteropServices;

namespace MyApp.Benchmarks;

/// <summary>A small readonly struct payload.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Point(int X, int Y);
