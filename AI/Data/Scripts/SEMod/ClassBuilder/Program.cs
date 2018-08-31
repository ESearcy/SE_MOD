using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = "H:\\SEMODS\\FighterCommand\\Data\\Scripts\\SEMod\\SEMod\\INGAME\\classes";
            var configFile = "H:\\SEMODS\\FighterCommand\\Data\\Scripts\\SEMod\\SEMod\\INGAME\\classes\\implementations";


            string[] files = Directory.GetFileSystemEntries(path, "*.cs", SearchOption.AllDirectories);

            Dictionary<string, string> contents = new Dictionary<string, string>();

            foreach (String file in files){
                string classname = Path.GetFileName(file).Replace(".cs","");
                //string cont = File.ReadAllText(file);
                String[] lines = File.ReadAllLines(file);
                int breaksFound = 0;
                StringBuilder content = new StringBuilder();

                for(int i = 0;i<lines.Length && breaksFound < 2; i++)
                {
                    String line = lines[i];
                    if (line.Contains("//////"))
                    {
                        if (breaksFound < 1 && line.Replace("//////", "").Trim().Length > 0)
                            content.AppendLine(line.Replace("//////", "").Trim());

                        breaksFound++;
                    }
                    else if (breaksFound == 1 && line.Trim().Length > 0 && !line.Trim().StartsWith("//"))
                        content.AppendLine(line.Trim());
                }
                contents.Add(classname, content.ToString());
            }
            string[] conFiles = Directory.GetFileSystemEntries(configFile, "*.txt", SearchOption.AllDirectories);

            foreach (string file in conFiles)
            {
                string parent = Path.GetDirectoryName(path);
                string[] stypes = File.ReadAllLines(file);

                foreach(string line in stypes)
                {
                    string concatedClasses = "";
                    string shipType = line.Split(':')[0];
                    string[] requiredClasses = line.Split(':')[1].Split(',');

                    foreach (string req in requiredClasses)
                    {
                        concatedClasses += "\n"+ contents[req].Trim();
                    }
                    concatedClasses = contents[shipType].Trim()+ "\n" + concatedClasses;

                    string outputPath = parent + "\\\\" + shipType +".txt";

                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    File.WriteAllText(outputPath, concatedClasses);
                }
            }
            


            files.Count();
        }
    }
}
