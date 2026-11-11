using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private const string SwitchGroup="General", DefaultGroup="Commander placement", AdvancedGroup="General", FpsGroup="FPS squads", RallyGroup="Rally", DiagnosticsGroup="Diagnostics";
        private const string AdvancedDescription="Human and Alien cohesion have independent switches, range boundaries and travel/health settings. Profile values interpolate between range endpoints. Dispersed selections can coordinate members already within the destination arrival radius. Use /cohesion for humans, /aliencohesion for aliens, and /cohesiondebug next to trace your next grouped move.";

        private static void FormationCommand(Player p,string args)
        {
            if(!p||!ready||!Game.GetIsServer())return;
            string text=(args??"").Trim();
            string root=text.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
            if(root.TrimStart('/','!','.').Equals("scout",StringComparison.OrdinalIgnoreCase)){ScoutCommand(p,args);return;}
            string value=Argument(args);
            if(value==""||value=="status"){FormationStatus(p);return;}
            if(value=="advanced"){Reply(p,AdvancedDescription);return;}
            if(value.StartsWith("enabled ",StringComparison.Ordinal))value=value.Substring(8).Trim();
            if(value!="on"&&value!="off"){Reply(p,"/formation status | /formation enabled on|off (admin)");return;}
            if(!DemandAdmin(p))return;
            bool wasEnabled=ServerEnabled;
            bool saved=SetServerEnabled(value=="on",p);
            if(wasEnabled&&!ServerEnabled)DisableFormationOverrides();
            Reply(p,"Formation mod "+(ServerEnabled?"ON":"OFF")+(saved?" (saved).":" (save failed)."));
        }

        private static void DisableFormationOverrides()
        {
            // Finish suppressed production handoffs before relinquishing our reservations.
            var pending=Pending.Values.ToArray();
            ClearCohesionState("master disabled");
            ClearFpsStates();ClearRallyState();Scouts.Clear();activeIndependent=false;activeLayout=null;
            foreach(var ticket in pending)
            {
                var unit=ticket.Unit;
                if(!unit||unit.IsDestroyed||unit.Team!=ticket.Team||unit.PlayerControlled||unit.ControlledBy||unit.InCompartment||
                    (unit.Driver&&unit.Driver.ControlledBy))continue;
                try{unit.RallyTo(ticket.NativeAnchor);}
                catch(Exception e){MelonLoader.MelonLogger.Warning("Restoring native rally: "+e.Message);}
            }
        }

        // Both menu forms and chat call this one report. Existing per-setting status
        // handlers supply their own values/formatting; no layout or preference is created.
        private static void FormationStatus(Player p)
        {
            Reply(p,"Si_Formation 3.7.0; server-wide configuration; enabled "+(ServerEnabled?"ON":"OFF")+"; effective "+(Active?"ON":"OFF")+".");
            SpacingCommand(p,"/formationspace status");
            RallyCommand(p,"/rallyspace status");
            foreach(string command in new[]{"sizeinfluence","alienspread","fpsspace","fpsdistance","fpsallowance"})
                TuningCommand(p,"/"+command+" status",command);
            MovementFixCommand(p,"/movementfix status");
            TravelCoverCommand(p,"/travelcoverdisabler status");
            Reply(p,CohesionSummary(false));
            Reply(p,CohesionSummary(true));
            Reply(p,"FPS distance uses (5m + player radius) x "+Multiplier(ServerSettings.FpsDistance).ToString("0.###",CultureInfo.InvariantCulture)+"; physical clearance can increase it.");
            Reply(p,"Applied features "+(Active?"active":"inactive")+": click/drag spacing, footprint clearance, Alien scaling, FPS follow/move/attack layouts, reserved ground/air rally parking.");
            Reply(p,"Rally: "+Pending.Count+" pending, "+Parked.Count+" reserved; FPS: "+FpsFollowers.Count+" follow groups, "+FpsTasks.Count+" move/attack groups.");
            if(p.IsCommander)ScoutCommand(p,"/scout status");
        }

        public static string ModUIData()
        {
            var commands=new JArray
            {
                // /formation (no arguments) and /formation status share the same report.
                new JObject{["t"]="/formation",["l"]="Formation Status",["d"]="Show /formation status using the current server configuration.",["a"]=false,
                    ["ui"]=new JObject{["kind"]="button",["group"]=SwitchGroup}}
            };
            AddSwitch(commands,"/formation enabled","Formation mod",ServerEnabled,"Enable or disable server-wide formation tuning.");
            commands.Add(AdminEntry("/formation advanced","About cohesion controls",AdvancedDescription,"explanation",AdvancedGroup));
            AddSwitch(commands,"!movementfix","Suppress native group slowdown",ServerSettings.MovementFix,"Use independent speeds for new grouped orders. Enabled faction cohesion applies its own pacing.",DefaultGroup);
            AddSwitch(commands,"!travelcoverdisabler","Travel cover preference disabler",TravelCoverPreferenceDisabler,"ON disables human infantry travel-waypoint cover preference. Combat cover behavior remains native.",DefaultGroup);
            AddSlider(commands,"formationspace","Formation spacing (click / drag)",ServerSettings.Spacing,DefaultSpacing,"Commander formations: spacing for new click/drag orders. 0% = baseline; 30% = 3x; 100% = 10x.");
            AddSlider(commands,"sizeinfluence","Large-unit influence",ServerSettings.SizeInfluence,DefaultSizeInfluence,"Commander and FPS formations: 0% uses each unit's footprint; 100% fully applies the largest footprint.");
            AddSlider(commands,"fpsspace","FPS unit spacing",ServerSettings.FpsSpacing,DefaultFpsSpacing,"FPS formations: trailing column, stopped fan and destination grid spacing.",group:FpsGroup);
            AddSlider(commands,"fpsdistance","FPS distance to player",ServerSettings.FpsDistance,DefaultFpsDistance,"FPS formations: 10% = 1.667x (5m + player radius); 30% = 3x; 100% = 10x. Physical clearance is retained.",group:FpsGroup);
            AddSlider(commands,"fpsallowance","FPS movement allowance",ServerSettings.FpsAllowance,DefaultFpsAllowance,"FPS formations: player displacement before repositioning; convoy threshold is twice this distance, minimum 10m.",1,"m",FpsGroup);
            AddSlider(commands,"rallyspace","Rally spacing",ServerSettings.Rally,DefaultRally,"Commander formations: parking spacing for newly produced units; physical clearance is retained.",group:RallyGroup);
            AddCohesionMenu(commands);
            AddCohesionDiagnostics(commands);
            return new JObject{["schema"]=2,["commands"]=commands}.ToString(Formatting.None);
        }

        // No player values or personalControls declaration: configuration belongs to the server.
        public static string ModUIPlayerData(ulong steamId)=>null;

        private static JObject AdminEntry(string command,string label,string description,string kind,string group)
            =>new JObject{["t"]=command,["l"]=label,["d"]=description,["a"]=true,["p"]="Generic",["role"]="admin",
                ["ui"]=new JObject{["kind"]=kind,["group"]=group}};
        private static void AddSwitch(JArray commands,string command,string label,bool value,string description,string group=SwitchGroup)
        {
            var row=AdminEntry(command,label,description,"checkbox",group);
            row["v"]=value?"on":"off";row["ch"]=new JArray(new JArray("on","ON"),new JArray("off","OFF"));
            commands.Add(row);
        }
        private static void AddSlider(JArray commands,string command,string label,int value,int defaultValue,string description,int minimum=0,string unit="%",string group=DefaultGroup)
        {
            var row=AdminEntry("!"+command,label,description+" Shared server setting.","slider",group);
            row["v"]=value.ToString(CultureInfo.InvariantCulture);
            var ui=(JObject)row["ui"];ui["min"]=minimum;ui["max"]=100;ui["step"]=1;ui["unit"]=unit;ui["default"]=defaultValue.ToString(CultureInfo.InvariantCulture);
            commands.Add(row);
        }
    }
}
