using System;
using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace Copilot
{
    public class CopilotSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        public string[] PartyElements { get; set; } = new string[5];

        public HotkeyNode TogglePauseHotkey { get; set; } = new HotkeyNode(Keys.OemPeriod);

        public ToggleNode IsPaused { get; set; } = new ToggleNode(false);

        [Menu(null, "Leader's name")]
        public TextNode TargetPlayerName { get; set; } = new TextNode("Leader");

        [Menu("Distances", "Follow Distance (~460 as default)")]
        public RangeNode<int> FollowDistance { get; set; } = new RangeNode<int>(460, 10, 1000);

        [Menu("Distances", "Idle Distance (~200 as default)")]
        public RangeNode<int> IdleDistance { get; set; } = new RangeNode<int>(200, 5, 1000);

        [Menu("Action", "Cooldown in milliseconds (~100 as default)")]
        public RangeNode<int> ActionCooldown { get; set; } = new RangeNode<int>(100, 50, 20000);

        [Menu("Blink Settings")]

        public BlinkSettings Blink { get; set; } = new BlinkSettings();
        [Menu("Potions Settings")]

        public PotionSettings Potions { get; set; } = new PotionSettings();
    }

    [Submenu(CollapsedByDefault = true)]
    public class BlinkSettings
    {
        [Menu("Enable Blink", "Enable Blink / TP")]
        public ToggleNode UseBlink { get; set; } = new ToggleNode(false);

        [Menu("Blink Settings", "Minimum range to attempt TP (default: 1000)")]
        public RangeNode<int> BlinkRange { get; set; } = new RangeNode<int>(1000, 10, 2000);

        [Menu("Blink Settings", "Cooldown in milliseconds (default: 500)")]
        public RangeNode<int> BlinkCooldown { get; set; } = new RangeNode<int>(500, 100, 10000);
    }

    [Submenu(CollapsedByDefault = true)]
    public class PotionSettings
    {
        [Menu("Enable Potion", "Enable potion usage")]
        public ToggleNode EnablePotions { get; set; } = new ToggleNode(false);

        public HealthPotionSettings HealthPotion { get; set; } = new HealthPotionSettings();
        public ManaPotionSettings ManaPotion { get; set; } = new ManaPotionSettings();
        public EnergyShieldPotionSettings ESPotion { get; set; } = new EnergyShieldPotionSettings();
    }

    [Submenu(CollapsedByDefault = true)]
    public class HealthPotionSettings
    {
        [Menu("Health Potion", "Use health potion")]
        public ToggleNode UseHealthPotion { get; set; } = new ToggleNode(false);

        [Menu("Health Potion", "Use when HP is below (%)")]
        public RangeNode<int> MinHealthPotion { get; set; } = new RangeNode<int>(50, 1, 100);

        [Menu("Health Potion", "Assigned key")]
        public HotkeyNode HealthPotionKey { get; set; } = new HotkeyNode(Keys.D1);

        [Menu("Health Potion", "Cooldown (ms)")]
        public RangeNode<int> HealthPotionCooldown { get; set; } = new RangeNode<int>(500, 100, 5000);

        private DateTime _nextAllowedHealthPotionUse { get; set; } = DateTime.Now;
    }

    [Submenu(CollapsedByDefault = true)]
    public class ManaPotionSettings
    {
        [Menu("Mana Potion", "Use mana potion")]
        public ToggleNode UseManaPotion { get; set; } = new ToggleNode(false);

        [Menu("Mana Potion", "Use when Mana is below (%)")]
        public RangeNode<int> MinManaPotion { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Mana Potion", "Assigned key")]
        public HotkeyNode ManaPotionKey { get; set; } = new HotkeyNode(Keys.D2);

        [Menu("Mana Potion", "Cooldown (ms)")]
        public RangeNode<int> ManaPotionCooldown { get; set; } = new RangeNode<int>(500, 100, 5000);

        private DateTime _nextAllowedManaPotionUse { get; set; } = DateTime.Now;
    }

    [Submenu(CollapsedByDefault = true)]
    public class EnergyShieldPotionSettings
    {
        [Menu("Energy Shield Potion", "Use ES potion")]
        public ToggleNode UseEnergyShieldPotion { get; set; } = new ToggleNode(false);

        [Menu("Energy Shield Potion", "Use when ES is below (%)")]
        public RangeNode<int> MinESPotion { get; set; } = new RangeNode<int>(30, 1, 100);

        [Menu("Energy Shield Potion", "Assigned key")]
        public HotkeyNode ESPotionKey { get; set; } = new HotkeyNode(Keys.D3);

        [Menu("Energy Shield Potion", "Cooldown (ms)")]
        public RangeNode<int> ESPotionCooldown { get; set; } = new RangeNode<int>(500, 100, 5000);

        private DateTime _nextAllowedESPotionUse { get; set; } = DateTime.Now;
    }
}
