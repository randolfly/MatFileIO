using MatFileIO.Ext;

using System.Runtime.InteropServices;

namespace MatFileIO.Test;

public class MemoryExtensionTests {
  [Fact]
  public void AsBytes_EmptyMemory_ReturnsEmpty() {
    Memory<int> source = Memory<int>.Empty;

    Memory<byte> bytes = source.AsBytes();

    Assert.True(bytes.IsEmpty);
    Assert.Equal(0, bytes.Length);
  }

  [Fact]
  public void AsBytes_MatchesByteSpan() {
    var source = new double[] { 1.0, 2.0, 3.0 }.AsMemory();

    Memory<byte> bytes = source.AsBytes();
    var spanBytes = MemoryMarshal.AsBytes(source.Span);

    Assert.Equal(spanBytes.Length, bytes.Length);
    for (int i = 0; i < spanBytes.Length; i++) {
      Assert.Equal(spanBytes[i], bytes.Span[i]);
    }
  }

  [Fact]
  public void AsBytes_SliceMapsOnlySliceAndIsZeroCopy() {
    int[] backing = [11, 22, 33];
    Memory<int> slice = backing.AsMemory(1, 1);

    Memory<byte> bytes = slice.AsBytes();
    Assert.Equal(sizeof(int), bytes.Length);

    bytes.Span.Fill(0);

    Assert.Equal(11, backing[0]);
    Assert.Equal(0, backing[1]);
    Assert.Equal(33, backing[2]);

    backing[1] = 123456789;
    byte[] expected = BitConverter.GetBytes(backing[1]);
    Assert.Equal(expected, bytes.ToArray());
  }
}