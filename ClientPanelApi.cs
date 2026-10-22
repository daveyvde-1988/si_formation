using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Si_Formation
{
    public static partial class FormationApi
    {
        /// <summary>Main-thread, player-scoped catalog page. No chat menu is opened.</summary>
        public static IReadOnlyDictionary<string,object> GetPlayerPanelPage(ulong steamId,string function,int page)
            =>Formations.ApiPanelPage(steamId,function,page);
        /// <summary>Narrow player action API. Caller must authenticate the player and scope its request.</summary>
        public static string RequestPlayerPanelAction(ulong steamId,string function,int option,string action,string revision)
            =>Formations.ApiPanelAction(steamId,function,option,action,revision);
    }

    public sealed partial class Formations
    {
        private static string panelEpoch=Guid.NewGuid().ToString("N");
        private static bool PanelFunction(Player p,string function)=>p&&p.Team&&TeamName(p.Team)!=null&&
            (p.IsCommander?function=="commander":new[]{"follow","move","attack"}.Contains(function));
        private static string PanelRevision(Player p,string function)
        {
            string data=panelEpoch+"|"+TeamName(p.Team)+"|"+function+"|"+
                string.Join("\n",MenuChoices(TeamName(p.Team),function).Select(f=>f.MenuOrder+":"+f.Name));
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-","").ToLowerInvariant();
        }
        internal static IReadOnlyDictionary<string,object> ApiPanelPage(ulong id,string function,int page)
        {
            var error=new Dictionary<string,object>{{"error","Formation menu unavailable for your current team or role."}};
            if(ApiCheck()!=ChangeResult.Success)return error;
            var p=Connected(id);
            if(!PanelFunction(p,function)||page<0||page>2048)return error;
            var selection=For(p);
            var choices=MenuChoices(TeamName(p.Team),function);
            int pages=Math.Max(1,(choices.Length+1+5)/6);
            page=Math.Min(page,pages-1);
            var numbers=new[]{1}.Concat(choices.Select(f=>f.MenuOrder)).Skip(page*6).Take(6).ToArray();
            var names=new[]{p.IsCommander?"Native commander formation":"Native (all FPS formations off)"}.Concat(choices.Select(f=>f.Name)).Skip(page*6).Take(6).ToArray();
            return new Dictionary<string,object>{
                {"page",page},{"pages",pages},{"numbers",numbers},{"names",names},
                {"selected",selection.PersonalEnabled?(Selected(p,function)?.MenuOrder??1):1},
                {"locked",p.IsCommander?Options(p).Pending==1||Locks.Any(g=>g.Owner==p&&g.Team==p.Team&&g.Members.Count>0):selection.FollowFacingLock},
                {"commanderLock",p.IsCommander},
                {"enabled",FunctionActive(p,function)},{"revision",PanelRevision(p,function)},
                {"leader",!p.IsCommander&&p.ControlledUnit&&p.Group!=null&&p.Group.Leader==p.ControlledUnit}
            };
        }
        internal static string ApiPanelAction(ulong id,string function,int option,string action,string revision)
        {
            if(ApiCheck()!=ChangeResult.Success)return "Formation service unavailable.";
            var p=Connected(id);
            if(!PanelFunction(p,function))return "Your team or role changed; reopen the panel.";
            if(revision!=PanelRevision(p,function))return "Formation list or round changed; refresh before choosing.";
            if(action=="select")return ChooseNumber(p,function,option)?"Formation selection applied.":"Formation selection rejected.";
            if(p.IsCommander&&action=="lock"&&(option==0||option==1))
            {
                if(option==1&&!CommanderAuthorized(p))return "Commander formations unavailable; lock was not armed.";
                LockCommand(p,option==1?"lock":"unlock");
                return option==1?"Formation lock armed for your next commander move.":"Commander formations unlocked; pending lock cancelled.";
            }
            if(p.IsCommander||!p.ControlledUnit||p.Group==null||p.Group.Leader!=p.ControlledUnit)return "This action requires an FPS squad leader.";
            if(action=="lock"&&(option==0||option==1))
            {
                FollowOrientationCommand(p,option==1?"/followformation lock":"/followformation unlock");
                return option==1?"Facing lock enabled.":"Facing lock disabled.";
            }
            if(action=="disband"&&option==0){DisbandAll(p);return "Squad dismissal processed; controlled units retained.";}
            return "Unsupported formation action.";
        }
    }
}
