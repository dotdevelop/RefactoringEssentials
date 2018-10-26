using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactoringEssentials.CSharp.Diagnostics
{
    [ExportCodeFixProvider(LanguageNames.CSharp), System.Composition.Shared]
    public class ReplaceWithSingleCallToLastCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(CSharpDiagnosticIDs.ReplaceWithSingleCallToLastAnalyzerID);
            }
        }

		public override FixAllProvider GetFixAllProvider()
		{
			return WellKnownFixAllProviders.BatchFixer;
		}

		static InvocationExpressionSyntax MakeSingleCall(InvocationExpressionSyntax anyInvoke)
        {
            var member = ((MemberAccessExpressionSyntax)anyInvoke.Expression).Name;
            ExpressionSyntax target;
            InvocationExpressionSyntax whereInvoke;
            if (MatchWhere(anyInvoke, out target, out whereInvoke))
            {
                var callerExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, target, member).WithAdditionalAnnotations(Formatter.Annotation);
                var argument = whereInvoke.ArgumentList.Arguments[0].WithAdditionalAnnotations(Formatter.Annotation);
                return SyntaxFactory.InvocationExpression(callerExpr, SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { argument })));
            }

            return null;
        }

		internal static bool MatchWhere(InvocationExpressionSyntax anyInvoke, out ExpressionSyntax target, out InvocationExpressionSyntax whereInvoke)
        {
            target = null;
            whereInvoke = null;

            if (anyInvoke.ArgumentList.Arguments.Count != 0)
                return false;
			if (!(anyInvoke.Expression is MemberAccessExpressionSyntax anyInvokeBase))
				return false;
			whereInvoke = anyInvokeBase.Expression as InvocationExpressionSyntax;
            if (whereInvoke == null || whereInvoke.ArgumentList.Arguments.Count != 1)
                return false;
			if (!(whereInvoke.Expression is MemberAccessExpressionSyntax baseMember) || baseMember.Name.Identifier.Text != "Where")
				return false;
			target = baseMember.Expression;

            return target != null;
        }

        public async override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;
            var diagnostics = context.Diagnostics;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var diagnostic = diagnostics.First();
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true) as InvocationExpressionSyntax;
            var newRoot = root.ReplaceNode(node, MakeSingleCall(node));
            context.RegisterCodeFix(CodeActionFactory.Create(node.Span, diagnostic.Severity, "Replace with single call to 'Last'", document.WithSyntaxRoot(newRoot)), diagnostic);
        }
    }
}