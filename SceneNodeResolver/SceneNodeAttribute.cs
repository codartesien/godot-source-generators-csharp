namespace Codartesien.SourceGenerators.SceneNodeResolver;

using System;

[AttributeUsage(AttributeTargets.Field)]
public class SceneNodeAttribute : Attribute
{
    public string NodePath { get; }

    public SceneNodeAttribute(string nodePath)
    {
        NodePath = nodePath;
    }
}

public interface ISceneNodeResolver
{
    public void ResolveNodes();
}
