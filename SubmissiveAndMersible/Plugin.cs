using ClickLib.Clicks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using static ECommons.GenericHelpers;

namespace SubmissiveAndMersible
{
    public unsafe sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Submissive and Mersible";
        private const string CommandName = "/sam";

        public DalamudPluginInterface PluginInterface { get; init; }
        public ICommandManager CommandManager { get; init; }

        private TaskManager taskManager;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;

            ECommonsMain.Init(pluginInterface, this);
            this.taskManager = new();
            this.taskManager.AbortOnTimeout = true;
            this.taskManager.ShowDebug = false;
            AutoCutsceneSkipper.Init((_) => this.taskManager.NumQueuedTasks > 0);

            this.CommandManager.AddHandler(CommandName, new CommandInfo((_, _) => CheckSubmersibleList())
            {
                HelpMessage = "Run this command after opening your list of submersibles."
            });
        }

        public void Dispose()
        {
            this.CommandManager.RemoveHandler(CommandName);
        }

        private bool CheckSubmersibleList()
        {
            if (!TryGetAddonByName<AddonSelectString>("SelectString", out var addon))
            {
                return false;
            }

            var atkUnitBase = &addon->AtkUnitBase;
            if (!IsAddonReady(atkUnitBase))
            {
                return false;
            }

            var textNode = atkUnitBase->UldManager.NodeList[3]->GetAsAtkTextNode();
            var text = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
            if (!text.Contains("Select a submersible."))
            {
                return false;
            }

            var menu = addon->PopupMenu.PopupMenu;
            for (var i = 0; i < menu.EntryCount - 1; i++) // last entry is "Nothing."
            {
                var str = MemoryHelper.ReadSeStringNullTerminated((nint)menu.EntryNames[i]).ExtractText();
                if (str.Contains("Voyage complete in")) // (Voyage complete in X)
                {
                    continue;
                }

                ClickSelectString.Using((nint)addon).SelectItem((ushort)i);
                this.taskManager.DelayNext(300);
                if (str.Contains("[Voyage complete]")) // [Voyage complete]
                {
                    this.taskManager.Enqueue(() => VoyageResultFinalize());
                    this.taskManager.DelayNext(4000);
                    this.taskManager.Enqueue(() => VoyageDetailsCancel());
                    this.taskManager.DelayNext(300);
                }
                this.taskManager.Enqueue(() => SelectItemWithText("Repair submersible components"));
                this.taskManager.DelayNext(300);
                this.taskManager.Enqueue(() => RepairSubmersibleComponents());
                this.taskManager.Enqueue(() => CloseRepairMenu());
                this.taskManager.DelayNext(300);
                this.taskManager.Enqueue(() => SelectItemWithText("View previous voyage log"));
                this.taskManager.DelayNext(300);
                this.taskManager.Enqueue(() => VoyageResultRedeploy());
                this.taskManager.DelayNext(300);
                this.taskManager.Enqueue(() => VoyageDetailsDeploy());
                this.taskManager.Enqueue(() => Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]);
                this.taskManager.Enqueue(() => !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || SelectItemWithText("Yes.", "Skip cutscene?"));
                this.taskManager.DelayNext(1000);
                this.taskManager.Enqueue(() => CheckSubmersibleList());
                return true;
            }

            return true;
        }

        private bool RepairSubmersibleComponents()
        {
            if (!TryGetAddonByName<AtkUnitBase>("CompanyCraftSupply", out var addon) || !IsAddonReady(addon))
            {
                return false;
            }

            // 0: item id (uint)
            // 1: icon id (uint)
            // 2: name (string)
            // 3: condition % (uint)
            // 4: ? (25004)
            // 5: index (uint)
            // 6: dye? (uint)
            // 7: ? (bool)
            // [...]
            // 32: rank (uint)
            // 33: exp text (string)
            // 34: exp % (float)

            for (var i = 0; i < 4; i++)
            {
                var conditionIndex = (i * 8) + 3;
                if (conditionIndex >= addon->AtkValuesCount)
                {
                    break;
                }

                var condition = addon->AtkValues[conditionIndex].UInt;
                if (condition >= 100)
                {
                    continue;
                }

                // trigger repair
                this.taskManager.EnqueueImmediate(MakeRepairTask(addon, i));
                this.taskManager.DelayNextImmediate(100);
                this.taskManager.EnqueueImmediate(() => AcceptRepair());
                this.taskManager.DelayNextImmediate(100);
            }

            return true;
        }

        private Func<bool?> MakeRepairTask(AtkUnitBase* addon, int index)
        {
            return () =>
            {
                if (!IsAddonReady(addon))
                {
                    return false;
                }

                var atkValues = new AtkValueArray(3, 0, index, 0, 0, 0);
                addon->FireCallback(atkValues.Length, atkValues);
                atkValues.Dispose();
                return true;
            };
        }

        private bool AcceptRepair()
        {
            if (!TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !IsAddonReady(addon))
            {
                return false;
            }

            ClickSelectYesNo.Using((nint)addon).Yes();
            return true;
        }

        private bool VoyageDetailsCancel() => Clicker.ClickAddonButton("AirShipExplorationDetail", 2);

        private bool VoyageDetailsDeploy() => Clicker.ClickAddonButton("AirShipExplorationDetail", 3);

        private bool CloseRepairMenu() => Clicker.ClickAddonButton("CompanyCraftSupply", 3);

        private bool VoyageResultFinalize() => Clicker.ClickAddonButton("AirShipExplorationResult", 2);

        private bool VoyageResultRedeploy() => Clicker.ClickAddonButton("AirShipExplorationResult", 3);

        private bool SelectItemWithText(string text, string? checkPrompt = null)
        {
            if (!TryGetAddonByName<AddonSelectString>("SelectString", out var addon))
            {
                return false;
            }

            var atkUnitBase = &addon->AtkUnitBase;
            if (!IsAddonReady(atkUnitBase))
            {
                return false;
            }

            if (checkPrompt != null)
            {
                var textNode = atkUnitBase->UldManager.NodeList[3]->GetAsAtkTextNode();
                var prompt = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
                if (prompt?.Contains(checkPrompt) != true)
                {
                    return false;
                }
            }

            var menu = addon->PopupMenu.PopupMenu;
            for (var i = 0; i < menu.EntryCount; i++)
            {
                var str = MemoryHelper.ReadSeStringNullTerminated((nint)menu.EntryNames[i]).ExtractText();
                if (str == text)
                {
                    ClickSelectString.Using((nint)addon).SelectItem((ushort)i);
                    return true;
                }
            }

            return false;
        }
    }
}
