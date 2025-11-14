using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotVcs.Gen;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ComponentAna : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id:"GDVCS001",
        title:"Component classes must implement IComponent",
        messageFormat:"类型‘{0}’被ComponentAttribute注解，但未实现IComponent接口",
        category:"Usage",
        defaultSeverity:DiagnosticSeverity.Error,
        isEnabledByDefault:true,
        description:"Component类必须要继承自IComponent接口.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);
    
    public override void Initialize(AnalysisContext context)
    {
        // 配置分析上下文
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 注册符号操作，检查所有命名类型（类、结构体等）
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // 只检查类类型
        if (namedTypeSymbol.TypeKind != TypeKind.Class)
            return;

        // 检查是否被 ComponentAttribute 注解
        var hasComponentAttribute = namedTypeSymbol.GetAttributes().Any(attr =>
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null)
                return false;

            // 检查特性名称（支持短名称和完全限定名称）
            return attrClass.Name == "ComponentAttribute" ||
                   attrClass.Name == "Component" ||
                   attrClass.ToDisplayString() == "GodotVcs.Lib.ComponentAttribute";
        });

        if (!hasComponentAttribute)
            return;

        // 检查是否实现了 IComponent 接口
        var implementsIComponent = namedTypeSymbol.AllInterfaces.Any(interf =>
        {
            var interfaceName = interf.ToDisplayString();
            return interf.Name == "IComponent" ||
                   interfaceName == "GodotVcs.Lib.IComponent";
        });

        // 如果没有实现 IComponent 接口，报告诊断
        if (implementsIComponent) return;
        // 获取类的声明语法节点位置
        var locations = namedTypeSymbol.Locations;
        if (locations.IsEmpty)
            return;

        // 尝试获取类名标识符的位置（更精确的错误位置）
        var diagnosticLocation = locations[0];
        foreach (var location in locations)
        {
            if (location.SourceTree == null) continue;
            var root = location.SourceTree.GetRoot(context.CancellationToken);
            var node = root.FindNode(location.SourceSpan);
            if (node is not ClassDeclarationSyntax classDecl) continue;
            diagnosticLocation = classDecl.Identifier.GetLocation();
            break;
        }

        var diagnostic = Diagnostic.Create(
            Rule,
            diagnosticLocation,
            namedTypeSymbol.Name);

        context.ReportDiagnostic(diagnostic);
    }
}