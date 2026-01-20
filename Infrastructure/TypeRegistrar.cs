using Microsoft.Extensions.DependencyInjection;

using Spectre.Console.Cli;

namespace RanaImageTool.Infrastructure;

public sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    private readonly IServiceCollection _builder = builder;

    public void Register(Type service, Type implementation)
        => _builder.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation)
        => _builder.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
        => _builder.AddSingleton(service, _ => factory());

    public ITypeResolver Build()
        => new TypeResolver(_builder.BuildServiceProvider());
}

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider = provider;

    public object? Resolve(Type? type)
        => type != null ? _provider.GetService(type) : null;

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }
}
