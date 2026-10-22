using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Silica.AI;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static Action<AIGroup> poolArmyGroup;

        private void InstallCommanderHandoverFix()
        {
            // The instance overload is private; bind its verified native implementation.
            // Do not recreate group removal or network replication ourselves.
            poolArmyGroup=(Action<AIGroup>)Delegate.CreateDelegate(typeof(Action<AIGroup>),
                AccessTools.Method(typeof(AIGroup),"ReturnToPool",Type.EmptyTypes));
            Patch(typeof(AIUnitHandler),"Init",transpiler:nameof(PatchCommanderHandover));
            Patch(typeof(AIUnitHandler),"Deinit",transpiler:nameof(PatchCommanderHandover));
        }

        private static IEnumerable<CodeInstruction> PatchCommanderHandover(IEnumerable<CodeInstruction> instructions)
        {
            var tasks=AccessTools.Method(typeof(AITaskBase),"ReturnToPool",new[]{typeof(Team)});
            var groups=AccessTools.Method(typeof(AIGroup),"ReturnToPool",new[]{typeof(Team)});
            int taskCalls=0,groupCalls=0;
            foreach(var i in instructions)
            {
                if(i.Calls(tasks))
                {i.operand=AccessTools.Method(typeof(Formations),nameof(PoolCommanderTasks));taskCalls++;}
                else if(i.Calls(groups))
                {i.operand=AccessTools.Method(typeof(Formations),nameof(PoolCommanderGroups));groupCalls++;}
                yield return i;
            }
            if(taskCalls!=1||groupCalls!=1)
                throw new InvalidOperationException("Expected one task and one group cleanup in AI commander unit-handler handover.");
        }

        private static bool FpsOwnedGroup(AIGroup group)
            =>group!=null&&group.Leader&&!group.Leader.IsDestroyed&&group.PlayerControlled;

        private static void PoolCommanderTasks(Team team)
        {
            if(!Ready||!Game.GetIsServer()||!team){AITaskBase.ReturnToPool(team);return;}
            // Preserve native Sentry exemptions. Shared tasks survive if an FPS squad
            // still uses them; the ordinary army groups detach during group cleanup.
            foreach(var task in team.Tasks.ToArray())
                if(task!=null&&task.Team==team&&task.Type!=EAITaskType.Sentry&&!task.Groups.Any(FpsOwnedGroup))
                    task.ReturnToPool();
        }

        private static void PoolCommanderGroups(Team team)
        {
            if(!Ready||!Game.GetIsServer()||!team){AIGroup.ReturnToPool(team);return;}
            foreach(var group in team.Groups.ToArray())
                if(group!=null&&group.Team==team&&!FpsOwnedGroup(group)&&
                    (group.Task==null||group.Task.Type!=EAITaskType.Sentry))
                    poolArmyGroup(group);
        }
    }
}
