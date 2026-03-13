using MatFileIO.Ext;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace MatFileIO;

/// <summary>
/// Classic MAT-file (v5) writer.
/// <para>
/// Currently only supports writing 1D <see cref="double"/> arrays (stored as an Nx1 MATLAB column vector).
/// </para>
/// </summary>
public sealed class MatWriter : IDisposable, IAsyncDisposable {
  private readonly FileStream _fileStream;
  private readonly MatWriteOptions _defaultOptions;

  /// <param name="fileName">Output mat file name (typically ends with <c>.mat</c>).</param>
  public MatWriter(string fileName, MatWriteOptions? options = null) {
    _fileStream = new FileStream(
      fileName,
      FileMode.Create,
      FileAccess.Write,
      FileShare.None,
      bufferSize: 1024 * 64,
      useAsync: true
    );

    _defaultOptions = options ?? new MatWriteOptions();
    WriteFileHeader(_fileStream);
  }

  /// <summary>
  /// Writes a 1D array as an Nx1 MATLAB column vector.
  /// <para>
  /// When compression is enabled, this writes an <c>miCOMPRESSED</c> element (zlib stream)
  /// containing a normal <c>miMATRIX</c> element.
  /// </para>
  /// </summary>
  public void WriteArray(string varName, ReadOnlySpan<double> data, MatWriteOptions? options = null) {
    var opt = options ?? _defaultOptions;
    ValidateVarName(varName);

    if (opt.Compress) {
      WriteCompressedMatrixElement(_fileStream, varName, data, opt.CompressionLevel);
    } else {
      WriteMatrixHeader(_fileStream, varName, data.Length);
      _fileStream.Write(MemoryMarshal.AsBytes(data));
    }
  }

  public void WriteArray(string varName, double[] data, MatWriteOptions? options = null) {
    WriteArray(varName, data.AsSpan(), options);
  }

  /// <summary>
  /// Async wrapper for <see cref="WriteArray(string,ReadOnlySpan{double},MatWriteOptions?)"/>.
  /// <para>
  /// NOTE: MAT v5 compression is CPU-bound; when compression is enabled we offload work to the thread pool.
  /// </para>
  /// </summary>
  public async Task WriteArrayAsync(string varName, Memory<double> data, MatWriteOptions? options = null, CancellationToken cancellationToken = default) {
    var opt = options ?? _defaultOptions;
    ValidateVarName(varName);

    if (opt.Compress) {
      await Task.Run(() => {
        cancellationToken.ThrowIfCancellationRequested();
        WriteCompressedMatrixElement(_fileStream, varName, data.Span, opt.CompressionLevel);
      }, cancellationToken);
    } else {
      WriteMatrixHeader(_fileStream, varName, data.Length);
      await _fileStream.WriteAsync(data.AsBytes(), cancellationToken);
    }
  }

  public Task WriteArrayAsync(string varName, double[] data, MatWriteOptions? options = null, CancellationToken cancellationToken = default) {
    return WriteArrayAsync(varName, data.AsMemory(), options, cancellationToken);
  }

  public void Dispose() {
    _fileStream.Dispose();
  }

  public ValueTask DisposeAsync() {
    return _fileStream.DisposeAsync();
  }

  private static void ValidateVarName(string varName) {
    if (string.IsNullOrWhiteSpace(varName)) {
      throw new ArgumentException("Variable name must not be empty.", nameof(varName));
    }
  }

  /// <summary>
  /// Write MAT-file header (128 bytes). Called in the constructor.
  /// </summary>
  private static void WriteFileHeader(Stream stream) {
    Span<byte> header = stackalloc byte[128];

    var description = Encoding.ASCII.GetBytes(
      "MATLAB 5.0 MAT-file, Platform: .NET, Created on: " +
      DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")
    );
    description.AsSpan(0, Math.Min(description.Length, 116)).CopyTo(header);

    // Version (2 bytes) + Endian indicator (2 bytes)
    header[124] = 0x00;
    header[125] = 0x01;
    header[126] = (byte)'I'; // Little Endian indicator: "IM"
    header[127] = (byte)'M';

    stream.Write(header);
  }

  private static void WriteCompressedMatrixElement(FileStream fileStream, string varName, ReadOnlySpan<double> data, CompressionLevel compressionLevel) {
    // miCOMPRESSED data element: tag then a zlib stream containing a complete data element (miMATRIX).
    // We stream-compress directly into the file, then seek back to fill in the compressed byte count.
    Span<byte> tag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(tag[..4], 15); // miCOMPRESSED = 15
    BinaryPrimitives.WriteInt32LittleEndian(tag[4..8], 0); // placeholder

    long tagPos = fileStream.Position;
    fileStream.Write(tag);

    long compressedStart = fileStream.Position;
    // MAT v5 "miCOMPRESSED" stores a zlib stream (RFC1950) containing a complete data element.
    using (var zlib = new ZLibStream(fileStream, compressionLevel, leaveOpen: true)) {
      WriteMatrixHeader(zlib, varName, data.Length);
      zlib.Write(MemoryMarshal.AsBytes(data));
    }
    long compressedEnd = fileStream.Position;

    long compressedBytes = compressedEnd - compressedStart;
    if (compressedBytes > int.MaxValue) throw new IOException("Compressed payload is too large for MAT v5 miCOMPRESSED element.");

    int pad = (int)(compressedBytes % 8 == 0 ? 0 : 8 - (compressedBytes % 8));
    int compressedBytesWithPadding = checked((int)compressedBytes + pad);

    // Patch the tag with the compressed size.
    fileStream.Position = tagPos;
    BinaryPrimitives.WriteInt32LittleEndian(tag[..4], 15);
    // Note: Some readers (including MathNet.Numerics.Data.Matlab) expect the size field of
    // miCOMPRESSED to include the 8-byte alignment padding.
    BinaryPrimitives.WriteInt32LittleEndian(tag[4..8], compressedBytesWithPadding);
    fileStream.Write(tag);

    // Return to end and pad to 8-byte alignment.
    fileStream.Position = compressedEnd;
    WritePadding(fileStream, (int)compressedBytes);
  }

  private static void WritePadding(Stream stream, int byteCount) {
    int pad = byteCount % 8 == 0 ? 0 : 8 - (byteCount % 8);
    if (pad == 0) return;

    Span<byte> zeros = stackalloc byte[8];
    stream.Write(zeros[..pad]);
  }

  private static void WriteMatrixHeader(Stream stream, string varName, int length) {
    // --- Begin Data Element ---
    int totalBytes = CalMatrixBytes(varName, length, 1);

    Span<byte> tag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(tag[..4], 14); // miMATRIX = 14
    BinaryPrimitives.WriteInt32LittleEndian(tag[4..8], totalBytes);
    stream.Write(tag);

    // Array Flags
    Span<byte> flagsTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(flagsTag[..4], 6); // miUINT32
    BinaryPrimitives.WriteInt32LittleEndian(flagsTag[4..8], 8); // 8 bytes
    stream.Write(flagsTag);

    Span<byte> flagsData = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(flagsData[..4], 6); // mxDOUBLE_CLASS
    BinaryPrimitives.WriteInt32LittleEndian(flagsData[4..8], 0); // flags
    stream.Write(flagsData);

    // Dimensions
    Span<byte> dimsTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(dimsTag[..4], 5); // miINT32
    BinaryPrimitives.WriteInt32LittleEndian(dimsTag[4..8], 8); // 2 dims * 4 bytes
    stream.Write(dimsTag);

    Span<byte> dims = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(dims[..4], length);
    BinaryPrimitives.WriteInt32LittleEndian(dims[4..8], 1);
    stream.Write(dims);

    // Name
    byte[] nameBytes = Encoding.ASCII.GetBytes(varName);
    int nameLen = nameBytes.Length;

    Span<byte> nameTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(nameTag[..4], 1); // miINT8
    BinaryPrimitives.WriteInt32LittleEndian(nameTag[4..8], nameLen);
    stream.Write(nameTag);
    stream.Write(nameBytes, 0, nameLen);

    // name padding
    int namePad = nameLen % 8 == 0 ? 0 : 8 - (nameLen % 8);
    for (int i = 0; i < namePad; i++) stream.WriteByte(0);

    // Data Tag
    Span<byte> dataTag = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(dataTag[..4], 9); // miDOUBLE
    BinaryPrimitives.WriteInt32LittleEndian(dataTag[4..8], length * 8);
    stream.Write(dataTag);
  }

  private static int CalMatrixBytes(string name, int rows, int cols) {
    // 1. Array Flags tag+data (8+8)
    // 2. Dimensions tag+data (8+8)
    // 3. Name tag+data (8+len+pad)
    // 4. Data tag+data (8+len), pad=0 since double is 8 bytes
    int nameLen = Encoding.ASCII.GetByteCount(name);
    int namePad = nameLen % 8 == 0 ? 0 : 8 - (nameLen % 8);

    int dataLen = checked(rows * cols * 8);
    int total = checked((8 + 8) + (8 + 8) + 8 + nameLen + namePad + 8 + dataLen);
    return total;
  }
}
