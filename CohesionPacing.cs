using Silica.AI;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static float NominalMoveSpeed(Unit unit,AgentMoveSpeed mode,bool reverse=false)
        {
            float speed;
            if(reverse&&unit is Vehicle vehicle)
                speed=vehicle.ReverseNormalSpeed;
            else if(unit is Soldier soldier)
            {
                switch(mode)
                {
                    case AgentMoveSpeed.Stealth: speed=soldier.WalkSpeed; break;
                    case AgentMoveSpeed.Fast: speed=soldier.SprintSpeed; break;
                    default: speed=soldier.RunSpeed; break;
                }
            }
            else if(unit is CreatureDecapod creature)
            {
                if(IsFormationAir(unit))
                    speed=mode==AgentMoveSpeed.Stealth?creature.FlyCreepSpeed:creature.FlyMoveSpeed;
                else
                    speed=mode==AgentMoveSpeed.Stealth?creature.CreepSpeed:creature.MoveSpeed;
            }
            else
                speed=mode==AgentMoveSpeed.Fast?unit.TopSpeed:unit.NormalSpeed;

            // Plan from requested-mode statistics. CanSprint, stance, native speed
            // scales and measured velocity describe transient execution state.
            return float.IsNaN(speed)||float.IsInfinity(speed)?1:Mathf.Max(.1f,speed);
        }

        private static void SampleCohesionProgress(CohesionMember member,Vector3 forward,float dt)
        {
            if(!member.Unit)return;
            Vector3 position=Flat(member.Unit.transform.position);
            if(member.Started&&dt>0)
            {
                float progress=Vector3.Dot(position-member.ProgressSamplePosition,forward)/dt;
                progress=Mathf.Clamp(progress,0,member.FreeSpeed);
                // Smooth approximately one second of actual forward progress.
                member.ProgressSpeed=Mathf.Lerp(member.ProgressSpeed,progress,dt/(1+dt));
            }
            member.ProgressSamplePosition=position;
        }

        private static float CohesionProgressTarget(CohesionMember member,float pacePosition,float paceSpeed,float nominalPace)
        {
            var group=member.Group;
            float position=Vector3.Dot(Flat(member.Unit.transform.position)-member.Offset,group.Forward);
            member.ProgressGap=position-pacePosition;
            float allowance=Mathf.Max(1,Radius(member.Unit)*.5f+nominalPace*group.Tuning.ArrivalWindow);
            float correction=Mathf.Sign(member.ProgressGap)*Mathf.Max(0,Mathf.Abs(member.ProgressGap)-allowance)/2;
            // Behind the pace setters, faster members may accelerate to catch up.
            // Ahead of them, their cap falls below the measured group pace.
            return Mathf.Clamp(paceSpeed-correction,0,member.FreeSpeed);
        }
    }
}
