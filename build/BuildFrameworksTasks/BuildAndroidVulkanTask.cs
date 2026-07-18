using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Build Android Vulkan")]
[IsDependentOn(typeof(BuildShadersVulkanTask))]
public sealed class BuildAndroidVulkanTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) =>
        context.IsRunningOnLinux() &&
        RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
        context.IsWorkloadInstalled("android");

    public override void Run(BuildContext context)
    {
        var configuration = context.DotNetPackSettings.Configuration.ToString();
        context.Shell("bash", $"native/monogame/android-vulkan/build-android.sh {configuration}");
        context.DotNetPack(context.GetProjectPath(ProjectType.Framework, "Android.Vulkan"), context.DotNetPackSettings);
    }
}
