using MatFileIO.Util;

namespace MatFileIO.Ext;

public static class MemoryExtension {
  /// <summary>
  /// Cast Memory<T> to Memory<byte> without copying the underlying data. T must be an unmanaged type.
  /// </summary>
  public static Memory<byte> AsBytes<T>(this Memory<T> source) where T : unmanaged {
    if (source.IsEmpty) return Memory<byte>.Empty;

    var manager = new ReinterpretMemoryManager<T>(source);
    return manager.Memory;
  }
}
