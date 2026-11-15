using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace miniJson.SourceGeneration
{
    [Generator(LanguageNames.CSharp)]
    public sealed class MiniJsonGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor InvalidContext = new DiagnosticDescriptor(
            "MJG001", "Invalid miniJson context",
            "Context '{0}' must be a top-level, non-generic, non-file-local partial class",
            "miniJson", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor ReflectionFallback = new DiagnosticDescriptor(
            "MJG002", "miniJson reflection fallback",
            "Type '{0}' uses reflection fallback because its shape is not supported by this generator",
            "miniJson", DiagnosticSeverity.Warning, true);

        private static readonly DiagnosticDescriptor InvalidRoot = new DiagnosticDescriptor(
            "MJG003", "Invalid miniJson registration",
            "Registration on '{0}' has an inaccessible/open type or an invalid/duplicate property name",
            "miniJson", DiagnosticSeverity.Error, true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var contexts = context.SyntaxProvider.ForAttributeWithMetadataName(
                "miniJson.MiniJsonSerializableAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, _) => attributeContext);
            context.RegisterSourceOutput(contexts, static (output, input) => Emit(output, input));
        }

        private static void Emit(SourceProductionContext output, GeneratorAttributeSyntaxContext input)
        {
            var symbol = (INamedTypeSymbol)input.TargetSymbol;
            var declaration = (ClassDeclarationSyntax)input.TargetNode;
            var attributes = symbol.GetAttributes()
                .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "miniJson.MiniJsonSerializableAttribute")
                .ToImmutableArray();
            var firstDeclaration = attributes[0].ApplicationSyntaxReference!.GetSyntax()
                .Ancestors().OfType<ClassDeclarationSyntax>().First();
            if (declaration.SyntaxTree != firstDeclaration.SyntaxTree || declaration.Span != firstDeclaration.Span)
            {
                return;
            }
            if (symbol.ContainingType != null || symbol.Arity != 0 || symbol.IsFileLocal ||
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                output.ReportDiagnostic(Diagnostic.Create(InvalidContext, declaration.Identifier.GetLocation(), symbol.Name));
                return;
            }

            var compilation = input.SemanticModel.Compilation;
            if (symbol.GetMembers().Any(member => member.Name.StartsWith("__MiniJsonRead_", StringComparison.Ordinal)))
            {
                output.ReportDiagnostic(Diagnostic.Create(InvalidRoot, declaration.Identifier.GetLocation(), symbol.Name));
                return;
            }
            var models = new List<Model>();
            var modelIndexes = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
            var roots = new List<(ITypeSymbol Type, string Name, bool Public)>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in attributes)
            {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? declaration.GetLocation();
                if (attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value is not ITypeSymbol type ||
                    attribute.ConstructorArguments[1].Value is not string name ||
                    !SyntaxFacts.IsValidIdentifier(name) || !names.Add(name) ||
                    name == symbol.Name ||
                    name.StartsWith("__MiniJson", StringComparison.Ordinal) ||
                    symbol.GetMembers(name).Length != 0 || HasOpenOrPrivateType(type) ||
                    type.IsRefLikeType || type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer ||
                    !compilation.IsSymbolAccessibleWithin(type, symbol))
                {
                    output.ReportDiagnostic(Diagnostic.Create(InvalidRoot, location, symbol.Name));
                    continue;
                }
                roots.Add((type, name, IsPublicType(type)));
                if (!Register(type, models, modelIndexes, compilation, symbol))
                {
                    output.ReportDiagnostic(Diagnostic.Create(ReflectionFallback, location, type.ToDisplayString()));
                }
            }

            var code = new StringBuilder("// <auto-generated/>\n#nullable disable\n");
            var hasNamespace = !symbol.ContainingNamespace.IsGlobalNamespace;
            if (hasNamespace)
            {
                code.Append("namespace ").Append(symbol.ContainingNamespace.ToDisplayString()).AppendLine("\n{");
            }
            code.Append(symbol.DeclaredAccessibility == Accessibility.Public ? "public " : "internal ");
            if (symbol.IsStatic)
            {
                code.Append("static ");
            }
            code.Append("partial class @").Append(symbol.Name).AppendLine("\n{");
            foreach (var root in roots)
            {
                code.Append(root.Public ? "public " : "internal ")
                    .Append("static global::miniJson.MiniJsonTypeInfo<").Append(TypeName(root.Type)).Append("> @")
                    .Append(root.Name).Append(" { get; } = new global::miniJson.MiniJsonTypeInfo<")
                    .Append(TypeName(root.Type)).Append(">(");
                if (modelIndexes.TryGetValue(root.Type, out var index))
                {
                    code.Append(MethodName(index));
                }
                else
                {
                    code.Append("static reader => reader.ReadFallback<").Append(TypeName(root.Type)).Append(">()");
                }
                code.AppendLine(");");
            }
            for (var index = 0; index < models.Count; index++)
            {
                EmitModel(code, models[index], index, modelIndexes);
            }
            code.AppendLine("}");
            if (hasNamespace)
            {
                code.AppendLine("}");
            }
            output.AddSource(symbol.ToDisplayString() + ".MiniJson.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
        }

        private static bool Register(ITypeSymbol type, List<Model> models,
            Dictionary<ITypeSymbol, int> indexes, Compilation compilation, INamedTypeSymbol context)
        {
            if (indexes.ContainsKey(type))
            {
                return true;
            }
            var model = CreateModel(type, compilation, context);
            if (model == null)
            {
                return false;
            }
            indexes.Add(type, models.Count);
            models.Add(model);
            if (type is IArrayTypeSymbol array)
            {
                Register(array.ElementType, models, indexes, compilation, context);
            }
            foreach (var member in model.Members)
            {
                Register(MemberType(member), models, indexes, compilation, context);
            }
            return true;
        }

        private static Model? CreateModel(ITypeSymbol type, Compilation compilation, INamedTypeSymbol context)
        {
            if (ScalarMethod(type) != null)
            {
                return new Model(type, ImmutableArray<ISymbol>.Empty);
            }
            if (type is IArrayTypeSymbol array)
            {
                if (!array.IsSZArray || array.ElementType.SpecialType == SpecialType.System_Byte ||
                    CreateModel(array.ElementType, compilation, context) == null)
                {
                    return null;
                }
                return new Model(type, ImmutableArray<ISymbol>.Empty);
            }
            if (type is not INamedTypeSymbol named || type.TypeKind != TypeKind.Class ||
                named.IsAbstract || named.IsStatic || named.SpecialType != SpecialType.None)
            {
                return null;
            }

            var members = ImmutableArray.CreateBuilder<ISymbol>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var current = named; current != null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }
                    if (member is IFieldSymbol field)
                    {
                        if (field.IsStatic || field.IsConst || field.IsRequired)
                        {
                            return null;
                        }
                    }
                    else if (member is IPropertySymbol property)
                    {
                        if (property.IsStatic || property.IsIndexer || property.IsRequired)
                        {
                            return null;
                        }
                    }
                    else
                    {
                        continue;
                    }
                    if (!names.Add(member.Name) || MemberType(member).IsRefLikeType ||
                        MemberType(member).TypeKind == TypeKind.Pointer ||
                        MemberType(member).TypeKind == TypeKind.FunctionPointer ||
                        !compilation.IsSymbolAccessibleWithin(MemberType(member), context))
                    {
                        return null;
                    }
                    members.Add(member);
                }
            }
            var orderedMembers = members.OrderBy(member => member.Name, StringComparer.Ordinal).ToImmutableArray();
            var constructors = named.InstanceConstructors.Where(ctor =>
                ctor.DeclaredAccessibility == Accessibility.Public &&
                compilation.IsSymbolAccessibleWithin(ctor, context)).ToArray();
            if (constructors.Any(ctor => ctor.Parameters.Length == 0) &&
                orderedMembers.All(member => IsWritable(member, compilation, context)))
            {
                return new Model(type, orderedMembers);
            }
            if (!named.IsRecord)
            {
                return null;
            }

            Model? constructorModel = null;
            foreach (var constructor in constructors)
            {
                if (constructor.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, named))
                {
                    continue;
                }
                var arguments = ImmutableArray.CreateBuilder<ISymbol>();
                foreach (var parameter in constructor.Parameters)
                {
                    var matches = orderedMembers.Where(member =>
                        string.Equals(member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                        SymbolEqualityComparer.Default.Equals(MemberType(member), parameter.Type)).ToArray();
                    if (parameter.RefKind != RefKind.None || matches.Length != 1 ||
                        arguments.Any(member => SymbolEqualityComparer.Default.Equals(member, matches[0])))
                    {
                        break;
                    }
                    arguments.Add(matches[0]);
                }
                if (arguments.Count != constructor.Parameters.Length ||
                    orderedMembers.Any(member => !arguments.Any(argument => SymbolEqualityComparer.Default.Equals(argument, member)) &&
                        !IsWritable(member, compilation, context)))
                {
                    continue;
                }
                if (constructorModel != null)
                {
                    return null;
                }
                constructorModel = new Model(type, orderedMembers, constructor, arguments.ToImmutable());
            }
            return constructorModel;
        }

        private static bool IsWritable(ISymbol member, Compilation compilation, INamedTypeSymbol context) =>
            member is IFieldSymbol field ? !field.IsReadOnly :
            member is IPropertySymbol property && property.SetMethod != null && !property.SetMethod.IsInitOnly &&
                compilation.IsSymbolAccessibleWithin(property.SetMethod, context);

        private static void EmitModel(StringBuilder code, Model model, int index,
            Dictionary<ITypeSymbol, int> indexes)
        {
            code.Append("private static ").Append(TypeName(model.Type)).Append(' ').Append(MethodName(index))
                .AppendLine("(global::miniJson.MiniJsonReader reader)\n{");
            var scalar = ScalarMethod(model.Type);
            if (scalar != null)
            {
                code.Append("return reader.").Append(scalar).AppendLine("();");
            }
            else if (model.Type is IArrayTypeSymbol array)
            {
                code.AppendLine("if (!reader.StartArray()) return null;")
                    .Append("var values = new global::System.Collections.Generic.List<")
                    .Append(TypeName(array.ElementType)).AppendLine(">();")
                    .AppendLine("if (reader.HasArrayValue())\n{\ndo\n{")
                    .Append("values.Add(").Append(ReadExpression(array.ElementType, indexes)).AppendLine(");")
                    .AppendLine("} while (reader.MoveNext());\n}\nreader.EndArray();\nreturn values.ToArray();");
            }
            else if (model.Constructor != null)
            {
                EmitConstructorModel(code, model, indexes);
            }
            else
            {
                code.AppendLine("if (!reader.StartObject()) return null;")
                    .Append("var result = new ").Append(TypeName(model.Type)).AppendLine("();")
                    .AppendLine("if (reader.HasObjectMember())\n{\ndo\n{\nvar name = reader.ReadMemberName();\nswitch (name)\n{");
                foreach (var member in model.Members)
                {
                    EmitMemberCase(code, member.Name);
                    code.Append("result.@").Append(member.Name).Append(" = ")
                        .Append(ReadExpression(MemberType(member), indexes)).AppendLine("; break;");
                }
                code.AppendLine("default: reader.ReadUnknownMember(result, name); break;\n}\n} while (reader.MoveNext());\n}")
                    .AppendLine("reader.EndObject();\nreturn result;");
            }
            code.AppendLine("}");
        }

        private static void EmitConstructorModel(StringBuilder code, Model model, Dictionary<ITypeSymbol, int> indexes)
        {
            code.AppendLine("if (!reader.StartObject()) return null;");
            for (var index = 0; index < model.Members.Length; index++)
            {
                var member = model.Members[index];
                var argumentIndex = model.ConstructorMembers.IndexOf(member);
                var parameter = argumentIndex < 0 ? null : model.Constructor!.Parameters[argumentIndex];
                code.Append(TypeName(MemberType(member))).Append(" value_").Append(index).Append(" = ")
                    .Append(parameter != null && parameter.HasExplicitDefaultValue
                        ? DefaultExpression(parameter) : "default(" + TypeName(MemberType(member)) + ")").AppendLine(";");
                if (argumentIndex < 0)
                {
                    code.Append("bool has_").Append(index).AppendLine(" = false;");
                }
            }
            code.AppendLine("if (reader.HasObjectMember())\n{\ndo\n{\nvar name = reader.ReadMemberName();\nswitch (name)\n{");
            for (var index = 0; index < model.Members.Length; index++)
            {
                var member = model.Members[index];
                EmitMemberCase(code, member.Name);
                code.Append("value_").Append(index).Append(" = ").Append(ReadExpression(MemberType(member), indexes)).Append("; ");
                if (!model.ConstructorMembers.Contains(member))
                {
                    code.Append("has_").Append(index).Append(" = true; ");
                }
                code.AppendLine("break;");
            }
            code.Append("default: reader.ReadUnknownMember<").Append(TypeName(model.Type))
                .AppendLine(">(name); break;\n}\n} while (reader.MoveNext());\n}\nreader.EndObject();");
            code.Append("var result = new ").Append(TypeName(model.Type)).Append('(')
                .Append(string.Join(", ", model.ConstructorMembers.Select(member => "value_" + model.Members.IndexOf(member))))
                .AppendLine(");");
            for (var index = 0; index < model.Members.Length; index++)
            {
                if (!model.ConstructorMembers.Contains(model.Members[index]))
                {
                    code.Append("if (has_").Append(index).Append(") result.@").Append(model.Members[index].Name)
                        .Append(" = value_").Append(index).AppendLine(";");
                }
            }
            code.AppendLine("return result;");
        }

        private static void EmitMemberCase(StringBuilder code, string name) =>
            code.Append("case var _ when global::System.MemoryExtensions.Equals(name, ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, true))
                .Append(", global::System.StringComparison.OrdinalIgnoreCase): ");

        private static string DefaultExpression(IParameterSymbol parameter)
        {
            var value = parameter.ExplicitDefaultValue;
            if (value == null)
            {
                return "default(" + TypeName(parameter.Type) + ")";
            }
            string literal;
            if (value is double number)
            {
                literal = double.IsNaN(number) ? "global::System.Double.NaN" :
                    double.IsPositiveInfinity(number) ? "global::System.Double.PositiveInfinity" :
                    double.IsNegativeInfinity(number) ? "global::System.Double.NegativeInfinity" :
                    number.ToString("R", CultureInfo.InvariantCulture) + "D";
            }
            else if (value is float single)
            {
                literal = float.IsNaN(single) ? "global::System.Single.NaN" :
                    float.IsPositiveInfinity(single) ? "global::System.Single.PositiveInfinity" :
                    float.IsNegativeInfinity(single) ? "global::System.Single.NegativeInfinity" :
                    single.ToString("R", CultureInfo.InvariantCulture) + "F";
            }
            else if (value is decimal amount)
            {
                literal = amount.ToString(CultureInfo.InvariantCulture) + "M";
                if (amount == 0 && decimal.GetBits(amount)[3] < 0)
                {
                    literal = "-" + literal;
                }
            }
            else
            {
                literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(value, true, false);
            }
            return "(" + TypeName(parameter.Type) + ")(" + literal + ")";
        }

        private static string ReadExpression(ITypeSymbol type, Dictionary<ITypeSymbol, int> indexes) =>
            indexes.TryGetValue(type, out var index)
                ? MethodName(index) + "(reader)"
                : "reader.ReadMemberFallback<" + TypeName(type) + ">()";

        private static string? ScalarMethod(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Int32: return "ReadInt32";
                case SpecialType.System_Int64: return "ReadInt64";
                case SpecialType.System_Byte: return "ReadByte";
                case SpecialType.System_Double: return "ReadDouble";
                case SpecialType.System_Single: return "ReadSingle";
                case SpecialType.System_Decimal: return "ReadDecimal";
                case SpecialType.System_Boolean: return "ReadBoolean";
                case SpecialType.System_String: return "ReadString";
                default: return null;
            }
        }

        private static ITypeSymbol MemberType(ISymbol member) =>
            member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;

        private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        private static string MethodName(int index) => "__MiniJsonRead_" + index;

        private static bool HasOpenOrPrivateType(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer)
            {
                return true;
            }
            if (type is IArrayTypeSymbol array)
            {
                return HasOpenOrPrivateType(array.ElementType);
            }
            if (type is not INamedTypeSymbol named)
            {
                return type.TypeKind == TypeKind.TypeParameter || type.TypeKind == TypeKind.Error;
            }
            return named.IsUnboundGenericType || named.IsFileLocal ||
                (named.DeclaredAccessibility != Accessibility.Public && named.DeclaredAccessibility != Accessibility.Internal) ||
                (named.ContainingType != null && HasOpenOrPrivateType(named.ContainingType)) ||
                named.TypeArguments.Any(HasOpenOrPrivateType);
        }

        private static bool IsPublicType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return IsPublicType(array.ElementType);
            }
            return type is INamedTypeSymbol named && named.DeclaredAccessibility == Accessibility.Public &&
                (named.ContainingType == null || IsPublicType(named.ContainingType)) &&
                named.TypeArguments.All(IsPublicType);
        }

        private sealed class Model
        {
            public Model(ITypeSymbol type, ImmutableArray<ISymbol> members,
                IMethodSymbol? constructor = null, ImmutableArray<ISymbol> constructorMembers = default)
            {
                Type = type;
                Members = members;
                Constructor = constructor;
                ConstructorMembers = constructorMembers;
            }

            public ITypeSymbol Type { get; }
            public ImmutableArray<ISymbol> Members { get; }
            public IMethodSymbol? Constructor { get; }
            public ImmutableArray<ISymbol> ConstructorMembers { get; }
        }
    }
}
