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
            internal float Fraction,Strength,HoldStrength,MinimumSpeed,ArrivalWindow,MaxDelay,MaxSpread,LagDistance,StuckSeconds,BreakPercent;
            internal float HoldError,HoldBreakPercent,ObstructedPercent,HoldCorrectionInterval;
        }
        private sealed class CohesionSetting
        {
            internal string Key,Label,Description,Group,Unit;
            internal float Default,Value,Minimum,Maximum,Step;
            internal bool Integer,Alien,Visible;
            internal MelonPreferences_Entry<float> Preference;
        }
        private sealed class CohesionPair
        {
            internal CohesionSetting Near,Far;
            internal bool Descending;
        }
        private const string CohesionGeneralGroup="General", CohesionProfileGroup="Distance profile";
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

        private static bool CohesionFinite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static string CohesionNumber(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        private static string CohesionFactionName(bool alien)=>alien?"Alien":"Human";
        private static string CohesionPreferenceKey(CohesionSetting setting)=>(setting.Alien?"AlienCohesion":"Cohesion")+setting.Key;
        private static string CohesionFactionGroup(bool alien)=>CohesionFactionName(alien)+" cohesion";
        private static Dictionary<string,CohesionSetting> CohesionFactionSettings(bool alien)
        {EnsureCohesionDefinitions();return alien?alienCohesionSettings:cohesionSettings;}
        private static float CohesionFactionValue(bool alien,string key)=>CohesionFactionSettings(alien)[key].Value;
        private static float CohesionValueFor(Team team,string key)=>CohesionFactionValue(IsAlienTeam(team),key);

        private static CohesionSetting DefineCohesion(string key,string label,string description,string group,float value,float minimum,float maximum,float step,string unit="",bool integer=false,bool visible=true)
        {
            var setting=new CohesionSetting{Key=key,Label=label,Description=description,Group=group,Default=value,Value=value,
                Minimum=minimum,Maximum=maximum,Step=step,Unit=unit,Integer=integer,Visible=visible};
            cohesionSettings.Add(key,setting);cohesionSettingOrder.Add(setting);return setting;
        }
        private static void DefineCohesionPair(string key,string label,string description,float near,float far,float minimum,float maximum,float step,string unit,bool descending)
        {
            var a=DefineCohesion(key+"Near",label+" (near)",description+" Value at zero travel distance; interpolates toward the far value at MaximumRange.",CohesionProfileGroup,near,minimum,maximum,step,unit);
            var b=DefineCohesion(key+"Far",label+" (far)",description+" Value at MaximumRange; interpolates from the near value at zero travel distance.",CohesionProfileGroup,far,minimum,maximum,step,unit);
            cohesionPairs.Add(new CohesionPair{Near=a,Far=b,Descending=descending});
        }
        private static void EnsureCohesionDefinitions()
        {
            if(cohesionSettings.Count!=0)return;
            DefineCohesion("MaximumRange","Maximum cohesion range","Interpolate one distance profile from zero to this travel distance. Longer commands use no custom pacing or holding.",CohesionGeneralGroup,5000,1,100000,1,"m");
            DefineCohesion("DestinationRadius","Destination arrival radius","When Nearby destination cohesion is ON, coordinate only eligible ground members of a dispersed selection already within this horizontal distance of the clicked destination or drag centre when the order is issued. Farther members move independently. Zero disables this fallback.",CohesionGeneralGroup,600,0,10000,25,"m");
            DefineCohesionPair("Strength","Cohesion pacing strength","Blend faster members toward the slowest tier's actual progress; zero leaves speed independent. The slowest tier has no custom cap.",1,.4f,0,1,.05f,"",true);
            DefineCohesionPair("HoldStrength","Formation hold strength","Strength of speed-based holding for an eligible remembered formation; zero disables holding. It does not steer paths or force a new selection into a shape.",1,0,0,1,.05f,"",true);
            DefineCohesionPair("MinimumSpeed","Minimum speed fraction","Minimum pacing cap as a fraction of the requested mode's uncapped speed; native movement can still be slower.",.05f,1,.05f,1,.05f,"",false);
            DefineCohesionPair("ArrivalWindow","Arrival allowance","Allowed lead over the slowest tier, in seconds of its nominal speed. Reverse uses an early-arrival time window.",.25f,5,0,60,.25f,"s",false);
            DefineCohesionPair("MaxDelay","Pacing delay budget","Per-unit cumulative slowdown budget in seconds; pacing also ends after its initial free-speed arrival estimate plus this allowance. Native travel can still take longer.",15,10,0,120,.5f,"s",true);

            // Existing common safety/config keys remain available for compatibility,
            // but the normal menu exposes only the twelve distance-profile controls.
            DefineCohesion("UpdateInterval","Update interval","Minimum interval between pacing evaluations.",CohesionGeneralGroup,.35f,.1f,5,.05f,"s",visible:false);
            DefineCohesion("InitialSpread","Starting spread allowance","Extra selection radius beyond the packed-unit footprint before trying the destination-local fallback.",CohesionGeneralGroup,250,0,2000,10,"m",visible:false);
            DefineCohesion("StartGrace","Movement start grace","Initial no-progress grace and allowed separation of queued segment starts.",CohesionGeneralGroup,5,0,30,.5f,"s",visible:false);
            DefineCohesion("DebugInterval","Debug sampling interval","Minimum periodic interval for enabled or watched traces.",CohesionGeneralGroup,3,.25f,60,.25f,"s",visible:false);
            DefineCohesion("MaxUnits","Maximum cohesion members","Maximum members in a custom command; oversized selections may use the destination-local fallback.",CohesionGeneralGroup,256,2,1024,1,"units",integer:true,visible:false);
            DefineCohesion("MaximumDuration","Maximum cohesion duration","Maximum command lifetime before speed ownership releases.",CohesionGeneralGroup,600,5,3600,5,"s",visible:false);
            DefineCohesion("Retention","Finished layout retention","Layout/history retention after captured moves finish; pending intended layouts have a separate lifetime.",CohesionGeneralGroup,500,0,1800,5,"s",visible:false);
            DefineCohesion("PathDetourRatio","Formation route detour limit","Route/direct-distance ratio above which a member counts as obstructed.",CohesionGeneralGroup,1.8f,1,10,.1f,"x",visible:false);
            DefineCohesion("HoldGrace","Sustained failure delay","Required duration of excess spread or failed holding checks before degradation or release.",CohesionGeneralGroup,2,.25f,30,.25f,"s",visible:false);
            DefineCohesion("TurnGrace","Formation turn allowance","Arrival pacing during a rotation before eligible formation holding is considered. Zero applies ordinary holding checks immediately.",CohesionGeneralGroup,6,0,30,.5f,"s",visible:false);
            DefineCohesion("ProgressDistance","Progress sample distance","Minimum position change or remaining-route decrease needed to refresh the progress timer.",CohesionGeneralGroup,1,.1f,20,.1f,"m",visible:false);
            DefineCohesion("TraceHistory","Retained trace samples","Maximum retained diagnostic samples per command.",CohesionGeneralGroup,80,10,500,1,"samples",integer:true,visible:false);
            // Shared defaults come from immutable descriptors, never loaded human values.
            // Keep the independently tuned Alien endpoints and startup grace.
            foreach(var human in cohesionSettingOrder.ToArray())
            {
                float value=human.Key switch
                {
                    "MaximumRange"=>5034,
                    "StrengthFar"=>.5f,
                    "HoldStrengthFar"=>.25f,
                    "MinimumSpeedNear"=>.1f,
                    "ArrivalWindowNear"=>.5f,
                    "ArrivalWindowFar"=>10,
                    "StartGrace"=>3,
                    _=>human.Default
                };
                var alien=new CohesionSetting{Key=human.Key,Label=human.Label,Description=human.Description,Group=human.Group,Unit=human.Unit,
                    Default=value,Value=value,Minimum=human.Minimum,Maximum=human.Maximum,Step=human.Step,Integer=human.Integer,Visible=human.Visible,Alien=true};
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
            groupCohesionPreference=serverPreferences.CreateEntry("GroupCohesion",true,description:"Enable bounded human commander move cohesion; enabled by default.");
            alienGroupCohesionPreference=serverPreferences.CreateEntry("AlienGroupCohesion",true,description:"Enable independently configured alien commander move cohesion; enabled by default.");
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
            foreach(var pair in cohesionPairs)
                if(!CohesionPairValid(pair,pair.Near.Value,pair.Far.Value))
                {
                    pair.Near.Value=pair.Near.Default;pair.Far.Value=pair.Far.Default;
                    pair.Near.Preference.Value=pair.Near.Value;pair.Far.Preference.Value=pair.Far.Value;
                    MelonLogger.Warning(CohesionFactionName(pair.Near.Alien)+" cohesion endpoints "+pair.Near.Key+"/"+pair.Far.Key+" are reversed; restored their defaults.");
                }
            MelonLogger.Msg("Cohesion uses one 0..MaximumRange distance profile per faction. Legacy Close/Medium/Long configuration keys are ignored; faction switches and common safety settings are retained.");
        }
        private static void SaveCohesionSettings()
        {
            if(groupCohesionPreference!=null)groupCohesionPreference.Value=CohesionEnabled;
            if(alienGroupCohesionPreference!=null)alienGroupCohesionPreference.Value=AlienCohesionEnabled;
            foreach(var setting in cohesionSettingOrder)if(setting.Preference!=null)setting.Preference.Value=setting.Value;
        }
        private static float CohesionEndpoint(bool alien,string key,float fraction)
            =>Mathf.Lerp(CohesionFactionValue(alien,key+"Near"),CohesionFactionValue(alien,key+"Far"),fraction);
        private static CohesionTuning CaptureCohesionTuning(float distance,Team team)
        {
            EnsureCohesionDefinitions();
            bool alien=IsAlienTeam(team);
            float maximum=CohesionFactionValue(alien,"MaximumRange");
            bool beyond=!CohesionFinite(distance)||distance>maximum;
            float fraction=CohesionFinite(distance)?Mathf.Clamp01(Mathf.Max(0,distance)/maximum):1;
            return new CohesionTuning{Range=beyond?"Beyond":"Distance",Fraction=fraction,
                Strength=beyond?0:CohesionEndpoint(alien,"Strength",fraction),HoldStrength=beyond?0:CohesionEndpoint(alien,"HoldStrength",fraction),
                MinimumSpeed=beyond?1:CohesionEndpoint(alien,"MinimumSpeed",fraction),ArrivalWindow=CohesionEndpoint(alien,"ArrivalWindow",fraction),
                MaxDelay=beyond?0:CohesionEndpoint(alien,"MaxDelay",fraction),
                // Safety tolerances vary continuously too, without restoring a large
                // per-range menu or reading the deprecated three-stage endpoints.
                MaxSpread=Mathf.Lerp(60,300,fraction),LagDistance=Mathf.Lerp(30,150,fraction),
                StuckSeconds=Mathf.Lerp(3,1.5f,fraction),BreakPercent=Mathf.Lerp(70,15,fraction),
                HoldError=Mathf.Lerp(.5f,50,fraction),HoldBreakPercent=Mathf.Lerp(40,10,fraction),
                ObstructedPercent=Mathf.Lerp(50,10,fraction),HoldCorrectionInterval=Mathf.Lerp(.35f,1.5f,fraction)};
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
            foreach(var pair in cohesionPairs)
                if(pair.Near==setting||pair.Far==setting)
                {
                    float nearValue=pair.Near==setting?value:pair.Near.Value,farValue=pair.Far==setting?value:pair.Far.Value;
                    if(!CohesionPairValid(pair,nearValue,farValue))
                    {error=pair.Near.Key+" must be "+(pair.Descending?">=":"<=")+" "+pair.Far.Key+".";return false;}
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
                Reply(p,CohesionSummary(alien)+" One distance profile: 0–"+CohesionNumber(settings["MaximumRange"].Value)+"m; longer moves have no custom pacing or holding.");
                Reply(p,"Use /"+command+" <key> <number>|status. Near/Far mean zero distance and MaximumRange. Legacy Close/Medium/Long keys are ignored; common safety keys remain available in cfg.");
                return;
            }
            string key=tokens[0];
            if(alien&&key.StartsWith("aliencohesion",StringComparison.OrdinalIgnoreCase))key=key.Substring(13);
            else if(key.StartsWith("cohesion",StringComparison.OrdinalIgnoreCase))key=key.Substring(8);
            if(!settings.TryGetValue(key,out var setting))
            {
                bool legacy=new[]{"close","medium","long"}.Any(prefix=>key.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
                Reply(p,legacy?"Close/Medium/Long keys are ignored in 3.8.0. Use MaximumRange and single-profile endpoints such as StrengthNear.":
                    "Unknown "+faction.ToLowerInvariant()+" cohesion key. Example: /"+command+" StrengthNear 1.");return;
            }
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
                    "Enable "+faction.ToLowerInvariant()+" custom pacing and eligible remembered-formation holding within MaximumRange. This faction's settings and switch are independent.",SwitchGroup);
                if(alien)
                {
                    AddSlider(commands,"alienspread","Alien: Placement spacing scale",ServerSettings.AlienSpread,DefaultAlienSpread,
                        "Scale Alien commander ground/large-air, FPS and rally spacing whether Alien cohesion is enabled or disabled. Lesser Spawn commander air uses its separate slider. Physical clearance is retained.",group:CohesionFactionGroup(true));
                    AddSlider(commands,"lesserspawnairspace","Alien: Lesser Spawn air spacing",ServerSettings.LesserSpawnAirSpacing,DefaultLesserSpawnAirSpacing,
                        "Extra commander spacing for Wasp, Dragonfly and Squid. 0% uses native line-footprint spacing with body clearance; +50% is 1.5x that baseline. Applies to new placements independently of other spacing controls.",
                        unit:"% extra",group:CohesionFactionGroup(true),maximum:MaximumLesserSpawnAirSpacing,step:5);
                }
                foreach(var setting in cohesionSettingOrder.Where(setting=>setting.Alien==alien&&setting.Visible))
                {
                    string label=faction+": "+setting.Label;
                    var row=AdminEntry("!"+command+" "+setting.Key,label,setting.Description+" Faction-specific setting for new commander moves.","slider",CohesionFactionGroup(alien));
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
