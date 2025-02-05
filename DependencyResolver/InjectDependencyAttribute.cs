namespace Codartesien.SourceGenerators.DependencyResolver;

using System;

/// <summary>
/// Attribute to mark a field as a dependency that should be injected.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class InjectDependencyAttribute : Attribute
{
}

public interface IDependencyResolver
{
    public void ResolveDependencies();
}
