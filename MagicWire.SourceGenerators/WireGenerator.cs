using System;
using System.Collections.Immutable;
using System.Linq;
using MagicWire.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicWire.SourceGenerators;

[Generator]
public sealed class WireGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: (ctx, _) => GetClassSymbolIfWire(ctx))
            .Where(static m => m is not null)!;
    
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(classDeclarations.Collect()), 
            (spc, source) => Execute(source.Left, source.Right, spc)
        );
    }
    
    private static INamedTypeSymbol GetClassSymbolIfWire(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax);
        return symbol is not null && symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "WireAttribute")
            ? symbol as INamedTypeSymbol
            : null;
    }
    
    private static INamedTypeSymbol GetClassSymbol(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax);
        return symbol as INamedTypeSymbol;
    }
    
    private static INamedTypeSymbol GetEnumSymbol(GeneratorSyntaxContext context)
    {
        var enumSyntax = (EnumDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(enumSyntax);
        return symbol as INamedTypeSymbol;
    }
    
    private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol> classes, SourceProductionContext context)
    {
        foreach (var classSymbol in classes)
        {
            var wireObject = BuildWireableObject(classSymbol);
            var source = CSharpCodeBuilder.Build(wireObject);
            context.AddSource($"{wireObject.ClassName}.g.cs", source);
        }
    }

    private static WireableObject BuildWireableObject(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var instanceName = GetWireName(symbol) ?? className[0].ToString().ToLower() + className.Substring(1);
        var isStandalone = symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "StandaloneAttribute");

        var obj = new WireableObject(ns, className, instanceName, isStandalone);

        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol field && HasWire(field))
            {
                var fieldName = field.Name;
                var propName = ToPascalCase(fieldName.TrimStart('_'));
                var frontendName = GetWireName(field) ?? propName;
                var type = field.Type.ToDisplayString();
                obj.Fields.Add(new WireableField(fieldName, propName, frontendName, type));
            }
            else if (member is IMethodSymbol method && HasWire(method))
            {
                if (method.IsPartialDefinition && HasToClient(method))
                {
                    var eventName = GetWireName(method) ?? method.Name;
                    obj.Events.Add(new WireableEvent(method.Name, eventName)
                    {
                        Parameters = method.Parameters.Select(p => Tuple.Create(p.Name, p.Type.ToDisplayString())).ToList()
                    });
                }
                else
                {
                    var operationName = GetWireName(method) ?? method.Name;
                    obj.Operations.Add(new WireableOperation(method.Name, operationName, method.ReturnType.ToDisplayString())
                    {
                        Parameters = method.Parameters.Select(p => Tuple.Create(p.Name, p.Type.ToDisplayString())).ToList()
                    });
                }
            }
        }

        return obj;
    }
    
    private static bool HasWire(ISymbol symbol) => symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "WireAttribute");
    private static bool HasToClient(ISymbol symbol) => symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ToClientAttribute");

    private static string GetWireName(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "WireNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value?.ToString();

    private static string ToPascalCase(string str)
    {
        if (string.IsNullOrWhiteSpace(str)) return str;
        return char.ToUpper(str[0]) + str.Substring(1);
    }

}