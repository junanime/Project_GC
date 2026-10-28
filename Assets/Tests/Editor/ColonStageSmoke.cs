namespace Vampire.Tests.Editor
{
    // Preserve the established test entry point; stage travel now uses the amphibious capsule.
    public static class ColonStageSmoke
    {
        public static void Run()=>CapsuleStageSmoke.Run();
        public static void RunChecksOnly()=>CapsuleStageSmoke.RunChecksOnly();
    }
}
