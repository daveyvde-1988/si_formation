using System;
using System.Collections.Generic;
using System.Linq;
using SilicaAdminMod;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private sealed class Menu
        {
            internal Team Team;
            internal bool Commander;
            internal string Function;
            internal FormationDefinition[] Choices=Array.Empty<FormationDefinition>();
        }
        private static readonly Dictionary<Player,Menu> Menus=new Dictionary<Player,Menu>();
        private static bool IsAdministrator(Player p)=>p&&p.IsAdmin()&&AdminMethods.PowerInPowers(Power.Generic,p.GetAdminPowers());
        private static void ToggleMenu(Player p)
        {
            if(!Ready||!Game.GetIsServer()||!p)return;
            if(Menus.Remove(p)){Reply(p,"menu closed.");return;}
            OpenMenu(p);
        }
        private static void OpenMenu(Player p)
        {
            if(!MenuAvailable(p,true))return;
            if(TeamName(p.Team)==null){Reply(p,"join a supported team first.");return;}
            For(p); // reset stale role/team state before installing the new menu
            var menu=new Menu {Team=p.Team,Commander=p.IsCommander,Function=p.IsCommander?"commander":null};
            Menus[p]=menu;ShowMenu(p,menu);
        }
        private static void ShowMenu(Player p,Menu menu)
        {
            if(!Running)Reply(p,"global formations OFF/unavailable; selections apply when enabled.");
            if(menu.Function==null)
            {
                Reply(p,"Formation functions\n/1 Move\n/2 Follow\n/3 Attack\n/4 Disband all");return;
            }
            string current=SelectedName(p,menu.Function);
            menu.Choices=MenuChoices(TeamName(p.Team),menu.Function);
            Reply(p,menu.Function+" — Current formation: "+SafeChat(current));
            Reply(p,"/1 Default");
            foreach(var f in menu.Choices)Reply(p,"/"+f.MenuOrder+" "+SafeChat(f.Name));
            if(menu.Choices.Length==0)Reply(p,"no valid JSON formations for this team/order; native orders retained.");
            Reply(p,"/back returns; /formation closes");
        }
        // Stable numbering shared by defaults and menus; selections never reorder the list.
        private static FormationDefinition[] MenuChoices(string team,string function)
            =>Catalog.Definitions.Values.Where(f=>f.Team==team&&f.Function==function)
                .OrderBy(f=>f.MenuOrder).ToArray();
        private static string SafeChat(string value)=>value.Replace("<","‹").Replace(">","›");
        // Consume only scoped menu input before AdminMod dispatches its chat event.
        // No global numeric or back commands are registered.
        private static bool MenuChat(Player player,string message,bool teamOnly,ref OnRequestPlayerChatArgs __result)
        {
            if(!HandleMenuChat(player,message,teamOnly))return true;
            __result=new OnRequestPlayerChatArgs {Player=player,Text=message,TeamOnly=teamOnly,Block=true};
            return false;
        }
        private static bool HostMenuChat(Player __instance,string __0,bool __1,ref bool __result)
        {
            if(!HandleMenuChat(__instance,__0,__1))return true;
            __result=false;return false;
        }
        private static bool HandleMenuChat(Player p,string message,bool teamOnly)
        {
            if(!Ready||!Game.GetIsServer()||!p||!Menus.TryGetValue(p,out Menu menu))return false;
            if(teamOnly&&SiAdminMod.Pref_Admin_AcceptTeamChatCommands!=null&&!SiAdminMod.Pref_Admin_AcceptTeamChatCommands.Value)return false;
            string text=(message??"").Trim();
            if(!MenuAvailable(p,false)){Menus.Remove(p);return false;}
            bool back=text.Equals("/back",StringComparison.OrdinalIgnoreCase);
            bool numeric=text.Length>1&&text[0]=='/'&&text.Skip(1).All(c=>c>='0'&&c<='9');
            if(!back&&!numeric)
            {
                // Hand other menus/commands their input and release our numeric ownership.
                if(text.Length>0&&HelperMethods.IsValidCommandPrefix(text[0])&&
                    !text.Split(' ','\t')[0].Equals("/formation",StringComparison.OrdinalIgnoreCase))Menus.Remove(p);
                return false;
            }
            if(menu.Team!=p.Team||menu.Commander!=p.IsCommander)
            {Menus.Remove(p);Reply(p,"team/role changed; choose again.");OpenMenu(p);return true;}
            if(back)
            {
                if(menu.Commander||menu.Function==null){Menus.Remove(p);Reply(p,"menu closed.");}
                else {menu.Function=null;ShowMenu(p,menu);}return true;
            }
            if(!int.TryParse(text.Substring(1),out int choice)||choice<1)
            {Reply(p,"invalid menu number; use /back.");return true;}
            if(menu.Function==null)
            {
                if(choice==4){DisbandAll(p);return true;}
                if(choice>3){Reply(p,"choose /1, /2, /3 or /4.");return true;}
                menu.Function=new[]{"move","follow","attack"}[choice-1];ShowMenu(p,menu);return true;
            }
            ChooseNumber(p,menu.Function,choice);return true;
        }
        private static bool EffectivePersonal(Player p)
            =>p&&(p.IsCommander?FunctionActive(p,"commander"):new[]{"move","follow","attack"}.Any(f=>FunctionActive(p,f)));
        private static void ReleasePlayerControl(Player p)
        {
            ReleaseCommander(p);
            if(!p)return;
            CancelPlayer(p);
            foreach(var leader in Followers.Values.Where(s=>s.Leader&&s.Leader.ControlledBy==p).Select(s=>s.Leader).Distinct().ToArray())ResetLeader(leader);
            if(p.ControlledUnit)ResetLeader(p.ControlledUnit);
        }
        private static void DisablePersonal(Player p)
        {
            ReleasePlayerControl(p);For(p).PersonalEnabled=false;Menus.Remove(p);
        }
        private static void ChoiceMessage(Player p,string name)
        {Menus.Remove(p);HelperMethods.SendChatMessageToPlayer(p,"formation "+SafeChat(name)+" selected");}
        private static void ApplyCustom(Player p,string function,FormationDefinition f)
        {
            var selection=For(p);
            selection.Functions[function]=f;selection.PersonalEnabled=true;
            if(function=="follow")ResetLeader(p.ControlledUnit);
            if(function=="attack")CancelPlayer(p);
            ChoiceMessage(p,f.Name);
        }
        private static bool ChooseNumber(Player p,string function,int number)
        {
            if(!Ready||!Game.GetIsServer()||!p||TeamName(p.Team)==null||(p.IsCommander?function!="commander":!new[]{"move","follow","attack"}.Contains(function)))
            {Reply(p,"formation menu unavailable for your current team/role.");return false;}
            if(number==1)
            {
                if(p.IsCommander){ReleasePlayerControl(p);For(p).Functions.Remove("commander");For(p).PersonalEnabled=true;}
                else DisablePersonal(p);
                ChoiceMessage(p,"Default");return true;
            }
            var f=MenuChoices(TeamName(p.Team),function).FirstOrDefault(v=>v.MenuOrder==number);
            if(f==null){Reply(p,"number is not in this formation list; /1 is Default.");return false;}
            ApplyCustom(p,function,f);return true;
        }
        private static void HandleShortcut(Player p,string text)
        {
            if(!Ready||!Game.GetIsServer()||!p){Reply(p,"requires an initialized server and issuing player.");return;}
            var parts=text.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            bool Number(string value,out int n)=>int.TryParse(value,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out n)&&n>=1&&n<=9999;
            if(p.IsCommander)
            {
                if(parts.Length==1&&Number(parts[0],out int choice)){ChooseNumber(p,"commander",choice);return;}
                Reply(p,"usage: /formation <commander option number> or /formation on|off|status");return;
            }
            if(parts.Length==2&&Number(parts[0],out int function)&&function<=3&&Number(parts[1],out int option))
            {ChooseNumber(p,new[]{"move","follow","attack"}[function-1],option);return;}
            Reply(p,"usage: /formation <function 1..3> <formation option> or /formation on|off|status");
        }
        private static void DisbandCommand(Player p,string args)
        {
            if(!CommandArgument.TryParse(args,out string action,out bool quoted)||quoted||!action.Equals("all",StringComparison.OrdinalIgnoreCase))
            {Reply(p,"usage: /disband all");return;}
            DisbandAll(p);
        }
        private static void DisbandAll(Player p)
        {
            if(!Ready||!Game.GetIsServer()||!p||p.IsCommander){Reply(p,"disband all is only available to FPS players.");return;}
            if(networkSender&&networkSender!=p){Reply(p,"issuing player mismatch.");return;}
            var group=p.Group;
            if(group==null){Menus.Remove(p);Reply(p,"no FPS group to disband.");return;}
            if(!p.ControlledUnit||group.Leader!=p.ControlledUnit){Reply(p,"you must lead your current FPS group.");return;}
            ReleasePlayerControl(p);Menus.Remove(p);
            int removed=0,protectedUnits=0,failed=0;
            // Same native removal used by FPSCommanding.KickFromGroupServer.
            // Suppress re-entrant follow updates until all eligible members are removed.
            generatedDepth++;
            try
            {
                foreach(var unit in group.Units.ToArray())
                {
                    if(!unit||unit==p.ControlledUnit)continue;
                    if(unit.PlayerControlled||unit.ControlledBy||(unit.Driver&&unit.Driver.ControlledBy)){protectedUnits++;continue;}
                    CancelUnit(unit);Followers.Remove(unit);Superseded.Remove(unit);
                    try{group.RemoveUnit(unit);if(group.Units.Contains(unit))failed++;else removed++;}
                    catch(Exception e){failed++;MelonLoader.MelonLogger.Warning("Disband removal failed: "+e.Message);}
                }
            }
            finally{generatedDepth--;}
            Reply(p,"disbanded "+removed+" units; leader retained."+(protectedUnits>0?" "+protectedUnits+" player/driver-controlled units retained.":"")+(failed>0?" "+failed+" removals failed; see server log.":""));
        }
        private static void PersonalStatus(Player p)
        {
            ReportMaster(p);
            if(!p)return;
            Reply(p,"personal formations "+(For(p).PersonalEnabled?"ON":"OFF")+"; effective "+(EffectivePersonal(p)?"ON":"OFF"));
            foreach(string function in p.IsCommander?new[]{"commander"}:new[]{"move","follow","attack"})
                Reply(p,function+": "+SafeChat(SelectedName(p,function))+" (effective "+(FunctionActive(p,function)?"ON":"native")+")");
            if(p.IsCommander)Reply(p,"commander formation maximum: "+CommanderOrderLimit+" eligible ground units per selection; locked travel maximum: "+LockTravelMaximum+" metres; pending "+(Options(p).Pending==1?"lock":Options(p).Pending==2?"scout":"none"));
            else Reply(p,"effective FPS group maximum: "+EffectiveLimit(p)+" (including leader)"+
                (!Running?"; formation limits inactive while global OFF/unavailable":""));
            if(IsAdministrator(p))Reply(p,"match-wide group limit: "+MatchLimit+"; commander order maximum: "+CommanderOrderLimit);
        }
    }
}
