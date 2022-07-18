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

		public enum SynthMode {
			NONE,
			CRAFT,
			UPGRADE
		}

		[HarmonyPatch]
		public class FasterCraft {

			public static UIStrengthening strengthMenu = null;
			public static UICraftMenu craftMenu = null;
			public static UICraftResult craftResult = null;
			public static UICraftSuccess success = null;
			public static CursorController cursor = null;
			public static CursorLinker lastSelect = null;
			public static CursorLinker okBtn = null;
			public static bool canSkip = false;
			public static bool startSynthAnim = false;
			public static bool doneUpgrade = false;
			public static SynthMode synthMode = SynthMode.NONE;

			public static void TrytoSearch(SynthMode mode) {
				CursorLinkConnector parent;
				switch (mode) {
					case SynthMode.CRAFT: {
							parent = craftMenu;
							okBtn = parent.GetComponentInChildren<UICraftSynthesisOK>(true)?.GetComponent<ButtonLinker>();
							if (okBtn == null) {
								Log.LogError("Failed to find OK Button!");
								return;
							}
							break;
						}
					case SynthMode.UPGRADE: {
							parent = strengthMenu;
							okBtn = parent.GetComponentInChildren<UIStrengtheningOK>(true)?.GetComponent<ButtonLinker>();
							if (okBtn == null) {
								Log.LogError("Failed to find OK Button!");
								return;
							}
							break;
						}
					default: {
							Log.LogError("No SynthMode defined!");
							return;
						}
				}

				success = parent.GetComponentInChildren<UICraftSuccess>(true);
				if (success == null) {
					Log.LogError("Failed to find success Component!");
					return;
				}
				cursor = parent.GetComponentInChildren<CursorController>(true);
				if (cursor == null) {
					Log.LogError("Failed to find cursor Component!");
					return;
				}
			}

			[HarmonyPatch(typeof(UICraftMenu), "Start")]
			[HarmonyPostfix]
			public static void StartCraft(UICraftMenu __instance) {
				synthMode = SynthMode.CRAFT;
				craftMenu = __instance;
				canSkip = true;
				Log.LogInfo("Starting crafting shorcut");
			}

			[HarmonyPatch(typeof(UICraftMenu), "OnDestroy")]
			[HarmonyPostfix]
			public static void EndCraft() {
				synthMode = SynthMode.NONE;
				craftMenu = null;
				craftResult = null;
				success = null;
				cursor = null;
				lastSelect = null;
				okBtn = null;
				canSkip = false;
				startSynthAnim = false;
				doneUpgrade = false;
				Log.LogInfo("Ending crafting shorcut");
			}

			[HarmonyPatch(typeof(UIStrengthening), "Start")]
			[HarmonyPostfix]
			public static void StartUpgrade(UIStrengthening __instance) {
				synthMode = SynthMode.UPGRADE;
				strengthMenu = __instance;
				canSkip = true;
				Log.LogInfo("Starting upgrade shorcut");
			}

			[HarmonyPatch(typeof(UIStrengthening), "OnDestroy")]
			[HarmonyPostfix]
			public static void EndUpgrade() {
				synthMode = SynthMode.NONE;
				strengthMenu = null;
				craftResult = null;
				success = null;
				cursor = null;
				lastSelect = null;
				okBtn = null;
				canSkip = false;
				startSynthAnim = false;
				doneUpgrade = false;
				Log.LogInfo("Ending upgrade shorcut");
			}

			[HarmonyPatch(typeof(UICraftResult), "Start")]
			[HarmonyPostfix]
			public static void ResultStart(UICraftResult __instance) {
				if (synthMode == SynthMode.NONE) {
					return;
				}
				craftResult = __instance;
			}

			[HarmonyPatch(typeof(UICraftResult), "Update")]
			[HarmonyPostfix]
			public static void ResultUpdate() {
				if (success == null || cursor == null || okBtn == null) {
					Log.LogInfo($"Something is missing");
					TrytoSearch(synthMode);
					return;
				}

				//success.IsDone() start at false
				Log.LogInfo($"canSkip:{canSkip}\tresultplay:{craftResult.isPlaying}\tsuccess:{success.IsDone()}");

				if ((craftResult.isPlaying || !success.IsDone()) && !canSkip) {
					if (startSynthAnim == false) {
						lastSelect = cursor?.NowFocusObject;
					}
					startSynthAnim = true;
				}
				//else if (!craftResult.isPlaying && success.IsDone() && !canSkip) { 
				//	canSkip = true;
				//}

				if (!craftResult.isPlaying && success.IsDone()) {
					canSkip = true;
					if (startSynthAnim) {
						startSynthAnim = false;
						if (synthMode == SynthMode.UPGRADE) {
							doneUpgrade = true;
						}
					}
				}

				//Log.LogInfo($"cursor focus: {cursor.NowFocusObject.gameObject.name}\nok button: {okBtn.gameObject.name}");

				if (doneUpgrade && lastSelect != null && cursor.NowFocusObject.gameObject.name == okBtn.gameObject.name) {
					doneUpgrade = false;
					cursor.NowFocusObject = lastSelect;
					Log.LogInfo("Try to change back position");
				}
			}

			[HarmonyPatch(typeof(GameMain), "Update")]
			[HarmonyPostfix]
			public static void InputUpdate() {
				if (RF5Input.Pad.End(RF5Input.Key.PS) && canSkip && !startSynthAnim) {
					if (synthMode == SynthMode.CRAFT && craftMenu != null) {
						Log.LogInfo("Crafting...");
						canSkip = false;
						craftMenu.DoSynthesis(CraftManager.DualWorkType);
					}
					else if (synthMode == SynthMode.UPGRADE && strengthMenu != null && !doneUpgrade) {
						Log.LogInfo("Upgrading...");
						canSkip = false;
						strengthMenu.DoStrengthening();
					}
				}
			}
		}
	}
}
