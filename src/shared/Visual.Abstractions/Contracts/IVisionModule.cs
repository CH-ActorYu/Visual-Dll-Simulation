namespace Visual.Abstractions.Contracts;

public interface IVisionModule
{
    string Name { get; }

    Version Version { get; }

    void Configure(ModuleConfiguration configuration);

    ModuleConfiguration ExportConfig();
}
