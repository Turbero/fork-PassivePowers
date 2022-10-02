using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace PassivePowers;

[UsedImplicitly]
public static class BossPowerPatches
{
	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyRunStaminaDrain))]
	private class Patch_SEMan_ModifyRunStaminaDrain
	{
		private static void Prefix(SEMan __instance, ref float drain)
		{
			if (__instance.m_character is Player player && Utils.getPassivePowers(player).Contains(Power.Eikthyr))
			{
				drain *= 1 - PassivePowers.runStaminaReduction.Value / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyJumpStaminaUsage))]
	private class Patch_SEMan_ModifyJumpStaminaDrain
	{
		private static void Prefix(SEMan __instance, ref float staminaUse)
		{
			if (__instance.m_character is Player player && Utils.getPassivePowers(player).Contains(Power.Eikthyr))
			{
				staminaUse *= 1 - PassivePowers.jumpStaminaReduction.Value / 100f;
			}
		}
	}
		
	[HarmonyPatch(typeof(Player), nameof(Player.GetJogSpeedFactor))]
	private class Patch_Player_GetJogSpeedFactor
	{
		private static void Postfix(Player __instance, ref float __result)
		{
			if (Utils.getPassivePowers(__instance).Contains(Power.Eikthyr))
			{
				__result += PassivePowers.movementSpeedIncrease.Value / 100f;
			}
		}
	}
		
	[HarmonyPatch(typeof(Player), nameof(Player.GetRunSpeedFactor))]
	private class Patch_Player_GetRunSpeedFactor
	{
		private static void Postfix(Player __instance, ref float __result)
		{
			if (Utils.getPassivePowers(__instance).Contains(Power.Eikthyr))
			{
				__result += PassivePowers.movementSpeedIncrease.Value / 100f;
			}
		}
	}

	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAttack))]
	private class Patch_SEMan_ModifyAttack
	{
		private static void Prefix(SEMan __instance, ref HitData hitData)
		{
			if (__instance.m_character is Player player)
			{
				if (Utils.getPassivePowers(player).Contains(Power.TheElder))
				{
					hitData.m_damage.m_chop *= 1 + PassivePowers.treeDamageIncrease.Value / 100f;
					hitData.m_damage.m_pickaxe *= 1 + PassivePowers.miningDamageIncrease.Value / 100f;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	private class Patch_Character_Damage
	{
		private static void Prefix(Character __instance, HitData hit)
		{
			if (__instance is Player player)
			{
				if (Utils.getPassivePowers(player).Contains(Power.Bonemass))
				{
					hit.m_damage.m_blunt *= 1 - PassivePowers.phyiscalDamageReduction.Value / 100f;
					hit.m_damage.m_pierce *= 1 - PassivePowers.phyiscalDamageReduction.Value / 100f;
					hit.m_damage.m_slash *= 1 - PassivePowers.phyiscalDamageReduction.Value / 100f;
				}

				if (Utils.getPassivePowers(player).Contains(Power.Yagluth))
				{
					hit.m_damage.m_fire *= 1 - PassivePowers.elementalDamageReduction.Value / 100f;
					hit.m_damage.m_frost *= 1 - PassivePowers.elementalDamageReduction.Value / 100f;
					hit.m_damage.m_poison *= 1 - PassivePowers.elementalDamageReduction.Value / 100f;
					hit.m_damage.m_lightning *= 1 - PassivePowers.elementalDamageReduction.Value / 100f;
				}
			}

			if (hit.GetAttacker() is Player attacker && Utils.getPassivePowers(attacker).Contains(Power.Yagluth))
			{
				hit.m_damage.m_fire += hit.GetTotalDamage() * PassivePowers.bonusFireDamage.Value / 100f;
			}
		}
	}
		
	[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyHealthRegen))]
	public static class Patch_SEMan_ModifyHealthRegen
	{
		[UsedImplicitly]
		public static void Postfix(SEMan __instance, ref float regenMultiplier)
		{
			if (__instance.m_character is Player player && Utils.getPassivePowers(player).Contains(Power.Bonemass))
			{
				regenMultiplier += PassivePowers.healthRegenIncrease.Value / 100f;
			}
		}
	}

	private static float ModerShipFactor(Ship ship)
	{
		List<Player> playersWithPower = ship.m_players.FindAll(p => Utils.getPassivePowers(p.m_nview.GetZDO()?.GetString("PassivePowers GuardianPowers") ?? "").Contains(Power.Moder));
		return (float)playersWithPower.Count / Mathf.Max(1, ship.m_players.Count);
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.IsWindControllActive))]
	private class Patch_Ship_IsWindControllActive
	{
		private static void Postfix(Ship __instance, ref bool __result)
		{
			float chance = PassivePowers.tailWindChance.Value / 100f * ModerShipFactor(__instance);

			Random.State state = Random.state;
			Random.InitState((int)(EnvMan.instance.m_totalSeconds / EnvMan.instance.m_windPeriodDuration));
			if (Random.Range(0f, 1f) < chance)
			{
				__result = true;
			}
			Random.state = state;
		}
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
	private class Patch_Ship_GetSailForce
	{
		private static float UpdateWindIntensity(float windIntensity, Ship ship)
		{
			float modifier = PassivePowers.windSpeedModifier.Value / 100f * ModerShipFactor(ship);
			if (Vector3.Angle(ship.transform.forward, EnvMan.instance.GetWindDir()) > 90)
			{
				return windIntensity * (1 - modifier);
			}
			return windIntensity * (1 + modifier);
		}

		private static readonly MethodInfo WindIntensityGetter = AccessTools.DeclaredMethod(typeof(EnvMan), nameof(EnvMan.GetWindIntensity));
		private static readonly MethodInfo WindIntensityUpdater = AccessTools.DeclaredMethod(typeof(Patch_Ship_GetSailForce), nameof(UpdateWindIntensity));

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
				if (instruction.opcode == OpCodes.Callvirt && instruction.OperandIs(WindIntensityGetter))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, WindIntensityUpdater);
				}
			}
		}
	}
}