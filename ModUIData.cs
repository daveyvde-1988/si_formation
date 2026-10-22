using System;
using System.Globalization;
using System.Linq;
using ModUI;
using Newtonsoft.Json.Linq;
namespace Si_Formation
{
    public sealed partial class Formations
    {
        public static string ModUIData()
        {
            return ModUIExport.New()
                .Info("Formations: "+(Running?"running":"off/unavailable")+". Choices remain available; gameplay rules are unchanged.")
                .Status("master","Formation service",Running?"ON":"OFF")
                .Action("/formation","Chat menu","Open the existing team/role formation menu.")
                .Action("/formation status","Status","Show your formation selections.")
                .Action("/formation help","Help","Show current commands and permissions.")
                .OnOff("/formation enabled","Global formations","Match-only master switch.",true,Enabled).Presentation("switch","General")
                .Value("/formationsizeglobal","FPS group limit","Match-wide maximum including the leader.",true,configuredLimit.ToString(CultureInfo.InvariantCulture)).Presentation("slider","Limits",1,50,1,defaultValue:(groupLimitDefault?.Value??BuiltInLimit).ToString(CultureInfo.InvariantCulture))
                .Value("/formationsizecommander","Commander selection limit","Saved maximum units per formation order.",true,CommanderOrderLimit.ToString(CultureInfo.InvariantCulture)).Presentation("slider","Limits",1,100,1,defaultValue:"75")
                .Value("/Formationtraveldistance","Locked travel maximum (m)","Saved maximum travel distance; zero keeps the centre in place.",true,LockTravelMaximum.ToString(CultureInfo.InvariantCulture)).Presentation("value","Movement",0,10000,1,defaultValue:"1000")
                .OnOff("/Formationspeedcap","Arrival speed cap","Match override; centre-based pacing stays active.",true,SpeedCapEnabled).Presentation("checkbox","Movement")
                .OnOff("/formation debug","Debug","Match diagnostic logging.",true,debug).Presentation("checkbox","Diagnostics")
                .Build();
        }
        public static string ModUIPlayerData(ulong steamId)
        {
            var p=Connected(steamId);
            if(!p||!Ready||TeamName(p.Team)==null)return null;
            var builder=ModUIExport.New();
            foreach(string function in p.IsCommander?new[]{"commander"}:new[]{"move","follow","attack"})
            {
                string revision=PanelRevision(p,function);
                var choices=new[]{("1","Native / default")}.Concat(MenuChoices(TeamName(p.Team),function).Select(f=>(f.MenuOrder.ToString(CultureInfo.InvariantCulture),f.Name))).ToArray();
                builder.Switch("/formation panel "+revision+" "+function,function+" formation","Choices for your current faction and role.",false,(Selected(p,function)?.MenuOrder??1).ToString(CultureInfo.InvariantCulture),choices)
                    .Presentation("dropdown","My formations");
            }
            return builder.Build();
        }
    }
}
