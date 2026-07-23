namespace BuildScripts;

[TaskName("Build iOS OpenGLES")]
[IsDependentOn(typeof(BuildNativeTask))]
public sealed class BuildiOSOpenGLESTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) =>
        Environment.Version.Major >= 10 &&
        context.IsRunningOnMacOs() && context.IsWorkloadInstalled("ios");

    public override void Run(BuildContext context)
    {
        var configuration = context.DotNetPackSettings.Configuration.ToString();
        context.Shell("bash", $"native/monogame/opengl/build-ios-xcframework.sh {configuration}");
        context.DotNetPack(context.GetProjectPath(ProjectType.Framework, "iOS.OpenGLES"), context.DotNetPackSettings);
    }
}
