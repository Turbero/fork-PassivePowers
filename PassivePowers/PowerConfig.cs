using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using JetBrains.Annotations;
using UnityEngine;

namespace PassivePowers;

public abstract class PowerConfig
{
	public abstract string Modifier { get; }
	public abstract string Desc { get; }
	public abstract string Unit { get; }

	public abstract object BoxedActive { get; }
	public abstract object BoxedPassive { get; }

	public static readonly Type[] Modifiers = typeof(PowerConfig<,>).Assembly.GetTypes().Where(t => typeof(PowerConfig).IsAssignableFrom(t) && !t.IsAbstract).OrderBy(t => t.Name).ToArray();
	public static readonly Dictionary<string, Type> ModifierMap = Modifiers.ToDictionary(t => t.Name, t => t);
	public static readonly string[] ModifierNames = Modifiers.Select(m => createFromFloats(m, 0, 0).Modifier).ToArray();

	public override bool Equals(object? obj) => obj is PowerConfig other && Modifier == other.Modifier;
	public override int GetHashCode() => Modifier.GetHashCode();
	
    public static PowerConfig createFromFloats(Type type, float active, float passive)
    {
        MethodInfo cast = type.GetMethod(nameof(MovementSpeed.Cast), BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;
        return (PowerConfig)type.GetConstructors()[0].Invoke(new[] { cast.Invoke(null, new object[] { active }), cast.Invoke(null, new object[] { passive }) });
    }
}

public abstract class PowerConfig<T, U>(T Active, T Passive): PowerConfig where T: struct, IConvertible where U: PowerConfig<T, U>
{
	public override object BoxedActive => Active;
	public override object BoxedPassive => Passive;

	private T Value(string powerName) => Player.m_localPlayer is {} player && Utils.CanApplyPower(player, powerName) ? player.m_seman.HaveStatusEffect(("PassivePowers Depletion " + powerName).GetStableHashCode()) ? default : Player.m_localPlayer.m_seman.HaveStatusEffect(("PassivePowers " + powerName).GetStableHashCode()) ? Active : Passive : default;
	
	public static T Total() => Cast(PassivePowers.activeBossConfigs.Sum(kv => kv.Value.Configs.Sum(c => c is U cfg ? cfg.Value(kv.Key).ToSingle(CultureInfo.InvariantCulture) : 0)));

	public static T Cast(float value) => (T)Convert.ChangeType(value, typeof(T));
}

[UsedImplicitly]
public class MovementSpeed(int active, int passive): PowerConfig<int, MovementSpeed>(active, passive)
{
	public override string Modifier => "Movement Speed";
	public override string Desc => "$powers_movement_speed_increase";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class SwimSpeed(int active, int passive): PowerConfig<int, SwimSpeed>(active, passive)
{
	public override string Modifier => "Swim Speed";
	public override string Desc => "$powers_swim_speed_increase";
	public override string Unit => "% increase";
}
[UsedImplicitly]
public class SwimStaminaUsage(int active, int passive): PowerConfig<int, SwimStaminaUsage>(active, passive)
{
	public override string Modifier => "Swim Stamina Usage";
	public override string Desc => "$powers_swim_stamina_usage";
	public override string Unit => "% decrease";
}

[UsedImplicitly]
public class RunStamina(int active, int passive): PowerConfig<int, RunStamina>(active, passive)
{
	public override string Modifier => "Run Stamina Reduction";
	public override string Desc => "$powers_run_stamina_reduction";
	public override string Unit => "% reduction";
}

[UsedImplicitly]
public class JumpStamina(int active, int passive): PowerConfig<int, JumpStamina>(active, passive)
{
	public override string Modifier => "Jump Stamina Reduction";
	public override string Desc => "$powers_jump_stamina_reduction";
	public override string Unit => "% reduction";
}

[UsedImplicitly]
public class TreeDamage(int active, int passive): PowerConfig<int, TreeDamage>(active, passive)
{
	public override string Modifier => "Tree Damage Increase";
	public override string Desc => "$powers_tree_damage";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class MiningDamage(int active, int passive): PowerConfig<int, MiningDamage>(active, passive)
{
	public override string Modifier => "Mining Damage Increase";
	public override string Desc => "$powers_stone_damage";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class PhysicalDamage(int active, int passive): PowerConfig<int, PhysicalDamage>(active, passive)
{
	public override string Modifier => "Physical Damage Taken Reduction";
	public override string Desc => "$powers_physical_damage_reduction";
	public override string Unit => "% reduction";
}

[UsedImplicitly]
public class HealthRegen(int active, int passive): PowerConfig<int, HealthRegen>(active, passive)
{
	public override string Modifier => "Health Regen Increase";
	public override string Desc => "$powers_health_regen_increase";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class TailWind(int active, int passive): PowerConfig<int, TailWind>(active, passive)
{
	public override string Modifier => "Tail Wind Chance";
	public override string Desc => "$powers_tailwind_chance";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class WindSpeed(int active, int passive): PowerConfig<int, WindSpeed>(active, passive)
{
	public override string Modifier => "Wind Speed Modifier";
	public override string Desc => "$powers_wind_modifier";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class ElementalDamage(int active, int passive): PowerConfig<int, ElementalDamage>(active, passive)
{
	public override string Modifier => "Elemental Damage Taken Reduction";
	public override string Desc => "$powers_elemental_damage_reduction";
	public override string Unit => "% reduction";
}

[UsedImplicitly]
public class BonusDamage(int active, int passive) : PowerConfig<int, BonusDamage>(active, passive)
{
    public override string Modifier => "Bonus Damage";
    public override string Desc => "$powers_additional_damage";
    public override string Unit => "% bonus";
}

[UsedImplicitly]
public class BonusFireDamage(int active, int passive): PowerConfig<int, BonusFireDamage>(active, passive)
{
	public override string Modifier => "Bonus Fire Damage";
	public override string Desc => "$powers_additional_fire_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusFrostDamage(int active, int passive): PowerConfig<int, BonusFrostDamage>(active, passive)
{
	public override string Modifier => "Bonus Frost Damage";
	public override string Desc => "$powers_additional_frost_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusLightningDamage(int active, int passive): PowerConfig<int, BonusLightningDamage>(active, passive)
{
	public override string Modifier => "Bonus Lightning Damage";
	public override string Desc => "$powers_additional_lightning_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusPoisonDamage(int active, int passive): PowerConfig<int, BonusPoisonDamage>(active, passive)
{
	public override string Modifier => "Bonus Poison Damage";
	public override string Desc => "$powers_additional_poison_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusSpiritDamage(int active, int passive): PowerConfig<int, BonusSpiritDamage>(active, passive)
{
	public override string Modifier => "Bonus Spirit Damage";
	public override string Desc => "$powers_additional_spirit_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusBluntDamage(int active, int passive): PowerConfig<int, BonusBluntDamage>(active, passive)
{
	public override string Modifier => "Bonus Blunt Damage";
	public override string Desc => "$powers_additional_blunt_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusPierceDamage(int active, int passive): PowerConfig<int, BonusPierceDamage>(active, passive)
{
	public override string Modifier => "Bonus Pierce Damage";
	public override string Desc => "$powers_additional_pierce_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusSlashDamage(int active, int passive): PowerConfig<int, BonusSlashDamage>(active, passive)
{
	public override string Modifier => "Bonus Slash Damage";
	public override string Desc => "$powers_additional_slash_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusFireDefense(int active, int passive): PowerConfig<int, BonusFireDefense>(active, passive)
{
	public override string Modifier => "Bonus Fire Defense";
	public override string Desc => "$powers_additional_fire_defense";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusFrostDefense(int active, int passive): PowerConfig<int, BonusFrostDefense>(active, passive)
{
	public override string Modifier => "Bonus Frost Defense";
	public override string Desc => "$powers_additional_frost_defense";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusLightningDefense(int active, int passive): PowerConfig<int, BonusLightningDefense>(active, passive)
{
	public override string Modifier => "Bonus Lightning Defense";
	public override string Desc => "$powers_additional_lightning_defense";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusPoisonDefense(int active, int passive): PowerConfig<int, BonusPoisonDefense>(active, passive)
{
	public override string Modifier => "Bonus Poison Defense";
	public override string Desc => "$powers_additional_poison_defense";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BonusSpiritDefense(int active, int passive): PowerConfig<int, BonusSpiritDefense>(active, passive)
{
	public override string Modifier => "Bonus Spirit Defense";
	public override string Desc => "$powers_additional_spirit_damage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class StaminaCrouchRegen(int active, int passive): PowerConfig<int, StaminaCrouchRegen>(active, passive)
{
	public override string Modifier => "Stamina Crouch Regen";
	public override string Desc => "$powers_sneak_stamina_reduction";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class AdrenalineBonus(int active, int passive): PowerConfig<int, AdrenalineBonus>(active, passive)
{
	public override string Modifier => "Adrenaline Bonus";
	public override string Desc => "$powers_adrenaline_bonus";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class StaggerResist(int active, int passive): PowerConfig<int, StaggerResist>(active, passive)
{
	public override string Modifier => "Stagger Resist";
	public override string Desc => "$powers_stagger_resist";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BlockStaminaUsage(int active, int passive): PowerConfig<int, BlockStaminaUsage>(active, passive)
{
	public override string Modifier => "Block Stamina Usage";
	public override string Desc => "$powers_block_stamina_usage";
	public override string Unit => "% bonus";
}
[UsedImplicitly]
public class BlockStaminaReturn(int active, int passive): PowerConfig<int, BlockStaminaReturn>(active, passive)
{
	public override string Modifier => "Block Stamina Return";
	public override string Desc => "$powers_block_stamina_return";
	public override string Unit => "+ bonus";
}

[UsedImplicitly]
public class EitrRegen(int active, int passive): PowerConfig<int, EitrRegen>(active, passive)
{
	public override string Modifier => "Eitr Regen Increase";
	public override string Desc => "$powers_eitr_regen";
	public override string Unit => "% increase";
}

[UsedImplicitly]
public class CarryWeight(int active, int passive): PowerConfig<int, CarryWeight>(active, passive)
{
	public override string Modifier => "Carry Weight Increase";
	public override string Desc => "$powers_carry_weight";
	public override string Unit => "+ increase";
}

public class BossConfig(HashSet<PowerConfig> configs)
{
    public readonly HashSet<PowerConfig> Configs = configs;

    public BossConfig(string configs) : this(new HashSet<PowerConfig>(configs.Split(',').Select(r =>
    {
	    string[] split = r.Split(':');
	    if (split.Length < 2)
	    {
		    return null;
	    }
	    if (!PowerConfig.ModifierMap.TryGetValue(split[0], out Type power))
	    {
		    return null;
	    }
	    if (!NumberParseFloat(split[1], out float passive))
	    {
		    return null;
	    }
	    float active = 0;
	    if (split.Length > 2 && !NumberParseFloat(split[2], out active))
	    {
		    return null;
	    }
	    return PowerConfig.createFromFloats(power, active, passive);
    }).Where(c => c is not null)!)) {}

    public override string ToString()
    {
    	return string.Join(",", Configs.Select(r => $"{r.GetType().Name}:{r.BoxedPassive}:{r.BoxedActive}"));
    }
    
	private static object createComboBox()
	{
		Rect settingWindowRect = (Rect)PassivePowers.configManager!.GetType().GetProperty("SettingWindowRect", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(PassivePowers.configManager); 
		
		Type comboType = PassivePowers.configManager.GetType().Assembly.GetType("ConfigurationManager.Utilities.ComboBox");
		// ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, GUIStyle listStyle, float windowYmax)
		return comboType.GetConstructors()[0].Invoke(new object[] { new Rect(), new GUIContent(""), PowerConfig.ModifierNames.Select(s => new GUIContent(s)).ToArray(), GUI.skin.button, settingWindowRect.yMax });
	}

	private static void updateComboBox(object comboBox, string current, Action<int> handler, int width)
	{
		Type comboType = comboBox.GetType();
		
		GUIContent buttonText = new(current);
		Rect dispRect = GUILayoutUtility.GetRect(buttonText, GUI.skin.button, GUILayout.Width(width));

		comboType.GetProperty("Rect")!.SetValue(comboBox, dispRect);
		comboType.GetProperty("ButtonContent")!.SetValue(comboBox, buttonText);
		comboType.GetMethod("Show")!.Invoke(comboBox, new object[] { handler });
	}

	private delegate bool NumberParse(string input, out float value);
	private static bool NumberParseInt(string input, out float value)
	{
		bool ret = int.TryParse(input, out int i);
        value = i;
        return ret;
	}

	private static bool NumberParseFloat(string input, out float value) => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	
	public static Action<ConfigEntryBase> DrawConfigTable(string powerName)
	{
		List<object> comboBoxes = new();
		Type? selectedPower = null;
		int selectedComboBox = 0;
		bool initialDraw = true;
		bool[] used = new bool[PowerConfig.Modifiers.Length]; 
    	return cfg =>
    	{
    		bool locked = cfg.Description.Tags.Select(a => a.GetType().Name == "ConfigurationManagerAttributes" ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a) : null).FirstOrDefault(v => v != null) ?? false;

    		HashSet<PowerConfig> newConfigs = new();
    		bool wasUpdated = false;

    		int RightColumnWidth = (int)(PassivePowers.configManager?.GetType().GetProperty("RightColumnWidth", BindingFlags.Instance | BindingFlags.NonPublic)!.GetGetMethod(true).Invoke(PassivePowers.configManager, Array.Empty<object>()) ?? 130);

    		GUILayout.BeginVertical();

    		HashSet<PowerConfig> configs = new BossConfig((string)cfg.BoxedValue).Configs;
		    bool added = false;

		    if (configs.Count == 0)
		    {
			    GUILayout.BeginHorizontal();
			    GUILayout.Label("No powers selected", new GUIStyle(GUI.skin.label) { stretchWidth = true });
			    added = GUILayout.Button("Add");
			    GUILayout.EndHorizontal();
		    }

		    int comboIndex = -1;
		    foreach (PowerConfig config in configs)
    		{
				GUILayout.BeginHorizontal();

				if (++comboIndex >= comboBoxes.Count)
			    {
				    comboBoxes.Add(createComboBox());
			    }
			    int comboIdx = comboIndex;
			    int modifierIdx = Array.IndexOf(PowerConfig.Modifiers, config.GetType());
			    updateComboBox(comboBoxes[comboIndex], config.Modifier, index =>
			    {
				    // ReSharper disable AccessToModifiedClosure
				    for (int i = 0; i <= index && index < used.Length - 1; ++i)
				    {
					    if (used[i] && i != modifierIdx)
					    {
						    ++index;
					    }
				    }
				    selectedPower = PowerConfig.Modifiers[index];
				    selectedComboBox = comboIdx;
			    }, RightColumnWidth - 3 - 21 - 3 - 21);
			    Type newPower = comboIndex == selectedComboBox && selectedPower is not null ? selectedPower : config.GetType();
			    wasUpdated = wasUpdated || config.GetType() != newPower;
			    // ReSharper restore AccessToModifiedClosure

			    bool deleted = GUILayout.Button("x", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked;
			    added = added || configs.Count != PowerConfig.Modifiers.Length && GUILayout.Button("+", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked;

			    GUILayout.EndHorizontal();
				    
			    float Query(string type, object current)
			    {
					GUILayout.BeginHorizontal();
				    GUILayout.Label(type + ": ", new GUIStyle(GUI.skin.label) { fixedWidth = 70 });
			        float amount = current is int i ? i : (float)current;
			        NumberParse parse = current is int ? NumberParseInt : NumberParseFloat;
			        if (parse(GUILayout.TextField(amount.ToString(CultureInfo.InvariantCulture), new GUIStyle(GUI.skin.textField) { fixedWidth = 60 }), out float newAmount) && newAmount != amount && !locked)
                    {
                        amount = newAmount;
                        wasUpdated = true;
                    }
				    GUILayout.Label(" " + config.Unit, new GUIStyle(GUI.skin.label) { stretchWidth = true });
	    			GUILayout.EndHorizontal();
                    return amount;
			    }
			    
			    float active = PassivePowers.requiredBossKillsActive[powerName].Value >= 0 ? Query("Active", config.BoxedActive) : (config.BoxedPassive is int ? (int)config.BoxedActive : (float)config.BoxedActive);
			    float passive = Query("Passive", config.BoxedPassive);
			    
			    if (deleted)
    			{
    				wasUpdated = true;
    			}
    			else
    			{
    				newConfigs.Add(PowerConfig.createFromFloats(newPower, active, passive));
    			}
    		}
		    selectedPower = null;

    		if (added)
    		{
    			wasUpdated = true;
    			newConfigs.Add(PowerConfig.createFromFloats(PowerConfig.Modifiers.First(p => newConfigs.All(c => p != c.GetType())), 0, 0));
				if (++comboIndex >= comboBoxes.Count)
			    {
				    comboBoxes.Add(createComboBox());
			    }
    		}

    		GUILayout.EndVertical();

    		if (wasUpdated || initialDraw)
    		{
			    initialDraw = false;
			    
    			cfg.BoxedValue = new BossConfig(newConfigs).ToString();

			    FieldInfo listContent = comboBoxes[0].GetType().GetField("listContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
			    used = new bool[PowerConfig.Modifiers.Length]; 
			    for (int i = 0; i < PowerConfig.Modifiers.Length; ++i)
			    {
				    used[i] = newConfigs.Any(p => p.GetType() == PowerConfig.Modifiers[i]);
			    }
			    comboIndex = -1;
			    foreach (PowerConfig config in newConfigs)
			    {
				    List<string> names = new();
				    for (int i = 0; i < used.Length; ++i)
				    {
					    if (!used[i] || PowerConfig.Modifiers[i] == config.GetType())
					    {
						    names.Add(PowerConfig.ModifierNames[i]);
					    }
				    }
				    listContent.SetValue(comboBoxes[++comboIndex], names.Select(s => new GUIContent(s)).ToArray());
			    }
    		}
    	};
    }
}
