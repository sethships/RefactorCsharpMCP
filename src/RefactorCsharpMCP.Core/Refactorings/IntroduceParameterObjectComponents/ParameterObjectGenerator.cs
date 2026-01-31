using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Utilities;

namespace RefactorCsharpMCP.Core.Refactorings.IntroduceParameterObjectComponents;

/// <summary>
/// Generates parameter object classes or records based on target framework capabilities.
/// Produces records for .NET 8+ (C# 9+) and traditional classes for .NET Framework 4.8.
/// </summary>
public class ParameterObjectGenerator
{
    /// <summary>
    /// Generates a parameter object class or record based on the target framework.
    /// </summary>
    /// <param name="className">The name for the new parameter object type.</param>
    /// <param name="parameters">The parameters to include in the parameter object.</param>
    /// <param name="useRecord">True to generate a record (C# 9+), false for a class.</param>
    /// <returns>A member declaration syntax for the parameter object.</returns>
    public MemberDeclarationSyntax Generate(
        string className,
        List<ParameterSyntax> parameters,
        bool useRecord)
    {
        if (useRecord)
        {
            return GenerateRecordDeclaration(className, parameters);
        }
        else
        {
            return GenerateClassDeclaration(className, parameters);
        }
    }

    /// <summary>
    /// Generates a record declaration with primary constructor.
    /// Example: public record AddressInfo(string Street, string City, string Zip);
    /// </summary>
    private RecordDeclarationSyntax GenerateRecordDeclaration(
        string className,
        List<ParameterSyntax> parameters)
    {
        var recordParameters = SyntaxFactory.SeparatedList(
            parameters.Select(p =>
                SyntaxFactory.Parameter(
                    SyntaxFactory.List<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(),
                    p.Type,
                    SyntaxFactory.Identifier(NamingHelper.ToPascalCase(p.Identifier.Text)),
                    null)));

        return SyntaxFactory.RecordDeclaration(
            SyntaxFactory.Token(SyntaxKind.RecordKeyword),
            SyntaxFactory.Identifier(className))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
            .WithParameterList(
                SyntaxFactory.ParameterList(recordParameters))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed);
    }

    /// <summary>
    /// Generates a traditional class declaration with constructor and readonly properties.
    /// Example:
    /// public class AddressInfo
    /// {
    ///     public string Street { get; }
    ///     public string City { get; }
    ///     public string Zip { get; }
    ///     public AddressInfo(string street, string city, string zip)
    ///     {
    ///         Street = street;
    ///         City = city;
    ///         Zip = zip;
    ///     }
    /// }
    /// </summary>
    private ClassDeclarationSyntax GenerateClassDeclaration(
        string className,
        List<ParameterSyntax> parameters)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Generate properties
        foreach (var param in parameters)
        {
            var propertyName = NamingHelper.ToPascalCase(param.Identifier.Text);
            var property = SyntaxFactory.PropertyDeclaration(
                param.Type ?? SyntaxFactory.ParseTypeName("object"),
                propertyName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("    ")),
                        SyntaxKind.PublicKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                .WithAccessorList(SyntaxFactory.AccessorList(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    )))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            members.Add(property);
        }

        // Generate constructor
        var constructorParams = SyntaxFactory.SeparatedList(
            parameters.Select(p =>
                SyntaxFactory.Parameter(
                    SyntaxFactory.List<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(),
                    p.Type,
                    SyntaxFactory.Identifier(NamingHelper.ToCamelCase(p.Identifier.Text)),
                    null)));

        var assignments = parameters.Select(p =>
        {
            var propertyName = NamingHelper.ToPascalCase(p.Identifier.Text);
            var paramName = NamingHelper.ToCamelCase(p.Identifier.Text);
            return (StatementSyntax)SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(propertyName),
                    SyntaxFactory.IdentifierName(paramName)))
                .WithLeadingTrivia(SyntaxFactory.Whitespace("        "))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        }).ToList();

        var constructor = SyntaxFactory.ConstructorDeclaration(className)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace("    ")),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
            .WithParameterList(SyntaxFactory.ParameterList(constructorParams))
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)),
                SyntaxFactory.List(assignments),
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("    ")),
                    SyntaxKind.CloseBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed))));

        members.Add(constructor);

        return SyntaxFactory.ClassDeclaration(className)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
            .WithMembers(SyntaxFactory.List(members))
            .WithOpenBraceToken(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
            .WithCloseBraceToken(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.CloseBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed);
    }
}
