using System;
using System.IO;
using MelonLoader;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static void FormationHelp(Player p)
        {
            if(!p)return;
            Reply(p,"/formation - menu; /formation help - commands; /formation status - status and selections.");
            Reply(p,"In the menu: /<number> selects; /back returns. Option 1 in a formation list is vanilla.");
            if(p.IsCommander)
            {
                Reply(p,"/formation <number> - commander formation; /formation lock - next move only; /formation unlock - release locks.");
                Reply(p,"/scout - next move selection scouts (at least five eligible units). Aircraft and excess units use native movement on normal moves.");
            }
            else
                Reply(p,"/formation <function> <number> - 1 Move, 2 Follow, 3 Attack; /disband all - disband your AI squad (leader only).");
            if(p.IsCommander||IsAdministrator(p))
                Reply(p,"Legacy compatibility only: /formationTolerance <10..100>, /FormationFallof <10..90>; neither changes current movement.");
            if(!IsAdministrator(p))return;
            Reply(p,"Admin: /formation on|off - global match switch; /formationsizeglobal <1..50>; /formationsize \"Player Name\" <1..50> (FPS, including leader).");
            Reply(p,"/formationsizecommander <1..100> | status - saved per-selection cap (default 75); /Formationtraveldistance <0..10000> | status - saved lock maximum metres (default 1000).");
            Reply(p,"Farther lock clicks stop the formation centre at that maximum; 0 keeps the centre in place. Soft-lock moves are not range-limited.");
            Reply(p,"/formationTravel <metres> - alias for /Formationtraveldistance; /Formationspeedcap on|off - final-arrival ceiling (centre pacing remains).");
            Reply(p,"/startdelaysoftlock <seconds>; /startdelayspeedlock <seconds>; /startdelaylock <seconds> (alias /startdelay lock). 0 disables, otherwise 0.1..5; match-only.");
            Reply(p,"Speed-based lock delay takes priority. For positional rows: /startdelayspeedlock 0 then /startdelaylock <seconds>.");
        }
        private static void EnsureBundledFormations()
        {
            string directory=Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"Formations_cfg");
            Directory.CreateDirectory(directory);
            // Never introduce duplicate definitions into an existing custom catalog.
            if(Directory.GetFiles(directory,"*.json").Length>0)return;
            string destination=Path.Combine(directory,"silica-formations.json");
            using(var input=typeof(Formations).Assembly.GetManifestResourceStream("Si_Formation.DefaultFormations.json"))
            {
                if(input==null)throw new InvalidOperationException("Bundled default formations resource is missing.");
                using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
            }
            MelonLogger.Msg("Installed bundled default formations: "+destination);
        }
    }
}
