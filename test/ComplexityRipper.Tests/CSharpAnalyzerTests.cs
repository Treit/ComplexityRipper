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

    [Fact]
    public void AnalyzeRepos_TrailingBackslash_RepoNameNotBlank()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_repo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
        File.WriteAllText(Path.Combine(tempDir, "Test.cs"), @"
class C
{
    void M() { }
}");

        try
        {
            var analyzer = new CSharpAnalyzer();
            var rootWithTrailingSlash = tempDir + Path.DirectorySeparatorChar;
            var result = analyzer.AnalyzeRepos(rootWithTrailingSlash);

            Assert.Single(result.Repos);
            Assert.NotEmpty(result.Repos[0].Name);
            Assert.Single(result.Functions);
            Assert.NotEmpty(result.Functions[0].Repo);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AnalyzeRepos_OrganizationalFolder_DescendsIntoSubfolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"test_org_{Guid.NewGuid():N}");
        var orgFolder = Path.Combine(root, "my_project");
        var repoA = Path.Combine(orgFolder, "RepoA");
        var repoB = Path.Combine(orgFolder, "RepoB");

        Directory.CreateDirectory(Path.Combine(repoA, ".git"));
        Directory.CreateDirectory(Path.Combine(repoB, ".git"));
        File.WriteAllText(Path.Combine(repoA, "A.cs"), "class A { void M() { } }");
        File.WriteAllText(Path.Combine(repoB, "B.cs"), "class B { void N() { } }");

        try
        {
            var analyzer = new CSharpAnalyzer();
            var result = analyzer.AnalyzeRepos(root);

            Assert.Equal(2, result.Repos.Count);
            Assert.Contains(result.Repos, r => r.Name == "my_project/RepoA");
            Assert.Contains(result.Repos, r => r.Name == "my_project/RepoB");
            Assert.Equal(2, result.Functions.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AnalyzeRepos_MixedReposAndOrgFolders_FindsAll()
    {
        var root = Path.Combine(Path.GetTempPath(), $"test_mixed_{Guid.NewGuid():N}");
        var directRepo = Path.Combine(root, "DirectRepo");
        var orgFolder = Path.Combine(root, "org_folder");
        var nestedRepo = Path.Combine(orgFolder, "NestedRepo");

        Directory.CreateDirectory(Path.Combine(directRepo, ".git"));
        Directory.CreateDirectory(Path.Combine(nestedRepo, ".git"));
        File.WriteAllText(Path.Combine(directRepo, "D.cs"), "class D { void X() { } }");
        File.WriteAllText(Path.Combine(nestedRepo, "N.cs"), "class N { void Y() { } }");

        try
        {
            var analyzer = new CSharpAnalyzer();
            var result = analyzer.AnalyzeRepos(root);

            Assert.Equal(2, result.Repos.Count);
            Assert.Contains(result.Repos, r => r.Name == "DirectRepo");
            Assert.Contains(result.Repos, r => r.Name == "org_folder/NestedRepo");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AnalyzeRepos_NonRepoSubfolder_WithNoNestedRepos_Skipped()
    {
        var root = Path.Combine(Path.GetTempPath(), $"test_empty_org_{Guid.NewGuid():N}");
        var emptyOrg = Path.Combine(root, "empty_org");
        var realRepo = Path.Combine(root, "RealRepo");

        Directory.CreateDirectory(emptyOrg);
        Directory.CreateDirectory(Path.Combine(realRepo, ".git"));
        File.WriteAllText(Path.Combine(realRepo, "R.cs"), "class R { void Z() { } }");

        try
        {
            var analyzer = new CSharpAnalyzer();
            var result = analyzer.AnalyzeRepos(root);

            Assert.Single(result.Repos);
            Assert.Equal("RealRepo", result.Repos[0].Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, content);
        return path;
    }
}
