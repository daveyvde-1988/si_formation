using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;
using SilicaAdminMod;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class CommanderOptions
        {
            internal Team Team;
            internal float Tolerance, Falloff;
            internal int Pending; // 1: snapshot lock; 2: scout. One pending action per commander.
        }
        private static readonly Dictionary<Player,CommanderOptions> CommanderSettings=new Dictionary<Player,CommanderOptions>();
        private static MelonPreferences_Entry<int> travelDefault,toleranceDefault,falloffDefault;
        private static int? moderatorTolerance,moderatorFalloff;
        private static int LockTravelMaximum=>(int)ValidOption(travelDefault?.Value??1000,0,10000,1000);
        private static void TravelDistanceCommand(Player p,string args)
        {
            if(!ApiReady||!IsAdministrator(p)){Reply(p,"admin Generic power required.");return;}
            if(!CommandArgument.TryParse(args,out string value,out bool quoted)||quoted)
            {Reply(p,"usage: /Formationtraveldistance <0..10000> | status");return;}
            if(value.Length==0||value.Equals("status",StringComparison.OrdinalIgnoreCase))
            {Reply(p,"Locked formation maximum "+LockTravelMaximum+" metres.");return;}
            if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int metres)||metres<0||metres>10000)
            {Reply(p,"usage: /Formationtraveldistance <0..10000> | status");return;}
            travelDefault.Value=metres;
            try{prefs.SaveToFile(false);FormatPreferences();Reply(p,"Locked formation maximum "+metres+" metres (saved; new moves only).");}
            catch(Exception e){MelonLogger.Error("Saving FormationTravelDistance: "+e);Reply(p,"Locked formation maximum "+metres+" metres; saving failed, see server log.");}
        }
        private static MelonPreferences_Entry<bool> speedCapDefault;
        private static bool? speedCapOverride;
        private static bool SpeedCapEnabled=>speedCapOverride??(speedCapDefault?.Value??true);
        private static void SpeedCapCommand(Player p,string args)
        {
            if(!ApiReady||!IsAdministrator(p)){Reply(p,"admin Generic power required.");return;}
            if(!CommandArgument.TryParse(args,out string action,out bool quoted)||quoted||
                (!action.Equals("on",StringComparison.OrdinalIgnoreCase)&&!action.Equals("off",StringComparison.OrdinalIgnoreCase)))
            {Reply(p,"usage: /Formationspeedcap on|off");return;}
            speedCapOverride=action.Equals("on",StringComparison.OrdinalIgnoreCase);
            foreach(var group in Locks)RefreshSpeedCaps(group);
            Reply(p,"Formation speed cap "+(SpeedCapEnabled?"ON":"OFF")+" for final arrival handling this match. Centre-based travel pacing remains active.");
        }
        private void RegisterCommanderOptions()
        {
            speedCapDefault=prefs.CreateEntry("FormationSpeedCap",true,description:"Optional faster-unit ceiling near final arrival; centre-based travel pacing is always active. Admin /Formationspeedcap on|off overrides for this match");
            RegisterCommanderCommand("Formationspeedcap",SpeedCapCommand);
            RegisterDepartureSettings();
            travelDefault=prefs.CreateEntry("FormationTravelDistance",1000,description:"Locked formation maximum travel distance in metres, 0..10000; farther clicks stop the snapshot centre at this distance");
            toleranceDefault=prefs.CreateEntry("FormationPositionTolerance",25,description:"Slot tolerance as percent of snapshot nearest-neighbour spacing, 10..100; vehicle-size minimum also applies");
            falloffDefault=prefs.CreateEntry("FormationFalloffSlowdown",25,description:"Percent outside slot tolerance to trigger slowdown, 10..90; recovery threshold is 60% of this value (25 -> 15)");
            RegisterCommanderCommand("scout",(p,a)=>ArmCommander(p,2));
            RegisterCommanderCommand("Formationtraveldistance",TravelDistanceCommand);
            RegisterCommanderCommand("formationTravel",TravelDistanceCommand);
            RegisterCommanderCommand("formationTolerance",(p,a)=>SetCommanderOption(p,a,1));
            RegisterCommanderCommand("FormationFallof",(p,a)=>SetCommanderOption(p,a,2));
        }
        private void RegisterCommanderCommand(string name,Action<Player,string> callback)
        {
            if(AdminMethods.FindAdminCommandFromString(name)!=null||PlayerMethods.FindPlayerCommandFromString(name)!=null)
                throw new InvalidOperationException("Command already registered: "+name);
            PlayerMethods.RegisterPlayerCommand(name,(p,a)=>callback(p,a),true);registered.Add(name);
        }
        private static float ValidOption(int value,int min,int max,int fallback)=>value>=min&&value<=max?value:fallback;
        private static CommanderOptions Options(Player p)
        {
            if(!CommanderSettings.TryGetValue(p,out var settings)||settings.Team!=p.Team)
                CommanderSettings[p]=settings=new CommanderOptions {Team=p.Team,
                    Tolerance=ValidOption(moderatorTolerance??toleranceDefault.Value,10,100,25),
                    Falloff=ValidOption(moderatorFalloff??falloffDefault.Value,10,90,25)};
            return settings;
        }
        private static bool CommanderAuthorized(Player p)
        {
            if(!Running||!p||!p.IsCommander||!p.Team||(networkSender&&networkSender!=p))
            {Reply(p,"requires an active commander and enabled server formations.");return false;}
            For(p);return true;
        }
        private static void LockCommand(Player p,string action)
        {
            if(action.Equals("lock",StringComparison.OrdinalIgnoreCase)){ArmCommander(p,1);return;}
            if(!p||!Game.GetIsServer()||!p.IsCommander){Reply(p,"commander only.");return;}
            var settings=Options(p);ReleaseCommander(p);settings.Pending=0;CommanderSettings[p]=settings;
            Reply(p,"All your locked formations released; pending action cancelled; movement settings restored.");
        }
        private static void ArmCommander(Player p,int action)
        {
            if(!CommanderAuthorized(p))return;
            Options(p).Pending=action;
            if(action==1){Reply(p,"formation lock armed for one move; maximum "+LockTravelMaximum+" metres");return;}
            Reply(p,"Scouting armed. Select units and issue a move order; the clicked destination will be ignored.");
        }
        private static void SetCommanderOption(Player p,string text,int index)
        {
            if(!ApiReady||!p||(!p.IsCommander&&!IsAdministrator(p))) {Reply(p,"commander or moderator Generic power required.");return;}
            if(!CommandArgument.TryParse(text,out var value,out var quoted)||quoted||
                !int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int number)||
                number<10||number>(index==1?100:90))
            {Reply(p,index==1?"position tolerance range: 10..100%.":"fall-off slowdown range: 10..90%.");return;}
            var s=Options(p);
            if(index==1)s.Tolerance=number;else s.Falloff=number;
            // Moderator changes affect defaults for future commander sessions in this match only.
            // These legacy overrides remain match-only; do not save them to the standalone configuration.
            if(!p.IsCommander) {if(index==1)moderatorTolerance=number;else moderatorFalloff=number;}
            Reply(p,(index==1?"Position tolerance: ":"Fall-off group slowdown: ")+number+"%."+
                (p.IsCommander?" Personal setting.":" Default for new commander sessions.")+(index==2?" Recovery below "+(number*0.6f).ToString("0.#",CultureInfo.InvariantCulture)+"%.":""));
        }
    }
}
