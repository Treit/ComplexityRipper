using ComplexityRipper.Analysis;

namespace ComplexityRipper.Tests;

public class CSharpAnalyzerTests
{
    [Fact]
    public void AnalyzeFile_SimpleClass_FindsMethod()
    {
        var code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var x = 1;
            var y = 2;
            var z = x + y;
        }
    }
}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");

            Assert.Single(functions);
            var func = functions[0];
            Assert.Equal("TestMethod", func.Function);
            Assert.Equal("TestClass", func.ClassName);
            Assert.Equal("TestNamespace", func.Namespace);
            Assert.Equal("TestRepo", func.Repo);
            Assert.Equal("C#", func.Language);
            Assert.Equal(1, func.CyclomaticComplexity);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeFile_Constructor_Found()
    {
        var code = @"
class MyClass
{
    public MyClass(int x)
    {
        var y = x;
    }
}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");

            Assert.Contains(functions, f => f.Function.Contains("MyClass") && f.Function.Contains("ctor"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeFile_MultipleMethods_FindsAll()
    {
        var code = @"
class MyClass
{
    void A() { }
    void B() { }
    void C() { }
}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");

            Assert.Equal(3, functions.Count);
            Assert.Contains(functions, f => f.Function == "A");
            Assert.Contains(functions, f => f.Function == "B");
            Assert.Contains(functions, f => f.Function == "C");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeFile_NestedClass_IncludesFullClassName()
    {
        var code = @"
class Outer
{
    class Inner
    {
        void M() { }
    }
}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");

            Assert.Single(functions);
            Assert.Equal("Outer.Inner", functions[0].ClassName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeFile_ComplexMethod_CorrectMetrics()
    {
        var code = @"
class C
{
    void M(int x, string y, bool flag)
    {
        if (x > 0 && flag)
        {
            for (int i = 0; i < x; i++)
            {
                if (y != null)
                {
                    Console.WriteLine(y);
                }
            }
        }
    }
}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");

            Assert.Single(functions);
            var func = functions[0];
            Assert.Equal(3, func.ParameterCount);
            Assert.True(func.CyclomaticComplexity >= 4); // if + && + for + if
            Assert.True(func.MaxNestingDepth >= 2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeFile_LocalFunction_Found()
    {
        var code = @"
class C
{
    void M()
    {
        int LocalFunc(int x)
        {
            return x * 2;
        }

        var result = LocalFunc(5);
    }
}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");

            Assert.Equal(2, functions.Count);
            Assert.Contains(functions, f => f.Function == "M");
            Assert.Contains(functions, f => f.Function.Contains("LocalFunc"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeFile_InvalidCode_DoesNotThrow()
    {
        var code = "this is not valid C# code {{{{ }}}}";
        var analyzer = new CSharpAnalyzer();
        var tempFile = WriteTempFile(code);

        try
        {
            var functions = analyzer.AnalyzeFile(tempFile, Path.GetDirectoryName(tempFile)!, "TestRepo");
            // Should not throw, may return empty or partial results
            Assert.NotNull(functions);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, content);
        return path;
    }
}
