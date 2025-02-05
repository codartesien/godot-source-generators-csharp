# Godot C# Source Generators

A collection of source generators for Godot 4+ C# projects.

## Installation

Git clone this repository and build the project using `dotnet build`. 

Then reference the generated assembly in your main project:

```xml
<ItemGroup>
    <ProjectReference Include="<path_to_solution>\Codartesien.SourceGenerators.csproj" OutputItemType="Analyzer" />
</ItemGroup>
```

If you need to debug the source generators, you can add the following to your main project file:

```xml
<ItemGroup>
    <!-- Uncomment to have the generated files dumped in the project (.godot/mono/temp/obj/Generated) -->
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</ItemGroup>
```

## Source Generators

### SceneNodeResolver

This source generator generates code to automatically resolve scene nodes in your scripts.
Your scene nodes must have a `SceneNode` attribute with the path to the node in the scene.
It generates a partial class with a public `ResolveNodes()` method that you can call in your `_Ready()` method.

```csharp
using Godot;
using Codartesien.SourceGenerators;

public partial class MyNode : Node
{
    [SceneNode("Sprite")]
    private Sprite _sprite;

    [SceneNode("%MyLabel")]
    private Label _label;

    public override void _Ready()
    {
        base._Ready();
        ResolveNodes();
    }
}
```

### DependencyResolver

This source generator generates code to automatically resolve dependencies targeting Godot globals (aka autoload or singletons) in your `Node` scripts.
Think of this as a simple dependency injection system for your Godot project. 

> [!NOTE]  
> As this generator relies on the `GetTree()` method, `[InjectDependency]` attribute can only be used in scripts that inherit from `Node` (so not in raw C# classes).

For example, if you add the `HappinessManager` class as a global node in your project, you'll be able to inject it in your scripts using the `[InjectDependency]` attribute.

```csharp
using Godot;
using Codartesien.SourceGenerators;

public partial class MyNode : Node
{
    [InjectDependency]
    private HappinessManager _happinessManager;

    public override void _Ready()
    {
        base._Ready();
        ResolveDependencies();
        
        _happinessManager.DoSomething();
    }
}
```

You can use the `[InjectDependency]` attribute on any property which related to a global node in your project.

Under the hood, the source generator creates a dictionary of all children of the `root` node and inject them in the fields marked with the `[InjectDependency]` attribute.


