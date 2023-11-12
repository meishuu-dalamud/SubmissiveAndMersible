using ClickLib.Enums;
using ClickLib.Structures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;
using static ECommons.GenericHelpers;

namespace SubmissiveAndMersible
{
    internal static unsafe class Clicker
    {
        internal unsafe delegate IntPtr ReceiveEventDelegate(AtkEventListener* eventListener, EventType evt, uint which, void* eventData, void* inputData);

        private static bool ClickButton(AtkUnitBase* atkUnitBase, uint nodeIndex)
        {
            var atkComponentButton = atkUnitBase->UldManager.NodeList[2]->GetAsAtkComponentButton();
            if (!atkComponentButton->IsEnabled)
            {
                return false;
            }

            var listener = &atkUnitBase->AtkEventListener;
            var receiveEventAddress = new IntPtr(listener->vfunc[2]);
            var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;

            var eventData = EventData.ForNormalTarget(atkComponentButton->AtkComponentBase.OwnerNode, atkUnitBase);
            var inputData = InputData.Empty();
            receiveEvent(listener, EventType.CHANGE, 0, eventData.Data, inputData.Data);

            return true;
        }

        public static bool ClickAddonButton(string addonName, uint buttonNodeIndex)
        {
            return TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon) && ClickButton(addon, buttonNodeIndex);
        }
    }
}
