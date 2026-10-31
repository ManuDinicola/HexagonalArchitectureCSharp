using System.Runtime.InteropServices;

namespace MyApp.Benchmarks;

/// <summary>A 128-byte value type, to see what a large struct costs inside a result.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct BigStruct(long A, long B, long C, long D, long E, long F, long G, long H, long I, long J, long K, long L, long M, long N, long O, long P);
