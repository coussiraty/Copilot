using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using ImGuiNET;

using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

//TODO: setting for focus on picking or following (task method??)

namespace Copilot
{
    public class Copilot : BaseSettingsPlugin<CopilotSettings>
    {
        private Entity _followTarget;
        private DateTime _nextAllowedActionTime = DateTime.Now; // Cooldown timer
        private DateTime _nextAllowedBlinkTime = DateTime.Now;
        private DateTime _nextAllowedPotionUse = DateTime.Now;
        private DateTime _nextSkillUsageTime = DateTime.Now;

        private Vector3 lastTargetPosition = Vector3.Zero;

        public override bool Initialise()
        {
            // Initialize plugin
            Name = "Copilot";
            lastTargetPosition = Vector3.Zero;
            return base.Initialise();
        }

        public override void DrawSettings()
        {
            try {
                if (ImGui.Button("Get Party List")) GetPartyList();

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Get the list of party members");

                // draw the party list
                ImGui.Text("Party List:");
                var i = 0;
                foreach (var playerName in Settings.PartyElements) {
                    if (string.IsNullOrEmpty(playerName)) continue;
                    i++;
                    if (ImGui.Button("Set " + playerName + " as target"))
                        Settings.TargetPlayerName.Value = playerName;
                }
                if (i == 0) ImGui.Text("No party members found");
            }
            catch (Exception) { /* Handle exceptions silently */ }
            base.DrawSettings();
        }


        public override void Render()
        {
            if (!GameController.Window.IsForeground()) return;

            // Handle pause/unpause toggle
            if (Settings.TogglePauseHotkey.PressedOnce()) Settings.IsPaused.Value = !Settings.IsPaused.Value;

            // If paused, disabled, or not ready for the next action, do nothing
            if (!Settings.Enable.Value || Settings.IsPaused.Value || DateTime.Now < _nextAllowedActionTime || GameController.Player == null || GameController.IsLoading) return;

            try
            {
                // Check if there are any UI elements blocking the player
                var checkpoint = GameController.IngameState.UIRoot?.Children?[1]?.Children?[64];
                var market = GameController.IngameState.UIRoot?.Children?[1]?.Children?[27];
                var leftPanel = GameController.IngameState.IngameUi.OpenLeftPanel;
                var rightPanel = GameController.IngameState.IngameUi.OpenRightPanel;
                var worldMap = GameController.IngameState.IngameUi.WorldMap;
                var npcDialog = GameController.IngameState.IngameUi.NpcDialog;
                var treePanel = GameController.IngameState.IngameUi.TreePanel;
                var atlasTreePanel = GameController.IngameState.IngameUi.AtlasTreePanel;


                if ((checkpoint?.IsVisible != null && (bool)checkpoint?.IsVisible) ||
                    (leftPanel?.IsVisible != null && (bool)leftPanel?.IsVisible) ||
                    (rightPanel?.IsVisible != null && (bool)rightPanel?.IsVisible) ||
                    (worldMap?.IsVisible != null && (bool)worldMap?.IsVisible) ||
                    (npcDialog?.IsVisible != null && (bool)npcDialog?.IsVisible) ||
                    (treePanel?.IsVisible != null && (bool)treePanel?.IsVisible) ||
                    (atlasTreePanel?.IsVisible != null && (bool)atlasTreePanel?.IsVisible) ||

                    (market?.IsVisible != null && (bool)market?.IsVisible))
                {
                    Keyboard.KeyDown(Keys.Space);
                    Keyboard.KeyUp(Keys.Space);
                    _nextAllowedActionTime = DateTime.Now.AddMilliseconds(Settings.ActionCooldown.Value);
                    return;
                }

                var resurrectPanel = GameController.IngameState.IngameUi.ResurrectPanel;
                if (resurrectPanel != null && resurrectPanel.IsVisible) {
                    var inTown = resurrectPanel?.ResurrectInTown;
                    var atCheckpoint = resurrectPanel?.ResurrectAtCheckpoint;
                    var btn = atCheckpoint ?? inTown; // if inTown is null, use atCheckpoint
                    if (btn != null && btn.IsVisible) {
                        var screenPoint = new Point((int)btn.GetClientRectCache.Center.X, (int)btn.GetClientRectCache.Center.Y);
                        Mouse.SetCursorPosition(screenPoint);
                        Thread.Sleep(300);
                        Mouse.LeftClick(screenPoint);
                    }
                    _nextAllowedActionTime = DateTime.Now.AddMilliseconds(1000);
                    return;
                }

                // TODO: handle picking up items??
                UsePotions();
                FollowTarget();
                UseAttackSkill();
            } 
            catch (Exception) { /* Handle exceptions silently */ }
        }

        public override void AreaChange(AreaInstance area)
        {
            lastTargetPosition = Vector3.Zero;
            _followTarget = null;
            base.AreaChange(area);
        }

        private void UsePotions()
        {
            if (!Settings.Potions.EnablePotions.Value)
                return;

            var now = DateTime.Now;
            var playerLife = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>();
            if (playerLife == null) return;

            double currentHP = playerLife.CurHP;
            double maxHP = playerLife.MaxHP;
            double hpPercent = (currentHP / maxHP) * 100;

            double currentMana = playerLife.CurMana;
            double maxMana = playerLife.MaxMana;
            double manaPercent = (currentMana / maxMana) * 100;

            double currentES = playerLife.CurES;
            double maxES = playerLife.MaxES;
            double esPercent = (maxES > 0) ? (currentES / maxES) * 100 : 100;

            if (Settings.Potions.HealthPotion.UseHealthPotion.Value &&
                hpPercent <= Settings.Potions.HealthPotion.MinHealthPotion.Value &&
                now >= Settings.Potions.HealthPotion._nextAllowedHealthPotionUse)
            {
                Keyboard.KeyDown(Settings.Potions.HealthPotion.HealthPotionKey.Value);
                Thread.Sleep(10);
                Keyboard.KeyUp(Settings.Potions.HealthPotion.HealthPotionKey.Value);

                Settings.Potions.HealthPotion._nextAllowedHealthPotionUse = now.AddMilliseconds(Settings.Potions.HealthPotion.HealthPotionCooldown.Value);
            }

            if (Settings.Potions.ESPotion.UseEnergyShieldPotion.Value &&
                esPercent <= Settings.Potions.ESPotion.MinESPotion.Value &&
                now >= Settings.Potions.ESPotion._nextAllowedESPotionUse)
            {
                Keyboard.KeyDown(Settings.Potions.ESPotion.ESPotionKey.Value);
                Thread.Sleep(10);
                Keyboard.KeyUp(Settings.Potions.ESPotion.ESPotionKey.Value);

                Settings.Potions.ESPotion._nextAllowedESPotionUse = now.AddMilliseconds(Settings.Potions.ESPotion.ESPotionCooldown.Value);
            }

            if (Settings.Potions.ManaPotion.UseManaPotion.Value &&
                manaPercent <= Settings.Potions.ManaPotion.MinManaPotion.Value &&
                now >= Settings.Potions.ManaPotion._nextAllowedManaPotionUse)
            {
                Keyboard.KeyDown(Settings.Potions.ManaPotion.ManaPotionKey.Value);
                Thread.Sleep(10);
                Keyboard.KeyUp(Settings.Potions.ManaPotion.ManaPotionKey.Value);
                
                Settings.Potions.ManaPotion._nextAllowedManaPotionUse = now.AddMilliseconds(Settings.Potions.ManaPotion.ManaPotionCooldown.Value);
            }
        }
        
        
        private bool AreValidMonstersNearby()
        {
            if (!Settings.AttackAssist.EnableAttackAssist.Value)
            {
                DebugWindow.LogMsg("[AttackAssist] Feature is disabled", 2.0f, Color.Yellow);
                return false;
            }

            var player = GameController.Player;
            if (player == null)
            {
                DebugWindow.LogError("[AttackAssist] Player entity not found!");
                return false;
            }

            var playerPos = player.Pos;
            float maxDistance = Settings.AttackAssist.MobDistanceThreshold.Value;

            var selectedRarities = new List<MonsterRarity>();
            if (Settings.AttackAssist.TargetWhite.Value) selectedRarities.Add(MonsterRarity.White);
            if (Settings.AttackAssist.TargetMagic.Value) selectedRarities.Add(MonsterRarity.Magic);
            if (Settings.AttackAssist.TargetRare.Value) selectedRarities.Add(MonsterRarity.Rare);
            if (Settings.AttackAssist.TargetUnique.Value) selectedRarities.Add(MonsterRarity.Unique);

            if (selectedRarities.Count == 0)
            {
                DebugWindow.LogMsg("[AttackAssist] No rarities selected!", 2.0f, Color.Orange);
                return false;
            }

            var monsterList = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster];

            var validMonsters = monsterList
                .Where(monster =>
                    monster != null &&
                    monster.IsValid &&
                    monster.IsAlive &&
                    !monster.IsTargetable &&
                    !monster.IsHidden &&
                    selectedRarities.Contains(monster.Rarity) &&
                    Vector3.Distance(playerPos, monster.Pos) <= maxDistance)
                .ToList();

            if (validMonsters.Count > 0)
            {
                DebugWindow.LogMsg($"[AttackAssist] Found {validMonsters.Count} valid monsters in range!", 2.0f, Color.Green);
                return true;
            }
            else
            {
                DebugWindow.LogMsg("[AttackAssist] No monsters found in range", 2.0f, Color.Red);
                return false;
            }
        }


        private void UseAttackSkill()
        {
            if (DateTime.Now < _nextSkillUsageTime)
                return;

            if (!Settings.AttackAssist.EnableAttackAssist.Value)
                return;

            if (!AreValidMonstersNearby())
                return;

            DebugWindow.LogMsg("[AttackAssist] Using attack skill!", 2.0f, Color.Cyan);

            Keyboard.KeyDown(Settings.AttackAssist.AssignedSkillKey.Value);
            Thread.Sleep(50); 
            Keyboard.KeyUp(Settings.AttackAssist.AssignedSkillKey.Value);

            _nextSkillUsageTime = DateTime.Now.AddMilliseconds(Settings.AttackAssist.SkillCooldown.Value);
        }

        private void FollowTarget()
        {
            try
            {
                _followTarget = GetFollowingTarget();
                var leaderPE = GetLeaderPartyElement();
                var myPos = GameController.Player.Pos;
                var isInTown = GameController.Area.CurrentArea.IsTown;
                var currentArea = GameController.Area.CurrentArea;

                if (_followTarget == null && !leaderPE.ZoneName.Equals(GameController.Area.CurrentArea.DisplayName)) {
                    var portal = GetBestPortalLabel();
                    var distanceToPortal = portal != null ? Vector3.Distance(myPos, portal.ItemOnGround.Pos) : 501;

                    if (currentArea.IsHideout && distanceToPortal <= 1000) { // if in hideout and near the portal
                        var screenPos = GameController.IngameState.Camera.WorldToScreen(portal.ItemOnGround.Pos);
                        var screenPoint = new Point((int)screenPos.X, (int)screenPos.Y);
                        Mouse.SetCursorPosition(screenPoint);
                        Thread.Sleep(500);
                        Mouse.LeftClick(screenPoint);
                        if (leaderPE?.TpButton != null && GetTpConfirmation() != null)
                        {
                            Keyboard.KeyDown(Keys.Space);
                            Keyboard.KeyUp(Keys.Space);
                        }
                    } else if (leaderPE?.TpButton != null) {
                        var screenPoint = GetTpButton(leaderPE);
                        Mouse.SetCursorPosition(screenPoint);
                        Thread.Sleep(100);
                        Mouse.LeftClick(screenPoint);

                        if (leaderPE.TpButton != null)
                        { // check if the tp confirmation is open
                            var tpConfirmation = GetTpConfirmation();
                            if (tpConfirmation != null)
                            {
                                screenPoint = new Point((int)tpConfirmation.GetClientRectCache.Center.X, (int)tpConfirmation.GetClientRectCache.Center.Y);
                                Mouse.SetCursorPosition(screenPoint);
                                Thread.Sleep(100);
                                Mouse.LeftClick(screenPoint);
                            }
                        }
                    }
                    _nextAllowedActionTime = DateTime.Now.AddMilliseconds(500);
                    return;
                }

                // Check distance to target
                if (_followTarget == null || isInTown) return;

                var targetPos = _followTarget.Pos;
                if (lastTargetPosition == Vector3.Zero) lastTargetPosition = targetPos;
                var distanceToTarget = Vector3.Distance(myPos, targetPos);

                // If within the follow distance, skip actions
                if (distanceToTarget <= Settings.FollowDistance.Value || distanceToTarget <= Settings.IdleDistance.Value) return;

                // check if is possible to move to the target but the tp confirmation is open
                if (leaderPE?.TpButton != null && GetTpConfirmation() != null)
                {
                    Keyboard.KeyDown(Keys.Space);
                    Keyboard.KeyUp(Keys.Space);
                }

                // check if there is areatransition in the area and boss
                // var thereIsBossNear = GameController.Entities.Any(e => e.Type == EntityType.Monster && e.IsAlive && e.Rarity == MonsterRarity.Unique && Vector3.Distance(myPos, e.Pos) < 2000);

                // check if the distance of the target changed significantly from the last position OR if there is a boss near and the distance is less than 2000
                if (distanceToTarget > 3000 /* || (thereIsBossNear && distanceToTarget < 2000) */) // TODO: fix this arena
                {
                    ClickBestPortal();
                    _nextAllowedActionTime = DateTime.Now.AddMilliseconds(1000);
                    return;
                }
                else if (Settings.Blink.UseBlink.Value && DateTime.Now > _nextAllowedBlinkTime && distanceToTarget > Settings.Blink.BlinkRange.Value)
                {
                    MoveToward(targetPos);
                    Keyboard.KeyDown(Keys.Space);
                    Keyboard.KeyUp(Keys.Space);
                    _nextAllowedBlinkTime = DateTime.Now.AddMilliseconds(Settings.Blink.BlinkCooldown.Value);
                }
                else
                {
                    MoveToward(targetPos);
                }

                // Set the cooldown for the next allowed action
                _nextAllowedActionTime = DateTime.Now.AddMilliseconds(Settings.ActionCooldown.Value);
            }
            catch (Exception) { /* Handle exceptions silently */ }
        }

        private Entity GetFollowingTarget()
        {
            try
            {
                var leaderName = Settings.TargetPlayerName.Value.ToLower();
                var target = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player].FirstOrDefault(x => string.Equals(x.GetComponent<Player>()?.PlayerName.ToLower(), leaderName, StringComparison.OrdinalIgnoreCase));
                return target;
            }
            catch (Exception e)
            {
                LogError(e.Message);
                return null;
            }
        }

        private PartyElementWindow GetLeaderPartyElement()
        {
            try
            {
                var partyElementList = GameController?.IngameState?.IngameUi?.PartyElement.Children?[0]?.Children;
                var leader = partyElementList?.FirstOrDefault(partyElement => partyElement?.Children?[0]?.Children?[0]?.Text?.ToLower() == Settings.TargetPlayerName.Value.ToLower());
                var leaderPartyElement = new PartyElementWindow
                {
                    PlayerName = leader.Children?[0]?.Children?[0]?.Text,
                    TpButton = leader?.Children?[leader.ChildCount == 4 ? 3 : 2],
                    ZoneName = leader?.Children?.Count == 4 ? leader.Children[2].Text : GameController.Area.CurrentArea.DisplayName
                };
                return leaderPartyElement;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void GetPartyList()
        {
            // Settings
            var partyElements = new string[5];
            try
            {
                var partyElementList = GameController?.IngameState?.IngameUi?.PartyElement.Children?[0]?.Children;
                var i = 0;
                foreach (var partyElement in partyElementList)
                {
                    var playerName = partyElement?.Children?[0]?.Children?[0]?.Text;
                    partyElements[i] = playerName;
                    i++;
                }
            }
            catch (Exception) { /* Handle exceptions silently */ }

            Settings.PartyElements = partyElements;
        }

        private LabelOnGround GetBestPortalLabel()
        {
            try
            {
                var currentZoneName = GameController.Area.CurrentArea.DisplayName;
                var portalLabels =
                    GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Where(x => x.ItemOnGround.Metadata.ToLower().Contains("areatransition") || x.ItemOnGround.Metadata.ToLower().Contains("portal"))
                        .OrderBy(x => Vector3.Distance(lastTargetPosition, x.ItemOnGround.Pos)).ToList();

                var random = new Random();

                return GameController?.Area?.CurrentArea?.IsHideout != null && (bool) GameController?.Area?.CurrentArea?.IsHideout
                    ? portalLabels?[random.Next(portalLabels.Count)]
                    : portalLabels?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private void ClickBestPortal()
        {
            try
            {
                var portal = GetBestPortalLabel();
                if (portal != null)
                {
                    var screenPos = GameController.IngameState.Camera.WorldToScreen(portal.ItemOnGround.Pos);
                    var screenPoint = new Point((int)screenPos.X, (int)screenPos.Y);
                    Mouse.SetCursorPosition(screenPoint);
                    Thread.Sleep(300);
                    Mouse.LeftClick(screenPoint);
                }
            }
            catch (Exception) { /* Handle exceptions silently */ }
        }

        private Point GetTpButton(PartyElementWindow leaderPE)
        {
            try
            {
                var windowOffset = GameController.Window.GetWindowRectangle().TopLeft;
                var elemCenter = (Vector2) leaderPE?.TpButton?.GetClientRectCache.Center;
                var finalPos = new Point((int) (elemCenter.X + windowOffset.X), (int) (elemCenter.Y + windowOffset.Y));

                return finalPos;
            }
            catch
            {
                return Point.Empty;
            }
        }

        private Element GetTpConfirmation()
        {
            try
            {
                var ui = GameController?.IngameState?.IngameUi?.PopUpWindow.Children[0].Children[0];

                if (ui.Children[0].Text.Equals("Are you sure you want to teleport to this player's location?"))
                    return ui.Children[3].Children[0];

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void MoveToward(Vector3 targetPos)
        {
            try
            {
                var screenPos = GameController.IngameState.Camera.WorldToScreen(targetPos);
                var screenPoint = new Point((int)screenPos.X, (int)screenPos.Y);

                Mouse.SetCursorPosition(screenPoint);
                Mouse.LeftClick(screenPoint);
                lastTargetPosition = targetPos;
            }
            catch (Exception) { /* Handle exceptions silently */ }
        }
    }
}

public class PartyElementWindow
{
    public string PlayerName { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public Element TpButton { get; set; } = new Element();

    public override string ToString()
    {
        return $"{PlayerName}, current zone: {ZoneName}";
    }
}