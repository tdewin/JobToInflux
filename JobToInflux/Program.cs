using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.Reflection;
using System.IO;
using static System.Environment;
using System.Diagnostics;

//thanks to stack overflow http://stackoverflow.com/questions/569606/how-can-i-make-service-apps-with-visual-c-sharp-express
//and http://stackoverflow.com/questions/1195478/how-to-make-a-net-windows-service-start-right-after-the-installation/1195621#1195621
//C:\Windows\Microsoft.NET\Framework\v4.0.30319>InstallUtil.exe
//for 64 bit builds : C:\Windows\Microsoft.NET\Framework64\v4.0.30319>InstallUtil.exe /logtoconsole=true c:\d\influxservice\JobToInflux.exe
//also run as adminsitrator

//add reference to System.Runtime.Serialization
//add reference to System.ServiceProcess
//add reference System.Configuration.Install
//also find nuget package InfluxDB.Collector (at the time in prerelease)
namespace JobToInflux
{
    class Program
    {
        static void InstallService(bool uninstall)
        {
            var installutil = String.Format("{0}\\Microsoft.NET\\Framework64\\v4.0.30319\\InstallUtil.exe", Environment.GetFolderPath(SpecialFolder.Windows));
            var thisexe = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;

            if (File.Exists(installutil))
            {
                Process p = new Process();
                p.StartInfo.FileName = installutil;
                p.StartInfo.Arguments = "/logtoconsole=true \"" + thisexe + "\""+(uninstall?" /uninstall":"");
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                if (stdout != "")
                {
                    System.Console.WriteLine("Output:\n " + stdout);
                }
                else { System.Console.WriteLine("Success"); }
            }
            else
            {
                System.Console.WriteLine("Can not find installutil, looked in : " + installutil);
            }
        }
        static void MakeDefConfig()
        {
            string directory = InfluxCollectConfig.GetDefaultPath();
            string filename = InfluxCollectConfig.GetDefaultConfigFileName();
            string configfile = String.Format("{0}\\{1}", directory, filename);

            //if there is no config, just make one from scratch
            try
            {
                InfluxCollectConfig.TryMakeDefaultConfig(directory, filename);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Got error " + e.Message);
            }
        }
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Length > 0 )
                {


                    switch (args[0]) {
                        case "makedefaultconfig":
                            MakeDefConfig();
                            break;
                        case "install":
                            InstallService(false);
                            MakeDefConfig();
                            break;
                        case "uninstall":
                            InstallService(true);
                            break;
                    }
                    
                } else
                {
                    System.Console.WriteLine("Try with makedefaultconfig | install | uninstall");
                }
                
            }
            else
            {
                System.ServiceProcess.ServiceBase.Run(new InfluxCollectService());
            }
        }
    }
}
