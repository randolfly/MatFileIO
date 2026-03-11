using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace MatFileIO;
/// <summary>
/// MAT-file writer class. Usage:
/// <code>
/// using var writer = new MatWriter("data.mat");
/// writer.WriteArray("myData", myData, myData.Length);
/// </code>
/// <strong>Currently ONLY SUPPORT 1D-double ARRAY</strong>
/// </summary>
/// <param name="fileName">written mat file name, should end with .mat</param> 
public class MatWriter : IDisposable, IAsyncDisposable {
  private readonly FileStream fileStream;
  private readonly SemaphoreSlim semaphoreSlim = new(1, 1);

  public MatWriter(string fileName) {
    fileStream = new(fileName, FileMode.Create, FileAccess.Write);
    WriteHeader();
  }

  /// <summary>
  /// Write Mat File Header. Called In the Constructor. The header is 128 bytes long, and should be followed by a 8-byte tag for the first data element.
  /// </summary>
  private void WriteHeader() {
    // Write MAT-file header (128 bytes)
    Span<byte> header = stackalloc byte[128];
    // 1. Description (max 116 bytes)
    var description = Encoding.ASCII.GetBytes(
        "MATLAB 5.0 MAT-file, Platform: .NET, Created on: "
        + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")
    );
    description.AsSpan(0, Math.Min(description.Length, 116)).CopyTo(header);
    // 2. Subsystem data offset (8 bytes, usually zero)
    // 3. Version (2 bytes) + Endian indicator (2 bytes)
    header[124] = 0x00; // version
    header[125] = 0x01; // version
    header[126] = (byte)'I'; // Little Endian indicator: 'IM'
    header[127] = (byte)'M';
    fileStream.Write(header);
  }


  public void WriteArray<T>(string varName, ReadOnlySpan<T> data) where T : unmanaged {
    WriteArrayMetaInfo<T>(varName, data.Length);

    // Write double data as bytes, no array copy
    var byteSpan = MemoryMarshal.Cast<T, byte>(data);
    fileStream.Write(byteSpan);
  }


  public async Task WriteArrayAsync<T>(string varName, ReadOnlyMemory<T> data) where T : unmanaged {
    await semaphoreSlim.WaitAsync();
    try {
      await Task.Run(() => WriteArray(varName, data.Span));
    } finally {
      semaphoreSlim.Release();
    }
  }

  private void WriteArrayMetaInfo<T>(string varName, int length) where T : unmanaged {
    // --- Begin Data Element ---
    var totalBytes = CalMatrixBytes(varName, typeof(T), length, 1);
    Span<byte> tag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(tag[..4], 14); // miMATRIX = 14
    BinaryPrimitives.WriteInt32LittleEndian(tag[4..8], totalBytes);
    fileStream.Write(tag);

    // Array Flags
    Span<byte> flagsTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(flagsTag[..4], 6); // miUINT32
    BinaryPrimitives.WriteInt32LittleEndian(flagsTag[4..8], 8); // 8 bytes
    fileStream.Write(flagsTag);

    Span<byte> flagsData = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(flagsData[..4], 6); // mxDOUBLE_CLASS
    BinaryPrimitives.WriteInt32LittleEndian(flagsData[4..8], 0); // flags
    fileStream.Write(flagsData);

    // Dimensions
    Span<byte> dimsTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(dimsTag[..4], 5); // miINT32
    BinaryPrimitives.WriteInt32LittleEndian(dimsTag[4..8], 8); // 2 dims * 4 bytes
    fileStream.Write(dimsTag);

    Span<byte> dims = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(dims[..4], length);
    BinaryPrimitives.WriteInt32LittleEndian(dims[4..8], 1);
    fileStream.Write(dims);

    // Name
    var nameBytes = Encoding.ASCII.GetBytes(varName);
    var nameLen = nameBytes.Length;
    Span<byte> nameTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(nameTag[..4], 1); // miINT8
    BinaryPrimitives.WriteInt32LittleEndian(nameTag[4..8], nameLen);
    fileStream.Write(nameTag);
    fileStream.Write(nameBytes, 0, nameLen);

    if (nameLen % 8 != 0) {
      for (var i = 0; i < 8 - nameLen % 8; i++) {
        fileStream.WriteByte(0); // padding
      }
    }

    // Data Tag
    Span<byte> dataTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(dataTag[..4], 9); // miDOUBLE
    BinaryPrimitives.WriteInt32LittleEndian(dataTag[4..8], length * 8);
    fileStream.Write(dataTag);
  }

  private static int CalMatrixBytes(string name, Type type, int rows, int cols) {
    // 1. Array Flags tag+data (8+8)
    // 2. Dimensions tag+data (8+8)
    // 3. Name tag+data (8+len+pad)
    // 4. Data tag+data (8+len), pad=0 since double is 8 bytes
    var nameLen = name.Length;
    var namePad = nameLen % 8 == 0 ? 0 : 8 - nameLen % 8;

    // TODO: use matching to get data type size in bytes, currently only support double (8 bytes)
    var dataLen = rows * cols * 8;

    var total = (8 + 8) + (8 + 8) + 8 + nameLen + namePad + 8 + dataLen;
    return total;
  }

  public void Dispose() {
    fileStream.Dispose();
  }

  public async ValueTask DisposeAsync() {
    await fileStream.DisposeAsync();
  }
}
