using System.Reflection;
using System.Xml.Linq;
using NetArchTest.Rules;

namespace Visual.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    private static readonly string[] CoreAssemblyNames =
    [
        "Visual.Abstractions",
        "Visual.Image",
        "Visual.IO",
        "Visual.Vision.Contracts",
        "Visual.VisionBase",
        "Visual.Distance"
    ];

    private static readonly string[] NativeSdkNamespaces =
    [
        "OpenCvSharp",
        "HalconDotNet",
        "Cognex.VisionPro"
    ];

    [Fact]
    public void Core_projects_must_not_depend_on_native_vision_sdks()
    {
        foreach (var assemblyName in CoreAssemblyNames)
        {
            var assembly = LoadAssembly(assemblyName);
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(NativeSdkNamespaces)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"{assemblyName} contains native SDK dependencies: {FormatFailures(result)}");
        }

        var forbiddenPackages = GetProjectFiles(CoreAssemblyNames)
            .SelectMany(ReadPackageReferences)
            .Where(IsNativeSdkName)
            .ToArray();

        Assert.Empty(forbiddenPackages);
    }

    [Fact]
    public void Distance_must_not_depend_on_engine_projects()
    {
        var result = Types.InAssembly(LoadAssembly("Visual.Distance"))
            .ShouldNot()
            .HaveDependencyOnAny("Visual.Engine")
            .GetResult();

        Assert.True(result.IsSuccessful, $"Distance contains engine dependencies: {FormatFailures(result)}");

        var distanceProject = GetProjectFiles(["Visual.Distance"]).Single();
        var engineReferences = ReadProjectReferences(distanceProject)
            .Where(reference => reference.Contains("Visual.Engine.", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(engineReferences);
    }

    [Fact]
    public void Public_contracts_must_not_expose_native_sdk_types()
    {
        var violations = CoreAssemblyNames
            .Select(LoadAssembly)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(GetPublicSignatureTypes)
            .Where(type => IsNativeSdkName(type.FullName ?? type.Name))
            .Select(type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static Assembly LoadAssembly(string name) => Assembly.Load(new AssemblyName(name));

    private static IEnumerable<string> GetProjectFiles(IEnumerable<string> assemblyNames)
    {
        var repositoryRoot = FindRepositoryRoot();
        var names = assemblyNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => names.Contains(Path.GetFileNameWithoutExtension(path)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Visual.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root containing Visual.sln.");
    }

    private static IEnumerable<string> ReadPackageReferences(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>();

    private static IEnumerable<string> ReadProjectReferences(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>();

    private static IEnumerable<Type> GetPublicSignatureTypes(Type exportedType)
    {
        yield return exportedType;

        foreach (var memberType in exportedType
                     .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                     .SelectMany(GetMemberTypes))
        {
            foreach (var type in ExpandType(memberType))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<Type> GetMemberTypes(MemberInfo member) => member switch
    {
        MethodInfo method => method.GetParameters().Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType),
        ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        EventInfo eventInfo when eventInfo.EventHandlerType is not null => [eventInfo.EventHandlerType],
        _ => []
    };

    private static IEnumerable<Type> ExpandType(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nestedType in ExpandType(elementType))
            {
                yield return nestedType;
            }
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var nestedType in ExpandType(genericArgument))
            {
                yield return nestedType;
            }
        }
    }

    private static bool IsNativeSdkName(string name) => NativeSdkNamespaces.Any(
        nativeNamespace => name.StartsWith(nativeNamespace, StringComparison.Ordinal));

    private static string FormatFailures(TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
