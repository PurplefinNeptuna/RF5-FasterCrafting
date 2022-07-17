using BepInEx;
using BepInEx.Logging;
using BepInEx.IL2CPP;
using HarmonyLib;

namespace FasterCrafting {
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BasePlugin {

		internal static new ManualLogSource Log;
		public override void Load() {
			// Plugin startup logic

			Plugin.Log = base.Log;
			Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

			Harmony.CreateAndPatchAll(typeof(FasterCraft));
		}

		[HarmonyPatch]
		public class FasterCraft {

			public static UICraftMenu craftMenu = null;
			public static UIStrengthening strengthMenu = null;
			public static UICraftSuccess success = null;
			public static CursorController cursor = null;
			public static bool startAnim = false;
			public static bool doneUpgrade = false;
			public static CursorLinker lastSelect = null;

			[HarmonyPatch(typeof(UICraftMenu), "Start")]
			[HarmonyPostfix]
			public static void StartCraft(UICraftMenu __instance) {
				craftMenu = __instance;
				Log.LogInfo("Starting crafting shorcut");
			}

			[HarmonyPatch(typeof(UICraftMenu), "OnDestroy")]
			[HarmonyPostfix]
			public static void EndCraft() {
				craftMenu = null;
				Log.LogInfo("Ending crafting shorcut");
			}

			[HarmonyPatch(typeof(UIStrengthening), "Start")]
			[HarmonyPostfix]
			public static void StartUpgrade(UIStrengthening __instance) {
				Log.LogInfo("Starting upgrade shorcut");
				strengthMenu = __instance;
			}

			[HarmonyPatch(typeof(UIStrengthening), "OnDestroy")]
			[HarmonyPostfix]
			public static void EndUpgrade() {
				strengthMenu = null;
				success = null;
				cursor = null;
				Log.LogInfo("Ending upgrade shorcut");
			}

			[HarmonyPatch(typeof(UICraftResult), "Update")]
			[HarmonyPostfix]
			public static void ResultUpdate() {
				if (strengthMenu != null && success != null && cursor != null) {
					if (!success.IsDone() && !startAnim) {
						startAnim = true;
						lastSelect = cursor.NowFocusObject;
						Log.LogInfo("Start upgrade anim");
					}
					else if (success.IsDone() && startAnim) {
						startAnim = false;
						doneUpgrade = true;
						Log.LogInfo("Done upgrade anim");
					}
				}
				if (doneUpgrade && lastSelect != null && lastSelect != cursor.NowFocusObject) {
					doneUpgrade = false;
					cursor.NowFocusObject = lastSelect;
					Log.LogInfo("Try to change back position");
				}
			}

			[HarmonyPatch(typeof(GameMain), "Update")]
			[HarmonyPostfix]
			public static void InputUpdate() {
				if (RF5Input.Pad.End(RF5Input.Key.PS)) {
					if (craftMenu != null) {
						Log.LogInfo("Crafting...");
						craftMenu.DoSynthesis(CraftManager.DualWorkType);
					}
					else if (strengthMenu != null) {
						Log.LogInfo("Upgrading...");
						success = strengthMenu.GetComponentInChildren<UICraftSuccess>(true);
						if (success == null) {
							Log.LogError("Failed to find success Component!");
						}
						cursor = strengthMenu.GetComponentInChildren<CursorController>(true);
						if (cursor == null) {
							Log.LogError("Failed to find cursor Component!");
						}
						strengthMenu.DoStrengthening();
					}
				}
			}
		}
	}
}
