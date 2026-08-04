// BED-172 — KayleighPlayer module rules (UE 5.8).

using UnrealBuildTool;

public class KayleighPlayer : ModuleRules
{
	public KayleighPlayer(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				"CoreUObject",
				"Engine",
				"InputCore",
				"EnhancedInput",
				"AudioCapture",
			});

		PrivateDependencyModuleNames.AddRange(
			new string[]
			{
				"AudioMixer",
			});
	}
}
