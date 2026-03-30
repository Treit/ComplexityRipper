using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityRipper.Analysis;

/// <summary>
/// Calculates cyclomatic complexity for a C# syntax node by counting decision points.
/// </summary>
public static class ComplexityCalculator
{
    /// <summary>
    /// Calculates the cyclomatic complexity of a function body.
    /// Starts at 1 and increments for each decision point.
    /// </summary>
    public static int Calculate(SyntaxNode node)
    {
        int complexity = 1;
        foreach (var descendant in node.DescendantNodesAndTokens())
        {
            if (descendant.IsNode)
            {
                complexity += descendant.AsNode() switch
                {
                    IfStatementSyntax => 1,
                    CaseSwitchLabelSyntax => 1,
                    CasePatternSwitchLabelSyntax => 1,
                    SwitchExpressionArmSyntax => 1,
                    ForStatementSyntax => 1,
                    ForEachStatementSyntax => 1,
                    WhileStatementSyntax => 1,
                    DoStatementSyntax => 1,
                    CatchClauseSyntax => 1,
                    ConditionalExpressionSyntax => 1, // ternary ?:
                    ConditionalAccessExpressionSyntax => 1, // ?.
                    _ => 0,
                };
            }
            else if (descendant.IsToken)
            {
                complexity += descendant.AsToken().Kind() switch
                {
                    SyntaxKind.AmpersandAmpersandToken => 1, // &&
                    SyntaxKind.BarBarToken => 1, // ||
                    SyntaxKind.QuestionQuestionToken => 1, // ??
                    _ => 0,
                };
            }
        }

        return complexity;
    }

    /// <summary>
    /// Calculates the maximum nesting depth of control-flow blocks within a syntax node.
    /// </summary>
    public static int CalculateMaxNestingDepth(SyntaxNode node)
    {
        int maxDepth = 0;
        CalculateNestingDepthRecursive(node, 0, ref maxDepth);
        return maxDepth;
    }

    private static void CalculateNestingDepthRecursive(SyntaxNode node, int currentDepth, ref int maxDepth)
    {
        foreach (var child in node.ChildNodes())
        {
            bool isNesting = child is IfStatementSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or SwitchStatementSyntax
                or SwitchExpressionSyntax
                or TryStatementSyntax
                or LockStatementSyntax
                or UsingStatementSyntax;

            int nextDepth = isNesting ? currentDepth + 1 : currentDepth;
            if (nextDepth > maxDepth)
            {
                maxDepth = nextDepth;
            }

            CalculateNestingDepthRecursive(child, nextDepth, ref maxDepth);
        }
    }
}
