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
        private const string AdvancedDescription="Shift-click/Shift-drag enables Move cohesion and checkpoint waits. Faster members follow the slowest nominal tier's progress. Human and Alien overdue waits have separate sliders, with an additional Alien combat allowance. Drags widen from a narrow column through square and wide layouts to a line. Single-click and drag spacing have separate sliders. Use /cohesion, /aliencohesion, /waypointwait and /cohesiondebug next.";

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
            ClearWaypointState();
            ClearFpsStates();ClearRallyState();Scouts.Clear();activeIndependent=false;activeLayout=null;
            foreach(var ticket in pending)
            {
                var unit=ticket.Unit;
                if(!unit||unit.IsDestroyed||RallyExcluded(unit)||unit.Team!=ticket.Team||unit.PlayerControlled||unit.ControlledBy||unit.InCompartment||
                    (unit.Driver&&unit.Driver.ControlledBy))continue;
                try{unit.RallyTo(ticket.NativeAnchor);}
                catch(Exception e){MelonLoader.MelonLogger.Warning("Restoring native rally: "+e.Message);}
            }
        }

        // Both menu forms and chat call this one report. Existing per-setting status
        // handlers supply their own values/formatting; no layout or preference is created.
        private static void FormationStatus(Player p)
        {
            Reply(p,"Si_Formation 3.18.0; server-wide configuration; enabled "+(ServerEnabled?"ON":"OFF")+"; effective "+(Active?"ON":"OFF")+".");
            SpacingCommand(p,"/formationspace status");
            RallyCommand(p,"/rallyspace status");
            foreach(string command in new[]{"dragspace","sizeinfluence","alienspread","fpsspace","fpsdistance","fpsallowance"})
                TuningCommand(p,"/"+command+" status",command);
            LesserSpawnAirSpacingCommand(p,"/lesserspawnairspace status");
            MovementFixCommand(p,"/movementfix status");
            PathfindingShortcutCommand(p,"/pathshortcut status");
            TravelCoverCommand(p,"/travelcoverdisabler status");
            DestinationCohesionCommand(p,"/destinationcohesion status");
            WaypointWaitCommand(p,"/waypointwait status");
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
            AddSlider(commands,"pathshortcut","Pathfinding shortcut pass",PathfindingShortcutDistance,DefaultPathfindingShortcutDistance,
                "Maximum horizontal span for native vehicle path shortcuts. Default 1000m; native limit 200m. 0 disables corner shortcuts. Applies to newly processed paths.",
                unit:"m",group:SwitchGroup,maximum:MaximumPathfindingShortcutDistance,step:25);
            commands.Add(AdminEntry("/formation advanced","About cohesion controls",AdvancedDescription,"explanation",AdvancedGroup));
            AddSwitch(commands,"!movementfix","Suppress native group slowdown",ServerSettings.MovementFix,"Use independent speeds for new grouped orders. Enabled faction cohesion applies its own pacing.",SwitchGroup);
            AddSwitch(commands,"!travelcoverdisabler","Travel cover preference disabler",TravelCoverPreferenceDisabler,"ON disables human infantry travel-waypoint cover preference. Combat cover behavior remains native.",SwitchGroup);
            AddSwitch(commands,"!destinationcohesion","Nearby destination cohesion",DestinationLocalCohesionEnabled,
                "Enable the nearby-destination fallback for scattered or oversized selections, for both factions. Uses each faction's Destination arrival radius (600m by default). ON by default; compact-group cohesion remains independent.",SwitchGroup);
            AddSlider(commands,"formationspace","Formation spacing (single click)",ServerSettings.Spacing,DefaultSpacing,"Commander formations: spacing for new single-click orders. 0% = baseline; 30% = 3x; 100% = 10x.");
            AddSlider(commands,"dragspace","Formation spacing (click and drag)",ServerSettings.DragSpacing,DefaultDragSpacing,"Packing gaps for new dragged formations. 0% = compact baseline; 30% = 3x; 100% = 10x. Drag length sets frontage. Physical clearance is retained.");
            AddSlider(commands,"sizeinfluence","Large-unit influence",ServerSettings.SizeInfluence,DefaultSizeInfluence,"Commander and FPS formations: 0% uses each unit's footprint; 100% fully applies the largest footprint.");
            AddSlider(commands,"fpsspace","FPS unit spacing",ServerSettings.FpsSpacing,DefaultFpsSpacing,"FPS formations: trailing column, stopped fan and destination grid spacing.",group:FpsGroup);
            AddSlider(commands,"fpsdistance","FPS distance to player",ServerSettings.FpsDistance,DefaultFpsDistance,"FPS formations: 10% = 1.667x (5m + player radius); 30% = 3x; 100% = 10x. Physical clearance is retained.",group:FpsGroup);
            AddSlider(commands,"fpsallowance","FPS movement allowance",ServerSettings.FpsAllowance,DefaultFpsAllowance,"FPS formations: player displacement before repositioning; convoy threshold is twice this distance, minimum 10m.",1,"m",FpsGroup);
            AddSlider(commands,"rallyspace","Rally spacing",ServerSettings.Rally,DefaultRally,"Commander formations: parking spacing for newly produced units; physical clearance is retained.",group:RallyGroup);
            AddCohesionMenu(commands);
            AddWaypointWaitSlider(commands,"human","Human overdue checkpoint wait",HumanWaypointWait,DefaultHumanWaypointWait,
                "Seconds the Human group waits after its shared ETA expires.");
            AddWaypointWaitSlider(commands,"alien","Alien overdue checkpoint wait",AlienWaypointWait,DefaultAlienWaypointWait,
                "Seconds the Alien group waits after its shared ETA expires.");
            AddWaypointWaitSlider(commands,"combat","Alien combat checkpoint wait",AlienCombatWait,DefaultAlienCombatWait,
                "Additional seconds for combat involving an included Alien member. Applied once per waypoint, including regrouping after combat.");
            AddCohesionDiagnostics(commands);
            // The original Si_ModUI_Server 1.4.4 drops command ui fields. Its data
            // passthrough retains these generic presentation hints for newer clients.
            // Commands/values/permissions still come from its authenticated command list.
            var presentation=new JArray(commands.OfType<JObject>().Select(row=>new JObject
                {["t"]=row["t"].DeepClone(),["ui"]=row["ui"].DeepClone()}));
            // Registered base-command forms retain their argument metadata.
            foreach(string command in new[]{"!aliencohesion","!cohesion","!waypointwait"})
                presentation.Add(new JObject{["t"]=command,["ui"]=new JObject{["group"]=DiagnosticsGroup}});
            return new JObject{["schema"]=2,["commands"]=commands,["controlPresentation"]=presentation}.ToString(Formatting.None);
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
        private static void AddWaypointWaitSlider(JArray commands,string endpoint,string label,float value,float defaultValue,string description)
        {
            var row=AdminEntry("!waypointwait "+endpoint,label,description,"slider",DefaultGroup);
            row["v"]=CohesionNumber(value);
            var ui=(JObject)row["ui"];ui["min"]=0;ui["max"]=120;ui["step"]=1;ui["unit"]="s";ui["default"]=CohesionNumber(defaultValue);
            commands.Add(row);
        }
        private static void AddSlider(JArray commands,string command,string label,int value,int defaultValue,string description,int minimum=0,string unit="%",string group=DefaultGroup,int maximum=100,int step=1)
        {
            var row=AdminEntry("!"+command,label,description+" Shared server setting.","slider",group);
            row["v"]=value.ToString(CultureInfo.InvariantCulture);
            var ui=(JObject)row["ui"];ui["min"]=minimum;ui["max"]=maximum;ui["step"]=step;ui["unit"]=unit;ui["default"]=defaultValue.ToString(CultureInfo.InvariantCulture);
            commands.Add(row);
        }
    }
}
