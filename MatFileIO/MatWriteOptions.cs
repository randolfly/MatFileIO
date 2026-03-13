using System.IO.Compression;

namespace MatFileIO;

/// <summary>
/// Options for writing classic (MAT v5) MAT-files.
/// </summary>
public sealed class MatWriteOptions {
  /// <summary>
  /// When true, variables are written as <c>miCOMPRESSED</c> data elements (zlib stream).
  /// </summary>
  public bool Compress { get; init; } = true;

  /// <summary>
  /// Compression level used for <c>miCOMPRESSED</c>. (MAT v5 uses zlib.)
  /// </summary>
  public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;
}

