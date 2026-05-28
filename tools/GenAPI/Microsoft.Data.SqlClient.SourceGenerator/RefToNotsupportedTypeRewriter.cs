using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Data.SqlClient.SourceGenerator
{
    public class RefToNotsupportedTypeRewriter : CSharpSyntaxRewriter
    {
        private static readonly BlockSyntax s_throwBlock;
        private static readonly ArrowExpressionClauseSyntax s_throwExpression;

        static RefToNotsupportedTypeRewriter()
        {
            ThrowStatementSyntax throwStatement =
            ThrowStatement(                                                                                   // throw new System.PlatformNotSupportedException("Microsoft.Data.SqlClient is not supported on this platform.")
                ObjectCreationExpression(                                                                     // new System.PlatformNotSupportedException("Microsoft.Data.SqlClient is not supported on this platform.")              
                    IdentifierName("System.PlatformNotSupportedException"),
                    ArgumentList(                                                                             // ("Microsoft.Data.SqlClient is not supported on this platform.")
                        SingletonSeparatedList(
                            Argument(                                                                         // "Microsoft.Data.SqlClient is not supported on this platform."
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal("Microsoft.Data.SqlClient is not supported on this platform.")    // Microsoft.Data.SqlClient is not supported on this platform.
                                )
                            )
                        )
                    ),
                    null
                )
            )
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));                                            // throw new System.PlatformNotSupportedException("Microsoft.Data.SqlClient is not supported on this platform.");

            s_throwBlock = Block(throwStatement)
                .NormalizeWhitespace(indentation: " ", eol: "");                                              // { throw new System.PlatformNotSupportedException("Microsoft.Data.SqlClient is not supported on this platform."); }
            s_throwExpression = ArrowExpressionClause(ThrowExpression(throwStatement.Expression))
                .NormalizeWhitespace();                                                                       // => throw new System.PlatformNotSupportedException("Microsoft.Data.SqlClient is not supported on this platform.")
        }

        public override SyntaxNode VisitAccessorList(AccessorListSyntax node)
        {
            if (node.Parent.IsKind(SyntaxKind.EventDeclaration))
            {
                return node;
            }

            AccessorListSyntax newNode = AccessorList();
            foreach (AccessorDeclarationSyntax accessor in node.Accessors)
            {
                newNode = newNode.AddAccessors(AccessorDeclaration(accessor.Kind(), accessor.AttributeLists, accessor.Modifiers, s_throwBlock));
            }

            return newNode.NormalizeWhitespace(indentation: " ", eol: "").WithTrailingTrivia(CarriageReturnLineFeed);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (node.ExpressionBody != null)
            {
#if GENAPI_COMPAT
                // Replacing property getter expression with accessor list with throw statement for compatibility with GenAPI generated source
                node = PropertyDeclaration(node.AttributeLists, node.Modifiers, node.Type, node.ExplicitInterfaceSpecifier, node.Identifier,
                                           AccessorList(SingletonList(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, s_throwBlock))), null, null, MissingToken(SyntaxKind.SemicolonToken))
                       .WithTriviaFrom(node);
#else
                node = node.WithExpressionBody(ThrowExpression);
#endif
            }

            return base.VisitPropertyDeclaration(node);
        }

        public override SyntaxNode VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (node.ExpressionBody != null)
            {
                node = node.WithExpressionBody(s_throwExpression);
            }

            return base.VisitIndexerDeclaration(node);
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Modifiers.Any(m => m.ValueText == "abstract"))
            {
                return base.VisitMethodDeclaration(node);
            }

#if GENAPI_COMPAT
            MethodDeclarationSyntax newNode = node.WithBody(s_throwBlock)
                .WithExpressionBody(null)
                .WithSemicolonToken(MissingToken(SyntaxKind.SemicolonToken))
                .WithTrailingTrivia(CarriageReturnLineFeed);
#else
            MethodDeclarationSyntax newNode = (node.ExpressionBody != null ? node.WithExpressionBody(s_throwExpression) : node.WithBody(s_throwBlock))
                .WithTrailingTrivia(CarriageReturnLineFeed);
#endif
            return base.VisitMethodDeclaration(newNode);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            ConstructorDeclarationSyntax newNode = (node.ExpressionBody != null ? node.WithExpressionBody(s_throwExpression) : node.WithBody(s_throwBlock))
                .WithTrailingTrivia(CarriageReturnLineFeed);
            return base.VisitConstructorDeclaration(newNode);
        }

#if GENAPI_COMPAT
        public override SyntaxNode VisitAttribute(AttributeSyntax node)
        {
            // Ref sources uses following form of the attribute: [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
            // , while GenAPI sources it is generated as:        [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            // Here for GenAPI compatibility we substitute former with a latter in source
            if (node.Name.ToString().EndsWith("DesignerSerializationVisibilityAttribute") &&
                node.ArgumentList.Arguments.Count > 0 && node.ArgumentList.ToFullString() == "(0)")
            {
                node = node.WithArgumentList(
                            AttributeArgumentList(
                                SingletonSeparatedList(
                                    AttributeArgument(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("System.ComponentModel.DesignerSerializationVisibility"),
                                            IdentifierName("Hidden"))))));
            }

            return base.VisitAttribute(node);
        }
#endif

#if GENAPI_COMPAT
        // Order enum elements by value (as GenAPI does), instead of by name (as source generator does)
        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            node = node.WithMembers(SeparatedList<EnumMemberDeclarationSyntax>(
                node.Members.OrderBy(m => int.Parse(m.EqualsValue.Value.ToFullString())).ToArray(),
                node.Members.GetSeparators()));
            return base.VisitEnumDeclaration(node);
        }
#endif

#if GENAPI_COMPAT
        public override SyntaxNode Visit(SyntaxNode node)
        {
            // Strip away any documentation, or preprocessor directives
            if (node is MemberDeclarationSyntax member)
            {
                if (!member.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                {
                    if (member.Modifiers != null && member.Modifiers.Count > 0 && member.Modifiers[0].HasLeadingTrivia)
                    {
                        node = member.WithModifiers(member.Modifiers.Replace(member.Modifiers[0], member.Modifiers[0].WithLeadingTrivia(RemoveExcessTrivia(member.Modifiers[0].LeadingTrivia))));
                    }
                    if (node is MethodDeclarationSyntax method && method.HasLeadingTrivia)
                    {
                        node = method.WithReturnType(method.ReturnType.WithLeadingTrivia(RemoveExcessTrivia(method.ReturnType.GetLeadingTrivia())));
                    }
                    if (node is BasePropertyDeclarationSyntax property && property.HasLeadingTrivia)
                    {
                        node = property.WithType(property.Type.WithLeadingTrivia(RemoveExcessTrivia(property.Type.GetLeadingTrivia())));
                    }
                    if (node is BaseTypeDeclarationSyntax typeNode)
                    {
                        node = typeNode.WithOpenBraceToken(typeNode.OpenBraceToken.WithLeadingTrivia(RemoveExcessTrivia(typeNode.OpenBraceToken.LeadingTrivia)))
                                       .WithCloseBraceToken(typeNode.CloseBraceToken.WithLeadingTrivia(RemoveExcessTrivia(typeNode.CloseBraceToken.LeadingTrivia)));
                    }
                }

                return base.Visit(node.WithLeadingTrivia(RemoveExcessTrivia(node.GetLeadingTrivia())));
            }

            return base.Visit(node);
        }

        private static SyntaxTriviaList RemoveExcessTrivia(SyntaxTriviaList trivias)
        {
            SyntaxTriviaList newTrivias = TriviaList();
            for (int i = 0; i < trivias.Count; i++)
            {
                SyntaxTrivia trivia = trivias[i];
                SyntaxTrivia? lastTrivia = newTrivias.LastOrDefault();
                if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                    !trivia.IsKind(SyntaxKind.DisabledTextTrivia) &&
                    !trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia) &&
                    !trivia.IsKind(SyntaxKind.IfDirectiveTrivia) &&
                    !trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) &&
                    !trivia.IsKind(SyntaxKind.ElifDirectiveTrivia))
                {
                    newTrivias = newTrivias.Add(trivia);
                }
                else if (lastTrivia != null &&
                    (
                    lastTrivia.Value.IsKind(SyntaxKind.WhitespaceTrivia) ||
                    lastTrivia.Value.IsKind(SyntaxKind.DisabledTextTrivia)
                    ))
                {
                    newTrivias = newTrivias.Remove(lastTrivia.Value);
                }
            }

            return newTrivias;
        }
#endif
    }
}
