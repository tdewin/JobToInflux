using System.Threading;
using InfluxDB.Collector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Environment;
using System.IO;
using System.Management.Automation;
using System.Reflection;

//use influx package
//InfluxDB.Collector

namespace JobToInflux
{
    public enum ServiceState { Running = 1, Paused, Stopped, ForceStop };

    public class InfluxCollectService : System.ServiceProcess.ServiceBase
    {
        private ReaderWriterLockSlim runningLock;
        private ServiceState state;
        private Thread thread;
        private InfluxCollectConfig influxconfig;

        //log 
        private const string sSource = "InfluxCollectService";
        private const string sLog = "Application";

        private PowerShell psinstance;


        public InfluxCollectService()
        {
            this.ServiceName = "InfluxCollectService";
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;

            runningLock = new ReaderWriterLockSlim();
            this.state = ServiceState.Stopped;
          
        }

        protected override void OnContinue()
        {
            runningLock.EnterWriteLock();
            this.state = ServiceState.Running;
            runningLock.ExitWriteLock();
        }

        protected override void OnPause()
        {
            runningLock.EnterWriteLock();
            this.state = ServiceState.Paused;
            runningLock.ExitWriteLock();
        }

        protected override void OnShutdown()
        {
            runningLock.EnterWriteLock();
            this.state = ServiceState.Stopped;
            runningLock.ExitWriteLock();


            if (thread != null) { thread.Join(); }
            
        }

        private ServiceState getState()
        {
            ServiceState cstate;
            runningLock.EnterReadLock();
            cstate = this.state;
            runningLock.ExitReadLock();
            return cstate;

        }
        private void WriteEntryInLog(string entry)
        {
            EventLog.WriteEntry(sSource, entry);
        }

        private string GetEmbededScript(string filename)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JobToInflux.powershell-"+filename+".ps1");
            StreamReader reader = new StreamReader(stream);
            var scr = reader.ReadToEnd();
            reader.Close();
            return scr;
        }
        private void WorkThread()
        {
            bool alreadyForceStopped = false;
            runningLock.EnterWriteLock();
            if (this.state != ServiceState.ForceStop)
            {
                this.state = ServiceState.Running;
            } else
            {
                alreadyForceStopped = true;
            }
            runningLock.ExitWriteLock();

            //updates the state every service but only running the command every 5 times
            int skip = 5;
            int skipcounter = 0;


            if (!alreadyForceStopped)
            {

                WriteEntryInLog(String.Format("Starting with {0} on db {1}", influxconfig.GetServiceString(), influxconfig.GetDB()));

                try
                {
                    MetricsCollector collector = new CollectorConfiguration().Tag.With("host", Environment.GetEnvironmentVariable("COMPUTERNAME")).Batch.AtInterval(TimeSpan.FromSeconds(2)).WriteTo.InfluxDB(influxconfig.GetServiceString(), influxconfig.GetDB()).CreateCollector();


                    var getjobcode = GetEmbededScript("getjobs");
                    psinstance.AddScript(getjobcode);
                    psinstance.Invoke();

                    if(psinstance.Streams.Error.Count > 0)
                    {
                        throw new Exception("Unable to fetch initial jobs "+psinstance.Streams.Error[0]);
                    }
                    psinstance.Streams.ClearStreams();

                    var getjobstatuscode = GetEmbededScript("getjobstatus");

                    ServiceState cstate = getState();

                    for (; cstate != ServiceState.Stopped && cstate != ServiceState.ForceStop; cstate = getState())
                    {
                        if (cstate != ServiceState.Paused && (skipcounter % skip == 0))
                        {


                            psinstance.AddScript(getjobstatuscode);
                            var jobstats = psinstance.Invoke();
                            if (psinstance.Streams.Error.Count > 0)
                            {
                                WriteEntryInLog("Failed Collection " + psinstance.Streams.Error[0]);
                            }
                            else
                            {
                                foreach (PSObject jobstat in jobstats)
                                {
                                    //@{jobname=$job.name;speed=$speed;progress=$progress;state=$state;running=$running} 
                                    collector.Write("jobprogress",
                                    new Dictionary<string, object>
                                    {
                                        { "speed",  Int64.Parse(jobstat.Properties["speed"].Value.ToString()) },
                                        { "progress", Int32.Parse(jobstat.Properties["progress"].Value.ToString()) },
                                        { "running", Int32.Parse(jobstat.Properties["running"].Value.ToString()) },
                                        { "state", jobstat.Properties["state"].Value.ToString() },
                                    },
                                    new Dictionary<string,string>
                                    {
                                        {"jobname",jobstat.Properties["jobname"].Value.ToString()}
                                    }
                                    );
                                }
                            }
                            psinstance.Streams.ClearStreams();

                            //make sure int never overflows
                            if (skipcounter >= 120)
                            {
                                WriteEntryInLog(String.Format("Heartbeating, service still working"));
                                skipcounter = 0;
                            }
                        }
                        skipcounter++;
                        Thread.Sleep(1000);
                    }

                    if (cstate == ServiceState.ForceStop)
                    {
                        this.Stop();
                    }
                }
                catch (Exception e)
                {
                    EventLog.WriteEntry(sSource, String.Format("Failure of some kind {0}", e.Message));
                }
            } else
            {
                this.Stop();
            }
        }

        protected override void OnStart(string[] args)
        {

            if (!EventLog.SourceExists(sSource))
                EventLog.CreateEventSource(sSource, sLog);

            WriteEntryInLog(String.Format("Starting by reading config"));

            string directory = InfluxCollectConfig.GetDefaultPath();
            string filename = InfluxCollectConfig.GetDefaultConfigFileName();
            string configfile = String.Format("{0}\\{1}", directory, filename);

            //if there is no config, just make one from scratch
            try
            {
                InfluxCollectConfig.TryMakeDefaultConfig(directory, filename);
            } catch (Exception e)
            {
                WriteEntryInLog(e.Message);
            }

            this.influxconfig = new InfluxCollectConfig();
            if(File.Exists(configfile))
            {
                try
                {
                    this.influxconfig = InfluxCollectConfig.ReadFromFile(configfile);
                } catch (Exception e)
                {
                    WriteEntryInLog(String.Format("Could not read config file {0} error {1}", configfile, e.Message));
                }
            }

            try
            {
                psinstance = PowerShell.Create();
                psinstance.AddScript("if ( (Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue) -eq $null ) { Add-PSSnapin VeeamPSSnapIn;}");
                psinstance.Invoke();

                if (psinstance.Streams.Error.Count > 0)
                {
                    throw new Exception("Error loading ps : " + psinstance.Streams.Error[0].ToString());
                }
                else
                {
                    psinstance.Streams.ClearStreams();
                }

                thread = new Thread(WorkThread);
                thread.Name = "Main Thread";
                thread.IsBackground = true;
                thread.Start();
            } catch (Exception e)
            {
                runningLock.EnterWriteLock();
                this.state = ServiceState.ForceStop;
                runningLock.ExitWriteLock();

                WriteEntryInLog(String.Format("Error on preloading, make sure you are running on a backup server {0}", e.Message));
                this.Stop();

                
            }
            

        }


        protected override void OnStop()
        {
            runningLock.EnterWriteLock();
            this.state = ServiceState.Stopped;
            runningLock.ExitWriteLock();


            if (thread != null) { thread.Join(); }

        }
    }
}