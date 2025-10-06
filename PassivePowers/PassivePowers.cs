using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LocalizationManager;
using ServerSync;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PassivePowers;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]
public class PassivePowers : BaseUnityPlugin
{
	private const string ModName = "Passive Powers";
	private const string ModVersion = "1.1.3";
	private const string ModGUID = "org.bepinex.plugins.passivepowers";

	private static readonly ConfigSync configSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	public static object? configManager;
	private static void reloadConfigDisplay() => configManager?.GetType().GetMethod("BuildSettingList")!.Invoke(configManager, Array.Empty<object>());

	private const int bossPowerCount = 7;

	private static ConfigEntry<Toggle> serverConfigLocked = null!;
	public static ConfigEntry<int> maximumBossPowers = null!;
	private static ConfigEntry<int> activeBossPowerCooldown = null!;
	private static ConfigEntry<int> activeBossPowerDuration = null!;
	private static ConfigEntry<int> activeBossPowerDepletion = null!;
	private static readonly ConfigEntry<KeyboardShortcut>[] bossPowerKeys = new ConfigEntry<KeyboardShortcut>[bossPowerCount];
	private static readonly Dictionary<string, ConfigEntry<string>> bossConfigs = new();
	public static readonly Dictionary<string, BossConfig> activeBossConfigs = new();
	public static readonly Dictionary<string, ConfigEntry<int>> requiredBossKillsPassive = new();
	public static readonly Dictionary<string, ConfigEntry<int>> requiredBossKillsActive = new();
	private static readonly Dictionary<string, ConfigEntry<Spread>> PowerSpread = new();

	private static float remainingCooldown => Player.m_localPlayer.m_guardianPowerCooldown;

	private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private void bossConfig(string PowerName, int index, string bossName, string defaults)
	{
		string category = $"{index} - {bossName}";
		ConfigEntry<string> cfg = config(category, "Effects", defaults, new ConfigDescription($"Powers of {bossName}", null, new ConfigurationManagerAttributes { CustomDrawer = BossConfig.DrawConfigTable(PowerName), Order = -10 }));
		cfg.SettingChanged += (_, _) => bossConfigChanged(PowerName);
		bossConfigs[PowerName] = cfg;
		bossConfigChanged(PowerName);

		ConfigEntry<int> cfgMinPassive = config(category, "Min kills (Passive)", 1, new ConfigDescription($"Minimum required kills of {bossName} to be able to use the passive power of the boss. Use 0 to allow everyone to use the passive power as soon as the trophy is hanging on the boss stone."));
		requiredBossKillsPassive[PowerName] = cfgMinPassive;

		ConfigEntry<int> cfgMinActive = config(category, "Min kills (Active)", -1, new ConfigDescription($"Minimum required kills of {bossName} to be able to use the active power of the boss. Use -1 to disable the active power and only allow the passive power."));
		requiredBossKillsActive[PowerName] = cfgMinActive;

		ConfigEntry<Spread> cfgSpread = config(category, "Activation spread", Spread.Everyone, new ConfigDescription($"Self: Only the player activating the boss power gets the buff.\nConditions Met: Only the nearby players that have the required boss kills receive the buff.\nEveryone: Every nearby player gets the buff."));
		PowerSpread[PowerName] = cfgSpread;
	}

	private static void bossConfigChanged(string PowerName) => activeBossConfigs[PowerName] = new BossConfig(bossConfigs[PowerName].Value);

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

	private readonly ConfigurationManagerAttributes activeBossPowerSettingAttributes = new();

	public void Awake()
	{
		Localizer.Load();

		Assembly? bepinexConfigManager = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ConfigurationManager");
		Type? configManagerType = bepinexConfigManager?.GetType("ConfigurationManager.ConfigurationManager");
		configManager = configManagerType == null ? null : BepInEx.Bootstrap.Chainloader.ManagerObject.GetComponent(configManagerType);

		serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		maximumBossPowers = config("1 - General", "Maximum boss powers", 2, new ConfigDescription("Sets the maximum number of boss powers that can be active at the same time.", new AcceptableValueRange<int>(1, bossPowerCount)));
		activeBossPowerCooldown = config("2 - Active Powers", "Cooldown for boss powers (seconds)", 600, new ConfigDescription("Cooldown after activating one of the boss powers. Cooldown is shared between all boss powers.", null, activeBossPowerSettingAttributes));
		activeBossPowerCooldown.SettingChanged += activeBossPowerSettingChanged;
		activeBossPowerDuration = config("2 - Active Powers", "Duration for active boss powers (seconds)", 30, new ConfigDescription("Duration of the buff from activating boss powers.", null, activeBossPowerSettingAttributes));
		activeBossPowerDuration.SettingChanged += activeBossPowerSettingChanged;
		activeBossPowerDepletion = config("2 - Active Powers", "Power loss duration after boss power activation (seconds)", 180, new ConfigDescription("Disables the passive effect of the boss power for the specified duration after the active effect ends.", null, activeBossPowerSettingAttributes));
		activeBossPowerDepletion.SettingChanged += activeBossPowerSettingChanged;

		bossConfig(Power.Eikthyr, 3, "Eikthyr", "RunStamina:15:60,JumpStamina:15:60,SwimStaminaUsage:15:60");
		bossConfig(Power.TheElder, 4, "The Elder", "HealthRegen:10:30,TreeDamage:20:60,MiningDamage:20:60");
		bossConfig(Power.Bonemass, 5, "Bonemass", "PhysicalDamage:10:85,BlockStaminaUsage:20:100,BlockStaminaReturn:1:5");
		bossConfig(Power.Moder, 6, "Moder", "BonusFrostDefense:10:30,TailWind:20:100,WindModifier:35:200,CarryWeight:30:300,MovementSpeed:5:10");
		bossConfig(Power.Yagluth, 7, "Yagluth", "BonusLightningDefense:10:85,BonusDamage:5:10"); //TODO Farming +25
		bossConfig(Power.Queen, 8, "Queen", "EitrRegen:25:100,BonusPoisonDefense:10:30,StaminaCrouchRegen:50:100");
		bossConfig(Power.Fader, 9, "Fader", "BonusFireDefense:10:30,AdrenalineBonus:25:100,StaggerResist:20:50");

		for (int i = 0; i < bossPowerCount; ++i)
		{
			bossPowerKeys[i] = config("2 - Active Powers", $"Shortcut for boss power {i + 1}", new KeyboardShortcut(KeyCode.Alpha1 + i, KeyCode.LeftAlt), new ConfigDescription($"Keyboard shortcut to activate the {i + 1}. boss power.", null, activeBossPowerSettingAttributes), false);
		}

		configSync.AddLockingConfigEntry(serverConfigLocked);

		activeBossPowerSettingAttributes.Browsable = Utils.ActivePowersEnabled();
		foreach (ConfigEntry<int> requiredKills in requiredBossKillsActive.Values)
		{
			requiredKills.SettingChanged += (_, _) =>
			{
				activeBossPowerSettingAttributes.Browsable = Utils.ActivePowersEnabled();
				reloadConfigDisplay();
			};
		}

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
	private static class AddRPCs
	{
		private static void Postfix(Player __instance)
		{
			__instance.m_nview.Register<string>("PassivePowers Activate BossPower", ActivateBossPowerRPC);
			__instance.m_nview.Register<string>("PassivePowers BossDied", BossDied);
		}
	}

	private static void ActivateBossPowerRPC(long peer, string bossPower)
	{
		if (Utils.ActivePowerEnabled(bossPower.Substring("PassivePowers ".Length)))
		{
			Player.m_localPlayer.GetSEMan().AddStatusEffect(bossPower.GetStableHashCode(), true);
		}
	}

	private static void activeBossPowerSettingChanged(object o, EventArgs e)
	{
		foreach (StatusEffect statusEffect in ObjectDB.instance.m_StatusEffects)
		{
			if (statusEffect.name.StartsWith("PassivePowers", StringComparison.Ordinal))
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
		if (Utils.ActivePowersEnabled())
		{
			for (int i = 0; i < bossPowerKeys.Length; ++i)
			{
				if (bossPowerKeys[i].Value.IsKeyDown() && Player.m_localPlayer.TakeInput())
				{
					List<string> powers = Utils.getPassivePowers(Player.m_localPlayer);
					if (i < powers.Count)
					{
						string power = powers[i];

						if (ObjectDB.instance.GetStatusEffect(("PassivePowers " + power).GetStableHashCode()) is { } power_se)
						{
							if (Utils.ActivePowerEnabled(power))
							{
								Player.m_localPlayer.m_guardianSE = ObjectDB.instance.GetStatusEffect(power.GetStableHashCode());
								Player.m_localPlayer.StartGuardianPower();
								Player.m_localPlayer.m_guardianSE = power_se;
							}
							else if (requiredBossKillsActive[power].Value > 0)
							{
								Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$powers_min_kills_active", requiredBossKillsActive[power].Value.ToString()));
							}
							else
							{
								Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$powers_not_active"));
							}
						}
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	private class ActivateBossPower
	{
		public static bool CheckKeyDown(bool activeKey) => activeKey && !bossPowerKeys.Any(powerKey => powerKey.Value.IsKeyDown());

		private static readonly MethodInfo CheckInputKey = AccessTools.DeclaredMethod(typeof(ActivateBossPower), nameof(CheckKeyDown));
		private static readonly MethodInfo InputKey = AccessTools.DeclaredMethod(typeof(ZInput), nameof(ZInput.GetButtonDown));
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

	[HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
	private static class DedicatedGuardianPowerRPC
	{
		private static StatusEffect? StatusEffectRPC(SEMan seman, int nameHash, bool resetTime, int itemLevel, float skillLevel)
		{
			string name = ObjectDB.instance.GetStatusEffect(nameHash).name;
			Spread spread = PowerSpread[name.Substring("PassivePowers ".Length)].Value;
			if (spread == Spread.ConditionsMet)
			{
				seman.m_nview.InvokeRPC("PassivePowers Activate BossPower", name);
			}
			else if (spread == Spread.Everyone || Player.m_localPlayer == seman.m_character)
			{
				return seman.AddStatusEffect(nameHash, resetTime, itemLevel, skillLevel);
			}
			return null;
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo AddStatusEffect = AccessTools.DeclaredMethod(typeof(SEMan), nameof(SEMan.AddStatusEffect), new[] { typeof(int), typeof(bool), typeof(int), typeof(float) });

			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(AddStatusEffect))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(DedicatedGuardianPowerRPC), nameof(StatusEffectRPC)));
				}
				else
				{
					yield return instruction;
				}
			}
		}
	}

	[HarmonyPatch]
	public class AddStatusEffects
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
					depletion_se.m_startMessage = Localization.instance.Localize("$powers_depleted_description", original_se.m_name);
					depletion_se.m_startMessageType = original_se.m_startMessageType;
					depletion_se.m_icon = original_se.m_icon;
					depletion_se.m_name = Localization.instance.Localize("$powers_depleted");
					depletion_se.m_tooltip = Localization.instance.Localize("$powers_depleted_description", original_se.m_name);
					depletion_se.m_ttl = activeBossPowerDepletion.Value;
					EffectList.EffectData effectData = new()
					{
						m_prefab = new GameObject(original_se.name + " Depletion Prefab"),
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
	private class RemoveActiveTooltip
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

		void addTooltips(Func<PowerConfig, int> value, string type)
		{
			foreach (PowerConfig config in activeBossConfigs[statusEffect.name].Configs)
			{
				int val = value(config);
				if (val > 0)
				{
					powers.Add(Localization.instance.Localize($"$powers_type_{type}: {config.Desc}", val.ToString()));
				}
			}
		}

		int requiredKills = requiredBossKillsPassive[statusEffect.name].Value;
		powers.Add("");
		if (requiredKills > 0)
		{
			powers.Add(Localization.instance.Localize("$powers_min_kills_passive", requiredKills.ToString()));
		}
		addTooltips(p => (int)p.BoxedPassive, "passive");

		requiredKills = requiredBossKillsActive[statusEffect.name].Value;
		if (requiredKills >= 0)
		{
			powers.Add("");
			if (requiredKills > 0)
			{
				powers.Add(Localization.instance.Localize("$powers_min_kills_active", requiredKills.ToString()));
				powers.Add("");
			}
			powers.Add(Localization.instance.Localize("$powers_activation_hint", Utils.getHumanFriendlyTime(activeBossPowerCooldown.Value), Utils.getHumanFriendlyTime(activeBossPowerDuration.Value)));
			addTooltips(p => (int)p.BoxedActive, "active");
			powers.Add(Localization.instance.Localize("$powers_depletion_hint", Utils.getHumanFriendlyTime(activeBossPowerDepletion.Value)));
		}

		statusEffect.m_tooltip = string.Join("\n", powers);
	}

	[HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))]
	private class RemoveOriginalEffectDescription
	{
		private static void Postfix(SE_Stats __instance, ref string __result)
		{
			if (__instance.name.StartsWith("GP_", StringComparison.Ordinal))
			{
				__result = __instance.m_tooltip;
			}
		}
	}

	[HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
	private class ModifyBossStoneHoverText
	{
		private static void Prefix(ItemStand __instance)
		{
			if (__instance.m_guardianPower is { } statusEffect)
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

		private static readonly MethodInfo ActivationStatusUpdater = AccessTools.DeclaredMethod(typeof(ModifyBossStoneHoverText), nameof(UpdateActivationStatus));
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
	private class SwitchSelectedPowers
	{
		private static string UpdateActivationStatus(string str, ItemStand itemStand)
		{
			if (Utils.getPassivePowers(Player.m_localPlayer).Contains(itemStand.m_guardianPower.name))
			{
				return str.Replace(activateStr, "$guardianstone_hook_power_deactivate");
			}
			return str;
		}

		private static bool CanActivatePower(ItemStand itemStand)
		{
			if (Utils.PassivePowerEnabled(itemStand.m_guardianPower.name))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$powers_min_kills_passive", requiredBossKillsPassive[itemStand.m_guardianPower.name].Value.ToString()));
			return false;
		}

		private static readonly MethodInfo ActivationStatusUpdater = AccessTools.DeclaredMethod(typeof(SwitchSelectedPowers), nameof(UpdateActivationStatus));
		private static readonly MethodInfo ActivatePowerCheck = AccessTools.DeclaredMethod(typeof(SwitchSelectedPowers), nameof(CanActivatePower));
		private const string activateStr = "$guardianstone_hook_power_activate";

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
		{
			Label cleanup = ilg.DefineLabel();

			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
				if (instruction.opcode == OpCodes.Ldstr && ((string)instruction.operand).Contains(activateStr))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, ActivationStatusUpdater);
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, ActivatePowerCheck);
					yield return new CodeInstruction(OpCodes.Brfalse, cleanup);
				}
			}

			yield return new CodeInstruction(OpCodes.Pop) { labels = new List<Label> { cleanup } };
			yield return new CodeInstruction(OpCodes.Pop);
			yield return new CodeInstruction(OpCodes.Pop);
			yield return new CodeInstruction(OpCodes.Ldc_I4_0);
			yield return new CodeInstruction(OpCodes.Ret);
		}
	}

	[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.AddActiveEffects))]
	private class AddPowerEffectsToCompendium
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
				if (ObjectDB.instance.GetStatusEffect(power.GetStableHashCode()) is { } se)
				{
					UpdateStatusEffectTooltip(se);

					stringBuilder.Append("<color=orange>" + Localization.instance.Localize(se.m_name) + "</color>\n");
					stringBuilder.Append(Localization.instance.Localize(se.GetTooltipString()));
					stringBuilder.Append("\n\n");
				}
			}
		}

		private static readonly MethodInfo GuardianPowerGetter = AccessTools.DeclaredMethod(typeof(Player), nameof(Player.GetGuardianPowerHUD));
		private static readonly MethodInfo GuardianPowerWriter = AccessTools.DeclaredMethod(typeof(AddPowerEffectsToCompendium), nameof(DisplayGuardianPowers));

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
		public TMP_Text name = null!;
		public Image icon = null!;
	}

	private static readonly List<HudPower> hudPowers = new();

	[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
	private class DisplayMultiplePowers
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
					name = root.Find("Name").GetComponent<TMP_Text>(),
				});
			}

			RectTransform gpRoot = __instance.m_gpRoot;
			GameObject powerContainer = new("powerContainer 1")
			{
				transform =
				{
					parent = gpRoot,
					localPosition = Vector3.zero,
				},
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
	private class DisplayBossPowerCooldown
	{
		private static bool Prefix(Hud __instance)
		{
			int index = 0;
			foreach (string s in Utils.getPassivePowers(Player.m_localPlayer))
			{
				if (ObjectDB.instance.GetStatusEffect(s.GetStableHashCode()) is { } se)
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

			__instance.m_gpCooldown.text = Utils.ActivePowersEnabled() ? remainingCooldown > 0f ? StatusEffect.GetTimeString(remainingCooldown) : Localization.instance.Localize("$hud_ready") : Localization.instance.Localize("$powers_type_passive");
			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SavePlayerData))]
	private static class SaveBossKills
	{
		private static void Prefix(PlayerProfile __instance, Player player)
		{
			foreach (KeyValuePair<string, float> stat in __instance.m_enemyStats)
			{
				player.m_customData[stat.Key] = stat.Value.ToString(CultureInfo.InvariantCulture);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.LoadPlayerData))]
	private static class LoadBossKills
	{
		private static void Postfix(PlayerProfile __instance, Player player)
		{
			if (ZNetScene.instance)
			{
				foreach (GameObject gameObject in ZNetScene.instance.m_prefabs)
				{
					if (gameObject.GetComponent<Character>() is { } character && player.m_customData.TryGetValue(character.m_name, out string valueStr) && float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !__instance.m_enemyStats.ContainsKey(character.m_name))
					{
						__instance.m_enemyStats[character.m_name] = value;
					}
				}
			}
		}
	}
	
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class CountBossKills
	{
		private static void Prefix(Character __instance)
		{
			if (__instance.IsBoss() && Utils.effectToBossMap.ContainsValue(__instance.m_name))
			{
				List<Player> nearbyPlayers = new();
				Player.GetPlayersInRange(__instance.transform.position, 50f, nearbyPlayers);

				foreach (Player p in nearbyPlayers)
				{
					if (p != Player.m_localPlayer)
					{
						p.m_nview.InvokeRPC("PassivePowers BossDied", __instance.m_name);
					}
				}
			}
		}
	}

	private static void BossDied(long sender, string bossName)
	{
		Game.instance.GetPlayerProfile().m_enemyStats.IncrementOrSet(bossName);
	}
}
