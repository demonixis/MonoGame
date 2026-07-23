using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Build Android OpenGLES")]
[IsDependentOn(typeof(BuildNativeTask))]
public sealed class BuildAndroidOpenGLESTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) =>
        Environment.Version.Major >= 10 &&
        context.IsRunningOnLinux() &&
        RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
        context.IsWorkloadInstalled("android");

    public override void Run(BuildContext context)
    {
        var configuration = context.DotNetPackSettings.Configuration.ToString();
        context.Shell("bash", $"native/monogame/android-opengles/build-android.sh {configuration}");
        context.DotNetPack(context.GetProjectPath(ProjectType.Framework, "Android.OpenGLES"), context.DotNetPackSettings);
    }
}
