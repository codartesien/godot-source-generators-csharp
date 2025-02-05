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

