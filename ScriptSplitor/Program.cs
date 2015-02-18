using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;

namespace ScriptSplitor
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<Key, List<string>> treeMap = new Dictionary<Key, List<string>>();

            string temp = string.Empty;
            int i = 0;
            Console.Write("\nStarting with split....");
            
            using (var reader = new StreamReader(ConfigurationManager.AppSettings["ReadFromPath"]))
            {
                while (!reader.EndOfStream)
                {
                    string row = reader.ReadLine();
                    i++;
                    if(i%10000==0)
                    Console.Write("\n\nNumber of rows processed :: " + i);
                    
                    if (row.StartsWith("--") || row.StartsWith("alter table", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(row) || string.IsNullOrEmpty(row))
                        continue;

                    temp = temp + row;
                    int invertedCommaCount = temp.Count(c => c == '\'');
                    if (invertedCommaCount % 2 != 0)
                        continue;

                    if (temp.IndexOf("update [dbo].", StringComparison.InvariantCultureIgnoreCase) != -1)
                        AddRowToDict(treeMap, temp, ScriptType.Update);

                    if (temp.IndexOf("insert into", StringComparison.InvariantCultureIgnoreCase) != -1)
                        AddRowToDict(treeMap, temp, ScriptType.Insert);

                    if (temp.IndexOf("delete from", StringComparison.InvariantCultureIgnoreCase) != -1)
                        AddRowToDict(treeMap, temp, ScriptType.Delete);
                    temp = string.Empty;
                }
            }

            WriteAllRowToFile(treeMap, true);
            Console.WriteLine("\n\n---- Mission Accomplish ----");
            Console.ReadLine();

        }

        public static void WriteAllRowToFile(Dictionary<Key, List<string>> treeMap,bool isLastBatch)
        {
            string writeToPath=ConfigurationManager.AppSettings["WriteToPath"];
            if(!Directory.Exists(writeToPath))
                Directory.CreateDirectory(Path.GetDirectoryName(writeToPath));

            Dictionary<Key, List<string>> copyTreeMap = new Dictionary<Key, List<string>>(treeMap);
            foreach (Key key in treeMap.Keys)
            {
                if (treeMap[key] != null )
                {
                    string filePath = writeToPath + key.KeyName + ".sql";
                    if (isLastBatch)
                    {
                        string countStr = "--Total rows affected = " + key.NumOfRows;
                        treeMap[key].Add(countStr);
                    }
                    if (treeMap[key].Count >= 0)
                    {
                        if (!File.Exists(filePath))
                            System.IO.File.WriteAllLines(filePath, treeMap[key]);
                        else
                            System.IO.File.AppendAllLines(filePath, treeMap[key]);

                        copyTreeMap[key] = new List<string>(); 
                    }
                }

            }

            OverwriteTreeMapWithCopy(treeMap, copyTreeMap);
        }


        public static void OverwriteTreeMapWithCopy(Dictionary<Key, List<string>> treeMap, Dictionary<Key, List<string>> copyTreeMap)
        {
            treeMap.Clear();
            foreach (var key in copyTreeMap.Keys)
                treeMap.Add(key, copyTreeMap[key]);
        }

        public static void AddRowToDict(Dictionary<Key, List<string>> treeMap, string row, ScriptType scriptType)
        {
            string tableName = GetTableName(row);
            if (!string.Equals(tableName, ScriptType.InvalidRow.ToString()))
            {
                string keyName = scriptType + "_" + tableName;
                Key key = treeMap.Keys.ToList().Find(x => string.Equals(x.KeyName, keyName, StringComparison.InvariantCultureIgnoreCase));
                if (key!=null && treeMap.ContainsKey(key))
                {
                    treeMap[key].Add(row);
                    key.NumOfRows++;
                }
                else
                {
                    WriteAllRowToFile(treeMap, false);
                    treeMap.Add(new Key() { KeyName=keyName,NumOfRows=1 }, new List<string>() { row });
                }
            }
        }

        public static string GetTableName(string row)
        {
            if (row.IndexOf("dbo", StringComparison.InvariantCultureIgnoreCase) == -1)
                return ScriptType.InvalidRow.ToString();

            try
            {
                int startIndx = GetIndex('[', 2, row);
                int endIndx = GetIndex(']', 2, row);
                string tableName = row.Substring(startIndx + 1, endIndx - startIndx - 1);
                return tableName.ToLower();
            }
            catch (Exception)
            {
                return ScriptType.InvalidRow.ToString();
            }
        }

        public static int GetIndex(char ch, int occurance, string str)
        {
            var result = str
              .Select((c, i) => new { c, i })
              .Where(x => x.c == ch)
              .Skip(occurance - 1)
              .FirstOrDefault();
            return result != null ? result.i : -1;
        }

    }

    public enum ScriptType
    {
        Update,
        Insert,
        Delete,
        InvalidRow
    }
}


