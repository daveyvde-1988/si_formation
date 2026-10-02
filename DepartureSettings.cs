using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static MelonPreferences_Entry<float> softDelayDefault,lockDelayDefault,speedLockDelayDefault;
        private static float? softDelayOverride,lockDelayOverride,speedLockDelayOverride;
        private static bool ValidDelay(float n)=>n==0||(!float.IsNaN(n)&&n>=0.1f&&n<=5f);
        private static float SoftDelay=>ValidDelay(softDelayOverride??softDelayDefault.Value)?softDelayOverride??softDelayDefault.Value:0.2f;
        private static float SpeedLockDelay=>ValidDelay(speedLockDelayOverride??speedLockDelayDefault.Value)?speedLockDelayOverride??speedLockDelayDefault.Value:0.3f;
        private static float LockDelay=>SpeedLockDelay>0?0:ValidDelay(lockDelayOverride??lockDelayDefault.Value)?lockDelayOverride??lockDelayDefault.Value:0;
        private void RegisterDepartureSettings()
        {
            softDelayDefault=prefs.CreateEntry("StartDelaySoftLock",0.2f,description:"Seconds between custom formation 20-percent departure batches (maximum five): 0 disables, otherwise 0.1..5");
            speedLockDelayDefault=prefs.CreateEntry("StartDelaySpeedLock",0.3f,description:"Seconds between hard-lock speed tiers, slowest TopSpeed first; overrides positional delay. 0 disables, otherwise 0.1..5");
            lockDelayDefault=prefs.CreateEntry("StartDelayLock",0f,description:"Seconds between explicit formation lock departure rows: 0 disables, otherwise 0.1..5");
            RegisterCommanderCommand("startdelayspeedlock",(p,a)=>DepartureCommand(p,a,true,true));
            RegisterCommanderCommand("startdelaysoftlock",(p,a)=>DepartureCommand(p,a,false));
            RegisterCommanderCommand("startdelaylock",(p,a)=>DepartureCommand(p,a,true));
            RegisterCommanderCommand("startdelay",(p,a)=>
            {
                if(!CommandArgument.TryParse(a,out var text,out bool quoted)||quoted){Reply(p,"usage: /startdelay lock <seconds>");return;}
                if(!text.StartsWith("lock ",StringComparison.OrdinalIgnoreCase)){Reply(p,"usage: /startdelay lock <seconds>");return;}
                DepartureCommand(p,"startdelaylock "+text.Substring(5),true);
            });
        }
        private static void DepartureCommand(Player p,string args,bool locked,bool bySpeed=false)
        {
            if(!ApiReady||!IsAdministrator(p)){Reply(p,"admin Generic power required.");return;}
            if(!CommandArgument.TryParse(args,out var text,out bool quoted)||quoted||
                !float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds)||!ValidDelay(seconds))
            {Reply(p,"delay: 0 to disable, or 0.1..5 seconds (decimal point).");return;}
            if(bySpeed){if(SpeedLockDelay>0||seconds>0)lockDelayOverride=0;speedLockDelayOverride=seconds;}
            else if(locked){lockDelayOverride=SpeedLockDelay>0?0:seconds;}else softDelayOverride=seconds;
            if(locked&&!bySpeed&&seconds>0&&SpeedLockDelay>0){Reply(p,"Speed-ordered departure is enabled; positional delay remains disabled. Use /startdelayspeedlock 0 first.");return;}
            Reply(p,(bySpeed?"Speed-ordered hard-lock":locked?"Lock":"Soft-lock")+" departure interval: "+seconds.ToString("0.###",CultureInfo.InvariantCulture)+" seconds. Applies to new moves this match; configuration unchanged.");
        }
        private static Dictionary<Unit,int> SpeedDepartureRows(IEnumerable<Unit> units)
        {
            var result=new Dictionary<Unit,int>();
            float tierSpeed=0;int tier=-1;
            foreach(var u in units.Where(u=>u).Distinct().OrderBy(u=>u.TopSpeed).ThenBy(u=>u.GetInstanceID()))
            {
                if(tier<0||u.TopSpeed>tierSpeed+0.001f){tier++;tierSpeed=u.TopSpeed;}
                result[u]=tier;
            }
            return result;
        }
        private static Dictionary<Unit,int> SoftDepartureBatches(IEnumerable<Unit> units,Vector3 forward)
        {
            var ordered=units.Where(u=>u).Distinct().OrderByDescending(u=>Vector3.Dot(Flat(u.transform.position),forward))
                .ThenBy(u=>u.GetInstanceID()).ToArray();
            int size=Math.Max(1,(ordered.Length+4)/5); // ceil(20%): never more than five starts
            var result=new Dictionary<Unit,int>();
            for(int i=0;i<ordered.Length;i++)result[ordered[i]]=i/size;
            return result;
        }
        // Project current positions onto the travel direction. A row's depth is based on
        // physical diameters; compare to its front edge, avoiding transitive row merging.
        private static Dictionary<Unit,int> DepartureRows(IEnumerable<Unit> units,Vector3 forward)
        {
            var result=new Dictionary<Unit,int>();
            float front=0,depth=0;int row=-1;
            foreach(var u in units.Where(u=>u).Distinct().OrderByDescending(u=>Vector3.Dot(Flat(u.transform.position),forward)).ThenBy(u=>u.GetInstanceID()))
            {
                float position=Vector3.Dot(Flat(u.transform.position),forward);
                float size=Math.Max(3,2*u.PhysicalRadius);
                if(row<0||front-position>Math.Max(depth,size)*0.6f){row++;front=position;depth=size;}
                result[u]=row;
            }
            return result;
        }
    }
}
