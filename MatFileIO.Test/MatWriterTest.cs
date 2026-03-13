using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;

namespace MatFileIO.Test;

public class MatWriterTest {
  private static double[] CreateStressData() =>
    Enumerable.Range(1, 1_000_000).Select(i => (double)i).ToArray();

  [Fact]
  public void MatWriter_Single_Variable_Write_Should_Pass() {
    const string testFileName = "test_single_variable.mat";
    const string testVarName = "myData";
    double[] data = [1.0d, 2.0d, 3.0d];
    try {
      {
        using var mw = new MatWriter(testFileName, new MatWriteOptions { Compress = true });
        mw.WriteArray(testVarName, data);
      }
      // use `MathNet.Numerics.Data.Matlab` to read the file back and verify the content
      Assert.True(File.Exists(testFileName), $"File {testFileName} should exist after writing.");

      Matrix<double> m = MatlabReader.Read<double>(testFileName, testVarName);
      Assert.Equal(data.Length, m.RowCount);
      Assert.Equal(1, m.ColumnCount);
      for (int i = 0; i < data.Length; i++) {
        Assert.Equal(data[i], m[i, 0]);
      }
    } finally {
      if (File.Exists(testFileName)) File.Delete(testFileName);
    }
  }

  [Fact]
  public void MatWriter_Single_Variable_Without_Compression_Write_Should_Pass() {
    const string testFileName = "test_single_variable.mat";
    const string testVarName = "myData";
    double[] data = [1.0d, 2.0d, 3.0d];
    try {
      {
        using var mw = new MatWriter(testFileName, new MatWriteOptions { Compress = false });
        mw.WriteArray(testVarName, data);
      }
      // use `MathNet.Numerics.Data.Matlab` to read the file back and verify the content
      Assert.True(File.Exists(testFileName), $"File {testFileName} should exist after writing.");

      Matrix<double> m = MatlabReader.Read<double>(testFileName, testVarName);
      Assert.Equal(data.Length, m.RowCount);
      Assert.Equal(1, m.ColumnCount);
      for (int i = 0; i < data.Length; i++) {
        Assert.Equal(data[i], m[i, 0]);
      }
    } finally {
      if (File.Exists(testFileName)) File.Delete(testFileName);
    }
  }


  [Fact]
  public void MatWriter_Single_Large_Variable_Write_Should_Pass() {
    const string testFileName = "test_single_variable.mat";
    const string testVarName = "myData";
    double[] data = CreateStressData();
    try {
      {
        using var mw = new MatWriter(testFileName, new MatWriteOptions { Compress = true });
        mw.WriteArray(testVarName, data);
      }
      // use `MathNet.Numerics.Data.Matlab` to read the file back and verify the content
      Assert.True(File.Exists(testFileName), $"File {testFileName} should exist after writing.");

      Matrix<double> m = MatlabReader.Read<double>(testFileName, testVarName);
      Assert.Equal(data.Length, m.RowCount);
      Assert.Equal(1, m.ColumnCount);
      for (int i = 0; i < data.Length; i++) {
        Assert.Equal(data[i], m[i, 0]);
      }
    } finally {
      if (File.Exists(testFileName)) File.Delete(testFileName);
    }
  }

  [Fact]
  public async Task MatWriter_Single_Variable_WriteAsync_Should_Pass() {
    const string testFileName = "test_single_variable_async.mat";
    const string testVarName = "myData";
    double[] data = [1.0d, 2.0d, 3.0d];
    try {
      {
        await using var mw = new MatWriter(testFileName,
          new MatWriteOptions { Compress = true });
        await mw.WriteArrayAsync(testVarName, data,
          cancellationToken: TestContext.Current.CancellationToken);
      }
      // use `MathNet.Numerics.Data.Matlab` to read the file back and verify the content
      Assert.True(File.Exists(testFileName), $"File {testFileName} should exist after writing.");

      Matrix<double> m = MatlabReader.Read<double>(testFileName, testVarName);
      Assert.Equal(data.Length, m.RowCount);
      Assert.Equal(1, m.ColumnCount);
      for (int i = 0; i < data.Length; i++) {
        Assert.Equal(data[i], m[i, 0]);
      }
    } finally {
      if (File.Exists(testFileName)) File.Delete(testFileName);
    }
  }

  [Fact]
  public async Task MatWriter_Single_Variable_WithOut_Compression_WriteAsync_Should_Pass() {
    const string testFileName = "test_single_variable_async.mat";
    const string testVarName = "myData";
    double[] data = [1.0d, 2.0d, 3.0d];
    try {
      {
        await using var mw = new MatWriter(testFileName,
          new MatWriteOptions { Compress = false });
        await mw.WriteArrayAsync(testVarName, data,
          cancellationToken: TestContext.Current.CancellationToken);
      }
      // use `MathNet.Numerics.Data.Matlab` to read the file back and verify the content
      Assert.True(File.Exists(testFileName), $"File {testFileName} should exist after writing.");

      Matrix<double> m = MatlabReader.Read<double>(testFileName, testVarName);
      Assert.Equal(data.Length, m.RowCount);
      Assert.Equal(1, m.ColumnCount);
      for (int i = 0; i < data.Length; i++) {
        Assert.Equal(data[i], m[i, 0]);
      }
    } finally {
      if (File.Exists(testFileName)) File.Delete(testFileName);
    }
  }


  [Fact]
  public async Task MatWriter_Single_Large_Variable_WriteAsync_Should_Pass() {
    const string testFileName = "test_single_variable_async1.mat";
    const string testVarName = "myData";
    double[] data = CreateStressData();
    try {
      {
        await using var mw = new MatWriter(testFileName,
          new MatWriteOptions { Compress = true });
        await mw.WriteArrayAsync(testVarName, data,
          cancellationToken: TestContext.Current.CancellationToken);
      }
      // use `MathNet.Numerics.Data.Matlab` to read the file back and verify the content
      Assert.True(File.Exists(testFileName), $"File {testFileName} should exist after writing.");

      Matrix<double> m = MatlabReader.Read<double>(testFileName, testVarName);
      Assert.Equal(data.Length, m.RowCount);
      Assert.Equal(1, m.ColumnCount);
      for (int i = 0; i < data.Length; i++) {
        Assert.Equal(data[i], m[i, 0]);
      }
    } finally {
      if (File.Exists(testFileName)) File.Delete(testFileName);
    }
  }
}
