using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace PassivePowers
{
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

			return (timeSpan.TotalMinutes >= 1 ? $"{(int)timeSpan.TotalMinutes} minute" + (timeSpan.TotalMinutes >= 2 ? "s" : "") + (timeSpan.Seconds != 0 ? " and " : "") : "") + (timeSpan.Seconds != 0 ? $"{timeSpan.Seconds} second" + (timeSpan.Seconds >= 2 ? "s" : "") : "");
		}
	}

	public static class Power
	{
		public const string Eikthyr = "GP_Eikthyr";
		public const string TheElder = "GP_TheElder";
		public const string Bonemass = "GP_Bonemass";
		public const string Moder = "GP_Moder";
		public const string Yagluth = "GP_Yagluth";
	}

	[HarmonyPatch(typeof(Player), nameof(Player.SetGuardianPower))]
	public class Patch_Player_SetGuardianPower
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

	public class PowerConfig<T>
	{
		public string powerName = null!;
		public ConfigEntry<T> active = null!;
		public ConfigEntry<T> passive = null!;

		public T Value => (Player.m_localPlayer.m_seman.HaveStatusEffect("PassivePowers Depletion " + powerName) ? default : Player.m_localPlayer.m_seman.HaveStatusEffect("PassivePowers " + powerName) ? active.Value : passive.Value)!;
	}
}
