using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PassivePowers;

public static class Utils
{
	public static bool GetToggle(this ConfigEntry<Toggle> toggle)
	{
		return toggle.Value == Toggle.On;
	}

	public static List<string> getPassivePowers(Player player) => getPassivePowers(player.m_guardianPower);
	public static List<string> getPassivePowers(string powerList) => powerList.IsNullOrWhiteSpace() ? new List<string>() : powerList.Split(',').ToList();

	public static string getHumanFriendlyTime(int seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

		string secondsText = Localization.instance.Localize(timeSpan.Seconds >= 2 ? "$powers_second_plural" : "$powers_second_singular", timeSpan.Seconds.ToString());
		string minutesText = Localization.instance.Localize(timeSpan.Minutes >= 2 ? "$powers_minute_plural" : "$powers_minute_singular", timeSpan.Minutes.ToString());
		return timeSpan.TotalMinutes >= 1 ? timeSpan.Seconds == 0 ? minutesText : Localization.instance.Localize("$powers_time_bind", minutesText, secondsText) : secondsText;
	}

	public static bool CanApplyPower(Player player, string power) => getPassivePowers(player).Contains(power) || player.GetSEMan().HaveStatusEffect(("PassivePowers " + power).GetStableHashCode());
	
	public static bool ActivePowersEnabled() => PassivePowers.requiredBossKillsActive.Any(v => v.Value.Value >= 0);

	private static readonly Dictionary<string, string> effectToBossMap = new()
	{
		{ Power.Eikthyr, "$enemy_eikthyr" },
		{ Power.TheElder, "$enemy_gdking" },
		{ Power.Bonemass, "$enemy_bonemass" },
		{ Power.Moder, "$enemy_dragon" },
		{ Power.Yagluth, "$enemy_goblinking" },
		{ Power.Queen, "$enemy_seekerqueen" },
		{ Power.Fader, "$enemy_fader" },
	};
	
	private static bool PowerEnabled(Dictionary<string, ConfigEntry<int>> required, string powerName)
	{
		int value = required[powerName].Value;
		Game.instance.GetPlayerProfile().m_enemyStats.TryGetValue(effectToBossMap[powerName], out float kills);
		return value >= 0 && value <= kills;
	}

	public static bool ActivePowerEnabled(string powerName) => PowerEnabled(PassivePowers.requiredBossKillsActive, powerName);
	public static bool PassivePowerEnabled(string powerName) => PowerEnabled(PassivePowers.requiredBossKillsPassive, powerName);

	// since KeyboardShortcut.IsPressed and KeyboardShortcut.IsDown behave unintuitively
	public static bool IsKeyDown(this KeyboardShortcut shortcut)
	{
		return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
	}
}

public static class Power
{
	public const string Eikthyr = "GP_Eikthyr";
	public const string TheElder = "GP_TheElder";
	public const string Bonemass = "GP_Bonemass";
	public const string Moder = "GP_Moder";
	public const string Yagluth = "GP_Yagluth";
	public const string Queen = "GP_Queen";
	public const string Fader = "GP_Fader";
}

[HarmonyPatch(typeof(Player), nameof(Player.SetGuardianPower))]
public class SetSelectedGuardianPowers
{
	private static bool Prefix(Player __instance, string name)
	{
		if (name == "" || name.Contains(","))
		{
			__instance.m_guardianPower = name;
		}
		else
		{
			List<string> powers = Utils.getPassivePowers(__instance);
			if (!powers.Remove(name))
			{
				powers.Add(name);
			}

			if (powers.Count > PassivePowers.maximumBossPowers.Value)
			{
				powers.RemoveAt(0);
			}

			__instance.m_guardianPower = string.Join(",", powers);
		}

		__instance.m_nview.GetZDO()?.Set("PassivePowers GuardianPowers", __instance.m_guardianPower);

		return false;
	}
}
