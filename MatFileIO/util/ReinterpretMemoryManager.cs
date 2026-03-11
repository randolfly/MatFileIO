using System.Buffers;
using System.Runtime.InteropServices;

namespace MatFileIO.Util;

/// <summary>
/// A Safe MemoryManager, which casts Memory<T> to Memory<byte> without copying the underlying data. T must be an unmanaged type.
/// </summary>
internal sealed class ReinterpretMemoryManager<T>(Memory<T> source) : MemoryManager<byte> where T : unmanaged {
  private readonly Memory<T> _sourceMemory = source;

  public override Span<byte> GetSpan() {
    Span<T> fromSpan = _sourceMemory.Span;

    return MemoryMarshal.AsBytes(fromSpan);
  }

  public override MemoryHandle Pin(int elementIndex = 0) {
    throw new NotSupportedException("Pinning is not supported for zero-copy I/O operations.");
  }

  public override void Unpin() {
    throw new NotSupportedException("Pinning is not supported for zero-copy I/O operations.");
  }

  protected override void Dispose(bool disposing) {

  }
}