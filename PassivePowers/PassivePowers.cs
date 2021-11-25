using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using UnityEngine.UI;

namespace PassivePowers
{
	[BepInPlugin(ModGUID, ModName, ModVersion)]
	public class PassivePowers : BaseUnityPlugin
	{
		private const string ModName = "Passive Powers";
		private const string ModVersion = "1.0.1";
		private const string ModGUID = "org.bepinex.plugins.passivepowers";

		private static readonly ConfigSync configSync = new(ModGUID) { DisplayName = ModName };

		private static readonly Assembly? bepinexConfigManager = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ConfigurationManager");
		private static readonly Type? configManagerType = bepinexConfigManager?.GetType("ConfigurationManager.ConfigurationManager");
		private static readonly object? configManager = configManagerType == null ? null : BepInEx.Bootstrap.Chainloader.ManagerObject.GetComponent(configManagerType);
		private static void reloadConfigDisplay() => configManagerType?.GetMethod("BuildSettingList")!.Invoke(configManager, Array.Empty<object>());

		private const int bossPowerCount = 5;

		private static ConfigEntry<Toggle> serverConfigLocked = null!;
		public static ConfigEntry<int> maximumBossPowers = null!;
		private static ConfigEntry<Toggle> activeBossPowers = null!;
		private static ConfigEntry<int> activeBossPowerCooldown = null!;
		private static ConfigEntry<int> activeBossPowerDuration = null!;
		private static ConfigEntry<int> activeBossPowerDepletion = null!;
		private static readonly ConfigEntry<KeyboardShortcut>[] bossPowerKeys = new ConfigEntry<KeyboardShortcut>[bossPowerCount];
		public static PowerConfig<int> runStaminaReduction = null!;
		public static PowerConfig<int> jumpStaminaReduction = null!;
		public static PowerConfig<int> movementSpeedIncrease = null!;
		public static PowerConfig<int> treeDamageIncrease = null!;
		public static PowerConfig<int> miningDamageIncrease = null!;
		public static PowerConfig<int> phyiscalDamageReduction = null!;
		public static PowerConfig<int> healthRegenIncrease = null!;
		public static PowerConfig<int> tailWindChance = null!;
		public static PowerConfig<int> windSpeedModifier = null!;
		public static PowerConfig<int> elementalDamageReduction = null!;
		public static PowerConfig<int> bonusFireDamage = null!;

		private static float remainingCooldown => Player.m_localPlayer.m_guardianPowerCooldown;

		private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
		{
			ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

			SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
			syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

			return configEntry;
		}

		private PowerConfig<T> powerConfig<T>(string powerName, string group, string name, T valuePassive, T valueActive, ConfigDescription description, bool synchronizedSetting = true)
		{
			return new()
			{
				powerName = powerName,
				passive = config(group, "Passive: " + name, valuePassive, description, synchronizedSetting),
				active = config(group, "Active: " + name, valueActive, new ConfigDescription(description.Description, description.AcceptableValues, activeBossPowerSettingAttributes), synchronizedSetting)
			};
		}

		private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

		private readonly ConfigurationManagerAttributes activeBossPowerSettingAttributes = new();

		public void Awake()
		{
			serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.Off, "If on, the configuration is locked and can be changed by server admins only.");
			maximumBossPowers = config("1 - General", "Maximum boss powers", 2, new ConfigDescription("Sets the maximum number of boss powers that can be active at the same time.", new AcceptableValueRange<int>(1, 5)));
			activeBossPowers = config("2 - Active Powers", "Boss powers can be activated", Toggle.Off, "Boss powers can still be activated.");
			activeBossPowerCooldown = config("2 - Active Powers", "Cooldown for boss powers (seconds)", 600, new ConfigDescription("Cooldown after activating one of the boss powers. Cooldown is shared between all boss powers.", null, activeBossPowerSettingAttributes));
			activeBossPowerCooldown.SettingChanged += activeBossPowerSettingChanged;
			activeBossPowerDuration = config("2 - Active Powers", "Duration for active boss powers (seconds)", 30, new ConfigDescription("Duration of the buff from activating boss powers.", null, activeBossPowerSettingAttributes));
			activeBossPowerDuration.SettingChanged += activeBossPowerSettingChanged;
			activeBossPowerDepletion = config("2 - Active Powers", "Power loss duration after boss power activation (seconds)", 180, new ConfigDescription("Disables the passive effect of the boss power for the specified duration after the active effect ends.", null, activeBossPowerSettingAttributes));
			activeBossPowerDepletion.SettingChanged += activeBossPowerSettingChanged;
			runStaminaReduction = powerConfig(Power.Eikthyr, "2 - Eikthyr", "Run stamina reduction (percentage)", 15, 40, new ConfigDescription("Reduces the stamina usage while running.", new AcceptableValueRange<int>(0, 100)));
			jumpStaminaReduction = powerConfig(Power.Eikthyr, "2 - Eikthyr", "Jump stamina reduction (percentage)", 15, 40, new ConfigDescription("Reduces the stamina usage while jumping.", new AcceptableValueRange<int>(0, 100)));
			movementSpeedIncrease = powerConfig(Power.Eikthyr, "2 - Eikthyr", "Movement speed increase (percentage)", 0, 35, new ConfigDescription("Increases the movement speed.", new AcceptableValueRange<int>(0, 100)));
			treeDamageIncrease = powerConfig(Power.TheElder, "3 - The Elder", "Damage increase to trees (percentage)", 20, 200, new ConfigDescription("Increases the damage done to trees.", new AcceptableValueRange<int>(0, 500)));
			miningDamageIncrease = powerConfig(Power.TheElder, "3 - The Elder", "Damage increase while mining (percentage)", 20, 200, new ConfigDescription("Increases the damage done to veins and stone.", new AcceptableValueRange<int>(0, 500)));
			phyiscalDamageReduction = powerConfig(Power.Bonemass, "4 - Bonemass", "Physical damage reduction (percentage)", 10, 85, new ConfigDescription("Reduces the phyiscal damage taken.", new AcceptableValueRange<int>(0, 100)));
			healthRegenIncrease = powerConfig(Power.Bonemass, "4 - Bonemass", "Health regeneration increase (percentage)", 10, 200, new ConfigDescription("Increases the health regeneration.", new AcceptableValueRange<int>(0, 500)));
			tailWindChance = powerConfig(Power.Moder, "5 - Moder", "Chance increase for tailwind (percentage)", 20, 100, new ConfigDescription("Increases the chance to get tailwind while sailing.", new AcceptableValueRange<int>(0, 100)));
			windSpeedModifier = powerConfig(Power.Moder, "5 - Moder", "Modifies the wind speed (percentage)", 35, 200, new ConfigDescription("Increases the speed of tailwind, decreases the speed of headwind.", new AcceptableValueRange<int>(0, 500)));
			elementalDamageReduction = powerConfig(Power.Yagluth, "6 - Yagluth", "Elemental damage reduction (percentage)", 10, 85, new ConfigDescription("Reduces the elemental damage taken.", new AcceptableValueRange<int>(0, 100)));
			bonusFireDamage = powerConfig(Power.Yagluth, "6 - Yagluth", "Bonus fire damage (percentage)", 10, 100, new ConfigDescription("Adds a percentage of your weapon damage as bonus fire damage to your attacks.", new AcceptableValueRange<int>(0, 500)));

			for (int i = 0; i < bossPowerCount; ++i)
			{
				bossPowerKeys[i] = config("2 - Active Powers", $"Shortcut for boss power {i + 1}", new KeyboardShortcut(KeyCode.Alpha1 + i, KeyCode.LeftAlt), new ConfigDescription($"Keyboard shortcut to activate the {i + 1}. boss power.", null, activeBossPowerSettingAttributes), false);
			}

			configSync.AddLockingConfigEntry(serverConfigLocked);

			activeBossPowerSettingAttributes.Browsable = activeBossPowers.Value == Toggle.On;
			activeBossPowers.SettingChanged += (_, _) =>
			{
				activeBossPowerSettingAttributes.Browsable = activeBossPowers.Value == Toggle.On;
				reloadConfigDisplay();
			};

			Assembly assembly = Assembly.GetExecutingAssembly();
			Harmony harmony = new(ModGUID);
			harmony.PatchAll(assembly);
		}

		private static void activeBossPowerSettingChanged(object o, EventArgs e)
		{
			foreach (StatusEffect statusEffect in ObjectDB.instance.m_StatusEffects)
			{
				if (statusEffect.name.StartsWith("PassivePowers"))
				{
					if (statusEffect.name.Contains("Depletion"))
					{
						statusEffect.m_ttl = activeBossPowerDepletion.Value;
					}
					else
					{
						statusEffect.m_cooldown = activeBossPowerCooldown.Value;
						statusEffect.m_ttl = activeBossPowerDuration.Value;
					}
				}
			}
		}

		private void Update()
		{
			if (activeBossPowers.GetToggle())
			{
				for (int i = 0; i < bossPowerKeys.Length; ++i)
				{
					if (bossPowerKeys[i].Value.IsDown() && Player.m_localPlayer.TakeInput())
					{
						List<string> powers = Utils.getPassivePowers(Player.m_localPlayer);
						if (i < powers.Count)
						{
							string power = powers[i];

							if (ObjectDB.instance.GetStatusEffect("PassivePowers " + power) is StatusEffect power_se)
							{
								Player.m_localPlayer.m_guardianSE = power_se;
								Player.m_localPlayer.StartGuardianPower();
							}
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Player), nameof(Player.Update))]
		private class Patch_Player_Update
		{
			public static bool CheckKeyDown(bool activeKey) => activeKey && !bossPowerKeys.Any(powerKey => powerKey.Value.IsDown());

			private static readonly MethodInfo CheckInputKey = AccessTools.DeclaredMethod(typeof(Patch_Player_Update), nameof(CheckKeyDown));
			private static readonly MethodInfo InputKey = AccessTools.DeclaredMethod(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) });
			private static readonly MethodInfo GuardianPowerStart = AccessTools.DeclaredMethod(typeof(Player), nameof(Player.StartGuardianPower));

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				foreach (CodeInstruction instruction in instructions)
				{
					if (instruction.opcode == OpCodes.Call && instruction.OperandIs(GuardianPowerStart))
					{
						yield return new CodeInstruction(OpCodes.Pop);
						yield return new CodeInstruction(OpCodes.Ldc_I4_0);
					}
					else
					{
						yield return instruction;
					}
					if (instruction.opcode == OpCodes.Call && instruction.OperandIs(InputKey))
					{
						yield return new CodeInstruction(OpCodes.Call, CheckInputKey);
					}
				}
			}
		}

		[HarmonyPatch]
		public class Patch_ObjectDB_Awake_CopyOtherDB
		{
			private static IEnumerable<MethodBase> TargetMethods() => new[]
			{
				AccessTools.DeclaredMethod(typeof(ObjectDB), nameof(ObjectDB.Awake)),
				AccessTools.DeclaredMethod(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)),
			};

			private static void Postfix(ObjectDB __instance)
			{
				List<StatusEffect> newEffects = new();
				foreach (StatusEffect original_se in __instance.m_StatusEffects.Where(se => se.name.StartsWith("GP_")))
				{
					string name = "PassivePowers " + original_se.name;
					if (__instance.m_StatusEffects.All(se => se.name != name))
					{
						StatusEffect power_se = ScriptableObject.CreateInstance<StatusEffect>();
						power_se.name = name;
						power_se.m_activationAnimation = original_se.m_activationAnimation;
						power_se.m_startEffects = original_se.m_startEffects;
						power_se.m_startMessage = original_se.m_startMessage;
						power_se.m_startMessageType = original_se.m_startMessageType;
						power_se.m_icon = original_se.m_icon;
						power_se.m_name = original_se.m_name;
						power_se.m_cooldown = activeBossPowerCooldown.Value;
						power_se.m_ttl = activeBossPowerDuration.Value;
						newEffects.Add(power_se);

						StatusEffect depletion_se = ScriptableObject.CreateInstance<StatusEffect>();
						depletion_se.name = "PassivePowers Depletion " + original_se.name;
						depletion_se.m_startMessage = $"The power of {original_se.m_name} has been depleted and will recharge over time.";
						depletion_se.m_startMessageType = original_se.m_startMessageType;
						depletion_se.m_icon = original_se.m_icon;
						depletion_se.m_name = "Depleted";
						depletion_se.m_ttl = activeBossPowerDepletion.Value;
						EffectList.EffectData effectData = new()
						{
							m_prefab = new GameObject(original_se.name + " Depletion Prefab")
						};
						effectData.m_prefab.AddComponent<PowerDepletionBehaviour>().statusEffect = depletion_se.name;
						power_se.m_stopEffects.m_effectPrefabs = new[] { effectData };
						newEffects.Add(depletion_se);
					}
				}
				__instance.m_StatusEffects.AddRange(newEffects);
			}
		}

		[HarmonyPatch(typeof(ItemStand), nameof(ItemStand.IsGuardianPowerActive))]
		private class Patch_ItemStand_IsGuardianPowerActive
		{
			private static void Postfix(out bool __result)
			{
				__result = false;
			}
		}

		private static void UpdateStatusEffectTooltip(StatusEffect statusEffect)
		{
			statusEffect.m_tooltip = "";

			List<string> powers = new();

			void addTooltips(Func<PowerConfig<int>, int> value, string type)
			{
				switch (statusEffect.name)
				{
					case Power.Eikthyr:
					{
						if (value(runStaminaReduction) > 0)
						{
							powers.Add($"{type}: Reduces stamina usage while sprinting by {value(runStaminaReduction)}%");
						}
						if (value(jumpStaminaReduction) > 0)
						{
							powers.Add($"{type}: Reduces stamina usage while jumping by {value(jumpStaminaReduction)}%");
						}
						if (value(movementSpeedIncrease) > 0)
						{
							powers.Add($"{type}: Increases movement speed by {value(movementSpeedIncrease)}%");
						}
						break;
					}
					case Power.TheElder:
					{
						if (value(treeDamageIncrease) > 0)
						{
							powers.Add($"{type}: Increases damage done to trees by {value(treeDamageIncrease)}%");
						}
						if (value(miningDamageIncrease) > 0)
						{
							powers.Add($"{type}: Increases damage done to stones and veins by {value(miningDamageIncrease)}%");
						}
						break;
					}
					case Power.Bonemass:
					{
						if (value(phyiscalDamageReduction) > 0)
						{
							powers.Add($"{type}: Reduces physical damage taken by {value(phyiscalDamageReduction)}%");
						}
						if (value(healthRegenIncrease) > 0)
						{
							powers.Add($"{type}: Increases health regeneration by {value(healthRegenIncrease)}%");
						}
						break;
					}
					case Power.Moder:
					{
						if (value(tailWindChance) > 0)
						{
							powers.Add($"{type}: Adds a {value(tailWindChance)}% chance to have tailwind while sailing");
						}
						if (value(windSpeedModifier) > 0)
						{
							powers.Add($"{type}: Modifies wind speed by up to {value(windSpeedModifier)}% while sailing");
						}
						break;
					}
					case Power.Yagluth:
					{
						if (value(elementalDamageReduction) > 0)
						{
							powers.Add($"{type}: Reduces elemental damage taken by {value(elementalDamageReduction)}%");
						}
						if (value(bonusFireDamage) > 0)
						{
							powers.Add($"{type}: Deals {value(bonusFireDamage)}% increased damage as fire");
						}
						break;
					}
				}
			}

			addTooltips(p => p.passive.Value, "Passive");
			if (activeBossPowers.GetToggle())
			{
				powers.Add("");
				powers.Add($"Can be activated every {Utils.getHumanFriendlyTime(activeBossPowerCooldown.Value)} to gain the following effect for {Utils.getHumanFriendlyTime(activeBossPowerDuration.Value)}");
				addTooltips(p => p.active.Value, "Active");
				powers.Add($"Activating the power will disable the passive effect for {Utils.getHumanFriendlyTime(activeBossPowerDepletion.Value)} afterwards");
			}

			statusEffect.m_tooltip = string.Join("\n", powers);
		}

		[HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))]
		private class Patch_SE_Stats_GetTooltipString
		{
			private static void Postfix(SE_Stats __instance, ref string __result)
			{
				if (__instance.name.StartsWith("GP_"))
				{
					__result = __instance.m_tooltip;
				}
			}
		}

		[HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
		private class Patch_ItemStand_GetHoverText
		{
			private static void Prefix(ItemStand __instance)
			{
				if (__instance.m_guardianPower is StatusEffect statusEffect)
				{
					UpdateStatusEffectTooltip(statusEffect);
				}
			}

			private static string UpdateActivationStatus(string str, ItemStand itemStand)
			{
				if (Utils.getPassivePowers(Player.m_localPlayer).Contains(itemStand.m_guardianPower.name))
				{
					return str.Replace(activateStr, "$guardianstone_hook_deactivate");
				}
				return str;
			}

			private static readonly MethodInfo ActivationStatusUpdater = AccessTools.DeclaredMethod(typeof(Patch_ItemStand_GetHoverText), nameof(UpdateActivationStatus));
			private const string activateStr = "$guardianstone_hook_activate";

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				foreach (CodeInstruction instruction in instructions)
				{
					yield return instruction;
					if (instruction.opcode == OpCodes.Ldstr && ((string)instruction.operand).Contains(activateStr))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, ActivationStatusUpdater);
					}
				}
			}
		}

		[HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact))]
		private class Patch_ItemStand_Interact
		{
			private static string UpdateActivationStatus(string str, ItemStand itemStand)
			{
				if (Utils.getPassivePowers(Player.m_localPlayer).Contains(itemStand.m_guardianPower.name))
				{
					return str.Replace(activateStr, "$guardianstone_hook_power_deactivate");
				}
				return str;
			}

			private static readonly MethodInfo ActivationStatusUpdater = AccessTools.DeclaredMethod(typeof(Patch_ItemStand_Interact), nameof(UpdateActivationStatus));
			private const string activateStr = "$guardianstone_hook_power_activate";

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				foreach (CodeInstruction instruction in instructions)
				{
					yield return instruction;
					if (instruction.opcode == OpCodes.Ldstr && ((string)instruction.operand).Contains(activateStr))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, ActivationStatusUpdater);
					}
				}
			}
		}

		[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.AddActiveEffects))]
		private class Patch_TextsDialog_AddActiveEffects
		{
			private static void DisplayGuardianPowers(StringBuilder stringBuilder)
			{
				List<string> powers = Utils.getPassivePowers(Player.m_localPlayer);
				if (powers.Count == 0)
				{
					return;
				}

				stringBuilder.Append("<color=yellow>" + Localization.instance.Localize("$inventory_selectedgp") + "</color>\n");
				foreach (string power in powers)
				{
					if (ObjectDB.instance.GetStatusEffect(power) is StatusEffect se)
					{
						UpdateStatusEffectTooltip(se);

						stringBuilder.Append("<color=orange>" + Localization.instance.Localize(se.m_name) + "</color>\n");
						stringBuilder.Append(Localization.instance.Localize(se.GetTooltipString()));
						stringBuilder.Append("\n\n");
					}
				}
			}

			private static readonly MethodInfo GuardianPowerGetter = AccessTools.DeclaredMethod(typeof(Player), nameof(Player.GetGuardianPowerHUD));
			private static readonly MethodInfo GuardianPowerWriter = AccessTools.DeclaredMethod(typeof(Patch_TextsDialog_AddActiveEffects), nameof(DisplayGuardianPowers));

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				List<CodeInstruction> instructionList = instructions.ToList();
				int i = 0;
				for (; i < instructionList.Count; ++i)
				{
					CodeInstruction instruction = instructionList[i];
					if (instruction.opcode == OpCodes.Callvirt && instruction.OperandIs(GuardianPowerGetter))
					{
						break;
					}
				}

				int leadingInstructionsEnd = i - 3; // 2 args, Player class load
				Label? label = null;

				// skip the whole branch
				for (; i < instructionList.Count; ++i)
				{
					if (instructionList[i].Branches(out label))
					{
						break;
					}
				}

				CodeInstruction stringBuilderLoad = instructionList[i + 1];
				stringBuilderLoad.labels = instructionList[leadingInstructionsEnd].labels;

				return instructionList.Take(leadingInstructionsEnd)
					.Concat(new[] { stringBuilderLoad, new CodeInstruction(OpCodes.Call, GuardianPowerWriter) })
					.Concat(instructionList.Skip(leadingInstructionsEnd).SkipWhile(instruction => !instruction.labels.Contains((Label)label!)));
			}
		}

		private class HudPower
		{
			public Transform root = null!;
			public Text name = null!;
			public Image icon = null!;
		}

		private static readonly List<HudPower> hudPowers = new();

		[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
		private class Patch_Hud_Awake
		{
			private static void Postfix(Hud __instance)
			{
				hudPowers.Clear();

				static void RegisterHudPower(Transform root)
				{
					hudPowers.Add(new HudPower
					{
						root = root,
						icon = root.Find("Icon").GetComponent<Image>(),
						name = root.Find("Name").GetComponent<Text>()
					});
				}

				RectTransform gpRoot = __instance.m_gpRoot;
				GameObject powerContainer = new("powerContainer 1")
				{
					transform =
					{
						parent = gpRoot,
						localPosition = Vector3.zero
					}
				};
				for (int i = gpRoot.childCount - 1; i >= 0; --i)
				{
					Transform child = gpRoot.GetChild(i);
					if (child != __instance.m_gpCooldown.transform && child.name != "TimeBar")
					{
						child.SetParent(powerContainer.transform, true);
					}
				}
				RegisterHudPower(powerContainer.transform);
				for (int i = 2; i <= bossPowerCount; ++i)
				{
					GameObject power = Instantiate(powerContainer, gpRoot);
					power.name = "powerContainer " + i;
					power.transform.position += new Vector3(0, 70 * (i - 1), 0);
					RegisterHudPower(power.transform);
				}
			}
		}

		[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateGuardianPower))]
		private class Patch_Hud_UpdateGuardianPower
		{
			private static bool Prefix(Hud __instance)
			{
				int index = 0;
				foreach (string s in Utils.getPassivePowers(Player.m_localPlayer))
				{
					if (ObjectDB.instance.GetStatusEffect(s) is StatusEffect se)
					{
						hudPowers[index].root.gameObject.SetActive(true);
						hudPowers[index].name.text = Localization.instance.Localize(se.m_name);
						hudPowers[index].icon.sprite = se.m_icon;
						hudPowers[index++].icon.color = remainingCooldown <= 0f ? Color.white : new Color(1f, 0.0f, 1f, 0.0f);
					}
				}

				__instance.m_gpRoot.gameObject.SetActive(index > 0);

				for (; index < hudPowers.Count; ++index)
				{
					hudPowers[index].root.gameObject.SetActive(false);
				}

				__instance.m_gpCooldown.text = activeBossPowers.GetToggle() ? remainingCooldown > 0f ? StatusEffect.GetTimeString(remainingCooldown) : Localization.instance.Localize("$hud_ready") : Localization.instance.Localize("$piece_guardstone_inactive");
				return false;
			}
		}
	}
}
