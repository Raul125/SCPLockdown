using ScpLockdown.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using MEC;
using Exiled.Events.EventArgs;
using ScpLockdown.States;
using Interactables.Interobjects.DoorUtils;
using System.Collections.ObjectModel;
using Exiled.API.Features;
using Exiled.API.Extensions;

namespace ScpLockdown.EventHandlers
{
    public class Handler
    {
        public ScpLockdown plugin;
        public LockdownStates _lockdownStates;
        public List<CoroutineHandle> runningCoroutines;
        public List<Exiled.API.Features.Door> Doorsdb { get; } = new List<Exiled.API.Features.Door>();

        public Handler(ScpLockdown plugin)
        {
            this.plugin = plugin;
            _lockdownStates = new LockdownStates();
            runningCoroutines = new List<CoroutineHandle>();
        }

        public void OnRoundStart()
        {
            Dictionary<RoleType, int> configScpList = new Dictionary<RoleType, int>();

            if (plugin.Config.ClassDLock > 0)
            {
                runningCoroutines.Add(Timing.RunCoroutine(Methods.OpenDoorsAfterTime()));
            }

            if (plugin.Config.CassieTime > 0)
            {
                runningCoroutines.Add(Timing.RunCoroutine(Methods.CassieMsg()));
            }

            foreach (var doortype in plugin.Config.LockedDoors)
            {
                var door = Map.Doors.First(x => x.Type == doortype.Key);
                door.Base.ServerChangeLock(DoorLockReason.AdminCommand, true);
                runningCoroutines.Add(Timing.CallDelayed(doortype.Value, () =>
                {
                    door.Base.ServerChangeLock(DoorLockReason.AdminCommand, false);
                }));
            }

            foreach (var entry in plugin.Config.AffectedScps)
            {
                if (!configScpList.Select(x => x.Key).Contains(entry.Key))
                {
                    configScpList.Add(entry.Key, entry.Value);
                }
            }

            Timing.CallDelayed(1, () =>
            {
                foreach (KeyValuePair<RoleType, int> scp in configScpList)
                {
                    _lockdownStates.ToggleLockedUpState(scp.Key);

                    switch (scp.Key)
                    {
                        case RoleType.Scp079:
                            runningCoroutines.Add(Timing.RunCoroutine(Methods.Unlock079s(scp.Value)));
                            break;
                        case RoleType.Scp173:
                            Methods.Lockdown173(scp);
                            break;
                        case RoleType.Scp106:
                            Methods.Lockdown106(scp);
                            break;
                        case RoleType.Scp049:
                            Methods.Lockdown049(scp);
                            break;
                        case RoleType.Scp096:
                            Methods.Lockdown096(scp);
                            break;
                        case RoleType.Scp93953:
                        case RoleType.Scp93989:
                            Methods.Lockdown939(scp);
                            break;
                    }
                }
            });
        }

        public void OnWaitingForPlayers()
        {
            ResetAllStates();
            if (plugin.Config.ClassDLock > 0)
            {
                this.Doorsdb.Clear();
                ReadOnlyCollection<Exiled.API.Features.Door> doors = Map.Doors;
                int num = doors.Count();
                for (int i = 0; i < num; i++)
                {
                    Exiled.API.Features.Door doorVariant = doors[i];
                    if (doorVariant.Base.name.StartsWith("Prison"))
                    {
                        doorVariant.Base.ServerChangeLock(DoorLockReason.AdminCommand, true);
                        this.Doorsdb.Add(doorVariant);
                    }
                }
            }
        }

        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (LockdownStates.Scp106LockedUp && ev.Player.Role == RoleType.Scp106)
            {
                ev.Player.IsGodModeEnabled = false;
            }

            // This makes Scp106 lockdown compatible with scpswap
            if (LockdownStates.Scp106LockedUp == true && ev.NewRole == RoleType.Scp106)
            {
                Timing.CallDelayed(1f, () =>
                {
                    if (ev.Player.Role == RoleType.Scp106)
                    {
                        Methods.LockSingle106(ev.Player);
                    }
                });
            }
        }

        public void OnRoundEnded(RoundEndedEventArgs ev)
        {
            foreach (CoroutineHandle coroutine in runningCoroutines)
            {
                Timing.KillCoroutines(coroutine);
            }
            runningCoroutines.Clear();
        }

        public void OnRoundRestarting()
        {
            // This prevents us from having unwanted coroutines running
            foreach (CoroutineHandle coroutine in runningCoroutines)
            {
                Timing.KillCoroutines(coroutine);
            }
            runningCoroutines.Clear();
        }

        public void OnFailingEscapePocketDimension(FailingEscapePocketDimensionEventArgs ev)
        {
            if (ev.Player.Role == RoleType.Scp106 && LockdownStates.Scp106LockedUp)
            {
                ev.Player.SendToPocketDimension();
                ev.IsAllowed = false;
            }
        }

        public void OnEscapingPocketDimension(EscapingPocketDimensionEventArgs ev)
        {
            if (ev.Player.Role == RoleType.Scp106 && LockdownStates.Scp106LockedUp)
            {
                ev.Player.SendToPocketDimension();
                ev.IsAllowed = false;
            }
        }

        public void OnCreatingPortal(CreatingPortalEventArgs ev)
        {
            if (LockdownStates.Scp106LockedUp)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnTeleporting(TeleportingEventArgs ev)
        {
            if (LockdownStates.Scp106LockedUp)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnInteractingTesla(InteractingTeslaEventArgs ev)
        {
            if (LockdownStates.Scp079LockedUp)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            // Using Player event because 079 event isn't working, idk why
            if (LockdownStates.Scp079LockedUp && ev.Player.Role == RoleType.Scp079)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnChangingCamera(ChangingCameraEventArgs ev)
        {
            if (LockdownStates.Scp079LockedUp && !plugin.Config.Scp079Camera)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnElevatorTeleport(ElevatorTeleportingEventArgs ev)
        {
            if (LockdownStates.Scp079LockedUp && !plugin.Config.Scp079Camera)
            {
                ev.IsAllowed = false;
            }
        }

        public void OnStartingSpeaker(StartingSpeakerEventArgs ev)
        {
            if (LockdownStates.Scp079LockedUp)
            {
                ev.IsAllowed = false;
            }
        }

        public void ResetAllStates()
        {
            _lockdownStates.ResetAllStates();
        }
    }
}
