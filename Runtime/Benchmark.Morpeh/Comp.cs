using System.Runtime.CompilerServices;
using Scellecs.Morpeh;

namespace Benchmark.Morpeh
{

public struct Comp<T> : IComponent
{
	public T V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Comp<T>(in T value) =>
		new() { V = value };
}

}
