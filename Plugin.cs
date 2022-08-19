using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
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
			Harmony.CreateAndPatchAll(typeof(Warper));
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
			public static ButtonWorkBase okBtn = null;
			//public static bool canSkip = false;
			public static bool startSynthAnim = false;
			public static bool startResultAnim = false;
			public static bool doneUpgrade = false;
			public static SynthMode synthMode = SynthMode.NONE;

			public static bool TrytoSearch(SynthMode mode) {
				CursorLinkConnector parent;
				switch (mode) {
					case SynthMode.CRAFT: {
							parent = craftMenu;
							okBtn = parent.GetComponentInChildren<UICraftSynthesisOK>(true);
							if (okBtn == null) {
								Log.LogError("Failed to find OK Button!");
								return false;
							}
							break;
						}
					case SynthMode.UPGRADE: {
							parent = strengthMenu;
							okBtn = parent.GetComponentInChildren<UIStrengtheningOK>(true);
							if (okBtn == null) {
								Log.LogError("Failed to find OK Button!");
								return false;
							}
							break;
						}
					default: {
							Log.LogError("No SynthMode defined!");
							return false;
						}
				}

				success = parent.GetComponentInChildren<UICraftSuccess>(true);
				if (success == null) {
					Log.LogError("Failed to find success Component!");
					return false;
				}
				cursor = parent.GetComponentInChildren<CursorController>(true);
				if (cursor == null) {
					Log.LogError("Failed to find cursor Component!");
					return false;
				}

				return true;
			}

			[HarmonyPatch(typeof(UICraftMenu), "Start")]
			[HarmonyPostfix]
			public static void StartCraft(UICraftMenu __instance) {
				synthMode = SynthMode.CRAFT;
				craftMenu = __instance;
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
				startSynthAnim = false;
				doneUpgrade = false;
				Log.LogInfo("Ending crafting shorcut");
			}

			[HarmonyPatch(typeof(UIStrengthening), "Start")]
			[HarmonyPostfix]
			public static void StartUpgrade(UIStrengthening __instance) {
				synthMode = SynthMode.UPGRADE;
				strengthMenu = __instance;
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
					bool found = TrytoSearch(synthMode);
					if (!found) return;
				}

				//success.IsDone() start at false
				//Log.LogInfo($"canSkip:{canSkip}\tresultplay:{craftResult.isPlaying}\tsuccess:{success.IsDone()}");
				//Log.LogInfo($"cursor focus: {cursor.NowFocusObject.gameObject.name}\nok button: {okBtn.gameObject.name}");

				if (craftResult.isPlaying) {
					startSynthAnim = true;
				}

				if (!craftResult.isPlaying && startSynthAnim && success.isActiveAndEnabled) {
					startSynthAnim = false;
					startResultAnim = true;
				}

				if (!success.isActiveAndEnabled && startResultAnim) {
					startResultAnim = false;
					if (synthMode == SynthMode.UPGRADE) {
						doneUpgrade = true;
					}
				}

				if (doneUpgrade && lastSelect != null) {
					doneUpgrade = false;
					cursor.NextFocusObject = lastSelect;
					cursor.NowFocusObject = lastSelect;
					Log.LogInfo("Try to change back position");
				}
			}

			[HarmonyPatch(typeof(UIMainController), "Update")]
			[HarmonyPostfix]
			public static void InputUpdate() {
				if (RF5Input.Pad.End(RF5Input.Key.PS) && !startResultAnim && !startSynthAnim) {
					if (synthMode == SynthMode.CRAFT && craftMenu != null) {
						Log.LogInfo("Crafting...");
						lastSelect = cursor?.NowFocusObject;
						okBtn?.ButtonWork(RF5Input.Key.A);
					}
					else if (synthMode == SynthMode.UPGRADE && strengthMenu != null && !doneUpgrade) {
						Log.LogInfo("Upgrading...");
						lastSelect = cursor?.NowFocusObject;
						okBtn?.ButtonWork(RF5Input.Key.A);
					}
				}
			}
		}

		[HarmonyPatch]
		public class Warper {
			[HarmonyPatch(typeof(UICraftMenu), nameof(UICraftMenu.CanRequestCraftNum))]
			[HarmonyPostfix]
			public static void CanRequestPatch(UICraftMenu __instance, ref bool __result) {
				if (__instance.CraftNum == 1 || __instance.CraftNum == __instance.CraftNumMax) {
					__result = true;
				}
			}

			[HarmonyPatch(typeof(UICraftMenu), nameof(UICraftMenu.RequestCraftNum))]
			[HarmonyPrefix]
			public static void RequestPatch(UICraftMenu __instance, ref bool isLeft) {
				if (__instance.CraftNum == 1 && isLeft) {
					__instance.CraftNum = __instance.CraftNumMax;
					isLeft = false;
				}
				else if (__instance.CraftNum == __instance.CraftNumMax && !isLeft) {
					__instance.CraftNum = 1;
					isLeft = true;
				}
			}
		}
	}
}
