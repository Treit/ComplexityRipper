using Microsoft.CodeAnalysis.CSharp;
using ComplexityRipper.Analysis;

namespace ComplexityRipper.Tests;

public class ComplexityCalculatorTests
{
    [Fact]
    public void Calculate_EmptyMethod_ReturnsOne()
    {
        var code = @"
class C {
    void M() { }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(1, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_SingleIf_ReturnsTwo()
    {
        var code = @"
class C {
    void M(bool x) {
        if (x) { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_IfElseIfElse_ReturnsThree()
    {
        var code = @"
class C {
    void M(int x) {
        if (x > 0) { }
        else if (x < 0) { }
        else { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(3, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ForLoop_ReturnsTwo()
    {
        var code = @"
class C {
    void M() {
        for (int i = 0; i < 10; i++) { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ForEachLoop_ReturnsTwo()
    {
        var code = @"
class C {
    void M(int[] items) {
        foreach (var item in items) { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_WhileLoop_ReturnsTwo()
    {
        var code = @"
class C {
    void M() {
        while (true) { break; }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_SwitchWithThreeCases_ReturnsFour()
    {
        var code = @"
class C {
    void M(int x) {
        switch (x) {
            case 1: break;
            case 2: break;
            case 3: break;
        }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(4, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_LogicalAnd_ReturnsTwo()
    {
        var code = @"
class C {
    void M(bool a, bool b) {
        if (a && b) { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(3, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_LogicalOr_ReturnsTwo()
    {
        var code = @"
class C {
    void M(bool a, bool b) {
        if (a || b) { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(3, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_NullCoalescing_ReturnsTwo()
    {
        var code = @"
class C {
    string M(string? s) {
        return s ?? ""default"";
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_TryCatch_ReturnsTwo()
    {
        var code = @"
class C {
    void M() {
        try { } catch (System.Exception) { }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_Ternary_ReturnsTwo()
    {
        var code = @"
class C {
    int M(bool x) {
        return x ? 1 : 0;
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ComplexMethod_ReturnsCorrectCount()
    {
        var code = @"
class C {
    void M(int x, bool flag) {
        if (x > 0 && flag) {
            for (int i = 0; i < x; i++) {
                if (i % 2 == 0 || flag) {
                    try { }
                    catch (System.Exception) { }
                }
            }
        } else if (x < 0) {
            while (x < 0) { x++; }
        }
    }
}";
        // 1 (base) + if + && + for + if + || + catch + else if + while = 9
        var method = GetFirstMethod(code);
        Assert.Equal(9, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void CalculateMaxNestingDepth_NoNesting_ReturnsZero()
    {
        var code = @"
class C {
    void M() {
        var x = 1;
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(0, ComplexityCalculator.CalculateMaxNestingDepth(method));
    }

    [Fact]
    public void CalculateMaxNestingDepth_SingleIf_ReturnsOne()
    {
        var code = @"
class C {
    void M(bool x) {
        if (x) { var y = 1; }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(1, ComplexityCalculator.CalculateMaxNestingDepth(method));
    }

    [Fact]
    public void CalculateMaxNestingDepth_NestedIfFor_ReturnsTwo()
    {
        var code = @"
class C {
    void M(bool x) {
        if (x) {
            for (int i = 0; i < 10; i++) { }
        }
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.CalculateMaxNestingDepth(method));
    }

    [Fact]
    public void CalculateMaxNestingDepth_DeeplyNested_ReturnsCorrectDepth()
    {
        var code = @"
class C {
    void M(int x) {
        if (x > 0) {
            for (int i = 0; i < x; i++) {
                while (true) {
                    try {
                        break;
                    } catch { }
                }
            }
        }
    }
}";
        // if(1) -> for(2) -> while(3) -> try(4)
        var method = GetFirstMethod(code);
        Assert.Equal(4, ComplexityCalculator.CalculateMaxNestingDepth(method));
    }

    [Fact]
    public void Calculate_SwitchExpression_CountsArms()
    {
        var code = @"
class C {
    string M(int x) {
        return x switch {
            1 => ""one"",
            2 => ""two"",
            _ => ""other"",
        };
    }
}";
        var method = GetFirstMethod(code);
        // 1 (base) + 3 switch expression arms = 4
        Assert.Equal(4, ComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ConditionalAccess_ReturnsTwo()
    {
        var code = @"
class C {
    int? M(string? s) {
        return s?.Length;
    }
}";
        var method = GetFirstMethod(code);
        Assert.Equal(2, ComplexityCalculator.Calculate(method));
    }

    private static Microsoft.CodeAnalysis.SyntaxNode GetFirstMethod(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        return root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();
    }
}
