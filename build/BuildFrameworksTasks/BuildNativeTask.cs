
namespace BuildScripts;

[TaskName("Build Native")]
[IsDependentOn(typeof(BuildMGFXCTask))]
[IsDependentOn(typeof(BuildNativeDependenciesTask))]
public sealed class BuildNativeTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var buildPremake = new BuildPremake();
        buildPremake.Run(context, "mgpipeline", "native/pipeline", "pipeline.sln");

        // Repack mgfxc now that the host converter exists. The deploy repack
        // later combines all RID-specific converter artifacts.
        context.DotNetPack(context.GetProjectPath(ProjectType.Tools, "MonoGame.Effect.Compiler"), context.DotNetPackSettings);

        var stockEffectsResult = context.StartProcess(
            "bash",
            new ProcessSettings { Arguments = "scripts/native-opengl/build-stock-effects.sh" });
        if (stockEffectsResult != 0)
            throw new Exception($"Native OpenGL stock effect generation failed! {stockEffectsResult}");

        buildPremake.Run(context, "mgruntime", "native/monogame", "monogame.sln");

        context.DotNetPack(context.GetProjectPath(ProjectType.Framework, "Native"), context.DotNetPackSettings);
        context.DotNetPack("src/NuGetPackages/MonoGame.Framework/MonoGame.Framework.csproj", context.DotNetPackSettings);

        // MonoGame.Runtime.* NuGet packages are packed in the "Pack Native Runtime" task,
        // which downloads native binaries from all platform/arch build agents first.
        // This is necessary because Linux arm64 and x64 are built on separate runners.

        context.PublishBinaries("Native");
    }
}
