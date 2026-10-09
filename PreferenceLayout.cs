using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;

namespace Si_Formation
{
    public sealed partial class Formations
    {
        private static string PreferencesPath=>Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory,"Formations_cfg","si_formation.cfg");
        public override void OnPreferencesSaved(string filepath)
        {
            if(!string.IsNullOrEmpty(filepath)&&string.Equals(Path.GetFullPath(filepath),Path.GetFullPath(PreferencesPath),StringComparison.OrdinalIgnoreCase))
                FormatPreferences();
        }
        private static void FormatPreferences()
        {
            try
            {
                if(!File.Exists(PreferencesPath))return;
                string original=File.ReadAllText(PreferencesPath);
                var values=new Dictionary<string,string>(StringComparer.Ordinal);
                var entry=new Regex(@"^([A-Za-z][A-Za-z0-9_]*)\s*=\s*(.*)$");
                bool section=false;
                foreach(string raw in original.Split('\n'))
                {
                    string line=raw.Trim();
                    if(line.Length==0||line.StartsWith("#"))continue;
                    if(line=="[Si_Formation]"&&!section){section=true;continue;}
                    var match=entry.Match(line);
                    // Do not rewrite files with extra sections, duplicate keys or syntax
                    // outside this simple preferences layout; never touch other categories.
                    if(!section||!match.Success||values.ContainsKey(match.Groups[1].Value))return;
                    string key=match.Groups[1].Value,value=match.Groups[2].Value;
                    if(key.StartsWith("StartDelay",StringComparison.Ordinal)&&float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float delay))
                        value=delay.ToString("G7",CultureInfo.InvariantCulture);
                    values.Add(key,value);
                }
                if(!section)return;
                string template;
                using(var input=typeof(Formations).Assembly.GetManifestResourceStream("Si_Formation.Preferences.cfg"))
                using(var reader=new StreamReader(input))template=reader.ReadToEnd();
                var result=new StringBuilder();
                foreach(string raw in template.Split('\n'))
                {
                    string line=raw.TrimEnd('\r');var match=entry.Match(line);
                    if(!match.Success){result.AppendLine(line);continue;}
                    string key=match.Groups[1].Value;
                    // Missing settings stay missing until MelonLoader creates them.
                    if(values.TryGetValue(key,out string value)){result.AppendLine(key+" = "+value);values.Remove(key);}
                }
                if(values.Count>0)
                {
                    result.AppendLine("# === Additional preserved settings ===");
                    foreach(var value in values)result.AppendLine(value.Key+" = "+value.Value);
                }
                string formatted=result.ToString().TrimEnd()+Environment.NewLine;
                if(formatted!=original)File.WriteAllText(PreferencesPath,formatted,new UTF8Encoding(false));
            }
            catch(Exception e){MelonLogger.Warning("Could not format si_formation.cfg: "+e.Message);}
        }
    }
}
