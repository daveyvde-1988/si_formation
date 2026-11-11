using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MelonLoader;
using Newtonsoft.Json.Linq;
using SilicaAdminMod;
using UnityEngine;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class CohesionTuning
        {
            internal string Range;
            internal float Strength,MinimumSpeed,ArrivalWindow,MaxDelay,MaxSpread,LagDistance,StuckSeconds,BreakPercent;
            internal float HoldError,HoldBreakPercent,ObstructedPercent,HoldCorrectionInterval;
        }
        private sealed class CohesionSetting
        {
            internal string Key,Label,Description,Group,Unit;
            internal float Default,Value,Minimum,Maximum,Step;
            internal bool Integer,Alien;
            internal MelonPreferences_Entry<float> Preference;
        }
        private sealed class CohesionPair
        {
            internal CohesionSetting Near,Far;
            internal bool Descending;
        }
        private const string CohesionGeneralGroup="General";
        private static readonly Dictionary<string,CohesionSetting> cohesionSettings=new Dictionary<string,CohesionSetting>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string,CohesionSetting> alienCohesionSettings=new Dictionary<string,CohesionSetting>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<CohesionSetting> cohesionSettingOrder=new List<CohesionSetting>();
        private static readonly List<CohesionPair> cohesionPairs=new List<CohesionPair>();
        private static MelonPreferences_Entry<bool> groupCohesionPreference,alienGroupCohesionPreference;
        private static bool CohesionEnabled,AlienCohesionEnabled;
        private static bool CohesionEnabledFor(Team team)=>IsAlienTeam(team)?AlienCohesionEnabled:CohesionEnabled;
        private static float CohesionTickInterval=>CohesionEnabled&&AlienCohesionEnabled?
            Mathf.Min(CohesionFactionValue(false,"UpdateInterval"),CohesionFactionValue(true,"UpdateInterval")):
            CohesionFactionValue(AlienCohesionEnabled,"UpdateInterval");
        private static float CohesionUpdateInterval=>CohesionValue("UpdateInterval");
        private static float CohesionInitialSpread=>CohesionValue("InitialSpread");
        private static float CohesionStartGrace=>CohesionValue("StartGrace");
        private static float CohesionDebugInterval=>CohesionValue("DebugInterval");
        private static int CohesionMaxUnits=>(int)CohesionValue("MaxUnits");
        private static float CohesionMaximumDuration=>CohesionValue("MaximumDuration");
        private static float CohesionRetention=>CohesionValue("Retention");
        private static float CohesionPathDetourRatio=>CohesionValue("PathDetourRatio");
        private static float CohesionHoldGrace=>CohesionValue("HoldGrace");
        private static float CohesionProgressDistance=>CohesionValue("ProgressDistance");
        private static int CohesionTraceHistory=>(int)CohesionValue("TraceHistory");
        private static float CohesionCloseRange=>CohesionValue("CloseRange");
        private static float CohesionMediumRange=>CohesionValue("MediumRange");
        private static float CohesionLongRange=>CohesionValue("LongRange");

        private static bool CohesionFinite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static string CohesionNumber(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        private static string CohesionFactionName(bool alien)=>alien?"Alien":"Human";
        private static string CohesionPreferenceKey(CohesionSetting setting)=>(setting.Alien?"AlienCohesion":"Cohesion")+setting.Key;
        private static string CohesionFactionGroup(bool alien,string group)=>CohesionFactionName(alien)+" cohesion \u2013 "+group;
        private static Dictionary<string,CohesionSetting> CohesionFactionSettings(bool alien)
        {EnsureCohesionDefinitions();return alien?alienCohesionSettings:cohesionSettings;}
        private static float CohesionFactionValue(bool alien,string key)=>CohesionFactionSettings(alien)[key].Value;
        private static float CohesionValueFor(Team team,string key)=>CohesionFactionValue(IsAlienTeam(team),key);
        private static float CohesionValue(string key)
        =>CohesionFactionValue(false,key);

        private static CohesionSetting DefineCohesion(string key,string label,string description,string group,float value,float minimum,float maximum,float step,string unit="",bool integer=false)
        {
            var setting=new CohesionSetting{Key=key,Label=label,Description=description,Group=group,Default=value,Value=value,
                Minimum=minimum,Maximum=maximum,Step=step,Unit=unit,Integer=integer};
            cohesionSettings.Add(key,setting);cohesionSettingOrder.Add(setting);return setting;
        }
        private static void DefineCohesionPair(string profile,string key,string label,string description,float near,float far,float minimum,float maximum,float step,string unit,bool descending)
        {
            string group=profile+" range";
            var a=DefineCohesion(profile+key+"Near",label+" (near)",description+" Interpolated from the minimum-range endpoint.",group,near,minimum,maximum,step,unit);
            var b=DefineCohesion(profile+key+"Far",label+" (far)",description+" Interpolated to the maximum-range endpoint.",group,far,minimum,maximum,step,unit);
            cohesionPairs.Add(new CohesionPair{Near=a,Far=b,Descending=descending});
        }
        private static void DefineCohesionProfile(string profile,float strengthNear,float strengthFar,float speedNear,float speedFar,
            float windowNear,float windowFar,float delayNear,float delayFar,float spreadNear,float spreadFar,float lagNear,float lagFar,
            float stuckNear,float stuckFar,float breakNear,float breakFar)
        {
            DefineCohesionPair(profile,"Strength","Pacing strength","Blend between independent speed and shared arrival pacing; 0 is independent, 1 is full configured pacing.",strengthNear,strengthFar,0,1,.05f,"",true);
            DefineCohesionPair(profile,"MinimumSpeed","Minimum speed fraction","Minimum pacing cap as a fraction of the requested mode's uncapped speed; native movement can still be slower.",speedNear,speedFar,.05f,1,.05f,"",false);
            DefineCohesionPair(profile,"ArrivalWindow","Arrival allowance","Allowed early-arrival window relative to the group's predicted arrival time.",windowNear,windowFar,0,60,.25f,"s",false);
            DefineCohesionPair(profile,"MaxDelay","Pacing delay budget","Per-unit cumulative slowdown budget in seconds; pacing also ends after the initial free-speed arrival estimate plus this allowance. Native travel can still take longer.",delayNear,delayFar,0,120,.5f,"s",true);
            DefineCohesionPair(profile,"MaxSpread","Extra radius allowance","Allowed increase in group radius over its captured starting footprint. Sustained excess releases medium/long cohesion and degrades close holding to arrival pacing; twice this allowance releases close cohesion.",spreadNear,spreadFar,0,2000,5,"m",profile=="Close");
            DefineCohesionPair(profile,"LagDistance","Progress lag allowance","Allowed route-progress lag behind the group's median before a member counts as unhealthy. Close-range breakup tightens with distance; medium and long allow more route divergence.",lagNear,lagFar,0,1000,5,"m",profile=="Close");
            DefineCohesionPair(profile,"StuckSeconds","No-progress timeout","How long an unheld member may fail to make progress before it counts as unhealthy, after the start grace.",stuckNear,stuckFar,.5f,30,.5f,"s",true);
            DefineCohesionPair(profile,"BreakPercent","Failure and casualty limit","Release cohesion when lost members reach this percentage of the captured group, or unhealthy members reach this percentage of the moving group. Larger values tolerate more failures.",breakNear,breakFar,1,100,1,"%",true);
        }
        private static void EnsureCohesionDefinitions()
        {
            if(cohesionSettings.Count!=0)return;
            DefineCohesion("CloseRange","Close/medium boundary","Close applies below this distance; exactly this boundary starts medium range.",CohesionGeneralGroup,500,1,10000,1,"m");
            DefineCohesion("MediumRange","Medium/long boundary","Medium includes this distance; longer moves use the long profile.",CohesionGeneralGroup,1000,2,50000,1,"m");
            DefineCohesion("LongRange","Long interpolation endpoint","Long-profile interpolation reaches its maximum-range values here and remains there for longer journeys.",CohesionGeneralGroup,3000,3,100000,1,"m");
            DefineCohesion("UpdateInterval","Update interval","Minimum interval between shared pacing evaluations.",CohesionGeneralGroup,.35f,.1f,5,.05f,"s");
            DefineCohesion("InitialSpread","Starting spread allowance","Extra selection-radius allowance beyond the approximate packed-unit footprint for whole-selection cohesion. More dispersed selections can use the destination arrival radius fallback.",CohesionGeneralGroup,250,0,2000,10,"m");
            DefineCohesion("DestinationRadius","Destination arrival radius","For a dispersed selection, coordinate only eligible ground members already within this horizontal distance of the clicked destination or drag centre when the order is issued. Farther members move independently. Zero disables this fallback.",CohesionGeneralGroup,500,0,10000,25,"m");
            DefineCohesion("StartGrace","Movement start grace","Initial grace before no-progress checks can mark starting members unhealthy.",CohesionGeneralGroup,3,0,30,.5f,"s");
            DefineCohesion("DebugInterval","Debug sampling interval","Minimum interval between periodic samples for enabled or watched cohesion traces.",CohesionGeneralGroup,3,.25f,60,.25f,"s");
            DefineCohesion("MaxUnits","Maximum cohesion members","Maximum units admitted to one custom cohesion command; larger commands retain independent movement.",CohesionGeneralGroup,256,2,1024,1,"units",true);
            DefineCohesion("MaximumDuration","Maximum cohesion duration","Maximum lifetime of a movement-cohesion command before its speed ownership is released.",CohesionGeneralGroup,600,5,3600,5,"s");
            DefineCohesion("Retention","Finished layout retention","How long layout identity and group diagnostics are retained after completion; active command ownership is managed separately.",CohesionGeneralGroup,120,0,1800,5,"s");
            DefineCohesion("PathDetourRatio","Formation route detour limit","Maximum route-length/direct-distance ratio allowed for formation holding before routes are treated as obstructed.",CohesionGeneralGroup,1.8f,1,10,.1f,"x");
            DefineCohesion("HoldGrace","Sustained failure delay","How long excess spread or failed formation-hold checks must persist before degrading or releasing cohesion.",CohesionGeneralGroup,2,.25f,30,.25f,"s");
            DefineCohesion("TurnGrace","Formation turn allowance","For a rotating short click, use arrival pacing during this allowance before holding a healthy shape. Otherwise continue arrival pacing. Zero starts holding immediately with normal failure checks.",CohesionGeneralGroup,6,0,30,.5f,"s");
            DefineCohesion("ProgressDistance","Progress sample distance","Minimum position change or decrease in remaining route distance required to refresh a member's no-progress timer.",CohesionGeneralGroup,1,.1f,20,.1f,"m");
            DefineCohesion("TraceHistory","Retained trace samples","Maximum diagnostic samples retained per cohesion group.",CohesionGeneralGroup,80,10,500,1,"samples",true);
            DefineCohesionProfile("Close",1,.85f,.25f,.4f,.75f,2,12,8,60,30,30,15,3,2.5f,70,55);
            DefineCohesionProfile("Medium",.65f,.35f,.5f,.7f,3,6,6,3,60,120,30,60,2.5f,2,40,30);
            DefineCohesionProfile("Long",.4f,.25f,.65f,.8f,8,15,4,2,180,300,90,150,2,1.5f,20,10);
            DefineCohesionPair("Close","HoldError","Formation offset allowance","Slot-offset tolerance for pacing and formation-health checks. Physical unit size can increase this tolerance; destinations and paths are not corrected.",3,6,.5f,100,.5f,"m",false);
            DefineCohesionPair("Close","HoldBreakPercent","Formation hold failure limit","Percentage of members that may fail formation holding before it degrades to arrival pacing. Must stay below the full cohesion failure limit.",40,30,1,100,1,"%",true);
            DefineCohesionPair("Close","ObstructedPercent","Obstructed-member limit","Percentage of obstructed or stuck moving members that can cancel close-range holding after the sustained failure delay; arrival pacing may continue.",50,35,1,100,1,"%",true);
            DefineCohesionPair("Close","HoldCorrectionInterval","Formation hold check interval","Minimum interval between hold-health checks that may cancel close-range holding. Speed caps use the cohesion update interval; destinations and paths are not corrected.",.75f,1.5f,.25f,10,.25f,"s",false);
            // Alien defaults come from immutable descriptor defaults, never loaded human values.
            foreach(var human in cohesionSettingOrder.ToArray())
            {
                var alien=new CohesionSetting{Key=human.Key,Label=human.Label,Description=human.Description,Group=human.Group,Unit=human.Unit,
                    Default=human.Default,Value=human.Default,Minimum=human.Minimum,Maximum=human.Maximum,Step=human.Step,Integer=human.Integer,Alien=true};
                alienCohesionSettings.Add(alien.Key,alien);cohesionSettingOrder.Add(alien);
            }
            foreach(var pair in cohesionPairs.ToArray())
                cohesionPairs.Add(new CohesionPair{Near=alienCohesionSettings[pair.Near.Key],Far=alienCohesionSettings[pair.Far.Key],Descending=pair.Descending});
        }

        private static bool CohesionPairValid(CohesionPair pair,float near,float far)=>pair.Descending?near>=far:near<=far;
        private static void LoadCohesionSettings()
        {
            EnsureCohesionDefinitions();
            if(serverPreferences==null)throw new InvalidOperationException("Load server settings before cohesion settings.");
            groupCohesionPreference=serverPreferences.CreateEntry("GroupCohesion",false,description:"Enable bounded human commander move cohesion; disabled by default.");
            alienGroupCohesionPreference=serverPreferences.CreateEntry("AlienGroupCohesion",false,description:"Enable independently configured alien commander move cohesion; disabled by default.");
            CohesionEnabled=groupCohesionPreference.Value;
            AlienCohesionEnabled=alienGroupCohesionPreference.Value;
            foreach(var setting in cohesionSettingOrder)
            {
                string key=CohesionPreferenceKey(setting);
                setting.Preference=serverPreferences.CreateEntry(key,setting.Default,description:CohesionFactionName(setting.Alien)+" cohesion. "+setting.Description);
                float raw=setting.Preference.Value;
                float value=CohesionFinite(raw)?Mathf.Clamp(raw,setting.Minimum,setting.Maximum):setting.Default;
                if(setting.Integer)value=(float)Math.Round(value);
                if(!CohesionFinite(raw)||raw!=value)MelonLogger.Warning(key+" has an invalid value; using "+CohesionNumber(value)+".");
                setting.Value=value;setting.Preference.Value=value;
            }
            foreach(bool alien in new[]{false,true})
            {
                var settings=CohesionFactionSettings(alien);
                if(!(settings["CloseRange"].Value<settings["MediumRange"].Value&&settings["MediumRange"].Value<settings["LongRange"].Value))
                {
                    foreach(string key in new[]{"CloseRange","MediumRange","LongRange"})
                    {var setting=settings[key];setting.Value=setting.Default;setting.Preference.Value=setting.Value;}
                    MelonLogger.Warning(CohesionFactionName(alien)+" cohesion boundaries must satisfy CloseRange < MediumRange < LongRange; restored 500/1000/3000m defaults.");
                }
            }
            foreach(var pair in cohesionPairs)
                if(!CohesionPairValid(pair,pair.Near.Value,pair.Far.Value))
                {
                    pair.Near.Value=pair.Near.Default;pair.Far.Value=pair.Far.Default;
                    pair.Near.Preference.Value=pair.Near.Value;pair.Far.Preference.Value=pair.Far.Value;
                    MelonLogger.Warning(CohesionFactionName(pair.Near.Alien)+" cohesion endpoints "+pair.Near.Key+"/"+pair.Far.Key+" are reversed; restored their defaults.");
                }
            foreach(bool alien in new[]{false,true})
            {
                var settings=CohesionFactionSettings(alien);
                if(new[]{"Near","Far"}.Any(endpoint=>settings["CloseHoldBreakPercent"+endpoint].Value>=settings["CloseBreakPercent"+endpoint].Value))
                {
                    foreach(string key in new[]{"CloseBreakPercentNear","CloseBreakPercentFar","CloseHoldBreakPercentNear","CloseHoldBreakPercentFar"})
                    {var setting=settings[key];setting.Value=setting.Default;setting.Preference.Value=setting.Value;}
                    MelonLogger.Warning(CohesionFactionName(alien)+" close formation-hold failure limits must stay below full cohesion failure limits; restored both pairs to defaults.");
                }
            }
        }
        private static void SaveCohesionSettings()
        {
            if(groupCohesionPreference!=null)groupCohesionPreference.Value=CohesionEnabled;
            if(alienGroupCohesionPreference!=null)alienGroupCohesionPreference.Value=AlienCohesionEnabled;
            foreach(var setting in cohesionSettingOrder)if(setting.Preference!=null)setting.Preference.Value=setting.Value;
        }
        private static float CohesionEndpoint(bool alien,string profile,string key,float fraction)
            =>Mathf.Lerp(CohesionFactionValue(alien,profile+key+"Near"),CohesionFactionValue(alien,profile+key+"Far"),fraction);
        private static CohesionTuning CaptureCohesionTuning(float distance,Team team)
        {
            EnsureCohesionDefinitions();
            bool alien=IsAlienTeam(team);
            float close=CohesionFactionValue(alien,"CloseRange"),medium=CohesionFactionValue(alien,"MediumRange"),far=CohesionFactionValue(alien,"LongRange");
            if(!CohesionFinite(distance))distance=far;
            distance=Mathf.Max(0,distance);
            string profile;float fraction;
            if(distance<close){profile="Close";fraction=distance/close;}
            else if(distance<=medium){profile="Medium";fraction=(distance-close)/(medium-close);}
            else{profile="Long";fraction=(distance-medium)/(far-medium);}
            fraction=Mathf.Clamp01(fraction);
            var tuning=new CohesionTuning{Range=profile,
                Strength=CohesionEndpoint(alien,profile,"Strength",fraction),MinimumSpeed=CohesionEndpoint(alien,profile,"MinimumSpeed",fraction),
                ArrivalWindow=CohesionEndpoint(alien,profile,"ArrivalWindow",fraction),MaxDelay=CohesionEndpoint(alien,profile,"MaxDelay",fraction),
                MaxSpread=CohesionEndpoint(alien,profile,"MaxSpread",fraction),LagDistance=CohesionEndpoint(alien,profile,"LagDistance",fraction),
                StuckSeconds=CohesionEndpoint(alien,profile,"StuckSeconds",fraction),BreakPercent=CohesionEndpoint(alien,profile,"BreakPercent",fraction),
                HoldError=6,HoldBreakPercent=30,ObstructedPercent=35,HoldCorrectionInterval=1.5f};
            if(profile=="Close")
            {
                tuning.HoldError=CohesionEndpoint(alien,profile,"HoldError",fraction);
                tuning.HoldBreakPercent=CohesionEndpoint(alien,profile,"HoldBreakPercent",fraction);
                tuning.ObstructedPercent=CohesionEndpoint(alien,profile,"ObstructedPercent",fraction);
                tuning.HoldCorrectionInterval=CohesionEndpoint(alien,profile,"HoldCorrectionInterval",fraction);
            }
            return tuning;
        }

        private static void RegisterCohesionCommands()
        {
            foreach(string name in new[]{"groupcohesion","cohesion","aliengroupcohesion","aliencohesion"})
                if(PlayerMethods.FindPlayerCommandFromString(name)!=null||AdminMethods.FindAdminCommandFromString(name)!=null)
                    throw new InvalidOperationException("Command already registered: "+name+".");
            AdminMethods.RegisterAdminCommand("groupcohesion",GroupCohesionCommand,Power.Generic,"Human commander group cohesion, on|off|status.");
            AdminMethods.RegisterAdminCommand("cohesion",CohesionCommand,Power.Generic,"Human cohesion tuning: <key> <number>|status.");
            AdminMethods.RegisterAdminCommand("aliengroupcohesion",AlienGroupCohesionCommand,Power.Generic,"Alien commander group cohesion, on|off|status.");
            AdminMethods.RegisterAdminCommand("aliencohesion",AlienCohesionCommand,Power.Generic,"Alien cohesion tuning: <key> <number>|status.");
        }
        private static void GroupCohesionCommand(Player p,string args)
            =>SetFactionCohesion(p,args,false);
        private static void AlienGroupCohesionCommand(Player p,string args)
            =>SetFactionCohesion(p,args,true);
        private static void SetFactionCohesion(Player p,string args,bool alien)
        {
            if(!DemandAdmin(p))return;
            string value=Argument(args);bool saved=true;
            if(value=="on"||value=="off")
            {
                if(alien)AlienCohesionEnabled=value=="on";else CohesionEnabled=value=="on";
                if(value=="off")ClearFactionCohesion(alien,CohesionFactionName(alien).ToLowerInvariant()+" switch off");
                saved=SaveSettings(p);
            }
            else if(value!=""&&value!="status"){Reply(p,"/"+(alien?"aliengroupcohesion":"groupcohesion")+" on|off|status");return;}
            Reply(p,CohesionSummary(alien)+(saved?"":" (save failed)"));
        }
        private static bool ValidateCohesionChange(CohesionSetting setting,float value,out string error)
        {
            error=null;
            if(!CohesionFinite(value)||value<setting.Minimum||value>setting.Maximum||setting.Integer&&value!=(float)Math.Round(value))
            {error=setting.Key+" requires "+(setting.Integer?"a whole number":"a finite number")+" from "+CohesionNumber(setting.Minimum)+" to "+CohesionNumber(setting.Maximum)+".";return false;}
            var settings=CohesionFactionSettings(setting.Alien);
            float close=setting.Key=="CloseRange"?value:settings["CloseRange"].Value;
            float medium=setting.Key=="MediumRange"?value:settings["MediumRange"].Value;
            float far=setting.Key=="LongRange"?value:settings["LongRange"].Value;
            if(!(close<medium&&medium<far))
            {error="Range boundaries must satisfy CloseRange < MediumRange < LongRange.";return false;}
            foreach(var pair in cohesionPairs)
                if(pair.Near==setting||pair.Far==setting)
                {
                    float nearValue=pair.Near==setting?value:pair.Near.Value,farValue=pair.Far==setting?value:pair.Far.Value;
                    if(!CohesionPairValid(pair,nearValue,farValue))
                    {error=pair.Near.Key+" must be "+(pair.Descending?">=":"<=")+" "+pair.Far.Key+".";return false;}
                }
            foreach(string endpoint in new[]{"Near","Far"})
            {
                string holdKey="CloseHoldBreakPercent"+endpoint,fullKey="CloseBreakPercent"+endpoint;
                float hold=setting.Key==holdKey?value:settings[holdKey].Value,full=setting.Key==fullKey?value:settings[fullKey].Value;
                if(hold>=full){error=holdKey+" must be < "+fullKey+" so formation holding releases before full cohesion.";return false;}
            }
            return true;
        }
        private static void CohesionCommand(Player p,string args)
            =>TuneFactionCohesion(p,args,false);
        private static void AlienCohesionCommand(Player p,string args)
            =>TuneFactionCohesion(p,args,true);
        private static void TuneFactionCohesion(Player p,string args,bool alien)
        {
            if(!DemandAdmin(p))return;
            EnsureCohesionDefinitions();
            var settings=CohesionFactionSettings(alien);
            string command=alien?"aliencohesion":"cohesion",faction=CohesionFactionName(alien);
            var tokens=Argument(args).Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            if(tokens.Length==0||tokens.Length==1&&tokens[0]=="status")
            {
                Reply(p,CohesionSummary(alien)+" Ranges <"+CohesionNumber(settings["CloseRange"].Value)+"m close, through "+CohesionNumber(settings["MediumRange"].Value)+"m medium, long endpoint "+CohesionNumber(settings["LongRange"].Value)+"m.");
                Reply(p,"Use /"+command+" <key> <number>, or /"+command+" <key> status. Keys are shown in the "+faction.ToLowerInvariant()+" cohesion controls and cfg.");
                return;
            }
            string key=tokens[0];
            if(alien&&key.StartsWith("aliencohesion",StringComparison.OrdinalIgnoreCase))key=key.Substring(13);
            else if(key.StartsWith("cohesion",StringComparison.OrdinalIgnoreCase))key=key.Substring(8);
            if(!settings.TryGetValue(key,out var setting))
            {Reply(p,"Unknown "+faction.ToLowerInvariant()+" cohesion key. Example: /"+command+" CloseStrengthNear 1.");return;}
            if(tokens.Length==1||tokens.Length==2&&tokens[1]=="status")
            {Reply(p,faction+" "+setting.Key+" = "+CohesionNumber(setting.Value)+(setting.Unit.Length==0?"":" "+setting.Unit)+". "+setting.Description);return;}
            if(tokens.Length!=2||!float.TryParse(tokens[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float value))
            {Reply(p,"/"+command+" "+setting.Key+" <number>|status; use a decimal point.");return;}
            if(!ValidateCohesionChange(setting,value,out string error)){Reply(p,error);return;}
            setting.Value=value;
            bool saved=SaveSettings(p);
            Reply(p,faction+" "+setting.Key+" = "+CohesionNumber(value)+(setting.Unit.Length==0?"":" "+setting.Unit)+"; shared server setting"+(saved?" (saved).":" (save failed)."));
        }

        private static void AddCohesionMenu(JArray commands)
        {
            EnsureCohesionDefinitions();
            foreach(bool alien in new[]{false,true})
            {
                string faction=CohesionFactionName(alien),command=alien?"aliencohesion":"cohesion";
                AddSwitch(commands,alien?"!aliengroupcohesion":"!groupcohesion",faction+" group cohesion",alien?AlienCohesionEnabled:CohesionEnabled,
                    "Enable "+faction.ToLowerInvariant()+" custom pacing and eligible close-range formation holding. This faction's settings and switch are independent.",CohesionFactionGroup(alien,CohesionGeneralGroup));
                if(alien)AddSlider(commands,"alienspread","Alien: Placement spacing scale",ServerSettings.AlienSpread,DefaultAlienSpread,
                    "Scale Alien commander, FPS and rally spacing whether Alien cohesion is enabled or disabled. Physical clearance is retained.",group:CohesionFactionGroup(true,CohesionGeneralGroup));
                foreach(var group in cohesionSettingOrder.Where(setting=>setting.Alien==alien).GroupBy(setting=>setting.Group))
                foreach(var setting in group)
                {
                    string scope=setting.Group==CohesionGeneralGroup?" Faction-specific server setting; changes can affect active groups.":" Faction-specific server profile; captured for new cohesion commands.";
                    string label=faction+(setting.Group==CohesionGeneralGroup?"":(" "+setting.Group.Replace(" range","")))+": "+setting.Label;
                    var row=AdminEntry("!"+command+" "+setting.Key,label,setting.Description+scope,"slider",CohesionFactionGroup(alien,setting.Group));
                    row["v"]=CohesionNumber(setting.Value);
                    var ui=(JObject)row["ui"];ui["min"]=setting.Minimum;ui["max"]=setting.Maximum;ui["step"]=setting.Step;
                    ui["unit"]=setting.Unit;ui["default"]=CohesionNumber(setting.Default);
                    commands.Add(row);
                }
            }
        }
        private static void AddCohesionDiagnostics(JArray commands)
        {
            AddSwitch(commands,"!cohesiondebug","Cohesion debug",CohesionDebugEnabled,"Trace human and alien groups. Off also clears individual and next-group watches.",DiagnosticsGroup);
            commands.Add(AdminEntry("!cohesiondebug status","Cohesion debug status","Show global trace state and watched groups.","button",DiagnosticsGroup));
            commands.Add(AdminEntry("!cohesiondebug groups","List cohesion groups","Show up to eight recent retained human or alien cohesion groups, including completed and released groups.","button",DiagnosticsGroup));
            commands.Add(AdminEntry("!cohesiondebug next","Watch next cohesion group","Trace your next grouped commander move, including any skip reason; commander role required.","button",DiagnosticsGroup));
        }
    }
}
